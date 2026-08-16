using Microsoft.EntityFrameworkCore;
using TimeTracker.Models;

namespace TimeTracker.Data;

public class AppDbContext : DbContext
{
    public DbSet<TodoTask> Tasks { get; set; }
    public DbSet<TimeLog> TimeLogs { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // У одной задачи много записей истории времени
        modelBuilder.Entity<TimeLog>()
            .HasOne<TodoTask>()
            .WithMany(t => t.TimeLogs)
            .HasForeignKey(tl => tl.TaskId);
    }
}

