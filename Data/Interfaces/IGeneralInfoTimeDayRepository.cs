using TimeTracker.Models;

namespace TimeTracker.Data.Interfaces;

public interface IGeneralInfoTimeDayRepository
{
    /// <summary>
    /// Получить информацию о времени по дате
    /// </summary>
    Task<GeneralInfoTimeDay?> GetGeneralInfoTimeDayAsync(DateOnly selectedDate, CancellationToken ct);
    Task AddOrUpdateGeneralInfoAsync(GeneralInfoTimeDay dayInfo, CancellationToken ct);
}
