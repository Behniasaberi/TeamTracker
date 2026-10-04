using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace TeamTracker.Data;

public class JsonDataStore : InMemoryTaskRepository, IDataReset
{
    public const int CurrentVersion = 4;

    private readonly string _path;
    private readonly TimeProvider _clock;

    // فارسی در فایل به صورت \u06xx ذخیره نشود تا فایل خوانا بماند
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public JsonDataStore(IWebHostEnvironment env, TimeProvider clock, ILogger<JsonDataStore> log)
    {
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "data.json");
        _clock = clock;

        DataFile? loaded = null;
        if (File.Exists(_path))
        {
            try { loaded = JsonSerializer.Deserialize<DataFile>(File.ReadAllText(_path)); }
            catch (JsonException ex) { log.LogWarning(ex, "data.json خراب بود؛ از نو ساخته می‌شود"); }
        }

        if (loaded is not null && loaded.Version == CurrentVersion)
        {
            Data = loaded;
            return;
        }

        // فایل نسخه‌ی قدیمی: بکاپ و ساخت داده‌ی نمونه‌ی جدید
        if (File.Exists(_path))
        {
            var backup = Path.Combine(dir, $"data.v{loaded?.Version ?? 0}.backup-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.Copy(_path, backup, overwrite: true);
            log.LogInformation("data.json قدیمی در {Backup} بکاپ شد", backup);
        }

        Data = SeedData.Create(clock);
        Data.Version = CurrentVersion;
        Save();
    }

    protected override void OnChanged() => Save();

    public void ResetToSample()
    {
        lock (Sync)
        {
            Data = SeedData.Create(_clock);
            Data.Version = CurrentVersion;
            Save();
        }
    }

    private void Save()
    {
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Data, JsonOpts));
        File.Move(tmp, _path, overwrite: true);
    }
}
