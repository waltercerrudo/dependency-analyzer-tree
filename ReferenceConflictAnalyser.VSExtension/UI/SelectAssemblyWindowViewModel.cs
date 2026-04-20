
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using ReferenceConflictAnalyser.VSExtension.UI.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;

namespace ReferenceConflictAnalyser.VSExtension.UI
{
    /// <summary>
    /// ViewModel (capa de presentación) de la ventana de selección de ensamblados en Visual Studio.
    /// Implementa el patrón MVVM con <see cref="INotifyPropertyChanged"/> para que los controles
    /// WPF de la vista se actualicen automáticamente al cambiar las propiedades.
    ///
    /// Responsabilidades:
    ///   - Exponer los comandos que la vista enlaza mediante data binding:
    ///       · <see cref="SelectAssemblyCommand"/>: abre un diálogo para elegir el ensamblado.
    ///       · <see cref="SelectConfigCommand"/>: abre un diálogo para elegir el config (habilitado
    ///         sólo cuando ya hay un ensamblado seleccionado).
    ///       · <see cref="AnalyzeConfigCommand"/>: ejecuta el análisis y abre el DGML en VS
    ///         (habilitado sólo cuando hay un ensamblado seleccionado).
    ///   - Mantener el estado de la UI: rutas seleccionadas, opciones de análisis y mensajes de aviso.
    ///   - Invocar el pipeline de análisis (<see cref="Workflow.CreateDependenciesGraph"/>) y
    ///     abrir el resultado en el editor DGML de Visual Studio mediante <see cref="DTEHelper.OpenFile"/>.
    ///   - Cerrar la ventana de herramientas tras ejecutar el análisis.
    ///   - Sugerir automáticamente el archivo de configuración cuando se selecciona un ensamblado.
    /// </summary>
    public class SelectAssemblyWindowViewModel : INotifyPropertyChanged
    {

        /// <summary>
        /// Inicializa el ViewModel configurando los comandos MVVM, los valores predeterminados
        /// y el mensaje de advertencia sobre el editor DGML.
        /// </summary>
        /// <param name="window">
        ///   Referencia al <see cref="ToolWindowPane"/> padre, necesaria para cerrar la ventana
        ///   después de ejecutar el análisis.
        /// </param>
        public SelectAssemblyWindowViewModel(ToolWindowPane window)
        {
            SelectAssemblyCommand = new GenericCommand<SelectAssemblyWindowViewModel, object>(this, SelectAssembly, (vm, p) => true);
            SelectConfigCommand = new GenericCommand<SelectAssemblyWindowViewModel, object>(this, SelectConfig, CanSelectConfig);
            AnalyzeConfigCommand = new GenericCommand<SelectAssemblyWindowViewModel, object>(this, Analyze, CanAnalyze);

            IgnoreSystemAssemblies = true;

            // Aviso sobre la dependencia del editor DGML de Visual Studio.
            Warning = "*The extension relies on the built-in DGML editor. In case you see a raw XML instead of a diagram run Visual Studio Installer, then: Modify -> Individual Components -> Code Tools -> Install DGML editor.";

            _window = window;
        }

        /// <summary>
        /// Comando para abrir el diálogo de selección de ensamblado (.dll / .exe).
        /// Siempre está habilitado (CanExecute = true).
        /// Al seleccionar un ensamblado, intenta sugerir automáticamente su archivo config.
        /// </summary>
        public ICommand SelectAssemblyCommand { get; private set; }

        /// <summary>
        /// Comando para abrir el diálogo de selección del archivo de configuración (.config).
        /// Sólo está habilitado cuando <see cref="AssemblyPath"/> no está vacío.
        /// El diálogo se abre en el mismo directorio que el ensamblado seleccionado.
        /// </summary>
        public ICommand SelectConfigCommand { get; private set; }

        /// <summary>
        /// Comando para ejecutar el análisis de dependencias con la configuración actual.
        /// Sólo está habilitado cuando <see cref="AssemblyPath"/> no está vacío.
        /// Tras el análisis, abre el DGML en VS y cierra esta ventana.
        /// </summary>
        public ICommand AnalyzeConfigCommand { get; private set; }

        /// <summary>
        /// Ruta al ensamblado (.dll o .exe) seleccionado para analizar.
        /// Notifica cambios para actualizar los estados habilitado/deshabilitado de los comandos.
        /// </summary>
        public string AssemblyPath
        {
            get { return _assemblyPath; }
            set { SetProperty(ref _assemblyPath, value, "AssemblyPath"); }
        }

        /// <summary>
        /// Ruta al archivo de configuración (.config) seleccionado.
        /// Se sugiere automáticamente al seleccionar el ensamblado y puede modificarse manualmente.
        /// </summary>
        public string ConfigPath
        {
            get { return _configPath; }
            set { SetProperty(ref _configPath, value, "ConfigPath"); }
        }

        /// <summary>
        /// Indica si los ensamblados del sistema ("mscorlib", "System", "System.*") deben
        /// excluirse del análisis. Valor predeterminado: true.
        /// </summary>
        public bool IgnoreSystemAssemblies
        {
            get { return _ignoreSystemAssemblies; }
            set { SetProperty(ref _ignoreSystemAssemblies, value, "IgnoreSystemAssemblies"); }
        }

        /// <summary>
        /// Mensaje de advertencia informativo mostrado en la UI, principalmente para indicar
        /// que el editor DGML de Visual Studio debe estar instalado para visualizar el resultado.
        /// </summary>
        public string Warning
        {
            get { return _warning; }
            set { SetProperty(ref _warning, value, "Warning"); }
        }

