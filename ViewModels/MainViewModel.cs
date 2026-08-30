using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TimeTracker.Data;
using TimeTracker.Models;

namespace TimeTracker.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly AppDbContext _db;

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
    private bool _isLunchIncluded = true;

    // Свойства для модального изменения времени
    private string _adjustHours = "0";
    private string _adjustMinutes = "0";
    private string _adjustSeconds = "0";
    /// <summary>
    /// Флаг: Добавление или вычитание времени в подзадачах
    /// </summary>
    private bool _isAdjustPositive = true;

    public ObservableCollection<TaskModel> Tasks { get; } = new();

    public string NewTaskName { get => _newTaskName; set { _newTaskName = value; OnPropertyChanged(); } }
    public DateTime SelectedDate { get => _selectedDate; set { _selectedDate = value; OnPropertyChanged(); _ = LoadTasksAndLogsAsync(); } }
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
    public bool IsLunchIncluded { get => _isLunchIncluded; set { _isLunchIncluded = value; OnPropertyChanged(); RecalculateWorkDayPlanAsync(); } }

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

    public MainViewModel(AppDbContext db)
    {
        _db = db;
        AddCommand = new RelayCommand<object>(async _ => await AddTaskAsync());
        ToggleTimerCommand = new RelayCommand<SubTaskLog>(async (subTask) => await ToggleTimerAsync(subTask));
        AddSubTaskCommand = new RelayCommand<TaskModel>(async (task) => await AddSubTaskAsync(task));

        DeleteTaskCommand = new RelayCommand<TaskModel>(async task => await DeleteTaskAsync(task));
        DeleteSubTaskCommand = new RelayCommand<SubTaskLog>(async subTask => await DeleteSubTaskAsync(subTask));
        ApplyTimeAdjustmentCommand = new RelayCommand<SubTaskLog>(async subTask => await ApplyTimeAdjustmentAsync(subTask));

        //SetupTimers();
    }

    public async Task Initialize()
    {
        await LoadTasksAndLogsAsync();
        SetupTimers();
    }

    // TODO: Вернуть название SetupGlobalTimer
    private void SetupTimers()
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

        // Таймер для обновления прогноза окончания дня каждую секунду
        //_uiRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        //_uiRefreshTimer.Tick += (s, e) => RecalculateWorkDayPlanAsync();
        //_uiRefreshTimer.Start();
    }

    /// <summary>
    /// Загружаем задачи и подзадачи на выбранную в UI дату
    /// </summary>
    private async Task LoadTasksAndLogsAsync()
    {
        // Очищаем трекер EF перед каждой загрузкой данных
        _db.ChangeTracker.Clear();

        // Выбранная дата в UI
        var selectedDateUi = DateOnly.FromDateTime(SelectedDate);

        // Сортировка: по убыванию даты и времени последнего выполнения
        var allTasks = await _db.Tasks
            .AsNoTracking()
            .OrderByDescending(t => t.LastUpdatedAt)
            .ToArrayAsync();

        // Вытаскиваем подзадачи за выбранный день
        var allSubTasks = await _db.SubTaskLogs
            .AsNoTracking()
            .Where(l => l.CreatedAt == selectedDateUi)
            .OrderByDescending(t => t.LastUpdatedAt)
            .ToArrayAsync();

        Tasks.Clear();

        foreach (var task in allTasks)
        {
            task.SubTasks.Clear();

            var subTasksByTask = allSubTasks
                .Where(l => l.TaskId == task.Id)
                .ToList();

            // Если логов/подзадач на этот день еще нет, создаем дефолтный лог для основной задачи
            if (subTasksByTask.Count == 0)
            {
                var defaultLog = new SubTaskLog
                {
                    TaskId = task.Id,
                    Name = null,
                    CreatedAt = selectedDateUi,
                    SecondsSpent = 0
                };
                subTasksByTask.Add(defaultLog);
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

        await LoadDayLogsAsync();
    }

    /// <summary>
    /// Загружаем общую информацию времени по дню
    /// </summary>
    private async Task LoadDayLogsAsync()
    {
        var dateOnly = DateOnly.FromDateTime(SelectedDate);
        var dayLog = await _db.GeneralInfoTimeDays
            .AsNoTracking()
            .Where(x => x.Date == dateOnly)
            .FirstOrDefaultAsync();

        if (dayLog != null)
        {
            StartTimeFormatted = dayLog.WorkStartTime?.ToLocalTime().ToString(@"HH\:mm\:ss") ?? "--:--:--";
            var pauseTs = TimeSpan.FromSeconds(dayLog.TotalPauseSeconds);
            TotalPauseFormatted = string.Create(null, $"{pauseTs.Hours:D2}:{pauseTs.Minutes:D2}:{pauseTs.Seconds:D2}");
        }
        else
        {
            StartTimeFormatted = "--:--:--";
            TotalPauseFormatted = "00:00:00";
        }

        await RecalculateWorkDayPlanAsync();
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
        var defaultSubTaskLog = new SubTaskLog
        {
            TaskId = task.Id,
            Name = null,
            CreatedAt = DateOnly.FromDateTime(DateTime.Today),
            SecondsSpent = 0
        };
        _db.SubTaskLogs.Add(defaultSubTaskLog);
        await _db.SaveChangesAsync();

        // Добавляем подзадачу на UI
        task.SubTasks.Add(defaultSubTaskLog);
        task.TotalDaySeconds = 0;

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

        var subTask = new SubTaskLog
        {
            TaskId = parentTask.Id,
            Name = subTaskName,
            CreatedAt = DateOnly.FromDateTime(DateTime.Today),
            SecondsSpent = 0,
            LastUpdatedAt = DateTime.UtcNow
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

        // Очищаем трекер перед изменениями
        _db.ChangeTracker.Clear();

        if (subTask.IsRunning)
        {
            subTask.IsRunning = false;
            subTask.LastUpdatedAt = DateTime.UtcNow;
            _activeSubTask = null;
            _globalTimer?.Stop();

            // TODO: Вернуть, если вариант ниже не будет работать
            //_db.Entry(subTask).State = EntityState.Modified;
            //await _db.SaveChangesAsync();
            await SaveCurrentProgressAsync(subTask);

            // Фиксируем старт паузы
            _pauseStartedAt = DateTime.UtcNow;

            await SortSubtasksOnlyAsync(subTask.TaskId, subTask);
        }
        else
        {
            var dateOnly = DateOnly.FromDateTime(DateTime.Today);

            // Фиксируем время первого старта за день
            var dayLog = await _db.GeneralInfoTimeDays
                .Where(x => x.Date == dateOnly)
                .FirstOrDefaultAsync();

            if (dayLog == null)
            {
                dayLog = new GeneralInfoTimeDay
                {
                    Date = dateOnly,
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
            }

            // Останавливаем любую другую работающую подзадачу
            if (_activeSubTask != null)
            {
                _activeSubTask.IsRunning = false;
                _activeSubTask.LastUpdatedAt = DateTime.UtcNow;
                await SaveCurrentProgressAsync();

                await SortSubtasksOnlyAsync(_activeSubTask.TaskId, _activeSubTask);
            }

            // Переключаем на сегодняшний день, если запуск идет из прошлого
            if (SelectedDate != DateTime.Today) SelectedDate = DateTime.Today;

            // Если у этой подзадачи еще нет Id в БД (виртуальная дефолтная запись), сохраняем её
            if (subTask.Id == 0)
            {
                _db.SubTaskLogs.Add(subTask);
                await _db.SaveChangesAsync();
            }

            _activeSubTask = subTask;
            _activeSubTask.IsRunning = true;
            _globalTimer?.Start();

            // Фикс бага закрытия позадач
            //await SortTasksAndSubtasksAsync(_activeSubTask.TaskId, _activeSubTask);

            //_db.Entry(_activeSubTask).State = EntityState.Modified;
            //await _db.SaveChangesAsync();
        }

        // TODO: точно нужно здесь?
        await LoadDayLogsAsync();

        // Поменять на это при необходимости
        //await RecalculateWorkDayPlanAsync();
    }

    /// <summary>
    /// Сортируем только подзадачи внутри родителя
    /// </summary>
    /// <param name="parentId"></param>
    /// <param name="activeSubTask"></param>
    private async Task SortSubtasksOnlyAsync(int parentId, SubTaskLog activeSubTask)
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
                .FirstOrDefaultAsync();

            if (dbParent != null)
            {
                dbParent.LastUpdatedAt = DateTime.UtcNow;
            }

            //activeSubTask.LastUpdatedAt = DateTime.UtcNow;
            //await _db.SaveChangesAsync();

            await SaveCurrentProgressAsync(activeSubTask);
        }
    }

    // TODO: Удалить
    /*private async Task SortTasksAndSubtasksAsync(int parentId, SubTaskLog activeSubTask)
    {
        var parent = Tasks.FirstOrDefault(t => t.Id == parentId);
        if (parent != null)
        {
            // Переносим подзадачу наверх списка внутри UI
            parent.SubTasks.Remove(activeSubTask);
            parent.SubTasks.Insert(0, activeSubTask);

            // Обновляем дату и время изменения родительской задачи для сортировки
            var dbParent = await _db.Tasks.FindAsync(parentId);
            if (dbParent != null)
            {
                dbParent.LastUpdatedAt = DateTime.UtcNow;
            }

            activeSubTask.LastUpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Переносим саму задачу наверх основного списка
            Tasks.Remove(parent);
            Tasks.Insert(0, parent);
        }
    }*/

    /// <summary>
    /// Расчет прогнозного времени завершения рабочего дня
    /// </summary>
    private async Task RecalculateWorkDayPlanAsync()
    {
        var selectedDateUi = DateOnly.FromDateTime(SelectedDate);

        var dayLog = await _db.GeneralInfoTimeDays
            .Where(x => x.Date == selectedDateUi)
            .FirstOrDefaultAsync();

        if (StartTimeFormatted == "--:--:--" || dayLog is null || dayLog.WorkStartTime == null)
        {
            EstimatedEndTimeFormatted = "--:--:--";
            return;
        }

        var startLocal = dayLog.WorkStartTime.Value.ToLocalTime();
        var totalPauseSec = dayLog.TotalPauseSeconds;

        // Если прямо сейчас идет пауза (таймер выключен), учитываем текущий простой в реальном времени
        if (_activeSubTask is null && _pauseStartedAt != null)
        {
            totalPauseSec += (int)(DateTime.UtcNow - _pauseStartedAt.Value).TotalSeconds;
        }

        var pauseTs = TimeSpan.FromSeconds(totalPauseSec);

        // Рассчет: ко времени начала работы добавляем 9 часов + общее время пауз.
        var rawResult = startLocal + TimeSpan.FromHours(9) + pauseTs;

        // TODO: Удалить
        // Метод расчета: "Из текущего времени вычитаем время начала, результат вычитаем из 9 и прибавляем к текущему времени"
        //var now = DateTime.Now;
        //var timeWorkedSoFar = now - startLocal;
        //var rawResult = now + (TimeSpan.FromHours(9) - timeWorkedSoFar);

        // Корректировка на паузу более 1 часа после 4 часов работы:
        //if (timeWorkedSoFar.TotalHours >= 4 && totalPauseSec > 3600)
        //{
        //    rawResult = rawResult.AddHours(-1);
        //}

        // Переключатель "Был ли обед" (Если включен — вычитаем 1 час из итогового времени нахождения на работе)
        if (IsLunchIncluded)
        {
            rawResult = rawResult.AddHours(-1);
        }

        EstimatedEndTimeFormatted = rawResult.ToString(@"HH:mm:ss");
        TotalPauseFormatted = string.Create(null, $"{pauseTs.Hours:D2}:{pauseTs.Minutes:D2}:{pauseTs.Seconds:D2}");
    }

    /// <summary>
    /// Применение ручной корректировки времени через Попап
    /// </summary>
    /// <param name="subTask"></param>
    private async Task ApplyTimeAdjustmentAsync(SubTaskLog? subTask)
    {
        if (subTask == null) return;
        if (!int.TryParse(AdjustHours, out int h) || !int.TryParse(AdjustMinutes, out int m) || !int.TryParse(AdjustSeconds, out int s))
        {
            MessageBox.Show("Введите корректные числовые значения!");
            return;
        }

        int totalAdjustmentSeconds = (h * 3600) + (m * 60) + s;
        if (!IsAdjustPositive) totalAdjustmentSeconds *= -1;
        _db.ChangeTracker.Clear();

        if (subTask.Id == 0)
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
        await _db.SaveChangesAsync();

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
    }

    /// <summary>
    /// Удаление родительской задачи вместе с подзадачами
    /// </summary>
    /// <param name="task"></param>
    private async Task DeleteTaskAsync(TaskModel? task)
    {
        if (task == null) return;

        var result = MessageBox.Show($"Вы уверены, что хотите удалить задачу '{task.Name}' и всю историю её подзадач?",
            "Удаление задачи",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        _db.ChangeTracker.Clear();

        // Каскадно удаляем из БД
        var dbTask = await _db.Tasks
            .Where(t => t.Id == task.Id)
            .FirstOrDefaultAsync();

        if (dbTask != null)
        {
            var subTasksQuery = _db.SubTaskLogs.Where(l => l.TaskId == task.Id);

            _db.SubTaskLogs.RemoveRange(subTasksQuery);
            _db.Tasks.Remove(dbTask);

            await _db.SaveChangesAsync();
        }
        Tasks.Remove(task);
        CalculateTotalTime();
    }

    /// <summary>
    /// Удаление конкретной подзадачи
    /// </summary>
    /// <param name="subTask"></param>
    private async Task DeleteSubTaskAsync(SubTaskLog? subTask)
    {
        if (subTask == null) return;

        var result = MessageBox.Show($"Удалить подзадачу '{subTask.DisplayName}'?",
            "Удаление подзадачи",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        _db.ChangeTracker.Clear();

        if (subTask.Id != 0)
        {
            var dbSubTask = await _db.SubTaskLogs
                .Where(t => t.Id == subTask.Id)
                .FirstOrDefaultAsync();

            if (dbSubTask != null)
            {
                _db.SubTaskLogs.Remove(dbSubTask);
                await _db.SaveChangesAsync();
            }
        }
        var parent = Tasks.FirstOrDefault(t => t.Id == subTask.TaskId);
        if (parent != null)
        {
            parent.SubTasks.Remove(subTask);
            parent.TotalDaySeconds = parent.SubTasks.Sum(s => s.SecondsSpent);
        }
        CalculateTotalTime();
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
        await SaveCurrentProgressAsync();
        _db.Dispose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string prop = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}