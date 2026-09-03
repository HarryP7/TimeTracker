using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TimeTracker.Data;
using TimeTracker.Data.Interfaces;
using TimeTracker.Models;
using TimeTracker.Services;

namespace TimeTracker.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    // TODO: удалить
    private readonly AppDbContext _db;

    // Репозитории
    private readonly ITaskRepository _taskRpository;
    private readonly ISubTaskRepository _subTaskRepository;
    private readonly IGeneralInfoTimeDayRepository _generalInfoTimeDayRepository;

    /// <summary>
    /// Глобальный таймер учета времени работы
    /// </summary>
    private DispatcherTimer? _globalTimer;

    // TODO: Переименовать на _estimatedEndWorkTimeTimer. Или удалить.
    /// <summary>
    /// Таймер для прогнозного времени окончания работы
    /// </summary>
    private DispatcherTimer? _uiRefreshTimer;

    /// <summary>
    /// Активная задача по которой запущен таймер
    /// </summary>
    private SubTaskLog? _activeSubTask;

    /// <summary>
    /// Время начала паузы
    /// </summary>
    private DateTime? _pauseStartedAt;

    /// <summary>
    /// Токен отмены
    /// </summary>
    private CancellationTokenSource _cts = new();

    private string _newTaskName = string.Empty;
    private DateTime _selectedDate = DateTime.Today;
    /// <summary>
    /// Формат общего затраченнго времени работы
    /// </summary>
    private string _totalTimeFormatted = "00:00:00";

    /// <summary>
    /// Формат времени начала работы
    /// </summary>
    private string _startTimeFormatted = "--:--:--";
    /// <summary>
    /// Формат общего времени пауз
    /// </summary>
    private string _totalPauseFormatted = "00:00:00";
    /// <summary>
    /// Формат прогнозного времени завершения работы
    /// </summary>
    private string _estimatedEndTimeFormatted = "--:--:--";
    /// <summary>
    /// Флаг: был ли обед
    /// </summary>
    private bool _isLunchIncluded = false;

    // Свойства для модального изменения времени
    private string _adjustHours = "0";
    private string _adjustMinutes = "0";
    private string _adjustSeconds = "0";
    /// <summary>
    /// Флаг: Добавление или вычитание времени в подзадачах
    /// </summary>
    private bool _isAdjustPositive = true;

    /// <summary>
    /// Текущее инфо по общему времени
    /// </summary>
    private GeneralInfoTimeDay? _currentDayInfo;

    public ObservableCollection<TaskModel> Tasks { get; } = new();

    public string NewTaskName { get => _newTaskName; set { _newTaskName = value; OnPropertyChanged(); } }
    public DateTime SelectedDate { get => _selectedDate; set { _selectedDate = value; OnPropertyChanged(); CancelAndReload(); } }
    public string TotalTimeFormatted { get => _totalTimeFormatted; set { _totalTimeFormatted = value; OnPropertyChanged(); } }

    /// <summary>
    /// Отображение времени начала
    /// </summary>
    public string StartTimeFormatted { get => _startTimeFormatted; set { _startTimeFormatted = value; OnPropertyChanged(); } }
    /// <summary>
    /// Отображение общего времени пауз
    /// </summary>
    public string TotalPauseFormatted { get => _totalPauseFormatted; set { _totalPauseFormatted = value; OnPropertyChanged(); } }
    /// <summary>
    /// Отображение прогнозного времени завершения работы
    /// </summary>
    public string EstimatedEndTimeFormatted { get => _estimatedEndTimeFormatted; set { _estimatedEndTimeFormatted = value; OnPropertyChanged(); } }
    /// <summary>
    /// Флаг для отображения: Был ли обед
    /// </summary>
    public bool IsLunchIncluded { get => _isLunchIncluded; set { _isLunchIncluded = value; OnPropertyChanged(); RecalculateWorkDayPlan(); } }

    // Отображение корректировки времени
    public string AdjustHours { get => _adjustHours; set { _adjustHours = value; OnPropertyChanged(); } }
    public string AdjustMinutes { get => _adjustMinutes; set { _adjustMinutes = value; OnPropertyChanged(); } }
    public string AdjustSeconds { get => _adjustSeconds; set { _adjustSeconds = value; OnPropertyChanged(); } }
    public bool IsAdjustPositive { get => _isAdjustPositive; set { _isAdjustPositive = value; OnPropertyChanged(); } }

    public ICommand AddCommand { get; }
    public ICommand ToggleTimerCommand { get; }
    public ICommand AddSubTaskCommand { get; }

    // Команды удаления задач и корректировки времени
    public ICommand DeleteTaskCommand { get; }
    public ICommand DeleteSubTaskCommand { get; }
    public ICommand ApplyTimeAdjustmentCommand { get; }

    public MainViewModel(AppDbContext db,
        ITaskRepository taskRpository,
        ISubTaskRepository subTaskRepository,
        IGeneralInfoTimeDayRepository generalInfoTimeDayRepository)
    {
        _db = db;
        _taskRpository = taskRpository;
        _subTaskRepository = subTaskRepository;
        _generalInfoTimeDayRepository = generalInfoTimeDayRepository;

        AddCommand = new RelayCommand<object>(async _ => await AddTaskAsync(_cts.Token));
        ToggleTimerCommand = new RelayCommand<SubTaskLog>(async (subTask) => await ToggleTimerAsync(subTask, _cts.Token));
        AddSubTaskCommand = new RelayCommand<TaskModel>(async (task) => await AddSubTaskAsync(task, _cts.Token));

        DeleteTaskCommand = new RelayCommand<TaskModel>(async task => await DeleteTaskAsync(task, _cts.Token));
        DeleteSubTaskCommand = new RelayCommand<SubTaskLog>(async subTask => await DeleteSubTaskAsync(subTask, _cts.Token));
        ApplyTimeAdjustmentCommand = new RelayCommand<SubTaskLog>(async subTask => await ApplyTimeAdjustmentAsync(subTask, _cts.Token));
    }

    public async Task Initialize()
    {
        await LoadTasksAndLogsAsync(_cts.Token);
        SetupGlobalTimer();
    }

    private void SetupGlobalTimer()
    {
        // Один таймер на всё приложение вместо таймера в каждом объекте
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
    private void CancelAndReload()
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        _ = LoadTasksAndLogsAsync(_cts.Token);
    }

    /// <summary>
    /// Загружаем задачи и подзадачи на выбранную в UI дату
    /// </summary>
    private async Task LoadTasksAndLogsAsync(CancellationToken ct)
    {
        // Выбранная дата в UI
        var selectedDateUi = DateOnly.FromDateTime(SelectedDate);

        var allTasks = await _taskRpository.GetAllTasksAsync(ct);

        // Вытаскиваем подзадачи за выбранный день
        var allSubTasks = await _subTaskRepository.GetSubTaskLogsByDateAsync(selectedDateUi, ct);

        Tasks.Clear();

        foreach (var task in allTasks)
        {
            task.SubTasks.Clear();

            var subTasksByTask = allSubTasks
                .Where(l => l.TaskId == task.Id)
                .ToArray();

            // Если логов/подзадач на этот день еще нет, создаем дефолтный лог для основной задачи
            if (subTasksByTask.Length == 0)
            {
                var defaultSubTask = new SubTaskLog
                {
                    TaskId = task.Id,
                    Name = null,
                    CreatedAt = selectedDateUi,
                    SecondsSpent = 0,
                    LastUpdatedAt = DateTime.UtcNow
                };
                await _subTaskRepository.AddSubTaskAsync(defaultSubTask, ct);

                subTasksByTask = [defaultSubTask];
            }

            // Если задача была активна, но мы сменили дату - визуально останавливаем её отображение
            //if (task == _activeSubTask && dateOnly != DateOnly.FromDateTime(DateTime.Today)) task.IsRunning = false;

            foreach (var subTask in subTasksByTask)
            {
                if (_activeSubTask != null && _activeSubTask.Id == subTask.Id && selectedDateUi == DateOnly.FromDateTime(DateTime.Today))
                {
                    subTask.IsRunning = true;
                }
                task.SubTasks.Add(subTask);
            }

            task.TotalDaySeconds = task.SubTasks.Sum(s => s.SecondsSpent);
            Tasks.Add(task);
        }

        CalculateTotalTime();

        await LoadDayLogsAsync(ct);
    }

    /// <summary>
    /// Загружаем общую информацию времени по дню
    /// </summary>
    private async Task LoadDayLogsAsync(CancellationToken ct)
    {
        var selectedDateUi = DateOnly.FromDateTime(SelectedDate);

        _currentDayInfo = await _generalInfoTimeDayRepository
            .GetGeneralInfoTimeDayAsync(selectedDateUi, ct);

        if (_currentDayInfo != null)
        {
            StartTimeFormatted = _currentDayInfo.WorkStartTime?.ToLocalTime().ToString(@"HH\:mm\:ss") ?? "--:--:--";
            var pauseTs = TimeSpan.FromSeconds(_currentDayInfo.TotalPauseSeconds);
            TotalPauseFormatted = string.Create(null, $"{pauseTs.Hours:D2}:{pauseTs.Minutes:D2}:{pauseTs.Seconds:D2}");
        }
        else
        {
            StartTimeFormatted = "--:--:--";
            TotalPauseFormatted = "00:00:00";
        }

        RecalculateWorkDayPlan();
    }

    /// <summary>
    /// Добавление основной задачи
    /// </summary>
    private async Task AddTaskAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(NewTaskName)) return;

        var task = new TaskModel
        {
            Name = NewTaskName,
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };

        await _taskRpository.AddTaskAsync(task, ct);

        // Сразу создаем дефолтную запись времени на сегодня
        var defaultSubTaskLog = new SubTaskLog
        {
            TaskId = task.Id,
            Name = null,
            CreatedAt = DateOnly.FromDateTime(DateTime.Today),
            SecondsSpent = 0,
            LastUpdatedAt = DateTime.UtcNow
        };
        await _subTaskRepository.AddSubTaskAsync(defaultSubTaskLog, ct);

        // Добавляем подзадачу на UI
        //task.SubTasks.Add(defaultSubTaskLog);
        task.TotalDaySeconds = 0;

        // Добавляем в начало списка
        Tasks.Insert(0, task);
        NewTaskName = string.Empty;
    }

    /// <summary>
    /// Добавление подзадачи
    /// </summary>
    private async Task AddSubTaskAsync(TaskModel? parentTask, CancellationToken ct)
    {
        if (parentTask == null) return;

        // Используем стандартный InputBox от VB для быстрого ввода без создания лишних окон/попапов (Самый простой вариант)
        string subTaskName = Microsoft.VisualBasic.Interaction.InputBox("Введите название подзадачи:", "Новая подзадача");
        if (string.IsNullOrWhiteSpace(subTaskName)) return;

        var subTask = new SubTaskLog
        {
            TaskId = parentTask.Id,
            Name = subTaskName,
            CreatedAt = DateOnly.FromDateTime(DateTime.Today),
            SecondsSpent = 0,
            LastUpdatedAt = DateTime.UtcNow
        };

        await _subTaskRepository.AddSubTaskAsync(subTask, ct);

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
    private async Task ToggleTimerAsync(SubTaskLog? subTask, CancellationToken ct)
    {
        if (subTask == null) return;

        // Очищаем трекер перед изменениями
        //_db.ChangeTracker.Clear();

        if (subTask.IsRunning)
        {
            subTask.IsRunning = false;
            subTask.LastUpdatedAt = DateTime.UtcNow;
            _activeSubTask = null;
            _globalTimer?.Stop();

            // TODO: Вернуть, если вариант ниже не будет работать
            //_db.Entry(subTask).State = EntityState.Modified;
            //await _db.SaveChangesAsync();
            //await SaveCurrentProgressAsync(subTask);
            await _subTaskRepository.UpdateSubTaskLogAsync(subTask, ct);

            // Фиксируем старт паузы
            _pauseStartedAt = DateTime.UtcNow;

            await SortSubtasksOnlyAsync(subTask.TaskId, subTask, ct);
        }
        else
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            if (_currentDayInfo == null)
            {
                _currentDayInfo = new GeneralInfoTimeDay
                {
                    Date = today,
                    WorkStartTime = DateTime.UtcNow,
                    TotalPauseSeconds = 0
                };
            }
            else if (_currentDayInfo.WorkStartTime == null)
            {
                _currentDayInfo.WorkStartTime = DateTime.UtcNow;
            }
            if (_pauseStartedAt != null)
            {
                var pauseDuration = (int)(DateTime.UtcNow - _pauseStartedAt.Value).TotalSeconds;
                _currentDayInfo.TotalPauseSeconds += pauseDuration;
                _pauseStartedAt = null;
            }
            await _generalInfoTimeDayRepository.AddOrUpdateGeneralInfoAsync(_currentDayInfo, ct);

            // Фиксируем время первого старта за день
            /*var dayLog = await _db.GeneralInfoTimeDays
                .Where(x => x.Date == today)
                .FirstOrDefaultAsync(ct);

            if (dayLog == null)
            {
                dayLog = new GeneralInfoTimeDay
                {
                    Date = today,
                    WorkStartTime = DateTime.UtcNow,
                    TotalPauseSeconds = 0
                };

                _db.GeneralInfoTimeDays.Add(dayLog);
                await _db.SaveChangesAsync();
            }
            else if (dayLog.WorkStartTime == null)
            {
                dayLog.WorkStartTime = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            // Если была активная пауза, высчитываем её длительность
            if (_pauseStartedAt != null)
            {
                var pauseDuration = (int)(DateTime.UtcNow - _pauseStartedAt.Value).TotalSeconds;
                dayLog.TotalPauseSeconds += pauseDuration;

                await _db.SaveChangesAsync();
                _pauseStartedAt = null;
            }*/

            // Останавливаем любую другую работающую подзадачу
            if (_activeSubTask != null)
            {
                _activeSubTask.IsRunning = false;
                _activeSubTask.LastUpdatedAt = DateTime.UtcNow;
                //await SaveCurrentProgressAsync();
                await _subTaskRepository.UpdateSubTaskLogAsync(_activeSubTask, ct);

                await SortSubtasksOnlyAsync(_activeSubTask.TaskId, _activeSubTask, ct);
            }

            // Переключаем на сегодняшний день, если запуск идет из прошлого
            if (SelectedDate != DateTime.Today) SelectedDate = DateTime.Today;

            // Если у этой подзадачи еще нет Id в БД (виртуальная дефолтная запись), сохраняем её
            /*if (subTask.Id == 0)
            {
                _db.SubTaskLogs.Add(subTask);
                await _db.SaveChangesAsync();
            }*/

            // Изменяем новую запущенную задачу
            _activeSubTask = subTask;
            _activeSubTask.IsRunning = true;
            _activeSubTask.LastUpdatedAt = DateTime.UtcNow;

            await _subTaskRepository.UpdateSubTaskLogAsync(_activeSubTask, ct);

            _globalTimer?.Start();
        }

        // TODO: точно нужно здесь?
        await LoadDayLogsAsync(ct);

        // Поменять на это при необходимости
        //await RecalculateWorkDayPlan();
    }

    /// <summary>
    /// Сортируем только подзадачи внутри родителя
    /// </summary>
    /// <param name="parentId"></param>
    /// <param name="activeSubTask"></param>
    private async Task SortSubtasksOnlyAsync(int parentId, SubTaskLog activeSubTask, CancellationToken ct)
    {
        var parent = Tasks.FirstOrDefault(t => t.Id == parentId);

        if (parent != null)
        {
            if (parent.SubTasks.Count > 1)
            {
                parent.SubTasks.Remove(activeSubTask);
                parent.SubTasks.Insert(0, activeSubTask);
            }

            // Тихо обновляем дату апдейта родителя в БД
            var dbParent = await _db.Tasks
                .Where(t => t.Id == parentId)
                .FirstOrDefaultAsync(ct);

            if (dbParent != null)
            {
                dbParent.LastUpdatedAt = DateTime.UtcNow;
            }

            //activeSubTask.LastUpdatedAt = DateTime.UtcNow;
            //await _db.SaveChangesAsync();

            //await SaveCurrentProgressAsync(activeSubTask, ct);
            await _subTaskRepository.UpdateSubTaskLogAsync(activeSubTask, ct);
        }
    }

    /// <summary>
    /// Расчет прогнозного времени завершения рабочего дня
    /// </summary>
    private void RecalculateWorkDayPlan()
    {
        if (_currentDayInfo == null || _currentDayInfo.WorkStartTime == null)
        {
            EstimatedEndTimeFormatted = "--:--:--";
            return;
        }

        var totalPauseSec = _currentDayInfo.TotalPauseSeconds;

        // Если прямо сейчас идет пауза (таймер выключен), учитываем текущий простой в реальном времени
        if (_activeSubTask is null && _pauseStartedAt != null)
        {
            totalPauseSec += (int)(DateTime.UtcNow - _pauseStartedAt.Value).TotalSeconds;
        }

        var estimatedEnd = WorkTimeCalculator.CalculateEstimatedEndTime(_currentDayInfo.WorkStartTime.Value, totalPauseSec, IsLunchIncluded);

        EstimatedEndTimeFormatted = estimatedEnd.ToString(@"HH:mm:ss");

        var pauseTs = TimeSpan.FromSeconds(totalPauseSec);
        TotalPauseFormatted = string.Create(null, $"{pauseTs.Hours:D2}:{pauseTs.Minutes:D2}:{pauseTs.Seconds:D2}");
    }

    /// <summary>
    /// Применение ручной корректировки времени через Попап
    /// </summary>
    /// <param name="subTask"></param>
    private async Task ApplyTimeAdjustmentAsync(SubTaskLog? subTask, CancellationToken ct)
    {
        if (subTask == null) return;

        if (!int.TryParse(AdjustHours, out int h) || !int.TryParse(AdjustMinutes, out int m) || !int.TryParse(AdjustSeconds, out int s))
        {
            MessageBox.Show("Введите корректные числовые значения!");
            return;
        }

        int totalAdjustmentSeconds = (h * 3600) + (m * 60) + s;
        if (!IsAdjustPositive) totalAdjustmentSeconds *= -1;
        //_db.ChangeTracker.Clear();

        subTask.SecondsSpent = Math.Max(0, subTask.SecondsSpent + totalAdjustmentSeconds);
        subTask.LastUpdatedAt = DateTime.UtcNow;
        await _subTaskRepository.UpdateSubTaskLogAsync(subTask, ct);

        /*if (subTask.Id == 0)
        {
            subTask.SecondsSpent = Math.Max(0, subTask.SecondsSpent + totalAdjustmentSeconds);
            _db.SubTaskLogs.Add(subTask);
        }
        else
        {
            var dbSubTask = await _db.SubTaskLogs
                .Where(st => st.Id == subTask.Id)
                .FirstOrDefaultAsync();

            if (dbSubTask != null)
            {
                dbSubTask.SecondsSpent = Math.Max(0, dbSubTask.SecondsSpent + totalAdjustmentSeconds);
                dbSubTask.LastUpdatedAt = DateTime.UtcNow;
                subTask.SecondsSpent = dbSubTask.SecondsSpent;
                subTask.LastUpdatedAt = dbSubTask.LastUpdatedAt;
            }
        }
        await _db.SaveChangesAsync();*/

        var parent = Tasks.FirstOrDefault(t => t.Id == subTask.TaskId);
        if (parent != null)
        {
            parent.TotalDaySeconds = parent.SubTasks.Sum(st => st.SecondsSpent);
        }

        CalculateTotalTime();

        // Сбрасываем поля формы
        AdjustHours = "0";
        AdjustMinutes = "0";
        AdjustSeconds = "0";
        MessageBox.Show("Время успешно скорректировано!");

        RecalculateWorkDayPlan();
    }

    /// <summary>
    /// Удаление родительской задачи вместе с подзадачами
    /// </summary>
    /// <param name="task"></param>
    private async Task DeleteTaskAsync(TaskModel? task, CancellationToken ct)
    {
        if (task == null) return;

        var result = MessageBox.Show($"Вы уверены, что хотите удалить задачу '{task.Name}' и всю историю её подзадач?",
            "Удаление задачи",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        await _taskRpository.DeleteTaskCascadingAsync(task.Id, ct);

        Tasks.Remove(task);
        CalculateTotalTime();
        RecalculateWorkDayPlan();
    }

    /// <summary>
    /// Удаление конкретной подзадачи
    /// </summary>
    /// <param name="subTask"></param>
    private async Task DeleteSubTaskAsync(SubTaskLog? subTask, CancellationToken ct)
    {
        if (subTask == null) return;

        var result = MessageBox.Show($"Удалить подзадачу '{subTask.DisplayName}'?",
            "Удаление подзадачи",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        await _subTaskRepository.DeleteSubTaskLogAsync(subTask.Id, ct);

        var parent = Tasks.FirstOrDefault(t => t.Id == subTask.TaskId);
        if (parent != null)
        {
            parent.SubTasks.Remove(subTask);
            parent.TotalDaySeconds = parent.SubTasks.Sum(s => s.SecondsSpent);
        }
        CalculateTotalTime();
        RecalculateWorkDayPlan();
    }

    private async Task SaveCurrentProgressAsync(SubTaskLog? activeSubTask = null)
    {
        activeSubTask ??= _activeSubTask;

        if (activeSubTask == null || activeSubTask.Id == 0) return;
        _db.Entry(activeSubTask).State = EntityState.Modified;
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
        _cts.Cancel();
        await SaveCurrentProgressAsync();
        _db.Dispose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string prop = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}