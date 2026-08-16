using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TimeTracker.Data;
using TimeTracker.Models;

namespace TimeTracker.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly AppDbContext _db;
    private string _newTaskName;

    public ObservableCollection<TaskModel> Tasks { get; set; }

    public string NewTaskName { get => _newTaskName; set { _newTaskName = value; OnPropertyChanged(); } }

    public ICommand AddCommand { get; }
    public ICommand ToggleTimerCommand { get; }

    public MainViewModel()
    {
        _db = new AppDbContext();
        _db.Database.EnsureCreated(); // Создает БД и таблицу, если их нет

        Tasks = new ObservableCollection<TaskModel>(_db.Tasks.ToList());

        AddCommand = new RelayCommand(AddTask);
        ToggleTimerCommand = new RelayCommand(ToggleTimer);
    }

    private void AddTask(object obj)
    {
        if (string.IsNullOrWhiteSpace(NewTaskName)) return;

        var task = new TaskModel { Name = NewTaskName, TotalSeconds = 0 };
        _db.Tasks.Add(task);
        _db.SaveChanges();

        Tasks.Add(task);
        NewTaskName = string.Empty;
    }

    private void ToggleTimer(object obj)
    {
        if (obj is TaskModel task)
        {
            if (task.IsRunning)
            {
                task.Stop();
                _db.SaveChanges(); // Сохраняем при остановке
            }
            else
            {
                // Останавливаем другие задачи (одновременно может работать только одна)
                foreach (var t in Tasks.Where(t => t.IsRunning))
                {
                    t.Stop();
                }

                task.Start(() => _db.SaveChanges()); // Сохраняем каждую секунду в БД
            }
        }
    }

    public void CloseConnection()
    {
        foreach (var t in Tasks.Where(t => t.IsRunning)) t.Stop();
        _db.SaveChanges();
        _db.Dispose();
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string prop = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}
