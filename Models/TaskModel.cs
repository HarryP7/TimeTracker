using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace TimeTracker.Models;

[Table("tasks")]
public class TaskModel : INotifyPropertyChanged
{
    private int _id;
    private string _name;
    private int _currentDaySeconds;
    private bool _isRunning;
    // NOTE: Возможно убрать
    //private DispatcherTimer _timer;

    [Column("id")]
    public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }

    [Column("name")]
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("total_seconds")]
    public int TotalSeconds
    {
        get => _currentDaySeconds;
        set { _currentDaySeconds = value; OnPropertyChanged(nameof(TotalSeconds)); OnPropertyChanged(nameof(FormattedTime)); }
    }

    [Column("last_updated_at")]
    /// <summary>
    /// Последнее обновлние. Для сортировки
    /// </summary>
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Хранит время за выбранный на экране день (не мапится напрямую в таблицу tasks)
    /// </summary>
    [NotMapped]
    public int CurrentDaySeconds
    {
        get => _currentDaySeconds;
        set { _currentDaySeconds = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormattedTime)); }
    }

    /// <summary>
    /// История времени. Навигационное свойство
    /// </summary>
    public List<TaskTimeLog> TimeLogs { get; set; } = new();

    /// <summary>
    /// Запущен ли таймер. Не сохраняем в БД, нужно только для UI
    /// </summary>
    [NotMapped]
    public bool IsRunning
    {
        get => _isRunning;
        set { _isRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(ButtonText)); }
    }

    [NotMapped]
    public string ButtonText => IsRunning ? "Стоп" : "Начать";

    [NotMapped]
    //public string FormattedTime => TimeSpan.FromSeconds(TotalSeconds).ToString(@"hh\:mm\:ss");
    public string FormattedTime
    {
        get
        {
            // Оптимизация аллокаций: структуры TimeSpan не аллоцируют память в куче
            var ts = TimeSpan.FromSeconds(CurrentDaySeconds);
            return string.Create(null, $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}");
        }
    }

    // NOTE: Возможно убрать
    //public void Start(Action onTick)
    //{
    //    IsRunning = true;
    //    _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
    //    _timer.Tick += (s, e) =>
    //    {
    //        TotalSeconds++;
    //        onTick?.Invoke(); // Вызываем сохранение каждую секунду
    //    };
    //    _timer.Start();
    //}
    //// NOTE: Возможно убрать
    //public void Stop()
    //{
    //    IsRunning = false;
    //    _timer?.Stop();
    //    _timer = null;
    //}

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string prop = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}
