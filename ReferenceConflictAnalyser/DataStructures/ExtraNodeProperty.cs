using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceConflictAnalyser.DataStructures
{
    /// <summary>
    /// Define las propiedades adicionales que se registran en el documento DGML mediante el
    /// elemento &lt;Properties&gt; y que pueden asignarse a nodos o aristas del grafo.
    ///
    /// Estas propiedades son visibles en el panel "Properties" de Visual Studio cuando el usuario
    /// selecciona un nodo o arista en el editor DGML. El atributo [Description] de cada valor
    /// se usa como etiqueta de la propiedad en ese panel, accedida mediante
    /// <see cref="ReferenceConflictAnalyser.Utils.EnumHelper.GetValuesWithDescriptions{T}"/>.
    ///
    /// Todas las propiedades son de tipo System.String en el DGML generado.
    /// </summary>
    public enum ExtraNodeProperty
    {
        /// <summary>
        /// Nombre completo (FullName) del ensamblado origen de una arista de dependencia.
        /// Incluye nombre simple, versión, cultura y token de clave pública.
        /// Se asigna al atributo del mismo nombre en el elemento &lt;Link&gt; del DGML.
        /// </summary>
        [Description("Source Node Details")]
        SourceNodeDetails,

        /// <summary>
        /// Nombre completo (FullName) del ensamblado destino de una arista de dependencia.
        /// Incluye nombre simple, versión, cultura y token de clave pública.
        /// Se asigna al atributo del mismo nombre en el elemento &lt;Link&gt; del DGML.
        /// </summary>
        [Description("Target Node Details")]
        TargetNodeDetails,

        /// <summary>
        /// Arquitectura de procesador del ensamblado (x86, x64, ARM, IA64, MSIL/AnyCPU).
        /// Se asigna como atributo extra en el elemento &lt;Node&gt; del DGML sólo cuando
        /// la arquitectura es distinta de <see cref="ProcessorArchitecture.None"/>.
        /// </summary>
        [Description("Platform Target (Processor Architecture)")]
        ProcessorArchitecture
    }
}
