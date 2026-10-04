using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamTracker.Models;

namespace TeamTracker.Data.Sqlite;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.Username).HasMaxLength(20);
            e.Property(u => u.FullName).HasMaxLength(40);
            e.Property(u => u.Role).HasMaxLength(10);
        });

        b.Entity<TaskItem>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.AssignedToId);
            e.HasIndex(t => t.CreatedById);
            e.Property(t => t.Title).HasMaxLength(150);

            // زیرمجموعه‌های تسک همیشه با خود تسک خوانده و ذخیره می‌شوند،
            // پس به‌صورت ستون JSON نگه داشته می‌شوند نه جدول جدا.
            e.Property(t => t.Checklist).AsJson();
            e.Property(t => t.WorkLogs).AsJson();
            e.Property(t => t.Events).AsJson();
            e.Property(t => t.Comments).AsJson();
        });

        b.Entity<Notification>(e =>
        {
            e.HasKey(n => n.Id);
            e.HasIndex(n => new { n.UserId, n.IsRead });
        });
    }
}

internal static class JsonColumn
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public static PropertyBuilder<List<T>> AsJson<T>(this PropertyBuilder<List<T>> property)
    {
        var comparer = new ValueComparer<List<T>>(
            (a, b) => Serialize(a) == Serialize(b),
            v => Serialize(v).GetHashCode(),
            v => Deserialize<T>(Serialize(v)));

        property.HasConversion(v => Serialize(v), v => Deserialize<T>(v), comparer);
        property.HasColumnType("TEXT");
        return property;
    }

    private static string Serialize<T>(List<T>? value) => JsonSerializer.Serialize(value ?? new List<T>(), Options);

    private static List<T> Deserialize<T>(string json) =>
        string.IsNullOrEmpty(json) ? new List<T>() : JsonSerializer.Deserialize<List<T>>(json, Options) ?? new List<T>();
}
