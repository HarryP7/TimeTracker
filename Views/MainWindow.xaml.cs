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
            await viewModel.Initialize();
        }
    }

    /// <summary>
    /// Нужен для корректной обработки события изменения состояния чекбокса знака плюс/минус в XAML
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void CheckBox_Unchecked(object sender, RoutedEventArgs e) { }

    /// <summary>
    /// Обработчик открытия ComboBox для автодополнения
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /*private void ComboBox_DropDownOpened(object sender, System.EventArgs e)
    {
        // Обновляем источник данных при открытии
        if (sender is System.Windows.Controls.ComboBox comboBox && comboBox.ItemsSource is System.Collections.IEnumerable)
        {
            // Просто обновляем, чтобы показать актуальные данные
            comboBox.IsDropDownOpen = true;
        }
    }*/
}