using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using TimeTracker.Data;
using TimeTracker.ViewModels;
using TimeTracker.Views;

namespace TimeTracker
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var services = new ServiceCollection();

            services.AddSingleton<IConfiguration>(config);

            // Регистрируем контекст БД с вычиткой строки подключения из appsettings
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(config.GetConnectionString("DefaultConnection")));

            // Регистрируем ViewModels и Windows
            services.AddTransient<MainViewModel>();
            services.AddSingleton<MainWindow>(sp => new MainWindow
            {
                DataContext = sp.GetRequiredService<MainViewModel>()
            });

            ServiceProvider = services.BuildServiceProvider();

            // Миграция/создание БД при старте (вне конструкторов классов)
            var db = ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

    }

}
