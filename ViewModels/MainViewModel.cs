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
    private SubTaskLog? _activeSubTask;

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
    public string TotalTimeFormatted
    {
        get => _totalTimeFormatted;
        set
        {
            _totalTimeFormatted = value;
            OnPropertyChanged();
        }
    }

    public ICommand AddCommand { get; }
    public ICommand ToggleTimerCommand { get; }
    public ICommand AddSubTaskCommand { get; }


    public MainViewModel(AppDbContext db)
    {
        _db = db;
        AddCommand = new RelayCommand<object>(async _ => await AddTaskAsync());
        ToggleTimerCommand = new RelayCommand<SubTaskLog>(async (subTask) => await ToggleTimerAsync(subTask));
        AddSubTaskCommand = new RelayCommand<TaskModel>(async (task) => await AddSubTaskAsync(task));
    }

    public async Task Initialize()
    {
        await LoadTasksAndLogsAsync();
        SetupGlobalTimer();
    }

    private void SetupGlobalTimer()
    {
        // Оптимизация: один таймер на всё приложение вместо таймера в каждом объекте
        _globalTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _globalTimer.Tick += async (s, e) =>
        {
            if (_activeSubTask != null)
            {
                _activeSubTask.SecondsSpent++;

                // Пересчитываем сумму для родительской задачи и общий итог
                var parent = Tasks.FirstOrDefault(t => t.Id == _activeSubTask.TaskId);
                if (parent != null) parent.TotalDaySeconds = parent.SubTasks.Sum(s => s.SecondsSpent);

                CalculateTotalTime();

                // Оптимизация аллокаций и диска: батчинг. Сохраняем в БД каждые 10 секунд или при стопе
                if (_activeSubTask.SecondsSpent % 10 == 0)
                {
                    await SaveCurrentProgressAsync();
                }
            }
        };
    }

    private async Task LoadTasksAndLogsAsync()
    {
        var dateOnly = DateOnly.FromDateTime(SelectedDate);

        // Сортировка: по убыванию даты и времени последнего выполнения
        var allTasks = await _db.Tasks
            .AsNoTracking()
            .OrderByDescending(t => t.LastUpdatedAt)
            .ToArrayAsync();

        // Вытаскиваем подзадачи за выбранный день
        var allLogs = await _db.SubTaskLogs
            .AsNoTracking()
            .Where(l => l.CreatedAt == dateOnly)
            .OrderByDescending(t => t.LastUpdatedAt)
            .ToArrayAsync();

        Tasks.Clear();

        // Вытаскиваем логи за выбранный день
        //var logs = await _db.SubTaskLogs
        //    .AsNoTracking()
        //    .Where(l => l.Date == dateOnly)
        //    .ToDictionaryAsync(l => l.TaskId, l => l.SecondsSpent);

        foreach (var task in allTasks)
        {
            task.SubTasks.Clear();

            var taskLogs = allLogs
                .Where(l => l.TaskId == task.Id)
                .ToList();

            // Если логов/подзадач на этот день еще нет, создаем дефолтный лог для основной задачи
            if (taskLogs.Count == 0)
            {
                var defaultLog = new SubTaskLog
                {
                    TaskId = task.Id,
                    Name = null,
                    CreatedAt = dateOnly,
                    SecondsSpent = 0
                };
                taskLogs.Add(defaultLog);
            }

            // Если задача была активна, но мы сменили дату - визуально останавливаем её отображение
            //if (task == _activeSubTask && dateOnly != DateOnly.FromDateTime(DateTime.Today)) task.IsRunning = false;

            foreach (var log in taskLogs)
            {
                if (_activeSubTask != null && _activeSubTask.Id == log.Id && dateOnly == DateOnly.FromDateTime(DateTime.Today))
                {
                    // NOTE: Зачем?
                    log.IsRunning = true;
                }
                task.SubTasks.Add(log);
            }

            task.TotalDaySeconds = task.SubTasks.Sum(s => s.SecondsSpent);
            Tasks.Add(task);
        }

        CalculateTotalTime();
    }

    /// <summary>
    /// Добавление основной задачи
    /// </summary>
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

        // Сразу создаем дефолтную запись времени на сегодня
        var defaultLog = new SubTaskLog
        {
            TaskId = task.Id,
            Name = null,
            CreatedAt = DateOnly.FromDateTime(DateTime.Today),
            SecondsSpent = 0
        };
        _db.SubTaskLogs.Add(defaultLog);
        await _db.SaveChangesAsync();

        // TODO: Удалить?
        task.SubTasks.Add(defaultLog);

        // Добавляем в начало списка
        Tasks.Insert(0, task);
        NewTaskName = string.Empty;
    }

    /// <summary>
    /// Добавление подзадачи
    /// </summary>
    private async Task AddSubTaskAsync(TaskModel? parentTask)
    {
        if (parentTask == null) return;

        // Используем стандартный InputBox от VB для быстрого ввода без создания лишних окон/попапов (Самый простой вариант)
        string subTaskName = Microsoft.VisualBasic.Interaction.InputBox("Введите название подзадачи:", "Новая подзадача");
        if (string.IsNullOrWhiteSpace(subTaskName)) return;

        var dateOnly = DateOnly.FromDateTime(DateTime.Today);
        var subTask = new SubTaskLog
        {
            TaskId = parentTask.Id,
            Name = subTaskName,
            CreatedAt = dateOnly,
            SecondsSpent = 0
        };

        _db.SubTaskLogs.Add(subTask);
        await _db.SaveChangesAsync();

        // Если мы сейчас смотрим сегодняшний день — сразу добавляем в интерфейс
        if (SelectedDate.Date == DateTime.Today)
        {
            // Добавляем в начало списка
            parentTask.SubTasks.Insert(0, subTask);
        }
    }

    /// <summary>
    /// Включение/выключение таймера
    /// </summary>
    private async Task ToggleTimerAsync(SubTaskLog? subTask)
    {
        if (subTask == null) return;

        if (subTask.IsRunning)
        {
            subTask.IsRunning = false;
            _activeSubTask = null;
            _globalTimer?.Stop();
            await SaveCurrentProgressAsync();
        }
        else
        {
            // Останавливаем любую другую работающую подзадачу
            if (_activeSubTask != null)
            {
                _activeSubTask.IsRunning = false;
                await SaveCurrentProgressAsync();
            }

            // Переключаем на сегодняшний день, если запуск идет из прошлого
            if (SelectedDate != DateTime.Today) SelectedDate = DateTime.Today;

            // Если у этой подзадачи еще нет Id в БД (виртуальная дефолтная запись), сохраняем её
            if (subTask.Id == 0)
            {
                _db.SubTaskLogs.Add(subTask);
                await _db.SaveChangesAsync();
            }

            _globalTimer?.Start();
            _activeSubTask = subTask;
            _activeSubTask.IsRunning = true;

            // Обновляем дату изменения родительской задачи для сортировки
            var parent = _db.Tasks.Find(subTask.TaskId);
            if (parent != null)
            {
                parent.LastUpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            _activeSubTask.LastUpdatedAt = DateTime.UtcNow;
            _db.Entry(_activeSubTask).State = EntityState.Modified;
            await _db.SaveChangesAsync();
        }
    }

    private async Task SaveCurrentProgressAsync()
    {

        if (_activeSubTask == null || _activeSubTask.Id == 0) return;
        _db.Entry(_activeSubTask).State = EntityState.Modified;
        await _db.SaveChangesAsync();
    }

    private void CalculateTotalTime()
    {
        int total = Tasks.Sum(t => t.TotalDaySeconds);
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