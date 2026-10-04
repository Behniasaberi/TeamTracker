using System.Text.RegularExpressions;
using TeamTracker.Models;
using TeamTracker.ViewModels;

namespace TeamTracker.Services;

public partial class UserService
{
    private readonly ITaskRepository _repo;
    public UserService(ITaskRepository repo) => _repo = repo;

    public const int MinPasswordLength = 4;

    [GeneratedRegex("^[a-z0-9_.]{3,20}$")]
    private static partial Regex UsernamePattern();

    public AppUser? Login(string username, string password)
    {
        var user = _repo.GetUserByUsername(username.Trim());
        return user is not null && PasswordHasher.Verify(password, user.PasswordHash) ? user : null;
    }

    public List<AppUser> Members(int leaderId) => _repo.GetMembersOf(leaderId);

    public AppUser GetMember(int leaderId, int memberId)
    {
        var m = _repo.GetUser(memberId);
        if (m is null || m.LeaderId != leaderId) throw new KeyNotFoundException("عضو پیدا نشد.");
        return m;
    }

    public AppUser AddMember(int leaderId, MemberFormVm vm)
    {
        var username = (vm.Username ?? "").Trim().ToLowerInvariant();
        if (!UsernamePattern().IsMatch(username))
            throw new InvalidOperationException("نام کاربری باید ۳ تا ۲۰ حرف انگلیسی کوچک، عدد، نقطه یا _ باشد.");
        if (_repo.GetUserByUsername(username) is not null)
            throw new InvalidOperationException("این نام کاربری قبلاً گرفته شده.");
        ValidatePassword(vm.Password);

        var user = new AppUser
        {
            FullName = RequireName(vm.FullName),
            Username = username,
            JobTitle = string.IsNullOrWhiteSpace(vm.JobTitle) ? "عضو تیم" : vm.JobTitle.Trim(),
            Role = Roles.Member,
            LeaderId = leaderId,
            PasswordHash = PasswordHasher.Hash(vm.Password!)
        };
        _repo.AddUser(user);
        return user;
    }

    public AppUser UpdateMember(int leaderId, int memberId, MemberFormVm vm)
    {
        var m = GetMember(leaderId, memberId);
        m.FullName = RequireName(vm.FullName);
        m.JobTitle = string.IsNullOrWhiteSpace(vm.JobTitle) ? m.JobTitle : vm.JobTitle.Trim();
        if (!string.IsNullOrEmpty(vm.Password))
        {
            ValidatePassword(vm.Password);
            m.PasswordHash = PasswordHasher.Hash(vm.Password);
        }
        _repo.UpdateUser(m);
        return m;
    }

    // عضوی که تسک دارد حذف نمی‌شود تا تاریخچه‌ی کار از بین نرود
    public void RemoveMember(int leaderId, int memberId)
    {
        var m = GetMember(leaderId, memberId);
        if (_repo.GetTasksAssignedTo(m.Id).Count > 0)
            throw new InvalidOperationException($"{m.FullName} تسک داره؛ اول تسک‌هاش رو به نفر دیگه بسپار.");
        _repo.DeleteUser(m.Id);
    }

    public AppUser UpdateProfile(int userId, string? fullName)
    {
        var u = _repo.GetUser(userId) ?? throw new KeyNotFoundException();
        u.FullName = RequireName(fullName);
        _repo.UpdateUser(u);
        return u;
    }

    public void ChangePassword(int userId, string? current, string? next)
    {
        var u = _repo.GetUser(userId) ?? throw new KeyNotFoundException();
        if (current is null || !PasswordHasher.Verify(current, u.PasswordHash))
            throw new InvalidOperationException("رمز فعلی درست نیست.");
        ValidatePassword(next);
        u.PasswordHash = PasswordHasher.Hash(next!);
        _repo.UpdateUser(u);
    }

    private static string RequireName(string? name)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name)) throw new InvalidOperationException("نام رو وارد کن.");
        if (name.Length > 40) throw new InvalidOperationException("نام حداکثر ۴۰ کاراکتر.");
        return name;
    }

    private static void ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            throw new InvalidOperationException($"رمز حداقل {Fa.N(MinPasswordLength)} کاراکتر باشه.");
    }
}
