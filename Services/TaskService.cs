using TeamTracker.Models;
using TeamTracker.ViewModels;

namespace TeamTracker.Services;

public class TaskService
{
    private readonly ITaskRepository _repo;
    private readonly TimeProvider _clock;

    public TaskService(ITaskRepository repo, TimeProvider clock)
    {
        _repo = repo;
        _clock = clock;
    }

    public const double MaxHoursPerDay = 24;

    private DateTime Now => _clock.GetLocalNow().DateTime;
    private DateTime Today => Now.Date;

    public List<AppUser> Members(int leaderId) => _repo.GetMembersOf(leaderId);

    public List<TaskItem> CreateTasks(int leaderId, TaskFormVm vm)
    {
        var allowed = _repo.GetMembersOf(leaderId).Select(m => m.Id).ToHashSet();
        var targets = vm.AssigneeIds.Distinct().Where(allowed.Contains).ToList();
        if (targets.Count == 0)
            throw new InvalidOperationException("حداقل یک نفر از اعضای تیم را انتخاب کنید.");

        // اگر چند نفر انتخاب شوند، برای هر نفر یک تسک مستقل ساخته می‌شود
        // تا ساعت و تیک هر شخص جدا ثبت شود.
        var created = new List<TaskItem>();
        foreach (var memberId in targets)
        {
            var t = new TaskItem
            {
                Title = vm.Title.Trim(),
                Description = vm.Description?.Trim() ?? "",
                Checklist = TaskItem.SplitLines(vm.Dos).Select(s => new CheckItem { Text = s }).ToList(),
                Donts = vm.Donts?.Trim() ?? "",
                Priority = vm.Priority,
                Deadline = vm.Deadline?.Date,
                AssignedToId = memberId,
                CreatedById = leaderId,
                CreatedAt = Now
            };
            AddEvent(t, ActivityKind.Created, leaderId);
            _repo.AddTask(t);
            Notify(memberId, leaderId, t, NotificationKind.Assigned, $"{NameOf(leaderId)} تسک «{t.Title}» رو بهت سپرد");
            created.Add(t);
        }
        return created;
    }

    public TaskItem EditTask(int leaderId, int taskId, TaskFormVm vm)
    {
        var task = GetLeaderTask(leaderId, taskId);
        if (task.IsLocked) throw new TaskLockedException();

        task.Title = vm.Title.Trim();
        task.Description = vm.Description?.Trim() ?? "";
        task.Donts = vm.Donts?.Trim() ?? "";
        task.Priority = vm.Priority;
        task.Deadline = vm.Deadline?.Date;

        // چک‌لیست جدید ساخته می‌شود، ولی مواردی که متنشان عوض نشده تیکشان حفظ می‌شود
        var old = task.Checklist.ToLookup(c => c.Text);
        task.Checklist = TaskItem.SplitLines(vm.Dos)
            .Select(s => old[s].FirstOrDefault() is { } prev
                ? new CheckItem { Text = s, Done = prev.Done, DoneAt = prev.DoneAt }
                : new CheckItem { Text = s })
            .ToList();

        AddEvent(task, ActivityKind.Edited, leaderId);
        _repo.UpdateTask(task);
        Notify(task.AssignedToId, leaderId, task, NotificationKind.Edited, $"{NameOf(leaderId)} تسک «{task.Title}» رو ویرایش کرد");
        return task;
    }

    public void DeleteTask(int leaderId, int taskId)
    {
        var task = GetLeaderTask(leaderId, taskId);
        if (task.IsLocked) throw new TaskLockedException();
        _repo.DeleteTask(task.Id);
    }

    public TaskItem GetLeaderTask(int leaderId, int taskId)
    {
        var t = _repo.GetTask(taskId);
        if (t is null || t.CreatedById != leaderId) throw new KeyNotFoundException("تسک پیدا نشد.");
        return t;
    }

