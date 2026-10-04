using System.ComponentModel.DataAnnotations;
using TeamTracker.Models;

namespace TeamTracker.ViewModels;

public class LoginVm
{
    [Required(ErrorMessage = "نام کاربری را وارد کنید")]
    [Display(Name = "نام کاربری")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "رمز عبور را وارد کنید")]
    [DataType(DataType.Password)]
    [Display(Name = "رمز عبور")]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public class TaskFormVm
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "عنوان تسک الزامی است")]
    [StringLength(150, ErrorMessage = "عنوان حداکثر ۱۵۰ کاراکتر")]
    [Display(Name = "عنوان تسک")]
    public string Title { get; set; } = "";

    [Display(Name = "توضیحات")]
    public string? Description { get; set; }

    [Display(Name = "چه کارهایی باید انجام شود؟ (هر خط یک مورد — عضو تیم تک‌تک تیکشان می‌زند)")]
    public string? Dos { get; set; }

    [Display(Name = "چه کارهایی نباید انجام شود؟ (هر خط یک مورد)")]
    public string? Donts { get; set; }

    [Display(Name = "اولویت")]
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;

    [DataType(DataType.Date)]
    [Display(Name = "مهلت انجام")]
    public DateTime? Deadline { get; set; }

    [Display(Name = "واگذار به")]
    public List<int> AssigneeIds { get; set; } = new();

    // فقط برای نمایش فرم
    public List<AppUser> AvailableMembers { get; set; } = new();
    public bool IsEdit => Id is not null;
}

public class WorkLogFormVm
{
    [DataType(DataType.Date)]
    [Display(Name = "تاریخ")]
    public DateTime Date { get; set; } = DateTime.Today;

    [Range(0.25, 24, ErrorMessage = "ساعت باید بین ۰٫۲۵ تا ۲۴ باشد")]
    [Display(Name = "ساعت کار")]
    public double Hours { get; set; }

    [StringLength(300)]
    [Display(Name = "چه کاری انجام دادی؟")]
    public string? Note { get; set; }
}

public class CompleteTaskVm
{
    [Display(Name = "تأیید می‌کنم کار انجام شد")]
    public bool Confirm { get; set; }

    [Range(0, 24)]
    [Display(Name = "ساعت کار امروز (اختیاری)")]
    public double? TodayHours { get; set; }

    [StringLength(1000)]
    [Display(Name = "گزارش پایانی")]
    public string? Note { get; set; }
}

public class MemberSummaryVm
{
    public AppUser Member { get; set; } = null!;
    public int Total { get; set; }
    public int Done { get; set; }
    public int Review { get; set; }
    public int InProgress { get; set; }
    public int NotStarted { get; set; }
    public int Overdue { get; set; }
    public double TotalHours { get; set; }
    public int WorkedDays { get; set; }
    public DateTime? LastActivity { get; set; }
    public int Percent => Total == 0 ? 0 : (int)Math.Round(Done * 100.0 / Total);

    // ویجت‌ها
    public double TodayHours { get; set; }
    public double WeekHours { get; set; }
    public double LastWeekHours { get; set; }
    public TaskItem? RunningTask { get; set; }
    public long RunningSeconds { get; set; }
    public List<double> Heat { get; set; } = new();
}

public class ActivityVm
{
    public DateTime When { get; set; }
    public string Who { get; set; } = "";
    public int WhoId { get; set; }
    public int TaskId { get; set; }
    public string TaskTitle { get; set; } = "";
    public string Text { get; set; } = "";
    public ActivityKind Kind { get; set; }
}

public class LeaderDashboardVm
{
    public List<MemberSummaryVm> Members { get; set; } = new();
    public List<TaskItem> Tasks { get; set; } = new();
    public List<TaskItem> ReviewQueue { get; set; } = new();
    public Dictionary<int, string> MemberNames { get; set; } = new();
    public List<ActivityVm> RecentActivity { get; set; } = new();
    public int? FilterMemberId { get; set; }
    public WorkStatus? FilterStatus { get; set; }

    public int TotalTasks { get; set; }
    public int DoneTasks { get; set; }
    public double TotalHours { get; set; }
    public double WeekHours { get; set; }
    public int OverdueTasks { get; set; }

    public DateTime HeatFrom { get; set; }
    public DateTime Today { get; set; }
    public DateTime Now { get; set; }
}

public class LeaderTaskDetailsVm
{
    public TaskItem Task { get; set; } = null!;
    public AppUser Assignee { get; set; } = null!;
    public Dictionary<int, string> Names { get; set; } = new();
}

public class MemberTaskVm
{
    public TaskItem Task { get; set; } = null!;
    public Dictionary<int, string> Names { get; set; } = new();
}

public enum BoardMode { Leader, Member }

public class BoardVm
{
    public BoardMode Mode { get; set; }
    public List<TaskItem> Tasks { get; set; } = new();
    public Dictionary<int, string> Names { get; set; } = new();
    public List<AppUser> Members { get; set; } = new();
    public int? FilterMemberId { get; set; }
    public DateTime Now { get; set; } = DateTime.Now;

    public record Column(WorkStatus Status, string Title, string Hint, string Empty);

    public IEnumerable<Column> Columns => Mode == BoardMode.Leader
        ? new[]
        {
            new Column(WorkStatus.NotStarted, "شروع نشده", "", "تسکی منتظر شروع نیست"),
            new Column(WorkStatus.InProgress, "در حال انجام", "", "کسی الان روی کاری نیست"),
            new Column(WorkStatus.Review, "منتظر تأیید تو", "", "چیزی برای تأیید نیست"),
            new Column(WorkStatus.Done, "تأیید شده", "قفل", "هنوز تسکی تأیید نشده"),
        }
        : new[]
        {
            new Column(WorkStatus.NotStarted, "شروع نشده", "", "تسک جدیدی نداری"),
            new Column(WorkStatus.InProgress, "در حال انجام", "", "کارت رو بکش اینجا تا شروع کنی"),
            new Column(WorkStatus.Review, "منتظر تأیید", "تیک خورده", "کارت رو بکش اینجا یا دایره‌ی تیک رو بزن"),
            new Column(WorkStatus.Done, "تأیید شده", "قفل", "تسک‌های تأییدشده اینجا میان"),
        };
}

public class KanbanCardVm
{
    public TaskItem Task { get; set; } = null!;
    public BoardMode Mode { get; set; }
    public string? OwnerName { get; set; }
    public DateTime Now { get; set; } = DateTime.Now;

    public bool IsLeader => Mode == BoardMode.Leader;

    public bool CanDrag => IsLeader ? !Task.IsLocked : Task.IsOpen;
}

public class MemberFormVm
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "نام رو وارد کن")]
    [StringLength(40)]
    [Display(Name = "نام")]
    public string? FullName { get; set; }

    [Display(Name = "نام کاربری")]
    public string? Username { get; set; }

    [StringLength(40)]
    [Display(Name = "سمت")]
    public string? JobTitle { get; set; }

    [Display(Name = "رمز عبور")]
    public string? Password { get; set; }
}

public class TeamMemberRowVm
{
    public AppUser User { get; set; } = null!;
    public int OpenTasks { get; set; }
    public int TotalTasks { get; set; }
}

public class TeamVm
{
    public List<TeamMemberRowVm> Members { get; set; } = new();
    public MemberFormVm NewMember { get; set; } = new();
}

public class ProfileVm
{
    public AppUser User { get; set; } = null!;
    public string? LeaderName { get; set; }
}

public class NotificationsVm
{
    public List<Notification> Items { get; set; } = new();
    public int Unread { get; set; }
    public DateTime Now { get; set; }
}
