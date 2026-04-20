//------------------------------------------------------------------------------
// <copyright file="SelectAssemblyWindowControl.xaml.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace ReferenceConflictAnalyser.VSExtension.UI
{
    using Microsoft.VisualStudio.Shell;
    using System.Diagnostics.CodeAnalysis;
    using System.Windows;
    using System.Windows.Controls;

    /// <summary>
    /// Code-behind del control de usuario WPF definido en SelectAssemblyWindowControl.xaml.
    ///
    /// Este control implementa la vista (View) del patrón MVVM y es responsable únicamente de:
    ///   1. Inicializar los componentes XAML mediante <see cref="InitializeComponent"/>.
    ///   2. Crear e inyectar el <see cref="SelectAssemblyWindowViewModel"/> como DataContext,
    ///      estableciendo así el enlace MVVM entre la vista y el ViewModel.
    ///
    /// Toda la lógica de presentación y los comandos residen en
    /// <see cref="SelectAssemblyWindowViewModel"/>; este code-behind no contiene lógica de negocio.
    ///
    /// El control es instanciado por <see cref="SelectAssemblyWindow"/> y recibe la referencia
    /// al <see cref="ToolWindowPane"/> padre para que el ViewModel pueda cerrar la ventana
    /// después de ejecutar el análisis.
    /// </summary>
    public partial class SelectAssemblyWindowControl : UserControl
    {
        /// <summary>
        /// Inicializa el control de usuario WPF y establece el DataContext con una instancia
        /// de <see cref="SelectAssemblyWindowViewModel"/> pasando la referencia al pane padre.
        /// </summary>
        /// <param name="parentWindow">
        ///   Referencia al <see cref="ToolWindowPane"/> que contiene este control.
        ///   Se usa en el ViewModel para cerrar la ventana tras completar el análisis.
        /// </param>
        public SelectAssemblyWindowControl(ToolWindowPane parentWindow)
        {
            this.InitializeComponent();
            this.DataContext = new SelectAssemblyWindowViewModel(parentWindow);
        }

        /// <summary>
        /// Manejador del evento Click del botón de depuración generado por la plantilla de VS.
        /// No tiene funcionalidad en la versión final; permanece como código generado automáticamente.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
        [SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions", Justification = "Sample code")]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Default event handler naming pattern")]
        private void button1_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                string.Format(System.Globalization.CultureInfo.CurrentUICulture, "Invoked '{0}'", this.ToString()),
                "SelectAssemblyWindow");
        }
    }
}