using System.Text.Json;
using TeamTracker.Models;
using TeamTracker.Services;

namespace TeamTracker.Data;

public class DataFile
{
    public int Version { get; set; }
    public List<AppUser> Users { get; set; } = new();
    public List<TaskItem> Tasks { get; set; } = new();
    public List<Notification> Notifications { get; set; } = new();
}

public class InMemoryTaskRepository : ITaskRepository
{
    protected readonly object Sync = new();
    protected DataFile Data;

    public InMemoryTaskRepository(DataFile? data = null) => Data = data ?? new DataFile();

    protected virtual void OnChanged() { }

    public DataFile Snapshot()
    {
        lock (Sync) return Clone(Data);
    }

    protected static T Clone<T>(T obj) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(obj))!;

    public AppUser? GetUser(int id)
    {
        lock (Sync) return Data.Users.FirstOrDefault(u => u.Id == id) is { } u ? Clone(u) : null;
    }

    public AppUser? GetUserByUsername(string username)
    {
        lock (Sync)
            return Data.Users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)) is { } u ? Clone(u) : null;
    }

    public List<AppUser> GetMembersOf(int leaderId)
    {
        lock (Sync) return Clone(Data.Users.Where(u => u.LeaderId == leaderId).OrderBy(u => u.Id).ToList());
    }

    public void AddUser(AppUser user)
    {
        lock (Sync)
        {
            user.Id = Data.Users.Count == 0 ? 1 : Data.Users.Max(u => u.Id) + 1;
            Data.Users.Add(Clone(user));
            OnChanged();
        }
    }

    public void UpdateUser(AppUser user)
    {
        lock (Sync)
        {
            var i = Data.Users.FindIndex(u => u.Id == user.Id);
            if (i < 0) throw new InvalidOperationException("کاربر پیدا نشد.");
            Data.Users[i] = Clone(user);
            OnChanged();
        }
    }

    public void DeleteUser(int id)
    {
        lock (Sync)
        {
            Data.Users.RemoveAll(u => u.Id == id);
            Data.Notifications.RemoveAll(n => n.UserId == id);
            OnChanged();
        }
    }

    public TaskItem? GetTask(int id)
    {
        lock (Sync) return Data.Tasks.FirstOrDefault(t => t.Id == id) is { } t ? Clone(t) : null;
    }

    public List<TaskItem> GetTasksCreatedBy(int leaderId)
    {
        lock (Sync) return Clone(Data.Tasks.Where(t => t.CreatedById == leaderId).ToList());
    }

    public List<TaskItem> GetTasksAssignedTo(int userId)
    {
        lock (Sync) return Clone(Data.Tasks.Where(t => t.AssignedToId == userId).ToList());
    }

    public void AddTask(TaskItem task)
    {
        lock (Sync)
        {
            task.Id = Data.Tasks.Count == 0 ? 1 : Data.Tasks.Max(t => t.Id) + 1;
            Data.Tasks.Add(Clone(task));
            OnChanged();
        }
    }

    public void UpdateTask(TaskItem task)
    {
        lock (Sync)
        {
            var i = Data.Tasks.FindIndex(t => t.Id == task.Id);
            if (i < 0) throw new InvalidOperationException("تسک پیدا نشد.");
            // قانون اصلی: تسک قفل‌شده به هیچ وجه قابل تغییر نیست
            if (Data.Tasks[i].IsLocked) throw new TaskLockedException();
            Data.Tasks[i] = Clone(task);
            OnChanged();
        }
    }

    public void DeleteTask(int id)
    {
        lock (Sync)
        {
            var t = Data.Tasks.FirstOrDefault(x => x.Id == id);
            if (t is null) return;
            if (t.IsLocked) throw new TaskLockedException();
            Data.Tasks.Remove(t);
            OnChanged();
        }
    }

    public void AddNotification(Notification n)
    {
        lock (Sync)
        {
            n.Id = Data.Notifications.Count == 0 ? 1 : Data.Notifications.Max(x => x.Id) + 1;
            Data.Notifications.Add(Clone(n));
            // فقط ۲۰۰ اعلان آخر هر نفر نگه داشته می‌شود
            var old = Data.Notifications.Where(x => x.UserId == n.UserId).OrderByDescending(x => x.Id).Skip(200).Select(x => x.Id).ToHashSet();
            if (old.Count > 0) Data.Notifications.RemoveAll(x => old.Contains(x.Id));
            OnChanged();
        }
    }

    public List<Notification> GetNotifications(int userId, int take)
    {
        lock (Sync) return Clone(Data.Notifications.Where(n => n.UserId == userId).OrderByDescending(n => n.Id).Take(take).ToList());
    }

    public int CountUnread(int userId)
    {
        lock (Sync) return Data.Notifications.Count(n => n.UserId == userId && !n.IsRead);
    }

    public void MarkRead(int userId, int? notificationId = null)
    {
        lock (Sync)
        {
            var changed = false;
            foreach (var n in Data.Notifications.Where(n => n.UserId == userId && !n.IsRead && (notificationId is null || n.Id == notificationId)))
            {
                n.IsRead = true;
                changed = true;
            }
            if (changed) OnChanged();
        }
    }
}
