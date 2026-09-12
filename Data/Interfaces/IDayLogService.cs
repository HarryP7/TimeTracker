using TimeTracker.Models;
namespace TimeTracker.Data.Interfaces;
/// <summary>
/// Сервис для работы с дневными логами времени
/// </summary>
public interface IDayLogService
{    
    /// <summary>
    /// Сохранить или обновить информацию о дне
    /// </summary>
    Task AddOrUpdateGeneralInfoAsync(GeneralInfoTimeDay dayInfo, CancellationToken ct);
    
    /// <summary>
    /// Обновить флаг "Был ли обед"
    /// </summary>
    Task UpdateLunchStatusAsync(GeneralInfoTimeDay? currentDayInfo, bool hasLunch, CancellationToken ct);
    
    /// <summary>
    /// Запустить таймер работы (установить время начала)
    /// </summary>
    Task<GeneralInfoTimeDay> StartWorkDayAsync(GeneralInfoTimeDay? currentDayInfo, DateOnly date, CancellationToken ct);
    
    /// <summary>
    /// Добавить время паузы к общему времени
    /// </summary>
    Task AddPauseTimeAsync(DateOnly date, int pauseSeconds, CancellationToken ct);
}