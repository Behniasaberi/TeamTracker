using TeamTracker.Data;
using TeamTracker.Models;
using TeamTracker.Services;
using TeamTracker.ViewModels;
using Xunit;

namespace TeamTracker.Tests;

public class TaskServiceTests
{
    private const int Leader = 1, Ali = 2, Sara = 3, Stranger = 9;

    private readonly ManualClock _clock = new(new DateTime(2026, 10, 4, 9, 0, 0)); // یکشنبه
    private readonly InMemoryTaskRepository _repo;
    private readonly TaskService _svc;

    public TaskServiceTests()
    {
        _repo = new InMemoryTaskRepository(new DataFile
        {
            Users =
            {
                new AppUser { Id = Leader, FullName = "پیمان", Username = "peyman", Role = Roles.Leader },
                new AppUser { Id = Ali, FullName = "بهنیا", Username = "behnia", Role = Roles.Member, LeaderId = Leader },
                new AppUser { Id = Sara, FullName = "مهرداد", Username = "mehrdad", Role = Roles.Member, LeaderId = Leader },
                new AppUser { Id = Stranger, FullName = "غریبه", Username = "x", Role = Roles.Member, LeaderId = 42 },
            }
        });
        _svc = new TaskService(_repo, _clock);
    }

    private int NewTask(int member = Ali, string dos = "مورد اول\nمورد دوم") =>
        _svc.CreateTasks(Leader, new TaskFormVm { Title = "تسک تست", Dos = dos, AssigneeIds = { member } }).Single().Id;

    private void Log(int id, double hours = 2, int member = Ali) =>
        _svc.LogWork(member, id, new WorkLogFormVm { Date = _clock.GetLocalNow().Date, Hours = hours });

    private void Submit(int id, int member = Ali) =>
        _svc.Submit(member, id, new CompleteTaskVm { Confirm = true, Note = "تمام شد" });

    [Fact]
    public void Create_for_several_members_makes_one_task_each_with_checklist()
    {
        var created = _svc.CreateTasks(Leader, new TaskFormVm
        {
            Title = "مشترک", Dos = "الف\n\nب  ", AssigneeIds = { Ali, Sara, Stranger }
        });

        Assert.Equal(2, created.Count); // غریبه عضو تیم نیست و نادیده گرفته می‌شود
        Assert.Equal(2, _repo.GetTask(created[0].Id)!.Checklist.Count);
        Assert.Equal("ب", _repo.GetTask(created[0].Id)!.Checklist[1].Text);
    }

    [Fact]
    public void Edit_keeps_ticks_of_unchanged_checklist_items()
    {
        var id = NewTask();
        _svc.ToggleCheck(Ali, id, 0, true);

        _svc.EditTask(Leader, id, new TaskFormVm { Title = "جدید", Dos = "مورد اول\nمورد سوم" });

        var t = _repo.GetTask(id)!;
        Assert.True(t.Checklist[0].Done);
        Assert.False(t.Checklist[1].Done);
        Assert.Equal("جدید", t.Title);
    }

    [Fact]
    public void Member_can_drag_between_first_two_columns_but_not_to_done()
    {
        var id = NewTask();
        _svc.MoveTask(Ali, id, WorkStatus.InProgress);
        Assert.Equal(WorkStatus.InProgress, _repo.GetTask(id)!.Status);

        Assert.Throws<InvalidOperationException>(() => _svc.MoveTask(Ali, id, WorkStatus.Done));
        Assert.Throws<InvalidOperationException>(() => _svc.MoveTask(Ali, id, WorkStatus.Review));
    }

    [Fact]
    public void Leader_can_drag_but_cannot_tick_on_behalf_of_member()
    {
        var id = NewTask();
        _svc.LeaderMove(Leader, id, WorkStatus.InProgress);
        Assert.Equal(WorkStatus.InProgress, _repo.GetTask(id)!.Status);

        Assert.Throws<InvalidOperationException>(() => _svc.LeaderMove(Leader, id, WorkStatus.Done));
    }

