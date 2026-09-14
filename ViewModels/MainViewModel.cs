using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TimeTracker.Data.Interfaces;
using TimeTracker.Models;
using TimeTracker.Services;

namespace TimeTracker.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    // Репозитории
    private readonly ITaskRepository _taskRepository;
    private readonly ISubTaskRepository _subTaskRepository;
    private readonly IGeneralInfoTimeDayRepository _generalInfoTimeDayRepository;
    
    // Сервисы
    private readonly IDayLogService _dayLogService;
    private readonly TaskManagementService _taskManagementService;
    private readonly TimeCalculationService _timeCalcService;

    /// <summary>
    /// Глобальный таймер учета времени работы
    /// </summary>
    private DispatcherTimer? _globalTimer;

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
    public bool IsLunchIncluded
    {
        get => _isLunchIncluded;
        set
        {
            _isLunchIncluded = value;
            OnPropertyChanged();
            UpdateLunchStatusInDbAsync(_cts.Token).ConfigureAwait(false);
            RecalculateWorkDayPlan();
        }
    }

    // Отображение корректировки времени
    public string AdjustHours { get => _adjustHours; set { _adjustHours = value; OnPropertyChanged(); } }
    public string AdjustMinutes { get => _adjustMinutes; set { _adjustMinutes = value; OnPropertyChanged(); } }
    public string AdjustSeconds { get => _adjustSeconds; set { _adjustSeconds = value; OnPropertyChanged(); } }
    public bool IsAdjustPositive { 
        get => _isAdjustPositive; 
        set { _isAdjustPositive = value; OnPropertyChanged(); OnPropertyChanged(nameof(ApplyEditTimeText)); } }

    public string ApplyEditTimeText => IsAdjustPositive ? "Прибавить" : "Вычесть";

    private int _editHours;
    private int _editMinutes;
    private int _editSeconds;

    public int EditHours { get => _editHours; set { _editHours = value; OnPropertyChanged(); } }
    public int EditMinutes { get => _editMinutes; set { _editMinutes = value; OnPropertyChanged(); } }
    public int EditSeconds { get => _editSeconds; set { _editSeconds = value; OnPropertyChanged(); } }

    public ICommand AddCommand { get; }
    public ICommand ToggleTimerCommand { get; }
    public ICommand AddSubTaskCommand { get; }

    // Команды удаления задач
    public ICommand DeleteTaskCommand { get; }
    public ICommand DeleteSubTaskCommand { get; }

    // Команды корректировки времени
    public ICommand IncreaseTimeCommand { get; }
    public ICommand DecreaseTimeCommand { get; }
    public ICommand SaveSubtaskTimeCommand { get; }
    public ICommand ApplyTimeAdjustmentCommand { get; }

    public MainViewModel(
        ITaskRepository taskRepository,
        ISubTaskRepository subTaskRepository,
        IGeneralInfoTimeDayRepository generalInfoTimeDayRepository,
        IDayLogService dayLogService,
        TaskManagementService taskManagementService,
        TimeCalculationService timeCalculationService)
    {
        _taskRepository = taskRepository;
        _subTaskRepository = subTaskRepository;
        _generalInfoTimeDayRepository = generalInfoTimeDayRepository;
        _dayLogService = dayLogService;
        _taskManagementService = taskManagementService;
        _timeCalcService = timeCalculationService;

        AddCommand = new RelayCommand<object>(async _ => await AddTaskAsync(_cts.Token));
        ToggleTimerCommand = new RelayCommand<SubTaskLog>(async (subTask) => await ToggleTimerAsync(subTask, _cts.Token));
        AddSubTaskCommand = new RelayCommand<TaskModel>(async (task) => await AddSubTaskAsync(task, _cts.Token));

        DeleteTaskCommand = new RelayCommand<TaskModel>(async task => await DeleteTaskAsync(task, _cts.Token));
        DeleteSubTaskCommand = new RelayCommand<SubTaskLog>(async subTask => await DeleteSubTaskAsync(subTask, _cts.Token));

        IncreaseTimeCommand = new RelayCommand<SubTaskLog>(ExecuteIncreaseTime);
        DecreaseTimeCommand = new RelayCommand<SubTaskLog>(ExecuteDecreaseTime);
        SaveSubtaskTimeCommand = new RelayCommand<SubTaskLog>(async (subTask) => await ExecuteSaveSubtaskTimeAsync(subTask));
        ApplyTimeAdjustmentCommand = new RelayCommand<SubTaskLog>(async subTask => await ApplyTimeAdjustmentAsync(subTask, _cts.Token));
    }

    public async Task Initialize()
    {
        SetupGlobalTimer();
        await LoadTasksAndLogsAsync(_cts.Token);
    }

    /// <summary>
    /// Установка глобального таймера
    /// </summary>
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

    /// <summary>
    /// Загружаем задачи и подзадачи на выбранную в UI дату
    /// </summary>
    private async Task LoadTasksAndLogsAsync(CancellationToken ct)
    {
        // Выбранная дата в UI
        var selectedDateUi = DateOnly.FromDateTime(SelectedDate);

        await _taskManagementService.LoadTasksAndLogsAsync(Tasks, selectedDateUi, ct);

        CalculateTotalTime();

        await LoadDayLogsAsync(ct);
    }

    /// <summary>
    /// Загружаем общую информацию времени по дню
    /// </summary>
    private async Task LoadDayLogsAsync(CancellationToken ct)
    {
        var selectedDateUi = DateOnly.FromDateTime(SelectedDate);

        _currentDayInfo = await _generalInfoTimeDayRepository.GetGeneralInfoTimeDayAsync(selectedDateUi, ct);

        if (_currentDayInfo != null)
        {
            StartTimeFormatted = _currentDayInfo.WorkStartTime?.ToLocalTime().ToString(@"HH\:mm\:ss") ?? "--:--:--";
            //TotalPauseFormatted = TimeCalculationService.FormatTime(_currentDayInfo.TotalPauseSeconds);
            
            // Обновляем флаг обеда из БД
            _isLunchIncluded = _currentDayInfo.HasLunch;
        }
        else
        {
            StartTimeFormatted = "--:--:--";
            TotalPauseFormatted = "00:00:00";
        }

        RecalculateWorkDayPlan();
    }

    private void CancelAndReload()
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        _ = LoadTasksAndLogsAsync(_cts.Token);
    }

    /// <summary>
    /// Добавление основной задачи
    /// </summary>
    private async Task AddTaskAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(NewTaskName)) return;

        var task = await _taskManagementService.AddTaskAsync(NewTaskName, ct);

        // Добавляем подзадачу на UI
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

        var subTask = await _taskManagementService.AddSubTaskAsync(parentTask, subTaskName, ct);

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

        if (subTask.IsRunning)
        {
            subTask.IsRunning = false;
            subTask.LastUpdatedAt = DateTime.UtcNow;
            _activeSubTask = null;
            _globalTimer?.Stop();

            await _subTaskRepository.UpdateSubTaskLogAsync(subTask, ct);

            // Фиксируем старт паузы
            _pauseStartedAt = DateTime.UtcNow;
        }
        else
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            // Запускаем рабочий день в БД
            _currentDayInfo = await _dayLogService.StartWorkDayAsync(_currentDayInfo, today, ct);

            if (_pauseStartedAt != null)
            {
                var pauseDuration = (int)(DateTime.UtcNow - _pauseStartedAt.Value).TotalSeconds;
                //await _dayLogService.AddPauseTimeAsync(today, pauseDuration, ct);
                _currentDayInfo.TotalPauseSeconds += pauseDuration;
                _pauseStartedAt = null;
                await _generalInfoTimeDayRepository.AddOrUpdateGeneralInfoAsync(_currentDayInfo!, ct);
            }

            // Останавливаем любую другую работающую подзадачу
            if (_activeSubTask != null)
            {
                _activeSubTask.IsRunning = false;
                _activeSubTask.LastUpdatedAt = DateTime.UtcNow;
                await _subTaskRepository.UpdateSubTaskLogAsync(_activeSubTask, ct);
            }

            // Переключаем на сегодняшний день, если запуск идет из прошлого
            if (SelectedDate != DateTime.Today) SelectedDate = DateTime.Today;

            // Изменяем новую запущенную задачу
            _activeSubTask = subTask;
            _activeSubTask.IsRunning = true;
            _activeSubTask.LastUpdatedAt = DateTime.UtcNow;

            await _subTaskRepository.UpdateSubTaskLogAsync(_activeSubTask, ct);

            _globalTimer?.Start();
        }

        await SortSubtasksOnlyAsync(subTask, ct);

        // TODO: точно нужно здесь?
        //await LoadDayLogsAsync(ct);

        // Поменять на это при необходимости
        RecalculateWorkDayPlan();
    }

    /// <summary>
    /// Сортируем только подзадачи внутри родителя
    /// </summary>
    /// <param name="activeSubTask"></param>
    private async Task SortSubtasksOnlyAsync(SubTaskLog activeSubTask, CancellationToken ct)
    {
        var uiParent = Tasks.FirstOrDefault(t => t.Id == activeSubTask.TaskId);

        if (uiParent != null)
        {
            if (uiParent.SubTasks.Count > 1)
            {
                uiParent.SubTasks.Remove(activeSubTask);
                uiParent.SubTasks.Insert(0, activeSubTask);
            }

            /*var dbParent = await _db.Tasks
                .Where(t => t.Id == parentId)
                .FirstOrDefaultAsync(ct);*/

            // Тихо обновляем дату апдейта родителя в БД
            var dbParent = await _taskRepository.GetTaskByIdAsync(activeSubTask.TaskId, ct);

            if (dbParent != null)
            {
                dbParent.LastUpdatedAt = DateTime.UtcNow;
            }

            // Обновляем дату апдейта активной подзадачи в БД
            activeSubTask.LastUpdatedAt = DateTime.UtcNow;
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

        EstimatedEndTimeFormatted = _timeCalcService
            .CalculateEstimatedEndTime(_currentDayInfo.WorkStartTime.Value, totalPauseSec, IsLunchIncluded);

        TotalPauseFormatted = TimeCalculationService.FormatTime(totalPauseSec);
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

        _timeCalcService.ApplyTimeAdjustment(subTask, h, m, s, IsAdjustPositive);
        await _subTaskRepository.UpdateSubTaskLogAsync(subTask, ct);

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

        await _taskRepository.DeleteTaskCascadingAsync(task.Id, ct);

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
        
        await _subTaskRepository.UpdateSubTaskLogAsync(activeSubTask, _cts.Token);
    }

    private void CalculateTotalTime()
    {
        TotalTimeFormatted = _timeCalcService.CalculateTotalTime(Tasks);
    }

    /* -------------- Методы для корректировки времени через попап -------------- */

    /// <summary>
    /// Добавить время
    /// </summary>
    private void ExecuteIncreaseTime(SubTaskLog? subTask)
    {
        //if (subTask == null) return;
        _isAdjustPositive = true;
        // Загружаем текущее время в поля ввода
        //var ts = TimeSpan.FromSeconds(subTask.SecondsSpent);
        //EditHours = ts.Hours;
        //EditMinutes = ts.Minutes;
        //EditSeconds = ts.Seconds;
    }

    /// <summary>
    /// Вычесть время
    /// </summary>
    /// <param name="subTask"></param>
    private void ExecuteDecreaseTime(SubTaskLog? subTask)
    {
        //if (subTask == null) return;
        _isAdjustPositive = false;
        // Загружаем текущее время в поля ввода
        //var ts = TimeSpan.FromSeconds(subTask.SecondsSpent);
        //EditHours = ts.Hours;
        //EditMinutes = ts.Minutes;
        //EditSeconds = ts.Seconds;
    }

    private async Task ExecuteSaveSubtaskTimeAsync(SubTaskLog? subTask)
    {
        if (subTask == null) return;

        // Применяем ручную корректировку на основе значений из EditHours/EditMinutes/EditSeconds
        var ts = TimeSpan.FromSeconds(subTask.SecondsSpent);
        
        int totalAdjustmentSeconds = (EditHours * 3600) + (EditMinutes * 60) + EditSeconds;
        if (!_isAdjustPositive) totalAdjustmentSeconds *= -1;

        subTask.SecondsSpent = Math.Max(0, subTask.SecondsSpent + totalAdjustmentSeconds);
        subTask.LastUpdatedAt = DateTime.UtcNow;
        
        await _subTaskRepository.UpdateSubTaskLogAsync(subTask, _cts.Token);

        var parent = Tasks.FirstOrDefault(t => t.Id == subTask.TaskId);
        if (parent != null)
        {
            parent.TotalDaySeconds = parent.SubTasks.Sum(st => st.SecondsSpent);
        }

        CalculateTotalTime();
        RecalculateWorkDayPlan();

        // Сбрасываем поля формы
        EditHours = 0;
        EditMinutes = 0;
        EditSeconds = 0;
    }

    /// <summary>
    /// Обновить статус обеда в БД
    /// </summary>
    private async Task UpdateLunchStatusInDbAsync(CancellationToken ct)
    {
        if (_currentDayInfo == null) return;

        await _dayLogService.UpdateLunchStatusAsync(_currentDayInfo, _isLunchIncluded, ct);
    }

    public async Task CloseConnection()
    {
        _globalTimer?.Stop();
        _cts.Cancel();
        await SaveCurrentProgressAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string prop = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}
