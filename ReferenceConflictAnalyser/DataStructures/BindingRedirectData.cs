using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceConflictAnalyser.DataStructures
{
    /// <summary>
    /// Modelo de datos que representa una redirección de enlace de ensamblado (.NET Assembly Binding Redirect),
    /// tal como está definida en el archivo de configuración (App.config / Web.config) bajo el elemento:
    ///
    /// <code>
    /// &lt;dependentAssembly&gt;
    ///   &lt;assemblyIdentity name="NombreEnsamblado" publicKeyToken="abc123" /&gt;
    ///   &lt;bindingRedirect oldVersion="1.0.0.0-2.0.0.0" newVersion="3.0.0.0" /&gt;
    /// &lt;/dependentAssembly&gt;
    /// </code>
    ///
    /// Una redirección indica al CLR que, cuando un ensamblado solicite cualquier versión de
    /// <see cref="AssemblyName"/> comprendida entre <see cref="OldVersionLowerBound"/> y
    /// <see cref="OldVersionUpperBound"/>, la resuelva usando la versión <see cref="NewVersion"/>.
    /// Esto permite que múltiples dependencias que requieren versiones diferentes del mismo
    /// ensamblado convivan en la misma aplicación sin lanzar excepciones de carga.
    ///
    /// Sólo se almacenan los componentes Major.Minor de las versiones porque el análisis
    /// de conflictos opera exclusivamente a ese nivel de granularidad.
    /// </summary>
    public class BindingRedirectData
    {
        /// <summary>
        /// Nombre simple del ensamblado al que aplica la redirección
        /// (valor del atributo "name" del elemento &lt;assemblyIdentity&gt;).
        /// </summary>
        public string AssemblyName { get; set; }

        /// <summary>
        /// Token de clave pública del ensamblado (valor del atributo "publicKeyToken"
        /// del elemento &lt;assemblyIdentity&gt;). Puede ser null para ensamblados sin nombre fuerte.
        /// </summary>
        public string PublicKeyToken { get; set; }

        /// <summary>
        /// Límite inferior (inclusive) del rango de versiones antiguas que serán redirigidas.
        /// Corresponde al primer componente del atributo "oldVersion" (p.ej. "1.0" en "1.0.0.0-2.0.0.0").
        /// Sólo contiene Major y Minor.
        /// </summary>
        public Version OldVersionLowerBound { get; set; }

        /// <summary>
        /// Límite superior (inclusive) del rango de versiones antiguas que serán redirigidas.
        /// Corresponde al segundo componente del atributo "oldVersion" (p.ej. "2.0" en "1.0.0.0-2.0.0.0").
        /// Si "oldVersion" especifica una sola versión, este valor es igual a <see cref="OldVersionLowerBound"/>.
        /// Sólo contiene Major y Minor.
        /// </summary>
        public Version OldVersionUpperBound { get; set; }

        /// <summary>
        /// Versión a la que se redirigen todas las versiones del rango anterior.
        /// Corresponde al atributo "newVersion" del elemento &lt;bindingRedirect&gt;.
        /// </summary>
        public Version NewVersion { get; set; }
    }
}
