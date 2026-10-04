using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamTracker.Models;
using TeamTracker.Services;
using TeamTracker.ViewModels;

namespace TeamTracker.Controllers;

[Authorize(Roles = Roles.Leader)]
public class LeaderController : BaseController
{
    private readonly TaskService _svc;
    private readonly ITaskRepository _repo;
    private readonly UserService _users;
    private readonly ReportService _reports;

    public LeaderController(TaskService svc, ITaskRepository repo, UserService users, ReportService reports)
    {
        _svc = svc;
        _repo = repo;
        _users = users;
        _reports = reports;
    }

    // داشبورد نظارت
    public IActionResult Index(int? memberId, WorkStatus? status)
    {
        NoCache();
        return View(_svc.Dashboard(CurrentUserId, memberId, status));
    }

    // برد کانبان کل تیم — تیم‌لید هم می‌تواند کارت‌ها را بکشد
    public IActionResult Board(int? memberId)
    {
        NoCache();
        var dash = _svc.Dashboard(CurrentUserId, memberId, null);
        return View(new BoardVm
        {
            Mode = BoardMode.Leader,
            Tasks = dash.Tasks,
            Names = dash.MemberNames,
            Members = dash.Members.Select(m => m.Member).ToList(),
            FilterMemberId = memberId,
            Now = dash.Now
        });
    }

    // گزارش یک نفر: همه‌ی تسک‌ها و ساعت‌هایش
    public IActionResult Member(int id)
    {
        NoCache();
        var member = _repo.GetUser(id);
        if (member is null || member.LeaderId != CurrentUserId) return NotFound();
        var dash = _svc.Dashboard(CurrentUserId, id, null);
        ViewBag.Summary = dash.Members.First(m => m.Member.Id == id);
        return View(dash);
    }

