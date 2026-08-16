namespace TimeTracker.Models;

/// <summary>
/// История времени
/// </summary>
public class TimeLog
{
    public int Id { get; set; }

    public int TaskId { get; set; }

    // TODO: заменить на DateOnly
    /// <summary>
    /// Дата логирования (хранится только дата, для простоты фильтрации)
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Сколько потрачено времени
    /// </summary>
    public TimeSpan Duration { get; set; }
}