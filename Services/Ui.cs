using Microsoft.AspNetCore.Html;

namespace TeamTracker.Services;

public static class Ui
{
    private static readonly Dictionary<string, string> Paths = new()
    {
        ["check"] = "<path d='M20 6 9 17l-5-5'/>",
        ["lock"] = "<rect x='4' y='11' width='16' height='10' rx='2'/><path d='M8 11V7a4 4 0 0 1 8 0v4'/>",
        ["clock"] = "<circle cx='12' cy='12' r='9'/><path d='M12 7v5l3 2'/>",
        ["calendar"] = "<rect x='3' y='5' width='18' height='16' rx='2'/><path d='M16 3v4M8 3v4M3 10h18'/>",
        ["plus"] = "<path d='M12 5v14M5 12h14'/>",
        ["board"] = "<rect x='3' y='4' width='5' height='16' rx='1.5'/><rect x='10' y='4' width='5' height='10' rx='1.5'/><rect x='17' y='4' width='4' height='13' rx='1.5'/>",
        ["grid"] = "<rect x='3' y='3' width='7' height='7' rx='1.5'/><rect x='14' y='3' width='7' height='7' rx='1.5'/><rect x='3' y='14' width='7' height='7' rx='1.5'/><rect x='14' y='14' width='7' height='7' rx='1.5'/>",
        ["users"] = "<circle cx='9' cy='8' r='3.5'/><path d='M2.5 20a6.5 6.5 0 0 1 13 0'/><path d='M16 4.5a3.5 3.5 0 0 1 0 7M18 14a6 6 0 0 1 4 6'/>",
        ["logout"] = "<path d='M15 4h3a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-3'/><path d='M10 17l-5-5 5-5M5 12h11'/>",
        ["alert"] = "<path d='M12 3 2 20h20L12 3z'/><path d='M12 10v4M12 17h.01'/>",
        ["x"] = "<path d='M18 6 6 18M6 6l12 12'/>",
        ["arrow"] = "<path d='M19 12H5M11 18l-6-6 6-6'/>",
        ["back"] = "<path d='M5 12h14M13 6l6 6-6 6'/>",
        ["edit"] = "<path d='M4 20h4L19 9l-4-4L4 16v4z'/><path d='M13.5 6.5l4 4'/>",
        ["trash"] = "<path d='M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3'/>",
        ["drag"] = "<circle cx='9' cy='6' r='1'/><circle cx='15' cy='6' r='1'/><circle cx='9' cy='12' r='1'/><circle cx='15' cy='12' r='1'/><circle cx='9' cy='18' r='1'/><circle cx='15' cy='18' r='1'/>",
        ["play"] = "<path d='M7 4v16l13-8z'/>",
        ["flag"] = "<path d='M5 21V4M5 4h11l-2 4 2 4H5'/>",
        ["note"] = "<path d='M6 3h9l4 4v14H6z'/><path d='M14 3v5h5M9 13h7M9 17h5'/>",
        ["activity"] = "<path d='M3 12h4l3-8 4 16 3-8h4'/>",
        ["stop"] = "<rect x='6' y='6' width='12' height='12' rx='2.5'/>",
        ["timer"] = "<circle cx='12' cy='13' r='8'/><path d='M12 9v4l2.5 2M9.5 2.5h5M12 2.5V5'/>",
        ["hourglass"] = "<path d='M6 3h12M6 21h12M7 3c0 5 10 5 10 9s-10 4-10 9M17 3c0 5-10 5-10 9s10 4 10 9'/>",
        ["undo"] = "<path d='M14 8H5V3'/><path d='M5 8a9 9 0 1 1 1.5 8'/>",
        ["sun"] = "<circle cx='12' cy='12' r='4'/><path d='M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4'/>",
        ["moon"] = "<path d='M20 14.5A8 8 0 0 1 9.5 4a8 8 0 1 0 10.5 10.5z'/>",
        ["inbox"] = "<path d='M3 13h5l1.5 3h5L16 13h5'/><path d='M5.5 5h13L21 13v6H3v-6z'/>",
        ["chart"] = "<path d='M4 20V10M10 20V4M16 20v-7M22 20H2'/>",
        ["list"] = "<path d='M9 6h11M9 12h11M9 18h11'/><path d='M3.5 6l1 1 2-2M3.5 12l1 1 2-2M3.5 18l1 1 2-2'/>",
        ["swap"] = "<path d='M7 4 3 8l4 4M3 8h13M17 20l4-4-4-4M21 16H8'/>",
        ["bell"] = "<path d='M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9'/><path d='M10.3 21a1.94 1.94 0 0 0 3.4 0'/>",
        ["chat"] = "<path d='M21 12a8 8 0 0 1-11.6 7.1L4 20l1-4.6A8 8 0 1 1 21 12z'/>",
        ["download"] = "<path d='M12 4v11M7 10l5 5 5-5M5 20h14'/>",
        ["user"] = "<circle cx='12' cy='8' r='4'/><path d='M4 21a8 8 0 0 1 16 0'/>",
        ["key"] = "<circle cx='8' cy='15' r='4'/><path d='M10.8 12.2 20 3M17 6l3 3M15 8l2 2'/>",
        ["send"] = "<path d='M21 3 3 10.5l7 2.5 2.5 7z'/><path d='M10 13l11-10'/>",
        ["sparkle"] = "<path d='M12 3v4M12 17v4M3 12h4M17 12h4M6 6l2.5 2.5M15.5 15.5 18 18M6 18l2.5-2.5M15.5 8.5 18 6'/>",
    };

    public static IHtmlContent Icon(string name, int size = 18, string? cls = null) =>
        new HtmlString(
            $"<svg class='ic {cls}' width='{size}' height='{size}' viewBox='0 0 24 24' fill='none' stroke='currentColor' " +
            $"stroke-width='2' stroke-linecap='round' stroke-linejoin='round' aria-hidden='true'>{Paths.GetValueOrDefault(name, "")}</svg>");

    private static readonly string[] AvatarColors =
        { "#6366f1", "#0ea5e9", "#10b981", "#f59e0b", "#ec4899", "#8b5cf6", "#14b8a6", "#ef4444" };

    public static string AvatarColor(int userId) => AvatarColors[Math.Abs(userId) % AvatarColors.Length];

    public static IHtmlContent Avatar(string name, int userId, string size = "") =>
        new HtmlString($"<span class='avatar {size}' style='--av:{AvatarColor(userId)}' title='{System.Net.WebUtility.HtmlEncode(name)}'>{System.Net.WebUtility.HtmlEncode(Initial(name))}</span>");

    public static string Initial(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        var main = parts.Length > 1 && parts[0] is "عمو" or "خاله" or "آقا" or "خانم" ? parts[1] : parts[0];
        return main[..1];
    }

    public static string Inv(double v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
