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
        if (currentDayInfo is not null && currentDayInfo.WorkStartTime is not null)
        {
            return currentDayInfo;
        }
        var dbCurrentDayInfo = await db.GeneralInfoTimeDays
            .FirstOrDefaultAsync(d => d.Date == date, ct);

        var startTime = DateTime.UtcNow;

        if (dbCurrentDayInfo == null)
        {
            dbCurrentDayInfo = new GeneralInfoTimeDay
            {
                Date = date,
                WorkStartTime = startTime,
                TotalPauseSeconds = 0,
                HasLunch = false
            };
            db.GeneralInfoTimeDays.Add(dbCurrentDayInfo);
        }
        else if (dbCurrentDayInfo.WorkStartTime == null)
        {
            dbCurrentDayInfo.WorkStartTime = startTime;
        }

        //db.Entry(currentDayInfo).State = EntityState.Modified;
        await db.SaveChangesAsync(ct);

        return dbCurrentDayInfo;
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
