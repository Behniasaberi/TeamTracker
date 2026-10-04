using System.Text.Json.Serialization;

namespace TeamTracker.Models;

public static class Roles
{
    public const string Leader = "Leader";
    public const string Member = "Member";
}

// NotStarted → InProgress → Review (عضو تیک زده) → Done (تیم‌لید تأیید کرده، قفل)
// از Review با برگشت تیم‌لید دوباره InProgress می‌شود
public enum WorkStatus
{
    NotStarted = 0,
    InProgress = 1,
    Review = 2,
    Done = 3
}

public enum TaskPriority
{
    Low = 0,
    Normal = 1,
    High = 2
}

public enum ActivityKind
{
    Created,
    Edited,
    Started,
    MovedBack,
    Logged,
    TimerStarted,
    Checked,
    Submitted,
    Approved,
    Rejected,
    Reassigned,
    Commented
}

public enum NotificationKind
{
    Assigned,
    Edited,
    Submitted,
    Approved,
    Rejected,
    Commented,
    Unassigned
}

public class AppUser
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.Member;
    public int? LeaderId { get; set; }
    public string? JobTitle { get; set; }
}

public class CheckItem
{
    public string Text { get; set; } = "";
    public bool Done { get; set; }
    public DateTime? DoneAt { get; set; }
}

public class TaskComment
{
    public int Id { get; set; }
    public int ByUserId { get; set; }
    public DateTime At { get; set; }
    public string Text { get; set; } = "";
}

public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ActorId { get; set; }
    public int? TaskId { get; set; }
    public NotificationKind Kind { get; set; }
    public string Text { get; set; } = "";
    public DateTime At { get; set; }
    public bool IsRead { get; set; }
}

public class TaskEvent
{
    public ActivityKind Kind { get; set; }
    public int ByUserId { get; set; }
    public DateTime At { get; set; }
    public string? Text { get; set; }
}

public class TaskItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    public List<CheckItem> Checklist { get; set; } = new();

    public string Donts { get; set; } = "";

    public int AssignedToId { get; set; }
    public int CreatedById { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;

    public DateTime CreatedAt { get; set; }
    public DateTime? Deadline { get; set; }
    public DateTime? StartedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public WorkStatus Status { get; set; } = WorkStatus.NotStarted;

    public bool IsLocked { get; set; }

    public string? CompletionNote { get; set; }

    public string? ReviewNote { get; set; }

    public DateTime? TimerStartedAt { get; set; }

    public List<WorkLog> WorkLogs { get; set; } = new();
    public List<TaskEvent> Events { get; set; } = new();
    public List<TaskComment> Comments { get; set; } = new();

    [JsonIgnore] public bool IsOpen => Status is WorkStatus.NotStarted or WorkStatus.InProgress;

    [JsonIgnore] public bool IsTimerRunning => TimerStartedAt is not null;

    [JsonIgnore] public double TotalHours => Math.Round(WorkLogs.Sum(w => w.Hours), 2);

    [JsonIgnore] public int WorkedDays => WorkLogs.Select(w => w.Date.Date).Distinct().Count();

    [JsonIgnore] public int SpanDays
    {
        get
        {
            if (StartedAt is null) return 0;
            var end = (CompletedAt ?? SubmittedAt ?? DateTime.Now).Date;
            return Math.Max(1, (end - StartedAt.Value.Date).Days + 1);
        }
    }

    [JsonIgnore] public bool IsOverdue => Deadline is not null && IsOpen && DateTime.Now.Date > Deadline.Value.Date;

    [JsonIgnore] public int ChecklistDone => Checklist.Count(c => c.Done);
    [JsonIgnore] public int ChecklistPercent => Checklist.Count == 0 ? 0 : (int)Math.Round(ChecklistDone * 100.0 / Checklist.Count);

    [JsonIgnore] public bool WasRejected => Status == WorkStatus.InProgress && !string.IsNullOrWhiteSpace(ReviewNote);

    [JsonIgnore] public IEnumerable<string> DontList => SplitLines(Donts);

    public static IEnumerable<string> SplitLines(string? s) =>
        (s ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public class WorkLog
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public double Hours { get; set; }
    public string? Note { get; set; }
    public DateTime LoggedAt { get; set; }
}
