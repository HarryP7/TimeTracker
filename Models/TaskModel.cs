using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace TimeTracker.Models;

[Table("tasks")]
public class TaskModel : INotifyPropertyChanged
{
    private int _id;
    private string _name;
    private int _totalSeconds;
    private bool _isRunning;
    private DispatcherTimer _timer;

    [Column("id")]
    public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }

    [Column("name")]
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

    [Column("total_seconds")]
    public int TotalSeconds
    {
        get => _totalSeconds;
        set { _totalSeconds = value; OnPropertyChanged(nameof(TotalSeconds)); OnPropertyChanged(nameof(FormattedTime)); }
    }

    // Не сохраняем в БД, нужно только для UI
    [NotMapped]
    public bool IsRunning
    {
        get => _isRunning;
        set { _isRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(ButtonText)); }
    }

    [NotMapped]
    public string ButtonText => IsRunning ? "Стоп" : "Начать";

    [NotMapped]
    public string FormattedTime => TimeSpan.FromSeconds(TotalSeconds).ToString(@"hh\:mm\:ss");

    public void Start(Action onTick)
    {
        IsRunning = true;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (s, e) =>
        {
            TotalSeconds++;
            onTick?.Invoke(); // Вызываем сохранение каждую секунду
        };
        _timer.Start();
    }

    public void Stop()
    {
        IsRunning = false;
        _timer?.Stop();
        _timer = null;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string prop = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}
