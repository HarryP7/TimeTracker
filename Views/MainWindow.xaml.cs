using System.Windows;
using TimeTracker.ViewModels;


namespace TimeTracker.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel; // Привязываем логику к визуалу
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _viewModel.CloseConnection(); // Безопасно закрываем таймеры и БД при выходе
        base.OnClosing(e);
    }
}