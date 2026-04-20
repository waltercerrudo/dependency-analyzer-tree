using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceConflictAnalyser.DataStructures
{

    /// <summary>
    /// Representa una arista dirigida del grafo de dependencias de ensamblados.
    /// Modela la relación "el ensamblado A referencia al ensamblado B", donde:
    ///   - <see cref="Assembly"/> es el ensamblado que contiene la referencia (origen).
    ///   - <see cref="ReferencedAssembly"/> es el ensamblado al que se hace referencia (destino).
    ///
    /// La igualdad y el hash se calculan sobre la representación de cadena
    /// "A.FullName -> B.FullName", lo que garantiza que dos aristas son iguales sólo si
    /// apuntan exactamente al mismo par origen-destino (incluyendo versión y token de clave pública).
    /// Esto permite almacenar instancias en un <see cref="HashSet{T}"/> sin duplicados.
    /// </summary>
    public class Reference
    {
        /// <summary>
        /// Inicializa una nueva referencia entre dos ensamblados.
        /// Precalcula la representación de cadena para uso eficiente en Equals y GetHashCode.
        /// </summary>
        /// <param name="assembly">Ensamblado que declara la referencia (nodo origen).</param>
        /// <param name="referencedAssembly">Ensamblado al que se hace referencia (nodo destino).</param>
        public Reference(ReferencedAssembly assembly, ReferencedAssembly referencedAssembly)
        {
            Assembly = assembly.AssemblyName;
            ReferencedAssembly = referencedAssembly.AssemblyName;

            // Se precalcula para evitar reconstruirla en cada comparación.
            _stringPresentation = string.Concat(Assembly.FullName, " -> ", ReferencedAssembly.FullName);
        }

        /// <summary>Ensamblado que declara la referencia (nodo origen de la arista).</summary>
        public AssemblyName Assembly { get; private set; }

        /// <summary>Ensamblado al que se hace referencia (nodo destino de la arista).</summary>
        public AssemblyName ReferencedAssembly { get; private set; }

        /// <summary>Representación de cadena precalculada: "Assembly.FullName -> ReferencedAssembly.FullName".</summary>
        private string _stringPresentation;

        /// <summary>
        /// Devuelve la representación de cadena de la arista en el formato
        /// "Assembly.FullName -> ReferencedAssembly.FullName".
        /// </summary>
        public override string ToString()
        {
            return _stringPresentation;
        }

        /// <summary>
        /// Dos referencias son iguales si su representación de cadena coincide
        /// (comparación sin distinción de mayúsculas/minúsculas).
        /// </summary>
        public override bool Equals(object obj)
        {
            return _stringPresentation.Equals(obj.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Hash calculado sobre la representación de cadena de la arista.
        /// Garantiza consistencia con <see cref="Equals"/>.
        /// </summary>
        public override int GetHashCode()
        {
            return _stringPresentation.GetHashCode();
        }
    }
}
