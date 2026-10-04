using TeamTracker.Data.Sqlite;
using TeamTracker.Services;

namespace TeamTracker.Data;

public interface IDataReset
{
    void ResetToSample();
}

public static class StorageSetup
{
    private static bool UseJson(IConfiguration config) =>
        string.Equals(config["Storage:Provider"], "Json", StringComparison.OrdinalIgnoreCase);

    public static IServiceCollection AddTeamTrackerStorage(this IServiceCollection services, IConfiguration config, IWebHostEnvironment env)
    {
        if (UseJson(config))
        {
            services.AddSingleton<JsonDataStore>();
            services.AddSingleton<ITaskRepository>(sp => sp.GetRequiredService<JsonDataStore>());
            services.AddSingleton<IDataReset>(sp => sp.GetRequiredService<JsonDataStore>());
        }
        else
        {
            SqliteStorage.Add(services, config, env);
        }
        return services;
    }

    public static void InitializeStorage(this WebApplication app)
    {
        if (UseJson(app.Configuration)) app.Services.GetRequiredService<JsonDataStore>();
        else SqliteStorage.Initialize(app.Services);
    }
}

public class DemoResetService : BackgroundService
{
    private readonly IDataReset _reset;
    private readonly TimeProvider _clock;
    private readonly IBoardNotifier _notifier;
    private readonly ILogger<DemoResetService> _log;

    public DemoResetService(IDataReset reset, TimeProvider clock, IBoardNotifier notifier, ILogger<DemoResetService> log)
    {
        _reset = reset;
        _clock = clock;
        _notifier = notifier;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            var now = _clock.GetLocalNow();
            var next = now.Date.AddHours(4);
            if (next <= now.DateTime) next = next.AddDays(1);
            await Task.Delay(next - now.DateTime, _clock, stop);

            _reset.ResetToSample();
            _log.LogInformation("Demo data reset");
            await _notifier.EveryoneReload();
        }
    }
}
