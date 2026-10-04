using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamTracker.Models;
using TeamTracker.Services;

namespace TeamTracker.Controllers;

[Authorize]
public class CommentsController : BaseController
{
    private readonly TaskService _svc;
    public CommentsController(TaskService svc) => _svc = svc;

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Add(int id, string? text) =>
        Run(() =>
        {
            var t = _svc.AddComment(CurrentUserId, id, text);
            return new Outcome(t, "commented", "نظر ثبت شد.", $"{CurrentUserName} روی «{t.Title}» نوشت: {Fa.Short(text?.Trim(), 60)}");
        }, () => RedirectToAction("Details", User.IsInRole(Roles.Leader) ? "Leader" : "Member", new { id }));
}
