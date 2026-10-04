using System.Globalization;
using TeamTracker.Models;

namespace TeamTracker.Services;

public class ReportService
{
    private readonly ITaskRepository _repo;
    public ReportService(ITaskRepository repo) => _repo = repo;

    private static readonly string[] WeekDays = { "یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه", "شنبه" };

    public (byte[] Content, string FileName) HoursReport(int leaderId, int? memberId, DateTime from, DateTime to)
    {
        if (to < from) (from, to) = (to, from);
        from = from.Date;
        to = to.Date;

        var members = _repo.GetMembersOf(leaderId)
            .Where(m => memberId is null || m.Id == memberId)
            .ToList();
        var names = members.ToDictionary(m => m.Id, m => m.FullName);
        var tasks = _repo.GetTasksCreatedBy(leaderId).Where(t => names.ContainsKey(t.AssignedToId)).ToList();

        var logs = tasks
            .SelectMany(t => t.WorkLogs.Select(l => (Task: t, Log: l)))
            .Where(x => x.Log.Date.Date >= from && x.Log.Date.Date <= to)
            .OrderBy(x => x.Log.Date).ThenBy(x => names[x.Task.AssignedToId])
            .ToList();

        var xlsx = new XlsxWriter();

        var detail = xlsx.AddSheet("ریز ساعت‌ها",
            new[] { "تاریخ", "روز", "نفر", "تسک", "ساعت", "شرح کار", "وضعیت تسک" },
            new[] { 13.0, 11, 14, 34, 9, 40, 14 });
        foreach (var (t, l) in logs)
            detail.Rows.Add(new object?[]
            {
                Fa.Date(l.Date), WeekDays[(int)l.Date.DayOfWeek], names[t.AssignedToId],
                t.Title, l.Hours, l.Note, Fa.Status(t.Status)
            });

        var days = Math.Max(1, (to - from).Days + 1);
        var summary = xlsx.AddSheet("خلاصه",
            new[] { "نفر", "سمت", "روزهای کاری", "مجموع ساعت", "میانگین در روز کاری", "تسک‌های تأییدشده", "تسک‌های باز" },
            new[] { 14.0, 14, 13, 13, 18, 16, 12 });
        foreach (var m in members)
        {
            var mine = logs.Where(x => x.Task.AssignedToId == m.Id).Select(x => x.Log).ToList();
            var workedDays = mine.Select(l => l.Date.Date).Distinct().Count();
            var hours = Math.Round(mine.Sum(l => l.Hours), 2);
            var mt = tasks.Where(t => t.AssignedToId == m.Id).ToList();
            summary.Rows.Add(new object?[]
            {
                m.FullName, m.JobTitle, workedDays, hours,
                workedDays == 0 ? 0 : Math.Round(hours / workedDays, 2),
                mt.Count(t => t.CompletedAt is { } c && c.Date >= from && c.Date <= to),
                mt.Count(t => !t.IsLocked)
            });
        }
        summary.Rows.Add(new object?[] { "جمع کل", null, null, Math.Round(logs.Sum(x => x.Log.Hours), 2), null, null, null });

        var info = xlsx.AddSheet("مشخصات گزارش", new[] { "عنوان", "مقدار" }, new[] { 18.0, 30 });
        info.Rows.Add(new object?[] { "از تاریخ", Fa.Date(from) });
        info.Rows.Add(new object?[] { "تا تاریخ", Fa.Date(to) });
        info.Rows.Add(new object?[] { "تعداد روز", days });
        info.Rows.Add(new object?[] { "اعضا", memberId is null ? "همه" : string.Join("، ", names.Values) });
        info.Rows.Add(new object?[] { "زمان ساخت", Fa.Stamp(DateTime.Now) });

        var pc = new PersianCalendar();
        string Stamp(DateTime d) => $"{pc.GetYear(d)}-{pc.GetMonth(d):00}-{pc.GetDayOfMonth(d):00}";
        return (xlsx.ToBytes(), $"TeamTracker_{Stamp(from)}_{Stamp(to)}.xlsx");
    }
}