    public TaskItem LeaderMove(int leaderId, int taskId, WorkStatus to)
    {
        var t = GetLeaderTask(leaderId, taskId);
        if (t.IsLocked) throw new TaskLockedException();
        if (t.Status == to) return t;

        if (t.Status == WorkStatus.Review)
            throw new InvalidOperationException("این تسک منتظر تأیید توست؛ یا تأییدش کن یا با دلیل برگردون.");
        if (to is WorkStatus.Review or WorkStatus.Done)
            throw new InvalidOperationException("اول باید خود عضو تیم تیک بزنه؛ بعد تو تأیید می‌کنی.");

        ApplyMove(t, to, leaderId);
        _repo.UpdateTask(t);
        return t;
    }

    public TaskItem Approve(int leaderId, int taskId, string? note)
    {
        var t = GetLeaderTask(leaderId, taskId);
        if (t.IsLocked) throw new TaskLockedException();
        if (t.Status != WorkStatus.Review)
            throw new InvalidOperationException("فقط تسکی که عضو تیم تیکش رو زده قابل تأییده.");

        t.Status = WorkStatus.Done;
        t.CompletedAt = Now;
        t.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        t.IsLocked = true;
        AddEvent(t, ActivityKind.Approved, leaderId, t.ReviewNote);
        _repo.UpdateTask(t);
        Notify(t.AssignedToId, leaderId, t, NotificationKind.Approved,
            $"{NameOf(leaderId)} «{t.Title}» رو تأیید کرد 🎉" + (t.ReviewNote is null ? "" : $" — {t.ReviewNote}"));
        return t;
    }

