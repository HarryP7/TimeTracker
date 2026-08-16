using System.Windows;
using TimeTracker.ViewModels;


namespace TimeTracker.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        this.Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            // Асинхронный безопасный старт без фризов UI-потока
            await viewModel.LoadDataAsync();
        }
    }

}