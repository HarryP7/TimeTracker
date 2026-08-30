using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeTracker.Models;

/// <summary>
/// Общая информация времени по дню
/// </summary>
[Table("general_info_time_day")]
public class GeneralInfoTimeDay
{
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
}