        /// <summary>
        /// Evento requerido por <see cref="INotifyPropertyChanged"/> para notificar a la vista
        /// cuando cambia el valor de alguna propiedad, provocando la actualización de los controles
        /// enlazados mediante data binding.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #region private

        private string _assemblyPath;
        private string _configPath;
        private bool _ignoreSystemAssemblies;
        private string _warning;
        private ToolWindowPane _window;

        /// <summary>
        /// Implementación genérica del patrón de notificación de cambio de propiedad.
        /// Actualiza el campo de respaldo sólo si el nuevo valor es diferente al actual,
        /// y dispara el evento <see cref="PropertyChanged"/> para notificar a la vista.
        /// La restricción <c>where T : IComparable</c> garantiza que la comparación es posible.
        /// </summary>
        /// <typeparam name="T">Tipo de la propiedad (debe ser comparable).</typeparam>
        /// <param name="field">Campo de respaldo de la propiedad (por referencia).</param>
        /// <param name="newValue">Nuevo valor que se quiere asignar.</param>
        /// <param name="propertyName">Nombre de la propiedad para el evento PropertyChanged.</param>
        private void SetProperty<T>(ref T field, T newValue, string propertyName)
           where T : IComparable
        {
            if ((field == null && newValue != null)
                || field.CompareTo(newValue) != 0)
            {
                field = newValue;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        /// <summary>
        /// Acción del comando <see cref="SelectAssemblyCommand"/>.
        /// Abre un diálogo de apertura de archivo filtrado a .dll y .exe, asigna la ruta
        /// seleccionada a <see cref="AssemblyPath"/> y llama a
        /// <see cref="ConfigurationHelper.TrySuggestConfigFile"/> para sugerir automáticamente
        /// el archivo de configuración asociado.
        /// </summary>
        private void SelectAssembly(SelectAssemblyWindowViewModel vm, object parameter)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "DLL, executable|*.dll;*.exe";
            if (dlg.ShowDialog() == true)
            {
                AssemblyPath = dlg.FileName;

                // Intentar sugerir automáticamente el config correspondiente al ensamblado.
                string configPath;
                if (ConfigurationHelper.TrySuggestConfigFile(AssemblyPath, out configPath))
                    ConfigPath = configPath;
                else
                    ConfigPath = "";
            }
        }

        /// <summary>
        /// Acción del comando <see cref="SelectConfigCommand"/>.
        /// Abre un diálogo de apertura de archivo filtrado a .config, inicializado en el
        /// directorio del ensamblado seleccionado para facilitar la navegación al usuario.
        /// </summary>
        private void SelectConfig(SelectAssemblyWindowViewModel vm, object parameter)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Configuration files|*.config";
            dlg.InitialDirectory = Path.GetDirectoryName(AssemblyPath);
            if (dlg.ShowDialog() == true)
            {
                ConfigPath = dlg.FileName;
            }
        }

        /// <summary>
        /// Condición de habilitación del comando <see cref="SelectConfigCommand"/>.
        /// El config sólo puede seleccionarse cuando ya hay un ensamblado especificado,
        /// para que el diálogo se abra en el directorio correcto.
        /// </summary>
        private bool CanSelectConfig(SelectAssemblyWindowViewModel vm, object parameter)
        {
            return !string.IsNullOrWhiteSpace(AssemblyPath);
        }

        /// <summary>
        /// Acción del comando <see cref="AnalyzeConfigCommand"/>.
        /// Ejecuta el análisis invocando <see cref="RunAnalysis"/> y después cierra la
        /// ventana de herramientas para no bloquear la vista del grafo DGML generado.
        /// </summary>
        private void Analyze(SelectAssemblyWindowViewModel vm, object parameter)
        {
            RunAnalysis();
            CloseWindow();
        }

        /// <summary>
        /// Cierra la ventana de herramientas ocultando su frame de VS.
        /// Se llama tras ejecutar el análisis para que el usuario pueda ver el resultado DGML.
        /// </summary>
        private void CloseWindow()
        {
            ((IVsWindowFrame)_window.Frame).Hide();
        }

        /// <summary>
        /// Ejecuta el pipeline completo de análisis de dependencias:
        ///   1. Llama a <see cref="Workflow.CreateDependenciesGraph"/> con los parámetros actuales.
        ///   2. Guarda el XML DGML resultante en un archivo temporal con extensión .dgml.
        ///   3. Abre el archivo en el editor DGML de Visual Studio mediante <see cref="DTEHelper.OpenFile"/>.
        ///
        /// Los errores se muestran en un MessageBox para informar al usuario sin propagar la excepción.
        /// </summary>
        private void RunAnalysis()
        {
            try
            {
                var graphDgml = Workflow.CreateDependenciesGraph(AssemblyPath, ConfigPath, IgnoreSystemAssemblies);

                // Usar un archivo temporal para no requerir que el usuario especifique una ruta de salida.
                var path = Path.GetTempFileName();
                path = Path.ChangeExtension(path, ".dgml");
                File.WriteAllText(path, graphDgml);

                DTEHelper.OpenFile(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Condición de habilitación del comando <see cref="AnalyzeConfigCommand"/>.
        /// El análisis sólo puede ejecutarse cuando hay un ensamblado seleccionado.
        /// </summary>
        private bool CanAnalyze(SelectAssemblyWindowViewModel vm, object parameter)
        {
            return !string.IsNullOrWhiteSpace(AssemblyPath);
        }

  



        #endregion
    }
}
