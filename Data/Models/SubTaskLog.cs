using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace TimeTracker.Models;

/// <summary>
/// Подзадача для хранения затраченного времени
/// </summary>
[Table("sub_task_logs")]
public class SubTaskLog : INotifyPropertyChanged
{
    private int _id;
    private int _taskId;
    private string? _name; // Имя подзадачи (null, если это просто лог времени самой задачи)
    private DateOnly _createdAt;
    private int _secondsSpent;
    private bool _isRunning;
    private DateTime _lastUpdatedAt = DateTime.UtcNow;

    [Column("id")]
    public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }

    [Column("task_id")]
    public int TaskId { get => _taskId; set { _taskId = value; OnPropertyChanged(); } }

    [Column("name")]
    public string? Name
    {
        get => _name;
        set
        {
            _name = value;
            // DisplayName зависит от Name, поэтому при изменении Name, нужно перерисовать DisplayName
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Дата создания (хранится только дата, для простоты фильтрации)
    /// </summary>
    [Column("created_at")]
    public DateOnly CreatedAt { get => _createdAt; set { _createdAt = value; OnPropertyChanged(); } }

    /// <summary>
    /// Обновление подзадачи. Для сортировки
    /// </summary>
    [Column("last_updated_at")]
    public DateTime LastUpdatedAt
    {
        get => _lastUpdatedAt;
        set
        {
            _lastUpdatedAt = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormattedUpdatedAt));
        }
    }

    /// <summary>
    /// Сколько потрачено времени
    /// </summary>
    [Column("seconds_spent")]
    public int SecondsSpent
    {
        get => _secondsSpent;
        set
        {
            _secondsSpent = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormattedTime));
        }
    }

    // Поля для UI
    [NotMapped]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "Основное время задачи" : $"• {Name}";

    [NotMapped]
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            _isRunning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ButtonText));
        }
    }

    [NotMapped]
    public string ButtonText => IsRunning ? "⏸" : "▶";

    [NotMapped]
    public string FormattedTime
    {
        get
        {
            var ts = TimeSpan.FromSeconds(SecondsSpent);
            return string.Create(null, $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}");
        }
    }

    // Для отображения при наведении (Локальное время)
    [NotMapped]
    public string FormattedUpdatedAt => $"Изменено: {LastUpdatedAt.ToLocalTime():HH:mm:ss}";


    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string prop = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}

