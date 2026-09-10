using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace TimeTracker.Models;

[Table("tasks")]
public class TaskModel : INotifyPropertyChanged
{
    private int _id;
    private string _name;
    private int _totalDaySeconds;

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
            // Оптимизация аллокаций: структуры TimeSpan не аллоцируют память в куче
            var ts = TimeSpan.FromSeconds(TotalDaySeconds);
            return string.Create(null, $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}");
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string prop = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}
