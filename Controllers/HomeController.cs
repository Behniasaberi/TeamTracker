using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamTracker.Models;

namespace TeamTracker.Controllers;

public class HomeController : Controller
{
    [Authorize]
    public IActionResult Index() =>
        User.IsInRole(Roles.Leader)
            ? RedirectToAction("Index", "Leader")
            : RedirectToAction("Index", "Member");

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