    public IActionResult Details(int id)
    {
        NoCache();
        try
        {
            var task = _svc.GetLeaderTask(CurrentUserId, id);
            var names = _svc.Members(CurrentUserId).ToDictionary(m => m.Id, m => m.FullName);
            names[CurrentUserId] = CurrentUserName;
            return View(new LeaderTaskDetailsVm { Task = task, Assignee = _repo.GetUser(task.AssignedToId)!, Names = names });
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet]
    public IActionResult Create(int? memberId)
    {
        var vm = new TaskFormVm { AvailableMembers = _svc.Members(CurrentUserId) };
        if (memberId is not null) vm.AssigneeIds.Add(memberId.Value);
        return View("Form", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Create(TaskFormVm vm)
    {
        if (vm.AssigneeIds.Count == 0)
            ModelState.AddModelError(nameof(vm.AssigneeIds), "حداقل یک نفر را انتخاب کنید.");
        if (!ModelState.IsValid)
        {
            vm.AvailableMembers = _svc.Members(CurrentUserId);
            return Task.FromResult<IActionResult>(View("Form", vm));
        }

        return Run(() =>
        {
            var created = _svc.CreateTasks(CurrentUserId, vm);
            var msg = created.Count > 1 ? $"تسک برای {Fa.N(created.Count)} نفر ثبت شد." : "تسک ثبت شد.";
            return new Outcome(created[0], "created", msg, $"{CurrentUserName} تسک «{vm.Title}» رو تعریف کرد");
        }, () => RedirectToAction(nameof(Index)));
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        try
        {
            var t = _svc.GetLeaderTask(CurrentUserId, id);
            if (t.IsLocked)
            {
                Error("این تسک تأیید و قفل شده و قابل ویرایش نیست.");
                return RedirectToAction(nameof(Details), new { id });
            }
            return View("Form", new TaskFormVm
            {
                Id = t.Id, Title = t.Title, Description = t.Description,
                Dos = string.Join('\n', t.Checklist.Select(c => c.Text)), Donts = t.Donts,
                Priority = t.Priority, Deadline = t.Deadline, AssigneeIds = { t.AssignedToId },
                AvailableMembers = _svc.Members(CurrentUserId)
            });
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(int id, TaskFormVm vm)
    {
        if (!ModelState.IsValid)
        {
            vm.Id = id;
            vm.AvailableMembers = _svc.Members(CurrentUserId);
            return Task.FromResult<IActionResult>(View("Form", vm));
        }
        return Run(() => new Outcome(_svc.EditTask(CurrentUserId, id, vm), "edited", "تغییرات ذخیره شد.",
                $"{CurrentUserName} تسک «{vm.Title}» رو ویرایش کرد"),
            () => RedirectToAction(nameof(Details), new { id }));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Delete(int id) =>
        Run(() =>
        {
            var t = _svc.GetLeaderTask(CurrentUserId, id);
            _svc.DeleteTask(CurrentUserId, id);
            return new Outcome(t, "deleted", "تسک حذف شد.", $"{CurrentUserName} تسک «{t.Title}» رو حذف کرد");
        }, () => RedirectToAction(nameof(Index)));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Move(int id, WorkStatus status) =>
        Run(() =>
        {
            var t = _svc.LeaderMove(CurrentUserId, id, status);
            var where = Fa.Status(status);
            return new Outcome(t, "moved", $"«{t.Title}» رفت به «{where}»",
                $"{CurrentUserName} «{t.Title}» رو برد به «{where}»");
        }, () => RedirectToAction(nameof(Board)));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Approve(int id, string? note, string? returnTo) =>
        Run(() =>
        {
            var t = _svc.Approve(CurrentUserId, id, note);
            return new Outcome(t, "approved", $"«{t.Title}» تأیید و قفل شد 🎉",
                $"{CurrentUserName} «{t.Title}» رو تأیید کرد 🎉");
        }, () => Back(id, returnTo));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Reject(int id, string? reason, string? returnTo) =>
        Run(() =>
        {
            var t = _svc.Reject(CurrentUserId, id, reason);
            return new Outcome(t, "rejected", $"«{t.Title}» برگشت خورد",
                $"{CurrentUserName} «{t.Title}» رو برگردوند: {t.ReviewNote}");
        }, () => Back(id, returnTo));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Reassign(int id, int memberId) =>
        Run(() =>
        {
            var t = _svc.Reassign(CurrentUserId, id, memberId);
            var name = _repo.GetUser(memberId)?.FullName;
            return new Outcome(t, "reassigned", $"«{t.Title}» سپرده شد به {name}",
                $"{CurrentUserName} «{t.Title}» رو سپرد به {name}");
        }, () => RedirectToAction(nameof(Board)));

    public IActionResult Team()
    {
        var tasks = _repo.GetTasksCreatedBy(CurrentUserId);
        return View(new TeamVm
        {
            Members = _users.Members(CurrentUserId).Select(m => new TeamMemberRowVm
            {
                User = m,
                TotalTasks = tasks.Count(t => t.AssignedToId == m.Id),
                OpenTasks = tasks.Count(t => t.AssignedToId == m.Id && !t.IsLocked)
            }).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult AddMember([Bind(Prefix = "NewMember")] MemberFormVm vm)
    {
        try
        {
            var m = _users.AddMember(CurrentUserId, vm);
            Success($"{m.FullName} به تیم اضافه شد. نام کاربری: {m.Username}");
        }
        catch (InvalidOperationException ex) { Error(ex.Message); }
        return RedirectToAction(nameof(Team));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult EditMember(int id, MemberFormVm vm)
    {
        try
        {
            var m = _users.UpdateMember(CurrentUserId, id, vm);
            Success(string.IsNullOrEmpty(vm.Password) ? $"اطلاعات {m.FullName} ذخیره شد." : $"اطلاعات و رمز {m.FullName} عوض شد.");
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { Error(ex.Message); }
        return RedirectToAction(nameof(Team));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult RemoveMember(int id)
    {
        try
        {
            var name = _users.GetMember(CurrentUserId, id).FullName;
            _users.RemoveMember(CurrentUserId, id);
            Success($"{name} از تیم حذف شد.");
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { Error(ex.Message); }
        return RedirectToAction(nameof(Team));
    }

    public IActionResult Export(int? memberId, DateTime? from, DateTime? to)
    {
        var end = to ?? DateTime.Today;
        var start = from ?? end.AddDays(-29);
        var (content, name) = _reports.HoursReport(CurrentUserId, memberId, start, end);
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    private IActionResult Back(int id, string? returnTo) => returnTo switch
    {
        "board" => RedirectToAction(nameof(Board)),
        "index" => RedirectToAction(nameof(Index)),
        _ => RedirectToAction(nameof(Details), new { id })
    };
}
