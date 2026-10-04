using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TeamTracker.Models;

namespace TeamTracker.Services;

public interface ITaskRepository
{
    AppUser? GetUser(int id);
    AppUser? GetUserByUsername(string username);
    List<AppUser> GetMembersOf(int leaderId);
    void AddUser(AppUser user);
    void UpdateUser(AppUser user);
    void DeleteUser(int id);

    TaskItem? GetTask(int id);
    List<TaskItem> GetTasksCreatedBy(int leaderId);
    List<TaskItem> GetTasksAssignedTo(int userId);
    void AddTask(TaskItem task);
    void UpdateTask(TaskItem task);
    void DeleteTask(int id);

    void AddNotification(Notification n);
    List<Notification> GetNotifications(int userId, int take);
    int CountUnread(int userId);
    void MarkRead(int userId, int? notificationId = null);
}

public class TaskLockedException : Exception
{
    public TaskLockedException() : base("این تسک تأیید و قفل شده و دیگر قابل تغییر نیست.") { }
}

public static class PasswordHasher
{
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 2) return false;
        var salt = Convert.FromBase64String(parts[0]);
        var expected = Convert.FromBase64String(parts[1]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

public static class Fa
{
    private static readonly PersianCalendar Pc = new();
    private const string Digits = "۰۱۲۳۴۵۶۷۸۹";

    public static readonly string[] MonthNames =
        { "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };

    public static string N(object? value)
    {
        var s = value switch
        {
            null => "",
            double d => Math.Round(d, 2).ToString(CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? ""
        };
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            sb.Append(ch switch
            {
                >= '0' and <= '9' => Digits[ch - '0'],
                '.' => '٫',
                _ => ch
            });
        return sb.ToString();
    }

    public static string Date(DateTime? d) =>
        d is null ? "—" : N($"{Pc.GetYear(d.Value):0000}/{Pc.GetMonth(d.Value):00}/{Pc.GetDayOfMonth(d.Value):00}");

    public static string DayMonth(DateTime d) => $"{N(Pc.GetDayOfMonth(d))} {MonthNames[Pc.GetMonth(d) - 1]}";

    public static string Stamp(DateTime? d) =>
        d is null ? "—" : $"{Date(d)} - {N(d.Value.ToString("HH:mm"))}";

    public static string Hours(double h)
    {
        var hh = (int)Math.Floor(h);
        var mm = (int)Math.Round((h - hh) * 60);
        if (mm == 60) { hh++; mm = 0; }
        if (hh == 0 && mm == 0) return "۰ ساعت";
        if (hh == 0) return $"{N(mm)} دقیقه";
        return mm == 0 ? $"{N(hh)} ساعت" : $"{N(hh)} ساعت و {N(mm)} دقیقه";
    }

    public static string HoursShort(double h) => $"{N(Math.Round(h, 1))} ساعت";

    public static string Ago(DateTime? d, DateTime now)
    {
        if (d is null) return "—";
        var diff = now - d.Value;
        if (diff.TotalMinutes < 1) return "همین الان";
        if (diff.TotalMinutes < 60) return $"{N((int)diff.TotalMinutes)} دقیقه پیش";
        if (d.Value.Date == now.Date) return $"{N((int)diff.TotalHours)} ساعت پیش";
        if (d.Value.Date == now.Date.AddDays(-1)) return "دیروز";
        if (diff.TotalDays < 7) return $"{N((int)Math.Ceiling(diff.TotalDays))} روز پیش";
        return Date(d);
    }

    public static string Status(WorkStatus s) => s switch
    {
        WorkStatus.NotStarted => "شروع نشده",
        WorkStatus.InProgress => "در حال انجام",
        WorkStatus.Review => "منتظر تأیید",
        WorkStatus.Done => "تأیید شده",
        _ => s.ToString()
    };

    public static string StatusClass(WorkStatus s) => s switch
    {
        WorkStatus.InProgress => "doing",
        WorkStatus.Review => "review",
        WorkStatus.Done => "done",
        _ => "todo"
    };

    public static string Priority(TaskPriority p) => p switch
    {
        TaskPriority.Low => "کم",
        TaskPriority.Normal => "معمولی",
        TaskPriority.High => "فوری",
        _ => p.ToString()
    };

    public static string PriorityClass(TaskPriority p) => p switch
    {
        TaskPriority.High => "high",
        TaskPriority.Low => "low",
        _ => "normal"
    };

    public static (string Text, string Cls)? Due(TaskItem t, DateTime today)
    {
        if (t.Deadline is null) return null;
        if (!t.IsOpen) return (Date(t.Deadline), "ok");
        var days = (t.Deadline.Value.Date - today.Date).Days;
        return days switch
        {
            < 0 => ($"{N(-days)} روز عقب", "late"),
            0 => ("مهلت: امروز", "today"),
            1 => ("مهلت: فردا", "soon"),
            <= 3 => ($"{N(days)} روز مونده", "soon"),
            _ => ($"{N(days)} روز مونده", "ok")
        };
    }

    public static string Activity(TaskEvent e) => e.Kind switch
    {
        ActivityKind.Created => "تسک رو تعریف کرد",
        ActivityKind.Edited => "تسک رو ویرایش کرد",
        ActivityKind.Started => "کار رو شروع کرد",
        ActivityKind.MovedBack => "تسک رو به «شروع نشده» برگردوند",
        ActivityKind.Logged => e.Text ?? "ساعت کار ثبت کرد",
        ActivityKind.TimerStarted => "تایمر رو روشن کرد",
        ActivityKind.Checked => e.Text ?? "یک مورد از چک‌لیست رو تیک زد",
        ActivityKind.Submitted => "تیک زد و برای تأیید فرستاد",
        ActivityKind.Approved => "تأیید کرد و تسک قفل شد",
        ActivityKind.Rejected => $"برگردوند: {e.Text}",
        ActivityKind.Reassigned => e.Text ?? "تسک رو به نفر دیگه سپرد",
        ActivityKind.Commented => $"نظر داد: {Short(e.Text, 70)}",
        _ => e.Kind.ToString()
    };

    public static string Short(string? s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s ?? "" : s[..max].TrimEnd() + "…";

    public static string ActivityClass(ActivityKind k) => k switch
    {
        ActivityKind.Approved => "done",
        ActivityKind.Submitted => "review",
        ActivityKind.Rejected => "rejected",
        ActivityKind.Checked or ActivityKind.TimerStarted => "minor",
        ActivityKind.Commented => "comment",
        _ => ""
    };
}
