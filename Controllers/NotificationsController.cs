using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamTracker.Models;
using TeamTracker.Services;

namespace TeamTracker.Controllers;

[Authorize]
public class NotificationsController : BaseController
{
    private readonly TaskService _svc;
    public NotificationsController(TaskService svc) => _svc = svc;

    public IActionResult Go(int id)
    {
        var n = _svc.Notifications(CurrentUserId, 200).FirstOrDefault(x => x.Id == id);
        if (n is null) return RedirectToAction("Index", "Home");
        _svc.MarkRead(CurrentUserId, id);
        if (n.TaskId is null) return RedirectToAction("Index", "Home");
        var controller = User.IsInRole(Roles.Leader) ? "Leader" : "Member";
        try
        {
            _svc.GetTaskFor(CurrentUserId, n.TaskId.Value);
            return RedirectToAction("Details", controller, new { id = n.TaskId });
        }
        catch (KeyNotFoundException)
        {
            Error("این تسک دیگه در دسترس نیست.");
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ReadAll()
    {
        _svc.MarkRead(CurrentUserId);
        return IsAjax ? Json(new { ok = true }) : RedirectToAction("Index", "Home");
    }
}
