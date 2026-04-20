using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ReferenceConflictAnalyser.VSExtension.UI.Utils
{
    /// <summary>
    /// Implementación genérica y reutilizable de <see cref="ICommand"/> para el patrón MVVM en WPF.
    /// Permite definir comandos de forma declarativa en el ViewModel sin necesidad de crear
    /// una clase separada por cada comando.
    ///
    /// Características:
    ///   - Acepta el ViewModel (<typeparamref name="TViewModel"/>) y el parámetro del comando
    ///     (<typeparamref name="TCommandParameter"/>) como tipos genéricos para evitar castings.
    ///   - Se suscribe al evento <see cref="INotifyPropertyChanged.PropertyChanged"/> del ViewModel
    ///     para disparar <see cref="CanExecuteChanged"/> automáticamente cuando cualquier propiedad
    ///     cambia, actualizando así el estado habilitado/deshabilitado de los controles enlazados.
    ///   - La lógica de ejecución (<paramref name="execute"/>) y la condición de habilitación
    ///     (<paramref name="canExecute"/>) se pasan como delegados en el constructor.
    ///
    /// Restricción de tipo: <typeparamref name="TViewModel"/> debe implementar
    /// <see cref="INotifyPropertyChanged"/> para garantizar que el comando puede reaccionar
    /// a los cambios de estado del ViewModel.
    /// </summary>
    /// <typeparam name="TViewModel">
    ///   Tipo del ViewModel propietario del comando. Debe implementar <see cref="INotifyPropertyChanged"/>.
    /// </typeparam>
    /// <typeparam name="TCommandParameter">
    ///   Tipo del parámetro que se pasa al comando desde la vista mediante CommandParameter.
    /// </typeparam>
    public class GenericCommand<TViewModel, TCommandParameter> : ICommand
       where TViewModel : INotifyPropertyChanged
    {
        Func<TViewModel, TCommandParameter, bool> _predicate;
        Action<TViewModel, TCommandParameter> _execute;
        TViewModel _viewModel;

        /// <summary>
        /// Inicializa el comando con su ViewModel, acción de ejecución y condición de habilitación.
        /// Se suscribe a <see cref="INotifyPropertyChanged.PropertyChanged"/> del ViewModel para
        /// propagar los cambios de estado al sistema de comandos de WPF.
        /// </summary>
        /// <param name="viewModel">ViewModel al que pertenece este comando.</param>
        /// <param name="execute">
        ///   Acción que se ejecuta al invocar el comando. Recibe el ViewModel y el parámetro.
        /// </param>
        /// <param name="canExecute">
        ///   Función que determina si el comando puede ejecutarse. Recibe el ViewModel y el
        ///   parámetro; devuelve true si el comando está habilitado.
        /// </param>
        public GenericCommand(TViewModel viewModel, Action<TViewModel, TCommandParameter> execute, Func<TViewModel, TCommandParameter, bool> canExecute)
        {
            _viewModel = viewModel;
            _predicate = canExecute;
            _execute = execute;

            // Al cambiar cualquier propiedad del ViewModel, WPF reevalúa CanExecute para
            // actualizar el estado habilitado/deshabilitado de los controles enlazados.
            _viewModel.PropertyChanged += _viewModel_PropertyChanged;
        }

        /// <summary>
        /// Propaga el evento PropertyChanged del ViewModel como CanExecuteChanged del comando.
        /// WPF escucha este evento para saber cuándo debe volver a evaluar CanExecute.
        /// </summary>
        private void _viewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            CanExecuteChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Evento requerido por <see cref="ICommand"/>. Se dispara cuando el estado
        /// de habilitación del comando puede haber cambiado, indicando a WPF que debe
        /// reevaluar <see cref="CanExecute"/>.
        /// </summary>
        public event EventHandler CanExecuteChanged;

        /// <summary>
        /// Evalúa si el comando puede ejecutarse invocando el predicado <c>canExecute</c>
        /// proporcionado en el constructor con el ViewModel actual y el parámetro del comando.
        /// </summary>
        /// <param name="parameter">Parámetro del comando proveniente de la vista (puede ser null).</param>
        /// <returns>true si el comando está habilitado; false en caso contrario.</returns>
        public bool CanExecute(object parameter)
        {
            return _predicate(_viewModel, (TCommandParameter)parameter);
        }

        /// <summary>
        /// Ejecuta la acción del comando invocando el delegado <c>execute</c>
        /// proporcionado en el constructor con el ViewModel actual y el parámetro del comando.
        /// </summary>
        /// <param name="parameter">Parámetro del comando proveniente de la vista (puede ser null).</param>
        public void Execute(object parameter)
        {
            _execute(_viewModel, (TCommandParameter)parameter);
        }
    }
}
