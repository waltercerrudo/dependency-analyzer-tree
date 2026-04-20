using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceConflictAnalyser.DataStructures
{

 /// <summary>
 /// Clasificación de un ensamblado dentro del grafo de dependencias.
 /// Determina el color del nodo en la visualización DGML y comunica al desarrollador
 /// el estado del ensamblado respecto al análisis de conflictos.
 ///
 /// El atributo [Description] de cada valor se usa como etiqueta en la leyenda del grafo
 /// DGML, accedida mediante <see cref="ReferenceConflictAnalyser.Utils.EnumHelper.GetDescription{T}"/>.
 /// </summary>
    public enum Category
    {
        /// <summary>
        /// Ensamblado raíz del análisis: el DLL o EXE que el usuario seleccionó como punto
        /// de partida. Sólo existe un nodo con esta categoría en cada grafo.
        /// Color: verde claro (LightGreen).
        /// </summary>
        [Description("Entry point for analysis")]
        EntryPoint,

        /// <summary>
        /// Ensamblado referenciado que se cargó correctamente y no presenta ningún conflicto
        /// de versión ni desajuste de arquitectura.
        /// Color: MintCream (casi blanco).
        /// </summary>
        [Description("Normal reference")]
        Normal,

        /// <summary>
        /// Ensamblado del que existen múltiples versiones incompatibles referenciadas por
        /// distintos ensamblados del grafo. Requiere intervención del desarrollador (p.ej.
        /// añadir un &lt;bindingRedirect&gt; en el config).
        /// Color: LightSalmon (salmón claro).
        /// </summary>
        [Description("Versions conflict")]
        VersionsConflicted,

        /// <summary>
        /// Ensamblado con un conflicto distinto al de versión, típicamente un desajuste de
        /// arquitectura de procesador (x86 vs x64, etc.).
        /// Color: Coral.
        /// </summary>
        [Description("Other conflict")]
        OtherConflict,

        /// <summary>
        /// Ensamblado con conflicto de versión que está resuelto por una redirección de enlace
        /// en el archivo de configuración. El CLR lo resolverá correctamente en tiempo de ejecución.
        /// Color: Khaki (amarillo pálido).
        /// </summary>
        [Description("Versions conflict is resolved by config file")]
        VersionsConflictResolved,

        /// <summary>
        /// Ensamblado que no pudo cargarse, ya sea porque el archivo no existe en ninguno de
        /// los directorios de búsqueda, porque el formato PE es inválido, o por cualquier otro
        /// error de carga. Se muestra con un nodo de comentario adjunto explicando la causa.
        /// Color: Crimson (rojo oscuro).
        /// </summary>
        [Description("Assembly is missed or failed to load")]
        Missed,

        /// <summary>
        /// Nodo auxiliar que muestra información adicional (mensaje de error, tipo de excepción,
        /// causas posibles) conectado al nodo del ensamblado problemático mediante una arista
        /// discontinua. No representa un ensamblado real.
        /// Color: White con texto gris.
        /// </summary>
        [Description("Detailed information")]
        Comment,

        /// <summary>
        /// Ensamblado presente en disco en los directorios de búsqueda pero que no es referenciado
        /// explícitamente por ningún ensamblado del grafo. Puede ser cargado en tiempo de ejecución
        /// mediante reflexión (Assembly.Load, AppDomain, etc.).
        /// Color: Gray.
        /// </summary>
        [Description("Unused assemblies")]
        UnusedAssembly
    }
}
