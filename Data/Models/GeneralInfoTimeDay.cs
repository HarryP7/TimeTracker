using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace TimeTracker.Models;

/// <summary>
/// Общая информация времени по дню
/// </summary>
[Table("general_info_time_day")]
public class GeneralInfoTimeDay : INotifyPropertyChanged
{
    private bool _hasLunch;

    /// <summary>
    /// Дата на которую записываем инфо
    /// </summary>
    [Key]
    [Column("date")]
    public DateOnly Date { get; set; }

    /// <summary>
    /// Время начала работы
    /// </summary>
    [Column("work_start_time")]
    public DateTime? WorkStartTime { get; set; }

    /// <summary>
    /// Общее время пауз
    /// </summary>
    [Column("total_pause_seconds")]
    public int TotalPauseSeconds { get; set; }

    /// <summary>
    /// Время начала текущей паузы (null — пауза не идёт)
    /// </summary>
    [Column("pause_started_at")]
    public DateTime? PauseStartedAt { get; set; }

    /// <summary>
    /// Время завершения рабочего дня (null — день не завершён)
    /// </summary>
    [Column("work_end_time")]
    public DateTime? WorkEndTime { get; set; }

    /// <summary>
    /// Баланс времени в секундах (может быть отрицательным).
    /// Рассчитывается при завершении рабочего дня: затраченное время - 8 часов
    /// </summary>
    [Column("time_balance_seconds")]
    public int TimeBalanceSeconds { get; set; }

    /// <summary>
    /// Был ли обед
    /// </summary>
    [Column("has_lunch")]
    public bool HasLunch
    {
        get => _hasLunch;
        set
        {
            if (_hasLunch != value)
            {
                _hasLunch = value;
                OnPropertyChanged();
            }
        }
    }


    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string prop = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}
