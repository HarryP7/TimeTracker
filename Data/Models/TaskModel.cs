using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;
using TimeTracker.Services;

namespace TimeTracker.Models;

[Table("tasks")]
public class TaskModel : INotifyPropertyChanged
{
    private int _id;
    private string _name;
    private int _totalDaySeconds;
    private bool _isExpanded;

    [Column("id")]
    public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }

    [Column("name")]
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Последнее обновление. Для сортировки
    /// </summary>
    [Column("last_updated_at")]
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Коллекция подзадач
    /// </summary>
    [NotMapped]
    public ObservableCollection<SubTaskLog> SubTasks { get; set; } = new();

    /// <summary>
    /// Хранит время за выбранный на экране день (не мапится напрямую в таблицу tasks)
    /// </summary>
    [NotMapped]
    public int TotalDaySeconds
    {
        get => _totalDaySeconds;
        set
        {
            _totalDaySeconds = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormattedTotalTime));
        }
    }

    [NotMapped]
    public string FormattedTotalTime
    {
        get
        {
            return TimeCalculationService.FormatTime(TotalDaySeconds);
        }
    }

    // Цвет текста для задач: серый, если ни одна подзадача не была запущена сегодня
    [NotMapped]
    public string TaskTextColor
    {
        get
        {
            bool hasRunningOrToday = SubTasks.Any(st => st.IsRunning || st.CreatedAt == DateOnly.FromDateTime(DateTime.Today));
            return hasRunningOrToday ? "#000000" : "#808080";
        }
    }

    /// <summary>
    /// Флаг для управления видимостью подзадач (раскрыть/скрыть)
    /// </summary>
    [NotMapped]
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    private RelayCommand<TaskModel>? _toggleExpandCommand;
    public RelayCommand<TaskModel> ToggleExpandCommand => _toggleExpandCommand ??= new RelayCommand<TaskModel>(_ => IsExpanded = !IsExpanded);

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string prop = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}
