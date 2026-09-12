using System.Collections.ObjectModel;
using TimeTracker.Data.Interfaces;
using TimeTracker.Models;

namespace TimeTracker.Services;

/// <summary>
/// Сервис для управления задачами и подзадачами
/// </summary>
public class TaskManagementService(
    ITaskRepository taskRepository,
    ISubTaskRepository subTaskRepository)
{
    /// <summary>
    /// Загрузить задачи и подзадачи для выбранной даты
    /// </summary>
    public async Task LoadTasksAndLogsAsync(
        ObservableCollection<TaskModel> uiTasks,
        DateOnly selectedDate,
        CancellationToken ct)
    {
        var allTasks = await taskRepository.GetAllTasksAsync(ct);

        // Вытаскиваем подзадачи за выбранный день
        var allSubTasks = await subTaskRepository.GetSubTaskLogsByDateAsync(selectedDate, ct);

        uiTasks.Clear();

        foreach (var task in allTasks)
        {
            task.SubTasks.Clear();

            var subTasksByTask = allSubTasks
                .Where(l => l.TaskId == task.Id)
                .ToArray();

            // Если логов/подзадач на этот день еще нет, создаем дефолтный лог для основной задачи
            if (subTasksByTask.Length == 0)
            {
                var defaultSubTask = new SubTaskLog
                {
                    TaskId = task.Id,
                    Name = null,
                    CreatedAt = selectedDate,
                    SecondsSpent = 0,
                    LastUpdatedAt = DateTime.UtcNow
                };
                await subTaskRepository.AddSubTaskAsync(defaultSubTask, ct);

                subTasksByTask = [defaultSubTask];
            }

            // Если задача была активна, но мы сменили дату - визуально останавливаем её отображение
            //if (task == _activeSubTask && dateOnly != DateOnly.FromDateTime(DateTime.Today)) task.IsRunning = false;

            foreach (var subTask in subTasksByTask)
            {
                // TODO: Вернуть, если будет необходимо
                /*if (_activeSubTask != null && _activeSubTask.Id == subTask.Id && selectedDate == DateOnly.FromDateTime(DateTime.Today))
                {
                    subTask.IsRunning = true;
                }*/
                task.SubTasks.Add(subTask);
            }

            task.TotalDaySeconds = task.SubTasks.Sum(s => s.SecondsSpent);
            uiTasks.Add(task);
        }
    }

    /// <summary>
    /// Добавить основную задачу
    /// </summary>
    public async Task<TaskModel> AddTaskAsync(string taskName, CancellationToken ct)
    {
        var task = new TaskModel
        {
            Name = taskName,
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };

        await taskRepository.AddTaskAsync(task, ct);

        // Сразу создаем дефолтную запись времени на сегодня
        var defaultSubTaskLog = new SubTaskLog
        {
            TaskId = task.Id,
            Name = null,
            CreatedAt = DateOnly.FromDateTime(DateTime.Today),
            SecondsSpent = 0,
            LastUpdatedAt = DateTime.UtcNow
        };
        await subTaskRepository.AddSubTaskAsync(defaultSubTaskLog, ct);

        return task;
    }

    /// <summary>
    /// Добавить подзадачу
    /// </summary>
    public async Task<SubTaskLog> AddSubTaskAsync(TaskModel parentTask, string subTaskName, CancellationToken ct)
    {
        var subTask = new SubTaskLog
        {
            TaskId = parentTask.Id,
            Name = subTaskName,
            CreatedAt = DateOnly.FromDateTime(DateTime.Today),
            SecondsSpent = 0,
            LastUpdatedAt = DateTime.UtcNow
        };

        await subTaskRepository.AddSubTaskAsync(subTask, ct);

        return subTask;
    }

    /// <summary>
    /// Удалить основную задачу вместе с подзадачами
    /// </summary>
    public async Task DeleteTaskAsync(int taskId, CancellationToken ct)
    {
        await taskRepository.DeleteTaskCascadingAsync(taskId, ct);
    }

    /// <summary>
    /// Удалить подзадачу
    /// </summary>
    public async Task DeleteSubTaskAsync(int subTaskId, CancellationToken ct)
    {
        await subTaskRepository.DeleteSubTaskLogAsync(subTaskId, ct);
    }

    /// <summary>
    /// Обновить подзадачу в БД
    /// </summary>
    public async Task UpdateSubTaskLogAsync(SubTaskLog subTask, CancellationToken ct)
    {
        await subTaskRepository.UpdateSubTaskLogAsync(subTask, ct);
    }
}
