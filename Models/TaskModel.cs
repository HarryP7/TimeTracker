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

    /// <summary>
    /// Последнее обновление. Для сортировки
    /// </summary>
    [Column("last_updated_at")]
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
    public string FormattedTime
    {
        get
        {
            // Оптимизация аллокаций: структуры TimeSpan не аллоцируют память в куче
            var ts = TimeSpan.FromSeconds(CurrentDaySeconds);
            return string.Create(null, $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}");
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string prop = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}
