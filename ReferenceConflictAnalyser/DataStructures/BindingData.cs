using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceConflictAnalyser.DataStructures
{
    /// <summary>
    /// Contenedor inmutable que agrupa toda la información de configuración de enlace
    /// de ensamblados extraída del archivo App.config o Web.config:
    ///   - <see cref="BindingRedirects"/>: redirecciones de versión que el CLR aplica en tiempo
    ///     de ejecución para resolver conflictos de versión de ensamblados.
    ///   - <see cref="SubFolders"/>: subdirectorios adicionales donde el CLR busca ensamblados
    ///     (probing paths definidos en &lt;probing privatePath&gt;).
    ///
    /// Se utiliza como objeto de transferencia entre <see cref="ConfigurationHelper"/>
    /// y los pasos posteriores del pipeline (<see cref="ReferenceReader"/> y
    /// <see cref="ReferenceAnalyser"/>).
    ///
    /// Si no se proporciona un archivo de configuración, o éste no contiene las secciones
    /// relevantes, ambas colecciones quedan vacías (nunca null), lo que simplifica el código
    /// consumidor al evitar comprobaciones de nulidad.
    /// </summary>
    public class BindingData
    {
        /// <summary>
        /// Crea un objeto <see cref="BindingData"/> con las colecciones proporcionadas.
        /// Los parámetros null se sustituyen por colecciones vacías para garantizar
        /// que las propiedades nunca devuelvan null.
        /// </summary>
        /// <param name="bindingRedirects">
        ///   Redirecciones de enlace leídas del config, o null si no hay ninguna.
        /// </param>
        /// <param name="subFolders">
        ///   Rutas de sondeo adicionales leídas del config, o null si no hay ninguna.
        /// </param>
        public BindingData(IEnumerable<BindingRedirectData> bindingRedirects = null, IEnumerable<string> subFolders = null)
        {
            BindingRedirects = bindingRedirects ?? Enumerable.Empty<BindingRedirectData>();
            SubFolders = subFolders ?? Enumerable.Empty<string>(); ;
        }

        /// <summary>
        /// Subdirectorios adicionales (relativos al directorio del ensamblado raíz) donde el CLR
        /// buscará ensamblados referenciados. Provienen del atributo "privatePath" del elemento
        /// &lt;probing&gt; en la sección &lt;assemblyBinding&gt; del archivo de configuración.
        /// Nunca es null; puede ser vacío.
        /// </summary>
        public IEnumerable<string> SubFolders { get; private set; }

        /// <summary>
        /// Lista de redirecciones de enlace que indican al CLR cómo resolver versiones antiguas
        /// de ensamblados a versiones más recientes. Se usan en <see cref="ReferenceAnalyser"/>
        /// para marcar conflictos como resueltos (<see cref="Category.VersionsConflictResolved"/>).
        /// Nunca es null; puede ser vacío.
        /// </summary>
        public IEnumerable<BindingRedirectData> BindingRedirects { get; private set; }
    }
}
