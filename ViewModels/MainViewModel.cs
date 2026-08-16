using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Windows.Input;
using TimeTracker.Commons;
using TimeTracker.Data;
using TimeTracker.Models;

namespace TimeTracker.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Выбранная дата
    /// </summary>
    private DateTime _selectedDate = DateTime.Today;

    /// <summary>
    /// Общее затраченное время на выбранную дату
    /// </summary>
    private TimeSpan _totalTimeSpent;

    /// <summary>
    /// Кастомная коллекция, во избежание множественных аллокаций UI элементов
    /// </summary>
    public BulkObservableCollection<TodoTask> Tasks { get; } = new();

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (_selectedDate == value) return;
            _selectedDate = value;
            OnPropertyChanged(nameof(SelectedDate));
            UpdateTotalTimeForDate(); // Пересчитываем время при смене даты (п.5)
        }
    }

    public TimeSpan TotalTimeSpent
    {
        get => _totalTimeSpent;
        private set
        {
            _totalTimeSpent = value;
            OnPropertyChanged(nameof(TotalTimeSpent));
        }
    }

    public ICommand LoadTasksCommand { get; }
    public RelayCommand<string> AddTaskCommand { get; }

    public MainViewModel(AppDbContext db)
    {
        _db = db;
        LoadTasksCommand = new RelayCommand(async () => await LoadDataAsync());
        AddTaskCommand = new RelayCommand<string>(async (title) => await AddTaskAsync(title), (title) => !string.IsNullOrWhiteSpace(title));
    }

    /// <summary>
    /// Асинхронный метод загрузки, вызываемый из Loaded-события окна View
    /// </summary>
    public async Task LoadDataAsync()
    {
        // Вычитываем данные эффективным запросом
        var data = await _db.Tasks
            .AsNoTracking()
            .Include(t => t.TimeLogs)
            // TODO: сделать выбор 2х дат, по умолчанию вчера и сегодня
            //.Where(t => t.LastUpdatedAt > DateTime.Today.Date.AddDays(-1))
            .Where(t => t.LastUpdatedAt > _selectedDate)
            // Сортировка по убыванию даты/времени
            .OrderByDescending(t => t.LastUpdatedAt)
            .ToListAsync();

        // Добавляем пачкой без вызова 1000 ивентов (Оптимизация аллокаций)
        Tasks.ReplaceRange(data);

        UpdateTotalTimeForDate();
    }

    private async Task AddTaskAsync(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return;

        var newTask = new TodoTask
        {
            Title = title,
            LastUpdatedAt = DateTime.UtcNow
        };

        _db.Tasks.Add(newTask);
        await _db.SaveChangesAsync();

        // Добавляем новую задачу в начало списка
        Tasks.Insert(0, newTask);
    }

    /// <summary>
    /// Расчет времени за выбранный день
    /// </summary>
    private void UpdateTotalTimeForDate()
    {
        var targetDate = SelectedDate.Date;

        // Оптимизация: используем чистый LINQ без промежуточных аллокаций списков
        var totalTicks = Tasks
            .SelectMany(t => t.TimeLogs)
            .Where(log => log.Date.Date == targetDate)
            .Sum(log => log.Duration.Ticks);

        TotalTimeSpent = TimeSpan.FromTicks(totalTicks);
    }

    /// <summary>
    /// Вызывается таймером при остановке/трекинге задачи
    /// </summary>
    public async Task LogTimeAsync(TodoTask task, TimeSpan duration)
    {
        var today = DateTime.Today;
        // Обновляем метку времени
        task.LastUpdatedAt = DateTime.UtcNow;

        // Ищем, трекали ли мы уже эту таску сегодня
        var existingLog = task.TimeLogs.FirstOrDefault(l => l.Date.Date == today);
        if (existingLog != null)
        {
            existingLog.Duration += duration;
        }
        else
        {
            var newLog = new TimeLog { Date = today, Duration = duration };
            task.TimeLogs.Add(newLog);
            _db.TimeLogs.Add(newLog);
        }

        await _db.SaveChangesAsync();

        // Пересортируем коллекцию на UI в начало
        var index = Tasks.IndexOf(task);
        if (index > 0)
        {
            Tasks.Move(index, 0);
        }

        UpdateTotalTimeForDate();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}
