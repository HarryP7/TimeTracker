using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using TimeTracker.Data;
using TimeTracker.Models;

namespace TimeTracker.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly AppDbContext _db;
    private DispatcherTimer? _globalTimer;
    private TaskModel? _activeTask;

    private string _newTaskName = string.Empty;
    private DateTime _selectedDate = DateTime.Today;
    private string _totalTimeFormatted = "00:00:00";

    public ObservableCollection<TaskModel> Tasks { get; } = new();

    public string NewTaskName
    {
        get => _newTaskName;
        set
        {
            _newTaskName = value;
            OnPropertyChanged();
        }
    }

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            _selectedDate = value;
            OnPropertyChanged();
            LoadTasksAndLogsAsync();
        }
    }
    public string TotalTimeFormatted { get => _totalTimeFormatted; set { _totalTimeFormatted = value; OnPropertyChanged(); } }

    public ICommand AddCommand { get; }
    public ICommand ToggleTimerCommand { get; }

    public MainViewModel(AppDbContext db)
    {
        _db = db;
        AddCommand = new RelayCommand<object>(async _ => await AddTaskAsync());
        ToggleTimerCommand = new RelayCommand<TaskModel>(async (task) => await ToggleTimerAsync(task));
    }

    public async Task Initialize()
    {
        await LoadTasksAndLogsAsync();
        SetupGlobalTimer();
    }

    private async Task LoadTasksAndLogsAsync()
    {
        var dateOnly = DateOnly.FromDateTime(SelectedDate);

        // Вытаскиваем логи за выбранный день
        var logs = await _db.TimeLogs
            .AsNoTracking()
            .Where(l => l.Date == dateOnly)
            .ToDictionaryAsync(l => l.TaskId, l => l.SecondsSpent);

        // Сортировка: по убыванию даты и времени последнего выполнения
        var allTasks = await _db.Tasks
            .AsNoTracking()
            .OrderByDescending(t => t.LastUpdatedAt)
            .ToArrayAsync();

        Tasks.Clear();
        foreach (var task in allTasks)
        {
            task.CurrentDaySeconds = logs.TryGetValue(task.Id, out int seconds) ? seconds : 0;
            // Если задача была активна, но мы сменили дату - визуально останавливаем её отображение
            if (task == _activeTask && dateOnly != DateOnly.FromDateTime(DateTime.Today)) task.IsRunning = false;
            Tasks.Add(task);
        }

        CalculateTotalTime();
    }

    private void SetupGlobalTimer()
    {
        // Оптимизация: один таймер на всё приложение вместо таймера в каждом объекте
        _globalTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _globalTimer.Tick += async (s, e) =>
        {
            if (_activeTask != null)
            {
                _activeTask.CurrentDaySeconds++;
                CalculateTotalTime();

                // Оптимизация аллокаций и диска: батчинг. Сохраняем в БД каждые 10 секунд или при стопе
                if (_activeTask.CurrentDaySeconds % 10 == 0)
                {
                    await SaveCurrentProgressAsync();
                }
            }
        };
        _globalTimer.Start();
    }

    private async Task AddTaskAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTaskName)) return;

        var task = new TaskModel
        {
            Name = NewTaskName,
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync();

        // Добавляем в начало списка
        Tasks.Insert(0, task);
        NewTaskName = string.Empty;
    }

    private async Task ToggleTimerAsync(TaskModel? task)
    {
        if (task == null) return;

        if (task.IsRunning)
        {
            task.IsRunning = false;
            _activeTask = null;
            await SaveCurrentProgressAsync();
        }
        else
        {
            // Останавливаем старую задачу
            if (_activeTask != null)
            {
                _activeTask.IsRunning = false;
                await SaveCurrentProgressAsync();
            }

            // Переключаем на сегодняшний день, если запуск идет из прошлого
            if (SelectedDate != DateTime.Today) SelectedDate = DateTime.Today;

            _activeTask = task;
            _activeTask.IsRunning = true;

            _activeTask.LastUpdatedAt = DateTime.UtcNow;
            _db.Entry(_activeTask).State = EntityState.Modified;
            await _db.SaveChangesAsync();
        }
    }

    private async Task SaveCurrentProgressAsync()
    {
        if (_activeTask == null) return;

        var dateOnly = DateOnly.FromDateTime(DateTime.Today);
        var log = await _db.TimeLogs
            .Where(l => l.TaskId == _activeTask.Id && l.Date == dateOnly)
            .FirstOrDefaultAsync();

        if (log == null)
        {
            log = new TaskTimeLog
            {
                TaskId = _activeTask.Id,
                Date = dateOnly,
                SecondsSpent = _activeTask.CurrentDaySeconds
            };
            _db.TimeLogs.Add(log);
        }
        else
        {
            log.SecondsSpent = _activeTask.CurrentDaySeconds;
        }
        await _db.SaveChangesAsync();
    }

    private void CalculateTotalTime()
    {
        int total = Tasks.Sum(t => t.CurrentDaySeconds);
        var ts = TimeSpan.FromSeconds(total);
        TotalTimeFormatted = string.Create(null, $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}");
    }

    public async Task CloseConnection()
    {
        _globalTimer?.Stop();
        await SaveCurrentProgressAsync();
        _db.Dispose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string prop = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}