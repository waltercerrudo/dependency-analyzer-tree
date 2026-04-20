//------------------------------------------------------------------------------
// <copyright file="MenuCommand.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace ReferenceConflictAnalyser.VSExtension.UI
{
    /// <summary>
    /// Comando de menú que integra el analizador de dependencias en el menú Herramientas
    /// de Visual Studio.
    ///
    /// Sigue el patrón singleton: sólo existe una instancia, creada mediante
    /// <see cref="Initialize"/> e inaccesible desde el exterior salvo a través de
    /// <see cref="Instance"/>.
    ///
    /// Al hacer clic en el elemento de menú, el método <see cref="MenuItemCallback"/>
    /// localiza o crea la ventana de herramientas <see cref="SelectAssemblyWindow"/> y
    /// la hace visible en el IDE de Visual Studio.
    ///
    /// El comando está identificado por el par (CommandSet GUID, CommandId) que debe coincidir
    /// con la definición en el archivo de tabla de comandos (.vsct) de la extensión.
    /// </summary>
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class MenuCommand
    {
        /// <summary>
        /// Command ID.
        /// Identificador numérico del comando dentro del grupo de comandos <see cref="CommandSet"/>.
        /// Debe coincidir con el valor definido en el archivo .vsct de la extensión.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// GUID del conjunto de comandos al que pertenece este comando.
        /// Identifica el grupo de comandos de esta extensión en el shell de Visual Studio y
        /// debe coincidir con el GUID definido en el archivo .vsct.
        /// </summary>
        public static readonly Guid CommandSet = new Guid("f016c470-17a2-4db5-9e3f-4177c3396288");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        /// <summary>Referencia al paquete VS propietario de este comando.</summary>
        private readonly Package package;

        /// <summary>
        /// Constructor privado. Registra el comando en el servicio de menús OLE de Visual Studio
        /// (<see cref="OleMenuCommandService"/>) asociándolo con el callback <see cref="MenuItemCallback"/>.
        /// El constructor es privado porque esta clase sigue el patrón singleton; la creación
        /// se realiza únicamente a través de <see cref="Initialize"/>.
        /// </summary>
        /// <param name="package">Paquete VS propietario (no nulo).</param>
        private MenuCommand(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            this.package = package;

            OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandID = new CommandID(CommandSet, CommandId);
                var menuItem = new System.ComponentModel.Design.MenuCommand(this.MenuItemCallback, menuCommandID);
                commandService.AddCommand(menuItem);
            }
        }

        /// <summary>
        /// Instancia singleton del comando. Se inicializa mediante <see cref="Initialize"/>
        /// y es accesible de sólo lectura desde el exterior.
        /// </summary>
        public static MenuCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Proveedor de servicios del paquete VS propietario. Permite obtener servicios de VS
        /// como <see cref="IMenuCommandService"/>, <see cref="IVsShell"/>, etc.
        /// </summary>
        private IServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Crea e inicializa la instancia singleton del comando.
        /// Debe llamarse una única vez desde <see cref="ReferenceConflictAnalyserPackage.InitializeAsync"/>
        /// cuando el hilo principal de la UI ya está disponible.
        /// </summary>
        public static void Initialize(Package package)
        {
            Instance = new MenuCommand(package);
        }

        /// <summary>
        /// Callback invocado cuando el usuario hace clic en el elemento de menú
        /// "Analizar dependencias de ensamblados" en el menú Herramientas de Visual Studio.
        ///
        /// Obtiene (o crea si no existe) la instancia 0 de <see cref="SelectAssemblyWindow"/>,
        /// que es la ventana flotante donde el usuario selecciona el ensamblado a analizar.
        /// A continuación llama a <see cref="IVsWindowFrame.Show"/> para hacerla visible.
        /// </summary>
        /// <param name="sender">Objeto que generó el evento (el elemento de menú).</param>
        /// <param name="e">Argumentos del evento (no usados).</param>
        private void MenuItemCallback(object sender, EventArgs e)
        {
            // Obtener o crear la instancia 0 de la ventana de herramientas.
            // El tercer parámetro (true) indica que se debe crear si no existe.
            ToolWindowPane window = this.package.FindToolWindow(typeof(SelectAssemblyWindow), 0, true);
            if ((null == window) || (null == window.Frame))
            {
                throw new NotSupportedException("Cannot create tool window");
            }

            IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }
    }
}