    public TaskItem Reject(int leaderId, int taskId, string? reason)
    {
        var t = GetLeaderTask(leaderId, taskId);
        if (t.IsLocked) throw new TaskLockedException();
        if (t.Status != WorkStatus.Review)
            throw new InvalidOperationException("فقط تسکی که منتظر تأییده رو می‌شه برگردوند.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("دلیل برگشت رو بنویس تا عضو تیم بدونه چی رو باید درست کنه.");

        t.Status = WorkStatus.InProgress;
        t.SubmittedAt = null;
        t.ReviewNote = reason.Trim();
        AddEvent(t, ActivityKind.Rejected, leaderId, t.ReviewNote);
        _repo.UpdateTask(t);
        Notify(t.AssignedToId, leaderId, t, NotificationKind.Rejected, $"{NameOf(leaderId)} «{t.Title}» رو برگردوند: {t.ReviewNote}");
        return t;
    }

    public TaskItem Reassign(int leaderId, int taskId, int newMemberId)
    {
        var t = GetLeaderTask(leaderId, taskId);
        if (t.IsLocked) throw new TaskLockedException();
        var members = _repo.GetMembersOf(leaderId).ToDictionary(m => m.Id);
        if (!members.TryGetValue(newMemberId, out var to))
            throw new InvalidOperationException("این نفر عضو تیم تو نیست.");
        if (t.AssignedToId == newMemberId) return t;
        if (!t.IsOpen)
            throw new InvalidOperationException("تسکی که تیک خورده رو نمی‌شه به نفر دیگه سپرد.");
        if (t.WorkLogs.Count > 0 || t.IsTimerRunning)
            throw new InvalidOperationException("روی این تسک ساعت ثبت شده؛ دیگه نمی‌شه به نفر دیگه سپرد.");

        var fromId = t.AssignedToId;
        var fromName = members.GetValueOrDefault(fromId)?.FullName ?? "؟";
        t.AssignedToId = newMemberId;
        AddEvent(t, ActivityKind.Reassigned, leaderId, $"تسک رو از {fromName} به {to.FullName} سپرد");
        _repo.UpdateTask(t);
        Notify(newMemberId, leaderId, t, NotificationKind.Assigned, $"{NameOf(leaderId)} تسک «{t.Title}» رو بهت سپرد");
        Notify(fromId, leaderId, t, NotificationKind.Unassigned, $"{NameOf(leaderId)} تسک «{t.Title}» رو به {to.FullName} سپرد");
        return t;
    }

    public LeaderDashboardVm Dashboard(int leaderId, int? memberId, WorkStatus? status)
    {
        var members = _repo.GetMembersOf(leaderId);
        var leader = _repo.GetUser(leaderId);
        var names = members.ToDictionary(m => m.Id, m => m.FullName);
        if (leader is not null) names[leader.Id] = leader.FullName;
        var all = _repo.GetTasksCreatedBy(leaderId);

        var today = Today;
        var weekStart = WeekStart(today);
        // نقشه‌ی فعالیت: ۶ هفته‌ی کامل (از شنبه) تا امروز
        var heatFrom = weekStart.AddDays(-35);
        var heatDays = (today - heatFrom).Days + 1;

        var summaries = members.Select(m =>
        {
            var mt = all.Where(t => t.AssignedToId == m.Id).ToList();
            var logs = mt.SelectMany(t => t.WorkLogs).ToList();
            var running = mt.FirstOrDefault(t => t.IsTimerRunning);
            var byDay = logs.GroupBy(l => l.Date.Date).ToDictionary(g => g.Key, g => Math.Round(g.Sum(l => l.Hours), 2));
            return new MemberSummaryVm
            {
                Member = m,
                Total = mt.Count,
                Done = mt.Count(t => t.Status == WorkStatus.Done),
                Review = mt.Count(t => t.Status == WorkStatus.Review),
                InProgress = mt.Count(t => t.Status == WorkStatus.InProgress),
                NotStarted = mt.Count(t => t.Status == WorkStatus.NotStarted),
                Overdue = mt.Count(t => t.IsOverdue),
                TotalHours = Math.Round(logs.Sum(l => l.Hours), 2),
                WorkedDays = byDay.Count,
                LastActivity = mt.SelectMany(t => t.Events).Where(e => e.ByUserId == m.Id)
                                 .Select(e => (DateTime?)e.At).DefaultIfEmpty(null).Max(),
                TodayHours = byDay.GetValueOrDefault(today),
                WeekHours = Math.Round(byDay.Where(kv => kv.Key >= weekStart).Sum(kv => kv.Value), 2),
                LastWeekHours = Math.Round(byDay.Where(kv => kv.Key >= weekStart.AddDays(-7) && kv.Key < weekStart).Sum(kv => kv.Value), 2),
                RunningTask = running,
                RunningSeconds = running is null ? 0 : Math.Max(0, (long)(Now - running.TimerStartedAt!.Value).TotalSeconds),
                Heat = Enumerable.Range(0, heatDays).Select(i => byDay.GetValueOrDefault(heatFrom.AddDays(i))).ToList()
            };
        }).ToList();

        var filtered = all.AsEnumerable();
        if (memberId is not null) filtered = filtered.Where(t => t.AssignedToId == memberId);
        if (status is not null) filtered = filtered.Where(t => t.Status == status);

        var activity = all
            .SelectMany(t => t.Events.Select(e => new ActivityVm
            {
                When = e.At,
                Who = names.GetValueOrDefault(e.ByUserId, "؟"),
                WhoId = e.ByUserId,
                TaskId = t.Id,
                TaskTitle = t.Title,
                Text = Fa.Activity(e),
                Kind = e.Kind
            }))
            .Where(a => a.Kind != ActivityKind.Checked)
            .OrderByDescending(a => a.When)
            .Take(14)
            .ToList();

        return new LeaderDashboardVm
        {
            Members = summaries,
            Tasks = SortForList(filtered).ToList(),
            ReviewQueue = all.Where(t => t.Status == WorkStatus.Review).OrderBy(t => t.SubmittedAt).ToList(),
            MemberNames = names,
            FilterMemberId = memberId,
            FilterStatus = status,
            RecentActivity = activity,
            TotalTasks = all.Count,
            DoneTasks = all.Count(t => t.Status == WorkStatus.Done),
            TotalHours = Math.Round(all.Sum(t => t.TotalHours), 2),
            WeekHours = Math.Round(summaries.Sum(s => s.WeekHours), 2),
            OverdueTasks = all.Count(t => t.IsOverdue),
            HeatFrom = heatFrom,
            Today = today,
            Now = Now
        };
    }

    private static IEnumerable<TaskItem> SortForList(IEnumerable<TaskItem> tasks) =>
        tasks.OrderBy(t => t.Status == WorkStatus.Done)
             .ThenByDescending(t => t.Status == WorkStatus.Review)
             .ThenByDescending(t => t.Priority)
             .ThenBy(t => t.Deadline ?? DateTime.MaxValue);

    public static DateTime WeekStart(DateTime day) =>
        day.Date.AddDays(-(((int)day.DayOfWeek + 1) % 7));

    public List<TaskItem> MyTasks(int userId) =>
        _repo.GetTasksAssignedTo(userId)
            .OrderBy(t => t.IsLocked)
            .ThenByDescending(t => t.IsTimerRunning)
            .ThenByDescending(t => t.Priority)
            .ThenBy(t => t.Deadline ?? DateTime.MaxValue)
            .ToList();

    public TaskItem GetMemberTask(int userId, int taskId)
    {
        var t = _repo.GetTask(taskId);
        if (t is null || t.AssignedToId != userId) throw new KeyNotFoundException("تسک پیدا نشد.");
        return t;
    }

    public TaskItem StartTask(int userId, int taskId)
    {
        var t = GetMemberTask(userId, taskId);
        EnsureOpen(t);
        if (t.Status == WorkStatus.NotStarted)
        {
            ApplyMove(t, WorkStatus.InProgress, userId);
            _repo.UpdateTask(t);
        }
        return t;
    }

    public TaskItem MoveTask(int userId, int taskId, WorkStatus to)
    {
        var t = GetMemberTask(userId, taskId);
        EnsureOpen(t);
        if (t.Status == to) return t;
        if (to is WorkStatus.Review or WorkStatus.Done)
            throw new InvalidOperationException("برای تموم کردن، تیک بزن و ساعت کار رو ثبت کن.");

        ApplyMove(t, to, userId);
        _repo.UpdateTask(t);
        return t;
    }

    public TaskItem LogWork(int userId, int taskId, WorkLogFormVm vm)
    {
        var t = GetMemberTask(userId, taskId);
        EnsureOpen(t);
        ValidateLog(t, vm.Date, vm.Hours);

        AddLog(t, vm.Date, vm.Hours, vm.Note, userId);
        _repo.UpdateTask(t);
        return t;
    }

    public TaskItem ToggleCheck(int userId, int taskId, int index, bool done)
    {
        var t = GetMemberTask(userId, taskId);
        EnsureOpen(t);
        if (index < 0 || index >= t.Checklist.Count) throw new InvalidOperationException("این مورد پیدا نشد.");

        var item = t.Checklist[index];
        if (item.Done == done) return t;
        item.Done = done;
        item.DoneAt = done ? Now : null;
        if (done) AddEvent(t, ActivityKind.Checked, userId, $"«{item.Text}» رو تیک زد");
        if (t.Status == WorkStatus.NotStarted && done) ApplyMove(t, WorkStatus.InProgress, userId);
        _repo.UpdateTask(t);
        return t;
    }

    public TaskItem TimerStart(int userId, int taskId)
    {
        var t = GetMemberTask(userId, taskId);
        EnsureOpen(t);
        if (t.IsTimerRunning) return t;

        var other = _repo.GetTasksAssignedTo(userId).FirstOrDefault(x => x.IsTimerRunning && x.Id != t.Id);
        if (other is not null)
            throw new InvalidOperationException($"تایمر تسک «{other.Title}» روشنه؛ اول اون رو متوقف کن.");

        if (t.Status == WorkStatus.NotStarted) ApplyMove(t, WorkStatus.InProgress, userId);
        t.TimerStartedAt = Now;
        AddEvent(t, ActivityKind.TimerStarted, userId);
        _repo.UpdateTask(t);
        return t;
    }

    public (TaskItem Task, double Hours) TimerStop(int userId, int taskId)
    {
        var t = GetMemberTask(userId, taskId);
        EnsureOpen(t);
        if (!t.IsTimerRunning) return (t, 0);

        var hours = StopTimer(t, userId);
        _repo.UpdateTask(t);
        return (t, hours);
    }

    public TaskItem Submit(int userId, int taskId, CompleteTaskVm vm)
    {
        var t = GetMemberTask(userId, taskId);
        EnsureOpen(t);
        if (!vm.Confirm) throw new InvalidOperationException("برای ثبت نهایی باید تیک تأیید را بزنید.");

        if (t.IsTimerRunning) StopTimer(t, userId);

        if (vm.TodayHours is > 0)
        {
            ValidateLog(t, Today, vm.TodayHours.Value);
            AddLog(t, Today, vm.TodayHours.Value, "ساعت روز پایانی", userId);
        }

        if (t.WorkLogs.Count == 0)
            throw new InvalidOperationException("قبل از زدن تیک، حداقل یک بار ساعت کار ثبت کنید (یا ساعت امروز را وارد کنید).");

        t.StartedAt ??= t.WorkLogs.Min(l => l.Date);
        t.Status = WorkStatus.Review;
        t.SubmittedAt = Now;
        t.CompletionNote = string.IsNullOrWhiteSpace(vm.Note) ? null : vm.Note.Trim();
        t.ReviewNote = null;
        AddEvent(t, ActivityKind.Submitted, userId, t.CompletionNote);
        _repo.UpdateTask(t);
        Notify(t.CreatedById, userId, t, NotificationKind.Submitted, $"{NameOf(userId)} «{t.Title}» رو تیک زد و منتظر تأیید توئه");
        return t;
    }

    public TaskItem GetTaskFor(int userId, int taskId)
    {
        var t = _repo.GetTask(taskId);
        if (t is null || (t.AssignedToId != userId && t.CreatedById != userId)) throw new KeyNotFoundException("تسک پیدا نشد.");
        return t;
    }

    public TaskItem AddComment(int userId, int taskId, string? text)
    {
        var t = GetTaskFor(userId, taskId);
        if (t.IsLocked) throw new TaskLockedException();
        text = text?.Trim();
        if (string.IsNullOrEmpty(text)) throw new InvalidOperationException("متن نظر خالیه.");
        if (text.Length > 1000) throw new InvalidOperationException("نظر حداکثر ۱۰۰۰ کاراکتر.");

        t.Comments.Add(new TaskComment
        {
            Id = t.Comments.Count == 0 ? 1 : t.Comments.Max(c => c.Id) + 1,
            ByUserId = userId,
            At = Now,
            Text = text
        });
        AddEvent(t, ActivityKind.Commented, userId, text);
        _repo.UpdateTask(t);

        var other = userId == t.AssignedToId ? t.CreatedById : t.AssignedToId;
        Notify(other, userId, t, NotificationKind.Commented, $"{NameOf(userId)} روی «{t.Title}» نوشت: {Fa.Short(text, 60)}");
        return t;
    }

    public List<Notification> Notifications(int userId, int take = 15) => _repo.GetNotifications(userId, take);
    public int UnreadCount(int userId) => _repo.CountUnread(userId);
    public void MarkRead(int userId, int? id = null) => _repo.MarkRead(userId, id);

    private void Notify(int toUserId, int actorId, TaskItem t, NotificationKind kind, string text)
    {
        if (toUserId == actorId) return;
        _repo.AddNotification(new Notification
        {
            UserId = toUserId, ActorId = actorId, TaskId = t.Id, Kind = kind, Text = text, At = Now
        });
    }

    private string NameOf(int userId) => _repo.GetUser(userId)?.FullName ?? "؟";

    private static void EnsureOpen(TaskItem t)
    {
        if (t.IsLocked) throw new TaskLockedException();
        if (t.Status == WorkStatus.Review)
            throw new InvalidOperationException("این تسک منتظر تأیید تیم‌لیده و فعلاً قابل تغییر نیست.");
    }

    private void ApplyMove(TaskItem t, WorkStatus to, int byUserId)
    {
        switch (to)
        {
            case WorkStatus.InProgress:
                t.Status = WorkStatus.InProgress;
                t.StartedAt ??= Now;
                AddEvent(t, ActivityKind.Started, byUserId);
                break;
            case WorkStatus.NotStarted:
                if (t.WorkLogs.Count > 0 || t.IsTimerRunning)
                    throw new InvalidOperationException("برای این تسک ساعت ثبت شده و نمی‌تواند به «شروع نشده» برگردد.");
                t.Status = WorkStatus.NotStarted;
                t.StartedAt = null;
                AddEvent(t, ActivityKind.MovedBack, byUserId);
                break;
            default:
                throw new InvalidOperationException("این جابه‌جایی مجاز نیست.");
        }
    }

    private double StopTimer(TaskItem t, int userId)
    {
        var started = t.TimerStartedAt!.Value;
        t.TimerStartedAt = null;
        var hours = Math.Round((Now - started).TotalHours, 2);
        if (hours < 1.0 / 60) return 0;

        // اگر تایمر از دیروز مانده، ساعت روی امروز ثبت می‌شود ولی از سقف روز بیشتر نمی‌شود
        var sameDay = t.WorkLogs.Where(l => l.Date.Date == Today).Sum(l => l.Hours);
        hours = Math.Min(hours, MaxHoursPerDay - sameDay);
        if (hours <= 0) return 0;

        AddLog(t, Today, hours, "ثبت با تایمر", userId);
        return hours;
    }

    private void ValidateLog(TaskItem t, DateTime date, double hours)
    {
        if (hours <= 0 || hours > MaxHoursPerDay)
            throw new InvalidOperationException("ساعت باید بین ۰ تا ۲۴ باشد.");
        if (date.Date > Today)
            throw new InvalidOperationException("نمی‌توانید برای روزهای آینده ساعت ثبت کنید.");
        if (date.Date < t.CreatedAt.Date)
            throw new InvalidOperationException("تاریخ نمی‌تواند قبل از ایجاد تسک باشد.");
        var sameDay = t.WorkLogs.Where(l => l.Date.Date == date.Date).Sum(l => l.Hours);
        if (sameDay + hours > MaxHoursPerDay)
            throw new InvalidOperationException("جمع ساعت‌های یک روز نمی‌تواند بیشتر از ۲۴ ساعت شود.");
    }

    private void AddLog(TaskItem t, DateTime date, double hours, string? note, int userId)
    {
        hours = Math.Round(hours, 2);
        t.WorkLogs.Add(new WorkLog
        {
            Id = t.WorkLogs.Count == 0 ? 1 : t.WorkLogs.Max(l => l.Id) + 1,
            Date = date.Date,
            Hours = hours,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            LoggedAt = Now
        });

        var text = $"{Fa.Hours(hours)} کار ثبت کرد" + (string.IsNullOrWhiteSpace(note) ? "" : $": {note.Trim()}");
        AddEvent(t, ActivityKind.Logged, userId, text);

        if (t.Status == WorkStatus.NotStarted)
        {
            t.Status = WorkStatus.InProgress;
            t.StartedAt = date.Date;
        }
        else if (t.StartedAt is null || date.Date < t.StartedAt.Value.Date)
        {
            t.StartedAt = date.Date;
        }
    }

    private void AddEvent(TaskItem t, ActivityKind kind, int userId, string? text = null) =>
        t.Events.Add(new TaskEvent { Kind = kind, ByUserId = userId, At = Now, Text = text });
}

public class ManualClock : TimeProvider
{
    private DateTime _now;
    public ManualClock(DateTime start) => _now = start;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(_now, DateTimeKind.Utc));

    public void Set(DateTime value) => _now = value;
    public void Advance(TimeSpan by) => _now += by;
}
