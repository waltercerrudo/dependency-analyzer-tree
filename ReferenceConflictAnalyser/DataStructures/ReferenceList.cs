using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceConflictAnalyser.DataStructures
{
    /// <summary>
    /// Contenedor del grafo completo de dependencias de ensamblados, formado por dos conjuntos:
    ///   - <see cref="Assemblies"/>: nodos del grafo (un <see cref="ReferencedAssembly"/> por cada
    ///     ensamblado único encontrado, incluido el punto de entrada).
    ///   - <see cref="References"/>: aristas dirigidas del grafo (un <see cref="Reference"/> por
    ///     cada par origen-destino único).
    ///
    /// El uso de <see cref="HashSet{T}"/> en ambas colecciones garantiza que no haya duplicados
    /// incluso cuando un ensamblado es referenciado por múltiples dependientes, gracias a las
    /// implementaciones de <see cref="object.Equals"/> y <see cref="object.GetHashCode"/> de
    /// <see cref="ReferencedAssembly"/> y <see cref="Reference"/>.
    /// </summary>
    public class ReferenceList
    {
        /// <summary>
        /// Agrega el ensamblado raíz del análisis al conjunto de nodos.
        /// Debe llamarse exactamente una vez antes de invocar <see cref="AddReference"/>.
        /// </summary>
        /// <param name="referencedAssembly">
        ///   Ensamblado raíz con categoría <see cref="Category.EntryPoint"/>.
        /// </param>
        public void AddEntryPoint(ReferencedAssembly referencedAssembly)
        {
            _assemblies.Add(referencedAssembly);
        }

        /// <summary>
        /// Registra la relación de dependencia entre <paramref name="assembly"/> y
        /// <paramref name="referencedAssembly"/>:
        ///   - Agrega <paramref name="referencedAssembly"/> al conjunto de nodos si no existe.
        ///   - Crea una nueva arista <see cref="Reference"/> y la agrega al conjunto de aristas.
        /// </summary>
        /// <param name="assembly">Ensamblado que declara la referencia (origen).</param>
        /// <param name="referencedAssembly">Ensamblado referenciado (destino).</param>
        /// <returns>
        ///   true si la arista es nueva (primera vez que se registra este par origen-destino);
        ///   false si ya existía. El llamador usa este valor para evitar recursar dos veces
        ///   por el mismo ensamblado.
        /// </returns>
        public bool AddReference(ReferencedAssembly assembly, ReferencedAssembly referencedAssembly)
        {
            _assemblies.Add(referencedAssembly);

            var reference = new Reference(assembly, referencedAssembly);
            // HashSet.Add devuelve false si el elemento ya existía.
            return _references.Add(reference);
        }

        /// <summary>Conjunto de aristas (dependencias) del grafo. Acceso de sólo lectura.</summary>
        public HashSet<Reference> References => _references;

        /// <summary>Conjunto de nodos (ensamblados) del grafo. Acceso de sólo lectura.</summary>
        public HashSet<ReferencedAssembly> Assemblies => _assemblies;

        #region private members

        private readonly HashSet<Reference> _references = new HashSet<Reference>();
        private readonly HashSet<ReferencedAssembly> _assemblies = new HashSet<ReferencedAssembly>();

        #endregion
    }
}
