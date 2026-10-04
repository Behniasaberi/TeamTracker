using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamTracker.Models;
using TeamTracker.Services;
using TeamTracker.ViewModels;

namespace TeamTracker.Controllers;

public class AccountController : BaseController
{
    private readonly UserService _users;
    private readonly ITaskRepository _repo;

    public AccountController(UserService users, ITaskRepository repo)
    {
        _users = users;
        _repo = repo;
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View(new LoginVm { ReturnUrl = returnUrl });
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = _users.Login(vm.Username, vm.Password);
        if (user is null)
        {
            ModelState.AddModelError("", "نام کاربری یا رمز عبور اشتباه است.");
            return View(vm);
        }

        await SignIn(user);
        if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl)) return Redirect(vm.ReturnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult Denied() => View();

    [Authorize, HttpGet]
    public IActionResult Profile()
    {
        var me = _repo.GetUser(CurrentUserId);
        if (me is null) return RedirectToAction(nameof(Login));
        var leader = me.LeaderId is { } lid ? _repo.GetUser(lid)?.FullName : null;
        return View(new ProfileVm { User = me, LeaderName = leader });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(string? fullName)
    {
        try
        {
            var me = _users.UpdateProfile(CurrentUserId, fullName);
            await SignIn(me); // نام جدید در نوار بالا دیده شود
            Success("نام ذخیره شد.");
        }
        catch (InvalidOperationException ex) { Error(ex.Message); }
        return RedirectToAction(nameof(Profile));
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public IActionResult ChangePassword(string? current, string? next, string? repeat)
    {
        try
        {
            if (next != repeat) throw new InvalidOperationException("تکرار رمز جدید یکی نیست.");
            _users.ChangePassword(CurrentUserId, current, next);
            Success("رمز عوض شد.");
        }
        catch (InvalidOperationException ex) { Error(ex.Message); }
        return RedirectToAction(nameof(Profile));
    }

    private Task SignIn(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role),
            new("JobTitle", user.JobTitle ?? "")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        return HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    }
}
