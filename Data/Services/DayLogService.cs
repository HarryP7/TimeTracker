using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography.Xml;
using TimeTracker.Data.Interfaces;
using TimeTracker.Models;

namespace TimeTracker.Data.Services;

// TODO: Убрать дублирующие методы
/// <summary>
/// Сервис для работы с дневными логами времени
/// </summary>
public class DayLogService(AppDbContext db) : IDayLogService
{
    public async Task AddOrUpdateGeneralInfoAsync(GeneralInfoTimeDay dayInfo, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var exists = await db.GeneralInfoTimeDays
            .AnyAsync(d => d.Date == dayInfo.Date, ct);

        if (!exists)
        {
            db.GeneralInfoTimeDays.Add(dayInfo);
        }
        else
        {
            db.Entry(dayInfo).State = EntityState.Modified;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateLunchStatusAsync(GeneralInfoTimeDay? currentDayInfo, bool hasLunch, CancellationToken ct)
    {
        /*var dayInfo = await db.GeneralInfoTimeDays
            .FirstOrDefaultAsync(d => d.Date == date, ct);*/

        if (currentDayInfo != null)
        {
            currentDayInfo.HasLunch = hasLunch;
            db.Entry(currentDayInfo).State = EntityState.Modified;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<GeneralInfoTimeDay> StartWorkDayAsync(
        GeneralInfoTimeDay? currentDayInfo,
        DateOnly date,
        CancellationToken ct)
    {
        /*var currentDayInfo = await db.GeneralInfoTimeDays
            .FirstOrDefaultAsync(d => d.Date == date, ct);*/

        var startTime = DateTime.UtcNow;

        if (currentDayInfo == null)
        {
            currentDayInfo = new GeneralInfoTimeDay
            {
                Date = date,
                WorkStartTime = startTime,
                TotalPauseSeconds = 0,
                HasLunch = false
            };
            db.GeneralInfoTimeDays.Add(currentDayInfo);
        }
        else if (currentDayInfo.WorkStartTime == null)
        {
            currentDayInfo.WorkStartTime = startTime;
        }

        db.Entry(currentDayInfo).State = EntityState.Modified;
        await db.SaveChangesAsync(ct);

        return currentDayInfo;
    }

    public async Task AddPauseTimeAsync(DateOnly date, int pauseSeconds, CancellationToken ct)
    {
        var dayInfo = await db.GeneralInfoTimeDays
            .FirstOrDefaultAsync(d => d.Date == date, ct);

        if (dayInfo != null)
        {
            dayInfo.TotalPauseSeconds += pauseSeconds;
            db.Entry(dayInfo).State = EntityState.Modified;
            await db.SaveChangesAsync(ct);
        }
    }
}
