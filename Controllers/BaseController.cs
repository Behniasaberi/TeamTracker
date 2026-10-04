using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TeamTracker.Models;
using TeamTracker.Services;

namespace TeamTracker.Controllers;

public record Outcome(TaskItem Task, string Kind, string Message, string? Broadcast = null);

public abstract class BaseController : Controller
{
    protected int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    protected string CurrentUserName => User.Identity?.Name ?? "";

    protected void Success(string msg) => TempData["Success"] = msg;
    protected void Error(string msg) => TempData["Error"] = msg;

    protected bool IsAjax => Request.Headers["X-Requested-With"] == "fetch";

    protected async Task<IActionResult> Run(Func<Outcome> op, Func<IActionResult> next)
    {
        try
        {
            var o = op();
            var notifier = HttpContext.RequestServices.GetRequiredService<IBoardNotifier>();
            await notifier.TaskChanged(o.Task, o.Kind, CurrentUserId, o.Broadcast ?? o.Message);

            if (IsAjax) return Json(new { ok = true, message = o.Message, kind = o.Kind, taskId = o.Task.Id });
            Success(o.Message);
        }
        catch (KeyNotFoundException)
        {
            return IsAjax ? NotFound(new { ok = false, message = "تسک پیدا نشد." }) : NotFound();
        }
        catch (Exception ex) when (ex is TaskLockedException or InvalidOperationException)
        {
            if (IsAjax) return BadRequest(new { ok = false, message = ex.Message });
            Error(ex.Message);
        }
        return next();
    }

    protected void NoCache() => Response.Headers.CacheControl = "no-store";
}
