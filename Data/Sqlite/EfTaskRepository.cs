using Microsoft.EntityFrameworkCore;
using TeamTracker.Models;
using TeamTracker.Services;

namespace TeamTracker.Data.Sqlite;

public class EfTaskRepository : ITaskRepository
{
    private static readonly object WriteLock = new();
    private readonly AppDbContext _db;

    public EfTaskRepository(AppDbContext db) => _db = db;

    private void Save()
    {
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public AppUser? GetUser(int id) =>
        _db.Users.AsNoTracking().FirstOrDefault(u => u.Id == id);

    public AppUser? GetUserByUsername(string username)
    {
        var name = username.Trim().ToLower();
        return _db.Users.AsNoTracking().FirstOrDefault(u => u.Username.ToLower() == name);
    }

    public List<AppUser> GetMembersOf(int leaderId) =>
        _db.Users.AsNoTracking().Where(u => u.LeaderId == leaderId).OrderBy(u => u.Id).ToList();

    public void AddUser(AppUser user)
    {
        lock (WriteLock)
        {
            user.Id = 0;
            _db.Users.Add(user);
            Save();
        }
    }

    public void UpdateUser(AppUser user)
    {
        lock (WriteLock)
        {
            if (!_db.Users.Any(u => u.Id == user.Id)) throw new InvalidOperationException("کاربر پیدا نشد.");
            _db.Users.Update(user);
            Save();
        }
    }

    public void DeleteUser(int id)
    {
        lock (WriteLock)
        {
            _db.Notifications.Where(n => n.UserId == id).ExecuteDelete();
            _db.Users.Where(u => u.Id == id).ExecuteDelete();
        }
    }

    public TaskItem? GetTask(int id) =>
        _db.Tasks.AsNoTracking().FirstOrDefault(t => t.Id == id);

    public List<TaskItem> GetTasksCreatedBy(int leaderId) =>
        _db.Tasks.AsNoTracking().Where(t => t.CreatedById == leaderId).ToList();

    public List<TaskItem> GetTasksAssignedTo(int userId) =>
        _db.Tasks.AsNoTracking().Where(t => t.AssignedToId == userId).ToList();

    public void AddTask(TaskItem task)
    {
        lock (WriteLock)
        {
            task.Id = 0;
            _db.Tasks.Add(task);
            Save();
        }
    }

    public void UpdateTask(TaskItem task)
    {
        lock (WriteLock)
        {
            var stored = _db.Tasks.Where(t => t.Id == task.Id).Select(t => (bool?)t.IsLocked).FirstOrDefault();
            if (stored is null) throw new InvalidOperationException("تسک پیدا نشد.");
            // قانون اصلی: تسک قفل‌شده به هیچ وجه قابل تغییر نیست
            if (stored.Value) throw new TaskLockedException();
            _db.Tasks.Update(task);
            Save();
        }
    }

    public void DeleteTask(int id)
    {
        lock (WriteLock)
        {
            var stored = _db.Tasks.Where(t => t.Id == id).Select(t => (bool?)t.IsLocked).FirstOrDefault();
            if (stored is null) return;
            if (stored.Value) throw new TaskLockedException();
            _db.Tasks.Where(t => t.Id == id).ExecuteDelete();
        }
    }

    public void AddNotification(Notification n)
    {
        lock (WriteLock)
        {
            n.Id = 0;
            _db.Notifications.Add(n);
            Save();

            var old = _db.Notifications.Where(x => x.UserId == n.UserId)
                .OrderByDescending(x => x.Id).Skip(200).Select(x => x.Id).ToList();
            if (old.Count > 0) _db.Notifications.Where(x => old.Contains(x.Id)).ExecuteDelete();
        }
    }

    public List<Notification> GetNotifications(int userId, int take) =>
        _db.Notifications.AsNoTracking().Where(n => n.UserId == userId)
            .OrderByDescending(n => n.Id).Take(take).ToList();

    public int CountUnread(int userId) =>
        _db.Notifications.Count(n => n.UserId == userId && !n.IsRead);

    public void MarkRead(int userId, int? notificationId = null)
    {
        lock (WriteLock)
        {
            _db.Notifications
                .Where(n => n.UserId == userId && !n.IsRead && (notificationId == null || n.Id == notificationId))
                .ExecuteUpdate(s => s.SetProperty(n => n.IsRead, true));
        }
    }
}