    [Fact]
    public void Task_with_logged_hours_cannot_go_back_to_not_started()
    {
        var id = NewTask();
        Log(id);
        Assert.Throws<InvalidOperationException>(() => _svc.MoveTask(Ali, id, WorkStatus.NotStarted));
        Assert.Throws<InvalidOperationException>(() => _svc.LeaderMove(Leader, id, WorkStatus.NotStarted));
    }

    [Fact]
    public void Member_cannot_touch_someone_elses_task()
    {
        var id = NewTask(Ali);
        Assert.Throws<KeyNotFoundException>(() => _svc.MoveTask(Sara, id, WorkStatus.InProgress));
    }

    [Fact]
    public void Submit_needs_hours_and_moves_task_to_review()
    {
        var id = NewTask();
        Assert.Throws<InvalidOperationException>(() => Submit(id));

        Log(id);
        Submit(id);

        var t = _repo.GetTask(id)!;
        Assert.Equal(WorkStatus.Review, t.Status);
        Assert.False(t.IsLocked);
        Assert.NotNull(t.SubmittedAt);
    }

    [Fact]
    public void Task_in_review_is_frozen_for_member()
    {
        var id = NewTask();
        Log(id);
        Submit(id);

        Assert.Throws<InvalidOperationException>(() => Log(id));
        Assert.Throws<InvalidOperationException>(() => _svc.ToggleCheck(Ali, id, 0, true));
        Assert.Throws<InvalidOperationException>(() => _svc.TimerStart(Ali, id));
    }

    [Fact]
    public void Approve_locks_task_forever()
    {
        var id = NewTask();
        Log(id);
        Submit(id);
        _svc.Approve(Leader, id, "عالی");

        var t = _repo.GetTask(id)!;
        Assert.Equal(WorkStatus.Done, t.Status);
        Assert.True(t.IsLocked);
        Assert.Throws<TaskLockedException>(() => _svc.EditTask(Leader, id, new TaskFormVm { Title = "x" }));
        Assert.Throws<TaskLockedException>(() => _svc.DeleteTask(Leader, id));
        Assert.Throws<TaskLockedException>(() => _svc.Reject(Leader, id, "دلیل"));
        // حتی دور زدن سرویس هم جواب نمی‌دهد: خود مخزن تسک قفل را تغییر نمی‌دهد
        t.Title = "هک";
        Assert.Throws<TaskLockedException>(() => _repo.UpdateTask(t));
    }

    [Fact]
    public void Only_review_tasks_can_be_approved()
    {
        var id = NewTask();
        Log(id);
        Assert.Throws<InvalidOperationException>(() => _svc.Approve(Leader, id, null));
    }

    [Fact]
    public void Reject_needs_reason_and_sends_task_back()
    {
        var id = NewTask();
        Log(id);
        Submit(id);

        Assert.Throws<InvalidOperationException>(() => _svc.Reject(Leader, id, "  "));
        _svc.Reject(Leader, id, "تست‌ها کامل نیست");

        var t = _repo.GetTask(id)!;
        Assert.Equal(WorkStatus.InProgress, t.Status);
        Assert.True(t.WasRejected);

        // دوباره می‌تواند کار کند و بفرستد؛ دلیل برگشت پاک می‌شود
        Log(id, 1);
        Submit(id);
        Assert.Null(_repo.GetTask(id)!.ReviewNote);
    }

    [Fact]
    public void Reassign_only_before_any_hours_are_logged()
    {
        var id = NewTask(Ali);
        _svc.Reassign(Leader, id, Sara);
        Assert.Equal(Sara, _repo.GetTask(id)!.AssignedToId);

        Assert.Throws<InvalidOperationException>(() => _svc.Reassign(Leader, id, Stranger));

        Log(id, 1, Sara);
        Assert.Throws<InvalidOperationException>(() => _svc.Reassign(Leader, id, Ali));
    }

