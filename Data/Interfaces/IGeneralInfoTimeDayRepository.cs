using TimeTracker.Models;

namespace TimeTracker.Data.Interfaces;

public interface IGeneralInfoTimeDayRepository
{
    Task<GeneralInfoTimeDay?> GetGeneralInfoTimeDayAsync(DateOnly date, CancellationToken ct);
    Task AddOrUpdateGeneralInfoAsync(GeneralInfoTimeDay dayInfo, CancellationToken ct);
}
