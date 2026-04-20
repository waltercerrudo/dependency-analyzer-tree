using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using ReferenceConflictAnalyser.DataStructures;
using System.Configuration;

namespace ReferenceConflictAnalyser
{
    /// <summary>
    /// Responsable de cargar de forma recursiva un ensamblado raíz y todos los ensamblados
    /// referenciados directa o indirectamente por él, construyendo un <see cref="ReferenceList"/>
    /// que representa el grafo completo de dependencias.
    ///
    /// Usa <see cref="Assembly.ReflectionOnlyLoadFrom"/> para inspectar los metadatos de los
    /// ensamblados sin ejecutar su código, lo que evita efectos secundarios y permite analizar
    /// ensamblados incompatibles con el entorno actual.
    ///
    /// Un caché interno (<c>_cache</c>) evita cargar el mismo ensamblado más de una vez y
    /// rompe ciclos en el grafo de dependencias.
    /// </summary>
    public class ReferenceReader
    {
        /// <summary>
        /// Punto de entrada del lector. Carga el ensamblado indicado, recorre recursivamente
        /// sus dependencias y detecta los ensamblados presentes en disco que no son referenciados
        /// por ningún nodo del grafo (ensamblados no utilizados).
        /// </summary>
        /// <param name="entryAssemblyFilePath">
        ///   Ruta al ensamblado raíz que se va a analizar (DLL o EXE).
        /// </param>
        /// <param name="subDirectories">
        ///   Subdirectorios adicionales (probing paths) donde buscar ensamblados referenciados,
        ///   generalmente provenientes del elemento &lt;probing&gt; del archivo de configuración.
        /// </param>
        /// <param name="skipSystemAssemblies">
        ///   Si es true, omite los ensamblados del sistema ("mscorlib", "System", "System.*")
        ///   para mantener el grafo enfocado en las dependencias propias del proyecto.
        /// </param>
        /// <returns>
        ///   Un <see cref="ReferenceList"/> con todos los ensamblados encontrados y sus relaciones.
        /// </returns>
        public ReferenceList Read(string entryAssemblyFilePath, IEnumerable<string> subDirectories, bool skipSystemAssemblies)
        {
            if (!File.Exists(entryAssemblyFilePath))
                throw new ArgumentException(string.Format("File does not exist: {0}", entryAssemblyFilePath));

            _skipSystemAssemblies = skipSystemAssemblies;
            _result = new ReferenceList();
            _cache = new Dictionary<string, ReferencedAssembly>();

            var workingDirectory = Path.GetDirectoryName(entryAssemblyFilePath);
            _searchDirectories = new List<string>() { workingDirectory };
            try
            {
                // Se agregan las rutas de sondeo adicionales, combinándolas con el directorio base.
                // Se descartan las que no existen para evitar errores posteriores.
                if (subDirectories != null)
                    _searchDirectories.AddRange(subDirectories
                                                    .Select(x => Path.Combine(workingDirectory, x))
                                                    .Where(x => Directory.Exists(x))); 
            }
            catch (Exception e)
            {
                throw new Exception($"\"runtime/assemblyBinding/probing\" element in the config file contains invalid paths. {e.Message}");          
            }
 
            AssemblyName[] entryPointReferences;
            var entryPoint = LoadEntryPoint(entryAssemblyFilePath, out entryPointReferences);
            _result.AddEntryPoint(entryPoint);

            // Recorrer en profundidad el árbol de referencias a partir del punto de entrada.
            ReadReferencesRecursively(entryPoint, entryPointReferences);

            // Detectar ensamblados en disco que no forman parte del grafo de dependencias.
            ReadUnusedAssemblies();

            return _result;
        }

        #region private members

        private bool _skipSystemAssemblies;
        private ReferenceList _result;
        private List<string> _searchDirectories;

        /// <summary>
        /// Caché que mapea el nombre completo de un ensamblado (FullName) al objeto
        /// <see cref="ReferencedAssembly"/> ya creado, evitando cargarlo dos veces y
        /// cortando ciclos en el grafo de dependencias.
        /// </summary>
        private Dictionary<string, ReferencedAssembly> _cache;