    [Fact]
    public void Timer_logs_elapsed_time_on_stop()
    {
        var id = NewTask();
        _svc.TimerStart(Ali, id);
        Assert.Equal(WorkStatus.InProgress, _repo.GetTask(id)!.Status);

        _clock.Advance(TimeSpan.FromMinutes(90));
        var (_, hours) = _svc.TimerStop(Ali, id);

        Assert.Equal(1.5, hours);
        var t = _repo.GetTask(id)!;
        Assert.False(t.IsTimerRunning);
        Assert.Equal(1.5, t.TotalHours);
    }

    [Fact]
    public void Only_one_timer_per_member()
    {
        var a = NewTask();
        var b = NewTask();
        _svc.TimerStart(Ali, a);
        Assert.Throws<InvalidOperationException>(() => _svc.TimerStart(Ali, b));
    }

    [Fact]
    public void Submitting_with_running_timer_logs_the_time_first()
    {
        var id = NewTask();
        _svc.TimerStart(Ali, id);
        _clock.Advance(TimeSpan.FromHours(2));
        Submit(id);

        var t = _repo.GetTask(id)!;
        Assert.Equal(WorkStatus.Review, t.Status);
        Assert.Equal(2, t.TotalHours);
        Assert.False(t.IsTimerRunning);
    }

    [Fact]
    public void Cannot_log_future_dates_or_more_than_24_hours_a_day()
    {
        var id = NewTask();
        var today = _clock.GetLocalNow().Date;
        Assert.Throws<InvalidOperationException>(() =>
            _svc.LogWork(Ali, id, new WorkLogFormVm { Date = today.AddDays(1), Hours = 1 }));

        Log(id, 20);
        Assert.Throws<InvalidOperationException>(() => Log(id, 5));
    }

    [Fact]
    public void Both_sides_can_comment_and_the_other_side_is_notified()
    {
        var id = NewTask();
        _svc.AddComment(Ali, id, "یه سؤال دارم");
        _svc.AddComment(Leader, id, "بپرس");

        Assert.Equal(2, _repo.GetTask(id)!.Comments.Count);
        Assert.Equal(1, _svc.UnreadCount(Leader));            // نظر بهنیا
        Assert.Equal(2, _svc.UnreadCount(Ali));               // تسک جدید + نظر پیمان
        Assert.Throws<KeyNotFoundException>(() => _svc.AddComment(Sara, id, "من هم!"));
        Assert.Throws<InvalidOperationException>(() => _svc.AddComment(Ali, id, "   "));
    }

    [Fact]
    public void Locked_task_has_closed_conversation()
    {
        var id = NewTask();
        Log(id);
        Submit(id);
        _svc.Approve(Leader, id, null);
        Assert.Throws<TaskLockedException>(() => _svc.AddComment(Ali, id, "سلام"));
    }

    [Fact]
    public void Review_cycle_notifies_the_right_people()
    {
        var id = NewTask();
        _svc.MarkRead(Ali);
        Log(id);
        Submit(id);
        Assert.Equal(NotificationKind.Submitted, _svc.Notifications(Leader).First().Kind);

        _svc.Reject(Leader, id, "تست بنویس");
        Assert.Equal(NotificationKind.Rejected, _svc.Notifications(Ali).First().Kind);

        Submit(id);
        _svc.Approve(Leader, id, null);
        Assert.Equal(NotificationKind.Approved, _svc.Notifications(Ali).First().Kind);
        Assert.Equal(2, _svc.UnreadCount(Ali));

        _svc.MarkRead(Ali);
        Assert.Equal(0, _svc.UnreadCount(Ali));
    }

    [Fact]
    public void Week_starts_on_saturday()
    {
        Assert.Equal(new DateTime(2026, 10, 3), TaskService.WeekStart(new DateTime(2026, 10, 4)));  // یکشنبه → شنبه
        Assert.Equal(new DateTime(2026, 10, 3), TaskService.WeekStart(new DateTime(2026, 10, 3)));  // خود شنبه
        Assert.Equal(new DateTime(2026, 10, 3), TaskService.WeekStart(new DateTime(2026, 10, 9)));  // جمعه
    }

