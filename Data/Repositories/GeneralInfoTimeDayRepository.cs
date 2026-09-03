using Microsoft.EntityFrameworkCore;
using TimeTracker.Data.Interfaces;
using TimeTracker.Models;

namespace TimeTracker.Data.Repositories
{
    public class GeneralInfoTimeDayRepository(AppDbContext db) : IGeneralInfoTimeDayRepository
    {
        public async Task<GeneralInfoTimeDay?> GetGeneralInfoTimeDayAsync(DateOnly selectedDate, CancellationToken ct) =>
            await db.GeneralInfoTimeDays
            .AsNoTracking()
            .Where(d => d.Date == selectedDate)
            .FirstOrDefaultAsync(ct);

        public async Task AddOrUpdateGeneralInfoAsync(GeneralInfoTimeDay dayInfo, CancellationToken ct)
        {
            db.ChangeTracker.Clear();
            var exists = await db.GeneralInfoTimeDays
                .AnyAsync(d => d.Date == dayInfo.Date, ct);

            if (!exists)
            {
                db.GeneralInfoTimeDays.Add(dayInfo);
            }
            else db.Entry(dayInfo).State = EntityState.Modified;

            await db.SaveChangesAsync(ct);
        }
    }
}
