using ReferenceConflictAnalyser.DataStructures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceConflictAnalyser
{
    /// <summary>
    /// Analiza el <see cref="ReferenceList"/> producido por <see cref="ReferenceReader"/> y
    /// clasifica cada ensamblado según los problemas detectados, actualizando su propiedad
    /// <see cref="ReferencedAssembly.Category"/> y la lista
    /// <see cref="ReferencedAssembly.PossibleLoadingErrorCauses"/>.
    ///
    /// Los tipos de problemas que detecta son:
    ///   - Conflictos de versión: distintos ensamblados referencian versiones incompatibles
    ///     del mismo ensamblado (Major.Minor difieren).
    ///   - Conflictos resueltos: el archivo de configuración contiene un &lt;bindingRedirect&gt;
    ///     que cubre el rango de versiones en conflicto.
    ///   - Errores de carga: ensamblados que no pudieron cargarse (faltantes, corruptos, etc.).
    ///   - Desajuste de arquitectura de procesador: la plataforma objetivo (x86/x64/ARM/IA64)
    ///     del ensamblado difiere de la del punto de entrada.
    public class ReferenceAnalyser
    {
        /// <summary>
        /// Ejecuta el pipeline completo de análisis sobre el <paramref name="list"/> proporcionado,
        /// mutando las categorías y las causas de error de cada ensamblado según los problemas
        /// encontrados.
        /// </summary>
        /// <param name="list">
        ///   Grafo de dependencias construido por <see cref="ReferenceReader"/>.
        /// </param>
        /// <param name="bindingRedirects">
        ///   Redirecciones de enlace leídas del archivo de configuración, usadas para determinar
        ///   si un conflicto de versiones ya está resuelto en tiempo de ejecución.
        /// </param>
        /// <returns>El mismo <paramref name="list"/> con las categorías actualizadas.</returns>
        public ReferenceList AnalyzeReferences(ReferenceList list, IEnumerable<BindingRedirectData> bindingRedirects)
        {
            _referenceList = list;

            FindConflicts();
            FindResolvedConflicts(bindingRedirects);
            AddExplanationToUnresolvedConflicts();
            AddExplanationToLoadingErrors();
            FindProcessorArchitectureMismatch();

            return _referenceList;
        }


        #region private

        private ReferenceList _referenceList { get; set; }

        /// <summary>
        /// Detecta conflictos de versión recorriendo todas las referencias del grafo.
        /// Dos versiones son incompatibles cuando difieren en Major o Minor (Build y Revision
        /// se consideran compatibles según la convención de .NET).
        ///
        /// Cuando se detecta un conflicto, todos los nodos del grafo cuyo nombre coincida y
        /// que aún estén en estado <see cref="Category.Normal"/> pasan a
        /// <see cref="Category.VersionsConflicted"/>.
        /// </summary>
        private void FindConflicts()
        {
            // Mapa nombre -> primera versión encontrada para ese ensamblado.
            var referencedVersions = new Dictionary<string, Version>();

            foreach (var reference in _referenceList.References)
            {
                if (referencedVersions.ContainsKey(reference.ReferencedAssembly.Name))
                {
                    // Si ya vimos una versión anterior de este ensamblado, comparamos.
                    if (!AreVersionCompatible(referencedVersions[reference.ReferencedAssembly.Name], reference.ReferencedAssembly.Version))
                    {
                        // Marcar TODOS los nodos con ese nombre como conflictivos,
                        // no sólo el recién encontrado.
                        var conflicts = _referenceList.Assemblies.Where(x => x.Name == reference.ReferencedAssembly.Name && x.Category == Category.Normal).ToArray();
                        foreach (var conflict in conflicts)
                            conflict.Category = Category.VersionsConflicted;
                    }
                }
                else
                {
                    referencedVersions.Add(reference.ReferencedAssembly.Name, reference.ReferencedAssembly.Version);
                }
            }
        }

        /// <summary>
        /// Para cada ensamblado marcado como <see cref="Category.VersionsConflicted"/>, comprueba
        /// si existe una redirección de enlace en el archivo de configuración cuyo rango
        /// (OldVersionLowerBound – OldVersionUpperBound) cubra la versión Major.Minor del ensamblado.
        /// En ese caso, la categoría pasa a <see cref="Category.VersionsConflictResolved"/>,
        /// indicando que el CLR resolverá el conflicto en tiempo de ejecución.
        /// </summary>
        /// <param name="bindingRedirects">
        ///   Lista de redirecciones de enlace del archivo de configuración.
        ///   Si es null o está vacía, el método no hace nada.
        /// </param>
        private void FindResolvedConflicts(IEnumerable<BindingRedirectData> bindingRedirects)
        {
            // Comparar sólo Major.Minor, ignorando Build y Revision, que son irrelevantes
            // para la resolución de conflictos de versión en .NET.
            if (bindingRedirects == null || !bindingRedirects.Any())
                return;

            var conflicts = _referenceList.Assemblies.Where(x => x.Category == Category.VersionsConflicted).ToArray();
            foreach (var conflict in conflicts)
            {
                var bindingRedirect = bindingRedirects.FirstOrDefault(x => x.AssemblyName == conflict.Name);
                if (bindingRedirect != null)
                {
                    var mainVersion = new Version(conflict.Version.Major, conflict.Version.Minor);

                    if (mainVersion >= bindingRedirect.OldVersionLowerBound
                       && mainVersion <= bindingRedirect.OldVersionUpperBound)
                    {
                        conflict.Category = Category.VersionsConflictResolved;
                        continue;
                    }
                }
            }
        }

        /// <summary>
        /// Determina si dos versiones de ensamblado son compatibles.
        /// La compatibilidad se evalúa sólo sobre Major y Minor, dado que .NET trata
        /// las diferencias en Build y Revision como cambios menores que no rompen la carga.
        /// </summary>
        /// <param name="version1">Primera versión a comparar.</param>
        /// <param name="version2">Segunda versión a comparar.</param>
        /// <returns>true si Major y Minor son idénticos; false si difieren.</returns>
        private bool AreVersionCompatible(Version version1, Version version2)
        {
            // Las versiones se consideran compatibles si sólo difieren en Build o Revision.
            return version1.Major == version2.Major
                && version1.Minor == version2.Minor;
        }

        /// <summary>
        /// Para cada ensamblado que no pudo cargarse (LoadingError != null), agrega mensajes
        /// explicativos a <see cref="ReferencedAssembly.PossibleLoadingErrorCauses"/> según
        /// el tipo de excepción:
        ///   - <see cref="FileNotFoundException"/>: el archivo del ensamblado no se encontró.
        ///   - <see cref="FileLoadException"/>: el archivo existe pero el CLR no pudo cargarlo
        ///     (identidad incorrecta, permisos, etc.).
        ///   - <see cref="BadImageFormatException"/>: el formato del PE es inválido o
        ///     incompatible (versión de .NET, DLL nativa, desajuste de bits 32/64).
        /// </summary>
        private void AddExplanationToLoadingErrors()
        {
            var failedToLoad = _referenceList.Assemblies.Where(x => x.LoadingError != null);
            foreach (var referencedAssembly in failedToLoad)
            {
                if (referencedAssembly.LoadingError is FileNotFoundException)
                {
                    referencedAssembly.PossibleLoadingErrorCauses.Add("The assembly is missed.");
                }
                else if (referencedAssembly.LoadingError is FileLoadException)
                {
                    referencedAssembly.PossibleLoadingErrorCauses.Add("The assembly file is found but cannot be loaded.");
                }
                else if (referencedAssembly.LoadingError is BadImageFormatException)
                {
                    referencedAssembly.PossibleLoadingErrorCauses.AddRange(new[]
                    {
                        "Either the assembly was developed with a later version of the .NET Framework then one which is used to load the assembly.",
                        "Or the assembly is not a .NET Framework assembly but an unmanaged dynamic link library or executable (such as a Windows system DLL).",
                        "Or the assembly built as a 32-bit assembly is loaded as a 64-bit assembly, and vice versa."
                    });
                }
            }
        }

        /// <summary>
        /// Agrega un mensaje genérico a todos los ensamblados aún marcados como
        /// <see cref="Category.VersionsConflicted"/> (es decir, cuyo conflicto no fue resuelto
        /// por una redirección de enlace), indicando al desarrollador que revise las aristas
        /// del grafo para identificar quiénes referencian versiones distintas.
        /// </summary>
        private void AddExplanationToUnresolvedConflicts()
        {
            var conflicts = _referenceList.Assemblies.Where(x => x.Category == Category.VersionsConflicted).ToArray();
            foreach (var conflict in conflicts)
            {
                conflict.PossibleLoadingErrorCauses.Add($"Different versions of this assembly are referenced by other assemblies. See reference links for details.");
            }
        }

        /// <summary>
        /// Detecta ensamblados cuya arquitectura de procesador es incompatible con la del
        /// punto de entrada y los marca como <see cref="Category.OtherConflict"/>.
        ///
        /// Reglas de compatibilidad:
        ///   - Si el punto de entrada es MSIL (AnyCPU), todos los ensamblados deberían ser
        ///     MSIL o None (sin información de arquitectura).
        ///   - Si el punto de entrada es x86/x64/ARM/IA64, los ensamblados deben coincidir
        ///     o ser MSIL/None.
        ///
        /// Los ensamblados incompatibles reciben un mensaje explicativo con ambas arquitecturas.
        /// </summary>
        private void FindProcessorArchitectureMismatch()
        {
            // Sólo tiene sentido verificar si el punto de entrada declara una arquitectura concreta.
            var processorArchitecture = _referenceList.Assemblies.First(x => x.Category == Category.EntryPoint).ProcessorArchitecture;
            if (processorArchitecture == ProcessorArchitecture.None)
                return;

            var mismatched = Enumerable.Empty<ReferencedAssembly>();
            switch (processorArchitecture)
            {
                // AnyCPU: cualquier ensamblado específico de plataforma es un desajuste potencial.
                case ProcessorArchitecture.MSIL:
                    mismatched = _referenceList.Assemblies
                        .Where(x => x.ProcessorArchitecture != ProcessorArchitecture.None && x.ProcessorArchitecture != ProcessorArchitecture.MSIL);
                    break;

                // Plataforma específica: sólo son compatibles MSIL (AnyCPU) o la misma arquitectura.
                case ProcessorArchitecture.Amd64:
                case ProcessorArchitecture.Arm:
                case ProcessorArchitecture.IA64:
                case ProcessorArchitecture.X86:
                    mismatched = _referenceList.Assemblies
                        .Where(x => x.ProcessorArchitecture != ProcessorArchitecture.None && x.ProcessorArchitecture != ProcessorArchitecture.MSIL && x.ProcessorArchitecture != processorArchitecture);
                    break;
            }

            foreach (var referencedAssembly in mismatched)
            {
                referencedAssembly.PossibleLoadingErrorCauses.Add($"The assembly platform target ({referencedAssembly.ProcessorArchitecture}) differs from the entry point assembly platform target ({processorArchitecture}).");
                // Sólo promover a OtherConflict si no está ya en una categoría de mayor gravedad.
                if (referencedAssembly.Category == Category.Normal || referencedAssembly.Category == Category.VersionsConflictResolved)
                    referencedAssembly.Category = Category.OtherConflict;
            }
        }


        #endregion
    }
}
