using Microsoft.EntityFrameworkCore;
using TimeTracker.Models;

namespace TimeTracker.Data;

public class AppDbContext : DbContext
{
    public DbSet<TaskModel> Tasks { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // TODO: Задавать через appsetings.json
        //optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=timetracker;Username=postgres;Password=your_password");
        optionsBuilder.UseNpgsql("Server=localhost;Port=5432;Database=timetracker;Username=postgres;Password=postgres;Pooling=true");
    }
}

