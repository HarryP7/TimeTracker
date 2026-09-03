using TimeTracker.Models;

namespace TimeTracker.Data.Interfaces;

public interface ITaskRepository
{
    Task<TaskModel[]> GetAllTasksAsync(CancellationToken ct);
    Task AddTaskAsync(TaskModel task, CancellationToken ct);

    /// <summary>
    /// Каскадное удаление из БД
    /// </summary>
    Task DeleteTaskCascadingAsync(int taskId, CancellationToken ct);
}
