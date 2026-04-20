//------------------------------------------------------------------------------
// <copyright file="ReferenceConflictAnalyserPackage.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using EnvDTE;
using ReferenceConflictAnalyser.VSExtension.UI.Utils;
using System.Threading;
using System.Threading.Tasks;

namespace ReferenceConflictAnalyser.VSExtension
{
    /// <summary>
    /// Paquete principal de la extensión de Visual Studio para el análisis de conflictos
    /// de dependencias de ensamblados .NET.
    ///
    /// Esta clase implementa el punto de entrada de la extensión VSIX mediante el patrón
    /// <see cref="AsyncPackage"/> del Managed Package Framework (MPF). Es responsable de:
    ///   1. Registrarse en el shell de Visual Studio con el GUID único definido en
    ///      <see cref="PackageGuidString"/>.
    ///   2. Registrar el comando de menú (<see cref="UI.MenuCommand"/>) en el menú Herramientas
    ///      de Visual Studio durante la inicialización asíncrona.
    ///   3. Obtener y exponer la instancia del servicio DTE (Development Tools Environment)
    ///      a través de <see cref="DTEHelper.CurrentDTE"/>, que permite abrir archivos
    ///      en el editor de Visual Studio desde el ViewModel.
    ///   4. Registrar la ventana de herramientas (<see cref="UI.SelectAssemblyWindow"/>)
    ///      como una ventana flotante de instancia única con dimensiones predefinidas.
    ///
    /// El paquete se carga de forma asíncrona en segundo plano (<c>AllowsBackgroundLoading = true</c>)
    /// para no bloquear el inicio de Visual Studio.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "2.0", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(ReferenceConflictAnalyserPackage.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    [ProvideToolWindow(typeof(UI.SelectAssemblyWindow), Style = VsDockStyle.Float, Height = 210, Width = 600, MultiInstances = false, Transient = true)]
    public sealed class ReferenceConflictAnalyserPackage : AsyncPackage
    {
        /// <summary>
        /// GUID único que identifica este paquete dentro del shell de Visual Studio.
        /// Debe coincidir con el GUID registrado en el archivo .vsixmanifest y en el
        /// archivo .pkgdef generado durante la compilación de la extensión.
        /// </summary>
        public const string PackageGuidString = "23edc301-292e-4c85-a285-2b65941bb8ab";

        /// <summary>
        /// Constructor del paquete. Se ejecuta cuando Visual Studio crea la instancia del paquete,
        /// antes de que éste sea "situado" (sited) en el entorno IDE. En este punto no están
        /// disponibles los servicios de Visual Studio, por lo que no se debe hacer ninguna
        /// inicialización que los requiera aquí.
        /// </summary>
        public ReferenceConflictAnalyserPackage()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.
        }

        #region Package Members

        /// <summary>
        /// Método de inicialización asíncrona del paquete. Se ejecuta después de que el paquete
        /// ha sido situado en el entorno IDE y los servicios de Visual Studio están disponibles.
        ///
        /// Realiza los siguientes pasos:
        ///   1. Llama a la inicialización base (<see cref="AsyncPackage.InitializeAsync"/>).
        ///   2. Cambia al hilo principal de la UI (<c>JoinableTaskFactory.SwitchToMainThreadAsync</c>)
        ///      porque el registro de comandos de menú y el acceso a DTE requieren el hilo principal.
        ///   3. Inicializa el singleton <see cref="UI.MenuCommand.Instance"/> que registra el
        ///      comando "Analizar dependencias de ensamblados" en el menú Herramientas.
        ///   4. Obtiene el servicio DTE y lo inyecta en <see cref="DTEHelper.CurrentDTE"/> para
        ///      que el ViewModel pueda abrir archivos en el editor de VS.
        /// </summary>
        /// <param name="cancellationToken">Token para cancelar la inicialización si VS se está cerrando.</param>
        /// <param name="progress">Permite reportar el progreso de la inicialización a VS.</param>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // Cambiar al hilo principal antes de registrar comandos de menú y acceder a DTE.
            await base.InitializeAsync(cancellationToken, progress);

            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            UI.MenuCommand.Initialize(this);
            DTEHelper.CurrentDTE = await GetServiceAsync(typeof(DTE)) as DTE;

        }

        #endregion
        
    }
}
