using System.Collections.ObjectModel;
using TimeTracker.Models;

namespace TimeTracker.Services;

/// <summary>
/// Сервис для управления таймерами и вычислениями времени
/// </summary>
public class TimeCalculationService()
{
    /// <summary>
    /// Расчет суммарного времени по всем задачам
    /// </summary>
    public string CalculateTotalTime(ObservableCollection<TaskModel> uiTasks)
    {
        int total = uiTasks.Sum(t => t.TotalDaySeconds);
        return FormatTime(total);
    }

    /// <summary>
    /// Вычислить отформатированное время на основе секунд
    /// </summary>
    /// <remark>Оптимизация аллокаций: структуры TimeSpan не аллоцируют память в куче</remark>
    public static string FormatTime(int totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return string.Create(null, $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}");
    }

    /// <summary>
    /// Применить корректировку времени к подзадаче
    /// </summary>
    public void ApplyTimeAdjustment(SubTaskLog subTask, int hours, int minutes, int seconds, bool isPositive)
    {
        int totalAdjustmentSeconds = (hours * 3600) + (minutes * 60) + seconds;
        if (!isPositive) totalAdjustmentSeconds *= -1;

        subTask.SecondsSpent = Math.Max(0, subTask.SecondsSpent + totalAdjustmentSeconds);
        subTask.LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Вычислить форматированное время окончания рабочего дня.
    /// Расчет: Время начала + 9 часов + паузы - 1 час (если был обед)
    /// </summary>
    public string CalculateEstimatedEndTime(DateTime startTime, int totalPauseSeconds, bool isLunchIncluded)
    {
        //return WorkTimeCalculator.CalculateEstimatedEndTime(startTime, totalPauseSeconds, isLunchIncluded);

        var result = startTime.ToLocalTime()
            .AddHours(9)
            .AddSeconds(totalPauseSeconds);

        // Переключатель "Был ли обед" (Если включен — вычитаем 1 час из итогового времени нахождения на работе)
        if (isLunchIncluded)
        {
            result = result.AddHours(-1);
        }

        return result.ToString(@"HH:mm:ss");
    }
}
