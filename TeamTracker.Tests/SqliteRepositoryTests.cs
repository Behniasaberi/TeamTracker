using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TeamTracker.Data;
using TeamTracker.Data.Sqlite;
using TeamTracker.Models;
using TeamTracker.Services;
using TeamTracker.ViewModels;
using Xunit;

namespace TeamTracker.Tests;

public sealed class SqliteRepositoryTests : IDisposable
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private readonly ManualClock _clock = new(new DateTime(2026, 10, 4, 10, 0, 0));

    public SqliteRepositoryTests()
    {
        _conn.Open();
        using var db = NewDb();
        db.Database.EnsureCreated();
        SqliteStorage.Seed(db, _clock);
    }

    public void Dispose() => _conn.Dispose();

    private AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);

    [Fact]
    public void Sample_data_round_trips_with_json_columns()
    {
        using var db = NewDb();
        var repo = new EfTaskRepository(db);

        var members = repo.GetMembersOf(SeedData.Peyman);
        Assert.Equal(4, members.Count);

        var board = repo.GetTasksAssignedTo(SeedData.Behnia).Single(t => t.Title == "برد کانبان با درگ‌اند‌دراپ");
        Assert.Equal(WorkStatus.InProgress, board.Status);
        Assert.True(board.WorkLogs.Count > 0);
        Assert.Equal(4, board.Checklist.Count);
        Assert.True(board.Comments.Count > 0);
        Assert.NotNull(repo.GetUserByUsername("BEHNIA"));
    }

    [Fact]
    public void Full_review_cycle_works_on_sqlite_and_lock_is_enforced()
    {
        using var db = NewDb();
        var repo = new EfTaskRepository(db);
        var svc = new TaskService(repo, _clock);

        var id = svc.CreateTasks(SeedData.Peyman, new TaskFormVm { Title = "تست SQLite", Dos = "الف", AssigneeIds = { SeedData.Mojtaba } }).Single().Id;
        svc.LogWork(SeedData.Mojtaba, id, new WorkLogFormVm { Date = _clock.GetLocalNow().Date, Hours = 2 });
        svc.ToggleCheck(SeedData.Mojtaba, id, 0, true);
        svc.AddComment(SeedData.Mojtaba, id, "تموم شد");
        svc.Submit(SeedData.Mojtaba, id, new CompleteTaskVm { Confirm = true });
        svc.Approve(SeedData.Peyman, id, "عالی");

        var t = repo.GetTask(id)!;
        Assert.True(t.IsLocked);
        Assert.Equal(2, t.TotalHours);
        Assert.True(t.Checklist[0].Done);
        Assert.Single(t.Comments);

        t.Title = "هک";
        Assert.Throws<TaskLockedException>(() => repo.UpdateTask(t));
        Assert.Throws<TaskLockedException>(() => repo.DeleteTask(id));
    }

    [Fact]
    public void Notifications_are_stored_and_marked_read()
    {
        using var db = NewDb();
        var repo = new EfTaskRepository(db);
        repo.MarkRead(SeedData.Peyman);
        Assert.Equal(0, repo.CountUnread(SeedData.Peyman));

        repo.AddNotification(new Notification { UserId = SeedData.Peyman, Text = "سلام", At = DateTime.Now });
        Assert.Equal(1, repo.CountUnread(SeedData.Peyman));
        Assert.Equal("سلام", repo.GetNotifications(SeedData.Peyman, 1).Single().Text);

        repo.MarkRead(SeedData.Peyman);
        Assert.Equal(0, repo.CountUnread(SeedData.Peyman));
    }

    [Fact]
    public void Users_can_be_added_updated_and_removed()
    {
        using var db = NewDb();
        var users = new UserService(new EfTaskRepository(db));
        var sara = users.AddMember(SeedData.Peyman, new MemberFormVm { FullName = "سارا", Username = "sara", Password = "1234" });
        Assert.True(sara.Id > 5);

        users.UpdateMember(SeedData.Peyman, sara.Id, new MemberFormVm { FullName = "سارا احمدی" });
        Assert.NotNull(users.Login("sara", "1234"));

        users.RemoveMember(SeedData.Peyman, sara.Id);
        Assert.Null(users.Login("sara", "1234"));
    }
}
