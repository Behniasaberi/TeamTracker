using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TeamTracker.Services;

namespace TeamTracker.Data.Sqlite;

public static class SqliteStorage
{
    // با تغییر ساختار جدول‌ها زیادش کن؛ دیتابیس قدیمی بکاپ و از نو ساخته می‌شود (migration نداریم)
    public const int SchemaVersion = 1;

    public static void Add(IServiceCollection services, IConfiguration config, IWebHostEnvironment env)
    {
        var connection = config.GetConnectionString("TeamTracker");
        if (string.IsNullOrWhiteSpace(connection))
        {
            var dir = Path.Combine(env.ContentRootPath, "App_Data");
            Directory.CreateDirectory(dir);
            connection = $"Data Source={Path.Combine(dir, "teamtracker.db")}";
        }

        services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));
        services.AddScoped<ITaskRepository, EfTaskRepository>();
        services.AddSingleton<IDataReset, SqliteReset>();
    }

    public static void Initialize(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("TeamTracker.Storage");

        var file = db.Database.GetDbConnection().DataSource;
        if (!string.IsNullOrEmpty(file) && File.Exists(file) && ReadVersion(db) != SchemaVersion)
        {
            var backup = $"{file}.backup-{DateTime.Now:yyyyMMdd-HHmmss}";
            SqliteConnection.ClearAllPools();
            File.Copy(file, backup, overwrite: true);
            log.LogInformation("Old database schema backed up to {Backup}", backup);
            db.Database.EnsureDeleted();
        }

        if (db.Database.EnsureCreated())
        {
            db.Database.ExecuteSqlRaw("PRAGMA user_version = " + SchemaVersion);
            Seed(db, clock);
            log.LogInformation("SQLite database created with sample data");
        }
        else if (!db.Users.Any())
        {
            Seed(db, clock);
        }
    }

    public static void Seed(AppDbContext db, TimeProvider clock)
    {
        var data = SeedData.Create(clock);
        db.Users.AddRange(data.Users);
        db.Tasks.AddRange(data.Tasks);
        db.Notifications.AddRange(data.Notifications);
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    private static int ReadVersion(AppDbContext db)
    {
        var conn = db.Database.GetDbConnection();
        db.Database.OpenConnection();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA user_version";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
        finally
        {
            db.Database.CloseConnection();
        }
    }

    private class SqliteReset : IDataReset
    {
        private readonly IServiceProvider _services;
        public SqliteReset(IServiceProvider services) => _services = services;

        public void ResetToSample()
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Notifications.ExecuteDelete();
            db.Tasks.ExecuteDelete();
            db.Users.ExecuteDelete();
            Seed(db, scope.ServiceProvider.GetRequiredService<TimeProvider>());
        }
    }
}
