Create a repository that handles all DB access
```csharp
public interface IFeatureFlagRepository
{
    Task<bool?> GetFlagAsync(string name);
    Task<IEnumerable<FeatureFlag>> GetAllAsync();
    Task UpdateFlagAsync(string name, bool isEnabled);
}

public class FeatureFlagRepository : IFeatureFlagRepository
{
    private readonly string _connectionString;

    public FeatureFlagRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection");
    }

    public async Task<bool?> GetFlagAsync(string name)
    {
        const string sql = "SELECT is_enabled FROM feature_flags WHERE name = @name";

        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        return await conn.QueryFirstOrDefaultAsync<bool?>(sql, new { name });
    }

    public async Task<IEnumerable<FeatureFlag>> GetAllAsync()
    {
        const string sql = "SELECT id, name, is_enabled, last_updated FROM feature_flags";

        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        return await conn.QueryAsync<FeatureFlag>(sql);
    }

    public async Task UpdateFlagAsync(string name, bool isEnabled)
    {
        const string sql = @"
            UPDATE feature_flags
            SET is_enabled = @isEnabled,
                last_updated = NOW()
            WHERE name = @name";

        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, new { name, isEnabled });
    }
}
```
Feature Flag Service (Dapper + IMemoryCache)
```csharp
public class FeatureFlagService : IFeatureFlagService
{
    private readonly IFeatureFlagRepository _repo;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    public FeatureFlagService(IFeatureFlagRepository repo, IMemoryCache cache)
    {
        _repo = repo;
        _cache = cache;
    }

    public async Task<bool> IsEnabledAsync(string featureName)
    {
        return await _cache.GetOrCreateAsync(featureName, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _cacheDuration;

            var value = await _repo.GetFlagAsync(featureName);
            return value ?? false;
        });
    }
}
```
Hybrid Sync Service (LISTEN/NOTIFY + Polling)
Almost Identical only DB Calls change
```csharp
public class FeatureFlagSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _config;
    private readonly TimeSpan _pollInterval = TimeSpan.FromMinutes(2);

    public FeatureFlagSyncService(IServiceScopeFactory scopeFactory, IMemoryCache cache, IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connString = _config.GetConnectionString("DefaultConnection");

        await using var conn = new Npgsql.NpgsqlConnection(connString);
        await conn.OpenAsync(stoppingToken);

        conn.Notification += (o, e) =>
        {
            var featureName = e.Payload;
            _cache.Remove(featureName);
        };

        using var cmd = new Npgsql.NpgsqlCommand("LISTEN feature_flags_updated;", conn);
        await cmd.ExecuteNonQueryAsync(stoppingToken);

        _ = Task.Run(() => PollLoop(stoppingToken));

        while (!stoppingToken.IsCancellationRequested)
        {
            await conn.WaitAsync(stoppingToken);
        }
    }

    private async Task PollLoop(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IFeatureFlagRepository>();

            var flags = await repo.GetAllAsync();

            foreach (var flag in flags)
            {
                _cache.Set(flag.Name, flag.IsEnabled, TimeSpan.FromMinutes(5));
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }
}
```
Admin API 
```csharp
[ApiController]
[Route("api/[controller]")]
public class FeatureFlagsController : ControllerBase
{
    private readonly IFeatureFlagRepository _repo;
    private readonly IMemoryCache _cache;

    public FeatureFlagsController(IFeatureFlagRepository repo, IMemoryCache cache)
    {
        _repo = repo;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var flags = await _repo.GetAllAsync();
        return Ok(flags);
    }

    [HttpPut("{name}")]
    public async Task<IActionResult> Update(string name, [FromBody] bool isEnabled)
    {
        await _repo.UpdateFlagAsync(name, isEnabled);

        _cache.Remove(name);

        return Ok(new { name, isEnabled });
    }
}
```
Program.cs
```csharp
builder.Services.AddMemoryCache();

builder.Services.AddScoped<IFeatureFlagRepository, FeatureFlagRepository>();
builder.Services.AddScoped<IFeatureFlagService, FeatureFlagService>();

builder.Services.AddHostedService<FeatureFlagSyncService>();
```

## Typed Feature flag implementation (Alternative)
```csharp
///This class represents all flags your system supports.
public class FeatureFlags
{
    public bool NewDashboard { get; init; }
    public bool BetaAPI { get; init; }
    public bool EnablePayments { get; init; }
}

//Create a Typed Provider Interface
public interface IFeatureFlagProvider
{
    Task<FeatureFlags> GetAsync();
}

//This provider uses your existing IFeatureFlagService (which uses Dapper + cache).
public class FeatureFlagProvider : IFeatureFlagProvider
{
    private readonly IFeatureFlagService _service;

    public FeatureFlagProvider(IFeatureFlagService service)
    {
        _service = service;
    }

    public async Task<FeatureFlags> GetAsync()
    {
        return new FeatureFlags
        {
            NewDashboard = await _service.IsEnabledAsync("NewDashboard"),
            BetaAPI = await _service.IsEnabledAsync("BetaAPI"),
            EnablePayments = await _service.IsEnabledAsync("EnablePayments")
        };
    }
}

//Register in DI
builder.Services.AddScoped<IFeatureFlagProvider, FeatureFlagProvider>();

//Use it in Controllers or Services
public class DashboardController : ControllerBase
{
    private readonly IFeatureFlagProvider _provider;

    public DashboardController(IFeatureFlagProvider provider)
    {
        _provider = provider;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var flags = await _provider.GetAsync();

        if (!flags.NewDashboard)
            return Forbid("Feature disabled");

        return Ok("Welcome to the new dashboard");
    }
}


```
