using Microsoft.EntityFrameworkCore;
using TimeTracker.Data.Interfaces;
using TimeTracker.Models;

namespace TimeTracker.Data.Repositories
{
    public class SubTaskRepository(AppDbContext db) : ISubTaskRepository
    {
        public async Task<SubTaskLog[]> GetSubTaskLogsByDateAsync(DateOnly date, CancellationToken ct) =>
            await db.SubTaskLogs
                .AsNoTracking()
                .Where(l => l.CreatedAt == date)
                .OrderByDescending(l => l.LastUpdatedAt)
                .ToArrayAsync(ct);

        public async Task AddSubTaskAsync(SubTaskLog subTask, CancellationToken ct)
        {
            //db.ChangeTracker.Clear();
            db.SubTaskLogs.Add(subTask);
            await db.SaveChangesAsync(ct);
        }

        public async Task UpdateSubTaskLogAsync(SubTaskLog subTask, CancellationToken ct)
        {
            //db.ChangeTracker.Clear();
            db.Entry(subTask).State = EntityState.Modified;
            await db.SaveChangesAsync(ct);
        }

        public async Task DeleteSubTaskLogAsync(int subTaskId, CancellationToken ct)
        {
            //db.ChangeTracker.Clear();

            var subTask = await db.SubTaskLogs
                .Where(l => l.Id == subTaskId)
                .FirstOrDefaultAsync(ct);

            if (subTask != null)
            {
                db.SubTaskLogs.Remove(subTask);
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
