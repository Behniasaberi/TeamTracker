using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamTracker.Models;
using TeamTracker.Services;
using TeamTracker.ViewModels;

namespace TeamTracker.Controllers;

[Authorize(Roles = Roles.Member)]
public class MemberController : BaseController
{
    private readonly TaskService _svc;
    private readonly ITaskRepository _repo;

    public MemberController(TaskService svc, ITaskRepository repo)
    {
        _svc = svc;
        _repo = repo;
    }

    // برد کانبان تسک‌های من
    public IActionResult Index()
    {
        NoCache();
        return View(new BoardVm { Mode = BoardMode.Member, Tasks = _svc.MyTasks(CurrentUserId), Now = DateTime.Now });
    }

    public IActionResult Details(int id)
    {
        NoCache();
        try
        {
            var task = _svc.GetMemberTask(CurrentUserId, id);
            var names = new Dictionary<int, string> { [CurrentUserId] = CurrentUserName };
            if (_repo.GetUser(task.CreatedById) is { } leader) names[leader.Id] = leader.FullName;
            return View(new MemberTaskVm { Task = task, Names = names });
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Move(int id, WorkStatus status) =>
        Run(() =>
        {
            var t = _svc.MoveTask(CurrentUserId, id, status);
            return status == WorkStatus.InProgress
                ? new Outcome(t, "moved", "کار روی تسک شروع شد", $"{CurrentUserName} کار روی «{t.Title}» رو شروع کرد")
                : new Outcome(t, "moved", "تسک به «شروع نشده» برگشت", $"{CurrentUserName} «{t.Title}» رو به «شروع نشده» برگردوند");
        }, () => RedirectToAction(nameof(Index)));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Start(int id, string? returnTo) =>
        Run(() =>
        {
            var t = _svc.StartTask(CurrentUserId, id);
            return new Outcome(t, "moved", "کار روی تسک شروع شد. موفق باشی!", $"{CurrentUserName} کار روی «{t.Title}» رو شروع کرد");
        }, () => Back(id, returnTo));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> LogWork(int id, [Bind(Prefix = "NewLog")] WorkLogFormVm vm, string? returnTo)
    {
        if (!ModelState.IsValid)
        {
            var msg = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage;
            if (IsAjax) return Task.FromResult<IActionResult>(BadRequest(new { ok = false, message = msg }));
            Error(msg);
            return Task.FromResult(Back(id, returnTo));
        }
        return Run(() =>
        {
            var t = _svc.LogWork(CurrentUserId, id, vm);
            return new Outcome(t, "logged", "ساعت کار ثبت شد.", $"{CurrentUserName} {Fa.Hours(vm.Hours)} روی «{t.Title}» ثبت کرد");
        }, () => Back(id, returnTo));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Check(int id, int index, bool done) =>
        Run(() =>
        {
            var t = _svc.ToggleCheck(CurrentUserId, id, index, done);
            return new Outcome(t, "checked", done ? "تیک خورد ✓" : "تیک برداشته شد",
                $"{CurrentUserName} چک‌لیست «{t.Title}» رو به‌روز کرد ({Fa.N(t.ChecklistDone)}/{Fa.N(t.Checklist.Count)})");
        }, () => RedirectToAction(nameof(Details), new { id }));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> TimerStart(int id) =>
        Run(() =>
        {
            var t = _svc.TimerStart(CurrentUserId, id);
            return new Outcome(t, "timer", "تایمر روشن شد ⏱", $"{CurrentUserName} تایمر «{t.Title}» رو روشن کرد");
        }, () => RedirectToAction(nameof(Details), new { id }));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> TimerStop(int id) =>
        Run(() =>
        {
            var (t, hours) = _svc.TimerStop(CurrentUserId, id);
            return hours > 0
                ? new Outcome(t, "logged", $"{Fa.Hours(hours)} ثبت شد", $"{CurrentUserName} با تایمر {Fa.Hours(hours)} روی «{t.Title}» ثبت کرد")
                : new Outcome(t, "timer", "تایمر متوقف شد (کمتر از یک دقیقه بود، ثبت نشد)");
        }, () => RedirectToAction(nameof(Details), new { id }));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Complete(int id, [Bind(Prefix = "Complete")] CompleteTaskVm vm, string? returnTo) =>
        Run(() =>
        {
            var t = _svc.Submit(CurrentUserId, id, vm);
            var leader = _repo.GetUser(t.CreatedById)?.FullName ?? "تیم‌لید";
            return new Outcome(t, "submitted", $"تیک خورد و برای تأیید {leader} فرستاده شد ✓",
                $"{CurrentUserName} «{t.Title}» رو تیک زد و منتظر تأیید توئه");
        }, () => Back(id, returnTo));

    private IActionResult Back(int id, string? returnTo) =>
        returnTo == "board" ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Details), new { id });
}
