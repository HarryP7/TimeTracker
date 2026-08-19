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
    private DateOnly _date;
    private int _secondsSpent;
    private bool _isRunning;

    [Column("id")]
    public int Id
    {
        get => _id;
        set
        {
            _id = value;
            OnPropertyChanged();
        }
    }

    [Column("task_id")]
    public int TaskId
    {
        get => _taskId;
        set
        {
            _taskId = value;
            OnPropertyChanged();
        }
    }

    [Column("name")]
    public string? Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Дата логирования (хранится только дата, для простоты фильтрации)
    /// </summary>
    [Column("created_at")]
    public DateOnly CreatedAt { get => _date; set { _date = value; OnPropertyChanged(); } }

    /// <summary>
    /// Последнее обновление. Для сортировки
    /// </summary>
    [Column("last_updated_at")]
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

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

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string prop = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}

