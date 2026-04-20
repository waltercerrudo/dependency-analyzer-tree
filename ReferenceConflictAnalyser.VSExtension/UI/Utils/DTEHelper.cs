using EnvDTE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceConflictAnalyser.VSExtension.UI.Utils
{
    /// <summary>
    /// Clase auxiliar estática que encapsula el acceso al servicio DTE (Development Tools
    /// Environment) de Visual Studio, proporcionando una interfaz simplificada para las
    /// operaciones de automatización del IDE que necesita la extensión.
    ///
    /// DTE es la interfaz COM principal de la API de automatización de Visual Studio.
    /// Permite controlar el IDE programáticamente: abrir archivos, gestionar proyectos,
    /// acceder a la solución, etc.
    ///
    /// La instancia de DTE se inyecta en <see cref="CurrentDTE"/> durante la inicialización
    /// del paquete (<see cref="ReferenceConflictAnalyserPackage.InitializeAsync"/>) y
    /// permanece disponible para toda la vida de la sesión de Visual Studio.
    /// </summary>
    public class DTEHelper
    {
        /// <summary>
        /// Instancia del servicio DTE de Visual Studio.
        /// Se establece una vez en <see cref="ReferenceConflictAnalyserPackage.InitializeAsync"/>
        /// y se usa por <see cref="SelectAssemblyWindowViewModel"/> para abrir el archivo DGML.
        /// </summary>
        public static DTE CurrentDTE { get; set; }

        /// <summary>
        /// Abre el archivo especificado en el editor de Visual Studio usando la API de automatización
        /// DTE (<see cref="EnvDTE.ItemOperations.OpenFile"/>). Visual Studio seleccionará
        /// automáticamente el editor adecuado según la extensión del archivo; para .dgml
        /// utilizará el editor DGML si está instalado.
        /// </summary>
        /// <param name="filePath">Ruta absoluta al archivo que se desea abrir en el IDE.</param>
        public static void OpenFile(string filePath)
        {
            CurrentDTE.ItemOperations.OpenFile(filePath);
        }
    }
}
