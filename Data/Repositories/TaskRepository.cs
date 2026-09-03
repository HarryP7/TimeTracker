using Microsoft.EntityFrameworkCore;
using TimeTracker.Data.Interfaces;
using TimeTracker.Models;

namespace TimeTracker.Data.Repositories;

public class TaskRepository(AppDbContext db) : ITaskRepository
{
    public async Task<TaskModel[]> GetAllTasksAsync(CancellationToken ct) =>
        await db.Tasks
        .AsNoTracking()
        // Сортировка: по убыванию даты и времени последнего выполнения
        .OrderByDescending(t => t.LastUpdatedAt)
        .ToArrayAsync(ct);

    public async Task AddTaskAsync(TaskModel task, CancellationToken ct)
    {
        //db.ChangeTracker.Clear();
        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task DeleteTaskCascadingAsync(int taskId, CancellationToken ct)
    {
        //db.ChangeTracker.Clear();

        // Каскадно удаляем из БД
        var task = await db.Tasks
            .Where(t => t.Id == taskId)
            .FirstOrDefaultAsync(ct);

        if (task != null)
        {
            var subTasksQuery = db.SubTaskLogs.Where(l => l.TaskId == taskId);

            db.SubTaskLogs.RemoveRange(subTasksQuery);
            db.Tasks.Remove(task);

            await db.SaveChangesAsync(ct);
        }
    }
}