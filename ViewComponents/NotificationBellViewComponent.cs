using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TeamTracker.Services;
using TeamTracker.ViewModels;

namespace TeamTracker.ViewComponents;

public class NotificationBellViewComponent : ViewComponent
{
    private readonly TaskService _svc;
    public NotificationBellViewComponent(TaskService svc) => _svc = svc;

    public IViewComponentResult Invoke()
    {
        if (!int.TryParse(UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Content("");

        return View(new NotificationsVm
        {
            Items = _svc.Notifications(userId, 15),
            Unread = _svc.UnreadCount(userId),
            Now = DateTime.Now
        });
    }
}
