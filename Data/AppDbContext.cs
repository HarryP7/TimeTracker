using Microsoft.EntityFrameworkCore;
using TimeTracker.Models;

namespace TimeTracker.Data;

public class AppDbContext : DbContext
{
    public DbSet<TaskModel> Tasks { get; set; }
    public DbSet<TaskTimeLog> TimeLogs { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // У одной задачи много записей истории времени
        modelBuilder.Entity<TaskTimeLog>()
            .HasOne<TaskModel>()
            .WithMany(t => t.TimeLogs)
            .HasForeignKey(tl => tl.TaskId);
    }
}

