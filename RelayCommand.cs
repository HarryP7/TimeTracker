using System.Windows.Input;

namespace TimeTracker;

/// <summary>
/// Команды для обработки нажатий кнопок
/// </summary>
public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Predicate<T?>? _canExecute;

    public RelayCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(TranslateParameter(parameter));

    public void Execute(object? parameter) => _execute(TranslateParameter(parameter));


    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    private static T? TranslateParameter(object? parameter)
    {
        if (parameter is null) return default;
        if (parameter is T target) return target;

        // Позволяет безопасно приводить типы (например, из string в int при CommandParameter)
        return (T)Convert.ChangeType(parameter, typeof(T));
    }
}