using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceConflictAnalyser.DataStructures
{
    /// <summary>
    /// Representa un nodo del grafo de dependencias, es decir, un ensamblado .NET individual
    /// con todos sus metadatos relevantes para el análisis de conflictos.
    ///
    /// Encapsula tanto la información obtenida de los metadatos del ensamblado (nombre, versión,
    /// arquitectura de procesador) como el estado derivado del análisis (categoría, errores de
    /// carga, causas posibles de conflicto).
    ///
    /// La igualdad se basa en el par (Name, Version): dos instancias de <see cref="ReferencedAssembly"/>
    /// representan el mismo nodo si tienen el mismo nombre y la misma versión, independientemente
    /// de otros atributos como el token de clave pública o la cultura.
    /// Esto permite almacenarlos en <see cref="HashSet{T}"/> sin duplicados.
    /// </summary>
    public class ReferencedAssembly
    {
        /// <summary>
        /// Crea un ensamblado que se cargó correctamente.
        /// La categoría inicial es <see cref="Category.Normal"/>; puede ser modificada
        /// posteriormente por <see cref="ReferenceAnalyser"/>.
        /// </summary>
        /// <param name="assemblyName">Metadatos del ensamblado obtenidos del CLR.</param>
        public ReferencedAssembly(AssemblyName assemblyName)
            : this(assemblyName, null)
        {
        }

        /// <summary>
        /// Crea un ensamblado que puede haber fallado al cargarse.
        /// Si <paramref name="loadingError"/> es no nulo, la categoría inicial es
        /// <see cref="Category.Missed"/>; de lo contrario, <see cref="Category.Normal"/>.
        /// </summary>
        /// <param name="assemblyName">Metadatos del ensamblado (pueden ser parciales si la carga falló).</param>
        /// <param name="loadingError">
        ///   Excepción que se produjo al intentar cargar el ensamblado, o null si se cargó bien.
        /// </param>
        public ReferencedAssembly(AssemblyName assemblyName, Exception loadingError)
        {
            AssemblyName = assemblyName;

            Name = assemblyName.Name;
            ProcessorArchitecture = assemblyName.ProcessorArchitecture;
            //PublicKeyToken = Encoding.UTF8.GetString(assemblyName.GetPublicKeyToken()).ToLowerInvariant();
            Version = assemblyName.Version;


            Category = loadingError == null ? Category.Normal : Category.Missed;
            LoadingError = loadingError;
            PossibleLoadingErrorCauses = new List<string>();

            GenerateHashCode();
        }

        /// <summary>Nombre simple del ensamblado (sin versión, cultura ni token).</summary>
        public string Name { get; private set; }
        //public string PublicKeyToken { get; private set; }

        /// <summary>Versión del ensamblado en formato Major.Minor.Build.Revision.</summary>
        public Version Version { get; private set; }

        /// <summary>
        /// Objeto <see cref="System.Reflection.AssemblyName"/> completo con todos los metadatos
        /// del ensamblado: nombre, versión, cultura, token de clave pública y arquitectura.
        /// </summary>
        public AssemblyName AssemblyName { get; private set; }

        /// <summary>
        /// Excepción producida al intentar cargar el ensamblado, o null si se cargó correctamente.
        /// Puede ser <see cref="FileNotFoundException"/>, <see cref="FileLoadException"/> o
        /// <see cref="BadImageFormatException"/>, entre otras.
        /// </summary>
        public Exception LoadingError { get; private set; }

        /// <summary>
        /// Lista de mensajes explicativos sobre los posibles motivos de los errores o conflictos
        /// detectados por <see cref="ReferenceAnalyser"/>. Se muestra en el nodo de comentario
        /// del grafo DGML.
        /// </summary>
        public List<string> PossibleLoadingErrorCauses { get; private set; }

        /// <summary>
        /// Arquitectura de procesador del ensamblado (x86, x64, ARM, IA64, MSIL/AnyCPU, o None).
        /// Se usa para detectar desajustes entre el punto de entrada y sus dependencias.
        /// </summary>
        public ProcessorArchitecture ProcessorArchitecture { get; set; }

        /// <summary>
        /// Clasificación del ensamblado según el resultado del análisis.
        /// Determina el color del nodo en el grafo DGML.
        /// Comienza en <see cref="Category.Normal"/> o <see cref="Category.Missed"/> y puede
        /// ser actualizada por <see cref="ReferenceAnalyser"/>.
        /// </summary>
        public Category Category { get; set; }

        /// <summary>
        /// Indica si el ensamblado tiene un desajuste de arquitectura de procesador respecto
        /// al punto de entrada. Se establece en <see cref="ReferenceAnalyser"/>.
        /// </summary>
        public bool ProcessorArchitectureMismatch { get; set; }

        private int _hashCode;


        /// <summary>
        /// Precalcula el hash basado en (Name, Version) para uso eficiente en HashSet y Dictionary.
        /// Nota: cultura y token de clave pública no se incluyen actualmente (TODO en el código original).
        /// </summary>
        private void GenerateHashCode()
        {
            //TODO: add token and culture
            _hashCode = new { Name, Version }.GetHashCode();
        }

        /// <summary>
        /// Dos instancias son iguales si tienen el mismo Name (sin distinción de mayúsculas) y
        /// la misma Version. Cultura y token de clave pública no se comparan actualmente.
        /// </summary>
        public override bool Equals(object obj)
        {
            var other = obj as ReferencedAssembly;
            if (other == null)
                return false;

            //TODO: add token and culture check
            return Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase) && Version == Version;
        }

        /// <summary>Hash precalculado basado en (Name, Version).</summary>
        public override int GetHashCode()
        {
            return _hashCode;
        }

    }
}
