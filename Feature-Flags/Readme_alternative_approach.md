
Create a simple table to store feature flags:
```sql
CREATE TABLE feature_flags (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) UNIQUE NOT NULL,
    is_enabled BOOLEAN NOT NULL DEFAULT false,
    last_updated TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
INSERT INTO feature_flags (name, is_enabled) VALUES
('NewDashboard', true),
('BetaAPI', false);
```
```csharp
public class FeatureFlag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class AppDbContext //Implemented from Dapper
{
    public FeatureFlag[] FeatureFlags { get; set; }
}
/*
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<FeatureFlag> FeatureFlags { get; set; }
}
*/
```
#### Feature FLag with IMemoryCache
```csharp
public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(string featureName);
}

public class FeatureFlagService : IFeatureFlagService
{
    private readonly AppDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    public FeatureFlagService(AppDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<bool> IsEnabledAsync(string featureName)
    {
        return await _cache.GetOrCreateAsync(featureName, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _cacheDuration;

            var flag = await _dbContext.FeatureFlags
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Name == featureName);

            return flag?.IsEnabled ?? false;
        });
    }
}

```
Program.cs
```
var builder = WebApplication.CreateBuilder(args);
//Replace with Dapper
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddMemoryCache();
builder.Services.AddScoped<IFeatureFlagService, FeatureFlagService>();

var app = builder.Build();
```
Usage Example
```csharp
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IFeatureFlagService _featureFlags;

    public DashboardController(IFeatureFlagService featureFlags)
    {
        _featureFlags = featureFlags;
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboard()
    {
        if (!await _featureFlags.IsEnabledAsync("NewDashboard"))
            return Forbid("Feature not enabled");

        return Ok("Welcome to the new dashboard!");
    }
}
```
#### Alternative IMplementation using background service

Database Polling with Background Service
* Run a hosted service that periodically checks the feature_flags table for updates.
* If a flag’s last_updated timestamp changes, invalidate the cache entry.
* This ensures updates propagate within seconds, not minutes.
```csharp
public class FeatureFlagRefreshService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(30);

    public FeatureFlagRefreshService(IServiceScopeFactory scopeFactory, IMemoryCache cache)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var flags = await dbContext.FeatureFlags.AsNoTracking().ToListAsync(stoppingToken);

            foreach (var flag in flags)
            {
                // Force cache refresh if DB changed
                _cache.Set(flag.Name, flag.IsEnabled, TimeSpan.FromMinutes(5));
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }
}

```
Program.cs
`builder.Services.AddHostedService<FeatureFlagRefreshService>();`

#### Postgres LISTEN/NOTIFY
Postgres has a pub/sub mechanism:
* Use NOTIFY feature_flags_updated when a flag changes.
* Your ASP.NET service uses LISTEN feature_flags_updated.
* On notification, invalidate the cache immediately.

Trigger in Postgres:
```sql
CREATE OR REPLACE FUNCTION notify_feature_flag_update()
RETURNS trigger AS $$
BEGIN
    PERFORM pg_notify('feature_flags_updated', NEW.name);
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER feature_flag_update_trigger
AFTER UPDATE ON feature_flags
FOR EACH ROW
EXECUTE FUNCTION notify_feature_flag_update();

```
Background Service

```csharp
public class FeatureFlagListener : BackgroundService
{
    private readonly IMemoryCache _cache;
    private readonly string _connectionString;

    public FeatureFlagListener(IMemoryCache cache, IConfiguration config)
    {
        _cache = cache;
        _connectionString = config.GetConnectionString("DefaultConnection");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(stoppingToken);

        conn.Notification += (o, e) =>
        {
            var featureName = e.Payload;
            _cache.Remove(featureName); // force reload on next request
        };

        using var cmd = new Npgsql.NpgsqlCommand("LISTEN feature_flags_updated;", conn);
        await cmd.ExecuteNonQueryAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await conn.WaitAsync(stoppingToken); // blocks until notification
        }
    }
}

```
Program.cs
`builder.Services.AddHostedService<FeatureFlagListener>();`

#### Approach 4
Combine LISTEN/NOTIFY (instant updates) with polling fallback (resilience) so your ASP.NET 8 API stays in sync with PostgreSQL feature flags even if notifications fail.

Hybrid Background Service
We’ll build a hosted service that:
* Listens for notifications (fast path).
* Polls periodically (fallback path).
* Invalidates cache entries when either mechanism detects a change
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

        // Subscribe to notifications
        conn.Notification += (o, e) =>
        {
            var featureName = e.Payload;
            _cache.Remove(featureName); // force reload on next request
        };

        using var cmd = new Npgsql.NpgsqlCommand("LISTEN feature_flags_updated;", conn);
        await cmd.ExecuteNonQueryAsync(stoppingToken);

        // Run polling loop in parallel
        _ = Task.Run(() => PollLoop(stoppingToken));

        // Block waiting for notifications
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
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var flags = await dbContext.FeatureFlags.AsNoTracking().ToListAsync(stoppingToken);

            foreach (var flag in flags)
            {
                _cache.Set(flag.Name, flag.IsEnabled, TimeSpan.FromMinutes(5));
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }
}
```
How It Works
LISTEN/NOTIFY → instant invalidation when a flag changes.
Polling → ensures cache stays fresh even if notifications fail (e.g., network hiccup).
IMemoryCache → avoids hammering Postgres, only reloads when needed.

#### Admin API
```csharp
[ApiController]
[Route("api/[controller]")]
public class FeatureFlagsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IMemoryCache _cache;

    public FeatureFlagsController(AppDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    // GET: api/featureflags
    [HttpGet]
    public async Task<IActionResult> GetAllFlags()
    {
        var flags = await _dbContext.FeatureFlags.AsNoTracking().ToListAsync();
        return Ok(flags);
    }

    // GET: api/featureflags/{name}
    [HttpGet("{name}")]
    public async Task<IActionResult> GetFlag(string name)
    {
        var flag = await _dbContext.FeatureFlags.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Name == name);

        if (flag == null) return NotFound();

        return Ok(flag);
    }

    // PUT: api/featureflags/{name}
    [HttpPut("{name}")]
    public async Task<IActionResult> UpdateFlag(string name, [FromBody] bool isEnabled)
    {
        var flag = await _dbContext.FeatureFlags.FirstOrDefaultAsync(f => f.Name == name);

        if (flag == null) return NotFound();

        flag.IsEnabled = isEnabled;
        flag.LastUpdated = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        // Invalidate cache immediately
        _cache.Remove(name);

        return Ok(flag);
    }
}

```
Integration with Sync Service
* When you update a flag via this API:
  * It updates Postgres.
  * Postgres trigger fires NOTIFY.
  * Listener invalidates cache.
  * Polling fallback ensures consistency if notifications fail.

✅ With this admin API, you now have a full feature flag system:

* Postgres → persistent storage.
* IMemoryCache → efficient reads.
* LISTEN/NOTIFY + Polling → real-time sync.
* Admin API → easy management.