        /// <summary>
        /// Recorre en profundidad (DFS) el árbol de referencias de un ensamblado.
        /// Para cada referencia:
        ///   - Si ya está en caché, reutiliza el nodo existente.
        ///   - Si el ensamblado pudo cargarse, agrega la relación y continúa recursivamente.
        ///   - Si el ensamblado no pudo cargarse (Missed), sólo registra la relación sin recursar.
        /// </summary>
        /// <param name="assembly">Ensamblado padre cuyos referencias se están procesando.</param>
        /// <param name="references">Array de nombres de ensamblados referenciados por <paramref name="assembly"/>.</param>
        private void ReadReferencesRecursively(ReferencedAssembly assembly, AssemblyName[] references)
        {
            foreach (var reference in references)
            {
                // Filtrar ensamblados del sistema si la opción está activada.
                if (_skipSystemAssemblies
                    &&
                        (reference.Name == "mscorlib"
                        || reference.Name == "System"
                        || reference.Name.StartsWith("System."))
                    )
                    continue;

                // Si el ensamblado ya fue procesado anteriormente, reutilizar el nodo cacheado
                // y registrar únicamente la nueva arista en el grafo.
                if (_cache.ContainsKey(reference.FullName))
                {
                    _result.AddReference(assembly, _cache[reference.FullName]);
                    continue;
                }

                AssemblyName[] referencedAssemblyReferences;
                var referencedAssembly = LoadReferencedAssembly(reference, out referencedAssemblyReferences);
                if (referencedAssembly.Category != Category.Missed)
                {
                    // AddReference devuelve true sólo la primera vez que se agrega la arista,
                    // lo que garantiza que no se recurse dos veces por el mismo ensamblado.
                    var isNewReference = _result.AddReference(assembly, referencedAssembly);
                    if (isNewReference)
                        ReadReferencesRecursively(referencedAssembly, referencedAssemblyReferences);
                }
                else
                {
                    // Los ensamblados faltantes se registran como nodos Missed sin continuar la recursión.
                    _result.AddReference(assembly, referencedAssembly);
                }
            }

        }

        /// <summary>
        /// Carga el ensamblado raíz del análisis usando ReflectionOnly para obtener sus metadatos
        /// sin ejecutar código estático. Asigna la categoría <see cref="Category.EntryPoint"/>
        /// y lo almacena en el caché.
        /// </summary>
        /// <param name="filePath">Ruta al ensamblado raíz.</param>
        /// <param name="assemblyReferences">
        ///   [out] Lista de ensamblados referenciados directamente por el punto de entrada.
        /// </param>
        /// <returns>El <see cref="ReferencedAssembly"/> que representa el ensamblado raíz.</returns>
        private ReferencedAssembly LoadEntryPoint(string filePath, out AssemblyName[] assemblyReferences)
        {
            assemblyReferences = Assembly.ReflectionOnlyLoadFrom(filePath).GetReferencedAssemblies();
            var assembly = AssemblyName.GetAssemblyName(filePath);

            var referencedAssembly = new ReferencedAssembly(assembly)
            {
                Category = Category.EntryPoint,
            };

            _cache.Add(assembly.FullName, referencedAssembly);
            return referencedAssembly;
        }