    [Fact]
    public void Dashboard_puts_submitted_tasks_in_review_queue()
    {
        var id = NewTask();
        Log(id);
        Submit(id);

        var d = _svc.Dashboard(Leader, null, null);
        Assert.Equal(id, d.ReviewQueue.Single().Id);
        Assert.Equal(2, d.Members.Single(m => m.Member.Id == Ali).WeekHours);
    }

    [Fact]
    public void Seed_data_builds_and_has_an_in_progress_task_ready_to_tick()
    {
        var data = SeedData.Create(_clock);
        var behniaTasks = data.Tasks.Where(t => t.AssignedToId == SeedData.Behnia).ToList();

        Assert.True(behniaTasks.Any(t => t.Status == WorkStatus.InProgress && t.WorkLogs.Count > 0 && !t.IsLocked));
        Assert.True(data.Tasks.Any(t => t.Status == WorkStatus.Review));
        Assert.True(data.Tasks.Count(t => t.IsLocked) >= 8);
        Assert.Equal(5, data.Users.Count);
    }
}

public class UserServiceTests
{
    private readonly InMemoryTaskRepository _repo = new(new DataFile
    {
        Users =
        {
            new AppUser { Id = 1, FullName = "پیمان", Username = "peyman", Role = Roles.Leader, PasswordHash = PasswordHasher.Hash("1234") },
            new AppUser { Id = 2, FullName = "بهنیا", Username = "behnia", Role = Roles.Member, LeaderId = 1, PasswordHash = PasswordHasher.Hash("1234") },
            new AppUser { Id = 3, FullName = "دیگری", Username = "other", Role = Roles.Member, LeaderId = 7, PasswordHash = PasswordHasher.Hash("1234") },
        }
    });

    private UserService Svc => new(_repo);

    [Fact]
    public void Leader_adds_member_who_can_log_in()
    {
        var m = Svc.AddMember(1, new MemberFormVm { FullName = "سارا", Username = "Sara", Password = "secret" });
        Assert.Equal("sara", m.Username);
        Assert.Equal(1, m.LeaderId);
        Assert.NotNull(Svc.Login("sara", "secret"));
        Assert.Null(Svc.Login("sara", "wrong"));
    }

    [Fact]
    public void Username_must_be_valid_and_unique()
    {
        Assert.Throws<InvalidOperationException>(() => Svc.AddMember(1, new MemberFormVm { FullName = "x", Username = "behnia", Password = "1234" }));
        Assert.Throws<InvalidOperationException>(() => Svc.AddMember(1, new MemberFormVm { FullName = "x", Username = "با فاصله", Password = "1234" }));
        Assert.Throws<InvalidOperationException>(() => Svc.AddMember(1, new MemberFormVm { FullName = "x", Username = "newone", Password = "12" }));
    }

    [Fact]
    public void Leader_manages_only_own_team()
    {
        Assert.Throws<KeyNotFoundException>(() => Svc.UpdateMember(1, 3, new MemberFormVm { FullName = "هک" }));
        Svc.UpdateMember(1, 2, new MemberFormVm { FullName = "بهنیا صابری", JobTitle = "فرانت‌اند", Password = "abcd" });
        Assert.Equal("بهنیا صابری", _repo.GetUser(2)!.FullName);
        Assert.NotNull(Svc.Login("behnia", "abcd"));
    }

    [Fact]
    public void Member_with_tasks_cannot_be_removed()
    {
        new TaskService(_repo, TimeProvider.System).CreateTasks(1, new TaskFormVm { Title = "t", AssigneeIds = { 2 } });
        Assert.Throws<InvalidOperationException>(() => Svc.RemoveMember(1, 2));

        var fresh = Svc.AddMember(1, new MemberFormVm { FullName = "موقت", Username = "temp", Password = "1234" });
        Svc.RemoveMember(1, fresh.Id);
        Assert.Null(_repo.GetUser(fresh.Id));
    }

    [Fact]
    public void Password_change_needs_current_password()
    {
        Assert.Throws<InvalidOperationException>(() => Svc.ChangePassword(2, "bad", "newpass"));
        Svc.ChangePassword(2, "1234", "newpass");
        Assert.NotNull(Svc.Login("behnia", "newpass"));
    }
}
