using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;
using TimeTracker.Data;
using TimeTracker.Data.Interfaces;
using TimeTracker.Data.Repositories;
using TimeTracker.ViewModels;
using TimeTracker.Views;

namespace TimeTracker;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;
    public static IHost? AppHost { get; private set; }

    public App()
    {
        // Настраиваем хост приложения (как в ASP.NET Core)
        AppHost = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Получаем строку подключения из appsettings.json
                string? connectionString = context.Configuration.GetConnectionString("DefaultConnection");

                // Регистрируем DbContext в DI-контейнере
                services.AddDbContext<AppDbContext>(options =>
                    options.UseNpgsql(connectionString));

                // Регистрируем ViewModels и Windows
                services.AddTransient<MainViewModel>();
                // Регистрируем главное окно
                services.AddSingleton<MainWindow>(sp => new MainWindow
                {
                    DataContext = sp.GetRequiredService<MainViewModel>()
                });

                // Регистрация репозиториев
                services.AddScoped<ITaskRepository, TaskRepository>();
                services.AddScoped<ISubTaskRepository, SubTaskRepository>();
                services.AddScoped<IGeneralInfoTimeDayRepository, GeneralInfoTimeDayRepository>();

            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        // 1. Запускаем хост приложения
        await AppHost!.StartAsync();

        // 2. Автоматически применяем миграции при старте
        try
        {
            // Создаем временную область (Scope) для безопасного разрешения scoped-сервисов
            using (var scope = AppHost.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Применяем все ожидающие миграции (если базы нет, она создастся)
                await db.Database.MigrateAsync();
            }
        }
        catch (Exception ex)
        {
            // Здесь крайне важно обработать ошибку (например, если PostgreSQL выключен)
            MessageBox.Show($"Ошибка при обновлении базы данных: {ex.Message}",
                            "Критическая ошибка",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);

            // Корректно завершаем приложение, так как без БД оно работать не сможет
            Shutdown();
            return;
        }

        //var config = new ConfigurationBuilder()
        //    .SetBasePath(AppContext.BaseDirectory)
        //    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        //    .Build();

        //var services = new ServiceCollection();

        //services.AddSingleton<IConfiguration>(config);

        // Регистрируем контекст БД с вычиткой строки подключения из appsettings
        //services.AddDbContext<AppDbContext>(options =>
        //    options.UseNpgsql(config.GetConnectionString("DefaultConnection")));

        // Регистрируем ViewModels и Windows
        //services.AddTransient<MainViewModel>();
        //services.AddSingleton<MainWindow>(sp => new MainWindow
        //{
        //    DataContext = sp.GetRequiredService<MainViewModel>()
        //});

        //ServiceProvider = services.BuildServiceProvider();

        // Миграция/создание БД при старте (вне конструкторов классов)
        //var db = ServiceProvider.GetRequiredService<AppDbContext>();
        //await db.Database.MigrateAsync();
        //db.Database.EnsureCreated();

        // 3. Если миграции прошли успешно — открываем главное окно
        var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await AppHost!.StopAsync();
        base.OnExit(e);
    }
}