        /// <summary>
        /// Carga un ensamblado referenciado buscándolo primero en todos los directorios de búsqueda
        /// (directorio base + probing paths). Si se encuentra en disco, se usa
        /// <see cref="Assembly.ReflectionOnlyLoadFrom"/> para obtener sus referencias y se leen los
        /// metadatos completos (incluyendo <see cref="ProcessorArchitecture"/>) con
        /// <see cref="AssemblyName.GetAssemblyName"/>. Si no se encuentra, se intenta
        /// <see cref="Assembly.ReflectionOnlyLoad"/> como último recurso (GAC u otras rutas del CLR).
        ///
        /// Si cualquier carga falla, el ensamblado queda marcado como <see cref="Category.Missed"/>
        /// con la excepción registrada para mostrarla en el grafo.
        /// </summary>
        /// <param name="reference">Nombre del ensamblado que se desea cargar.</param>
        /// <param name="referencedAssemblyReferences">
        ///   [out] Lista de ensamblados referenciados por el ensamblado cargado,
        ///   o null si la carga falló.
        /// </param>
        /// <returns>El <see cref="ReferencedAssembly"/> resultante (puede estar en estado Missed).</returns>
        private ReferencedAssembly LoadReferencedAssembly(AssemblyName reference, out AssemblyName[] referencedAssemblyReferences)
        {
            referencedAssemblyReferences = null;
            ReferencedAssembly referencedAssembly;
            try
            {
                string file = null;
                foreach(var dir in _searchDirectories)
                {
                    var files = Directory.GetFiles(dir, reference.Name + ".???", SearchOption.TopDirectoryOnly);
                    file = files.FirstOrDefault(x => x.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
                    if (file != null)
                        break;
                }

                if (file != null)
                {
                    referencedAssemblyReferences = Assembly.ReflectionOnlyLoadFrom(file).GetReferencedAssemblies();

                    // La carga ReflectionOnly no siempre devuelve el ProcessorArchitecture correcto;
                    // GetAssemblyName lee los metadatos directamente del PE y es más fiable.
                    var temp = AssemblyName.GetAssemblyName(file);
                    if (temp.FullName == reference.FullName)
                        reference.ProcessorArchitecture = temp.ProcessorArchitecture;
                }
                else
                {
                    // Si no se encontró el archivo en disco, intentar cargarlo desde el GAC
                    // o cualquier otra ruta que el CLR conozca.
                    referencedAssemblyReferences = Assembly.ReflectionOnlyLoad(reference.FullName).GetReferencedAssemblies();
                }
                referencedAssembly = new ReferencedAssembly(reference);
            }
            catch (Exception e)
            {
                // Cualquier fallo de carga se convierte en un nodo Missed con la excepción guardada.
                referencedAssembly = new ReferencedAssembly(reference, e);
            }

            // Guardar en caché sólo si no fue registrado previamente (puede ocurrir en grafos con
            // múltiples rutas al mismo ensamblado que llegan al mismo tiempo antes del caché).
            if (!_cache.ContainsKey(reference.FullName))
                _cache.Add(reference.FullName, referencedAssembly);

            return referencedAssembly;
        }

        /// <summary>
        /// Detecta ensamblados (DLL y EXE) que están físicamente presentes en los directorios
        /// de búsqueda pero que ningún nodo del grafo referencia explícitamente.
        /// Estos ensamblados se agregan al resultado con la categoría
        /// <see cref="Category.UnusedAssembly"/> para alertar al desarrollador de que
        /// podrían cargarse en tiempo de ejecución mediante <see cref="Assembly.Load"/> o
        /// métodos similares de reflexión.
        ///
        /// Los archivos que no sean ensamblados .NET válidos (por ejemplo, DLLs nativas) se
        /// ignoran silenciosamente mediante el bloque catch vacío.
        /// </summary>
        private void ReadUnusedAssemblies()
        {
            // Obtener los nombres (sin extensión) de todos los ensamblados ya presentes en el grafo.
            var loadedAssemblies = _result.Assemblies.Select(x => x.Name);

            var allFiles = new List<string>();
            foreach(var dir in _searchDirectories)
            {
                allFiles.AddRange(Directory
                    .GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly)
                    .Concat(Directory
                          .GetFiles(dir, "*.dll", SearchOption.TopDirectoryOnly)));
            }

            foreach(var filePath in allFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);

                // Si el nombre del archivo no coincide (sin distinción de mayúsculas) con ningún
                // ensamblado ya procesado, se trata como ensamblado no utilizado.
                if (!loadedAssemblies.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var assemblyName = AssemblyName.GetAssemblyName(filePath);
                        _result.Assemblies.Add(new ReferencedAssembly(assemblyName)
                        {
                            Category = Category.UnusedAssembly
                        });
                    }
                    catch(Exception e)
                    {
                        // Ignorar archivos que no son ensamblados .NET válidos (DLLs nativas, etc.).
                    }
                }
            }          
        }

        #endregion
    }
}
