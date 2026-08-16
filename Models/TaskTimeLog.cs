using System.ComponentModel.DataAnnotations.Schema;

namespace TimeTracker.Models;

/// <summary>
/// История времени
/// </summary>
[Table("task_time_logs")]
public class TaskTimeLog
{
    [Column("id")]
    public int Id { get; set; }

    [Column("task_id")]
    public int TaskId { get; set; }

    /// <summary>
    /// Дата логирования (хранится только дата, для простоты фильтрации)
    /// </summary>
    [Column("date")]
    public DateOnly Date { get; set; }

    /// <summary>
    /// Сколько потрачено времени
    /// </summary>
    [Column("seconds_spent")] 
    public int SecondsSpent { get; set; }
}