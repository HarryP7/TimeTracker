using TimeTracker.Models;

namespace TimeTracker.Data.Interfaces;

public interface ISubTaskRepository
{
    Task<SubTaskLog[]> GetSubTaskLogsByDateAsync(DateOnly date, CancellationToken ct);
    Task AddSubTaskAsync(SubTaskLog subTask, CancellationToken ct);
    Task UpdateSubTaskLogAsync(SubTaskLog subTask, CancellationToken ct);
    Task DeleteSubTaskLogAsync(int subTaskId, CancellationToken ct);
}
