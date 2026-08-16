namespace TimeTracker.Models;

/// <summary>
/// Задача которую делаем
/// </summary>
public class TodoTask
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Последнее обновлние. Для сортировки
    /// </summary>
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// История времени. Навигационное свойство
    /// </summary>
    public List<TimeLog> TimeLogs { get; set; } = new();
}
