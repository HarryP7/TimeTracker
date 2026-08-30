using Microsoft.EntityFrameworkCore;
using TimeTracker.Models;

namespace TimeTracker.Data;

public class AppDbContext : DbContext
{
    public DbSet<TaskModel> Tasks { get; set; }
    public DbSet<SubTaskLog> SubTaskLogs { get; set; }

    public DbSet<GeneralInfoTimeDay> GeneralInfoTimeDays { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // У одной задачи много подзадач
        modelBuilder.Entity<SubTaskLog>()
            .HasOne<TaskModel>()
            .WithMany(t => t.SubTasks)
            .HasForeignKey(tl => tl.TaskId);
    }
}