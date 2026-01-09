# Implement Feature Flag Service in ASP.NET Core 8 Application

#### Create table to store feature flags
First, create the table and the trigger in PostgreSQL. The trigger ensures that any manual SQL update to a flag automatically notifies the application.
```sql
-- 1. Create the Feature Flags table
CREATE TABLE feature_flags (
    key TEXT PRIMARY KEY,
    is_enabled BOOLEAN NOT NULL DEFAULT FALSE
);

-- 2. Create a function to notify on changes
CREATE OR REPLACE FUNCTION notify_flag_change() RETURNS trigger AS $$
BEGIN
  PERFORM pg_notify('feature_flag_changed', NEW.key);
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- 3. Attach the trigger
CREATE TRIGGER trg_flag_changed
AFTER UPDATE ON feature_flags
FOR EACH ROW EXECUTE FUNCTION notify_flag_change();

-- Seed some data
INSERT INTO feature_flags (key, is_enabled) VALUES ('BetaFeature', true);
```
#### Enitity and DbConnection
We will create a FeatureFlagService that handles three levels of data retrieval:
* Memory Cache: Check here first ($O(1)$).
* Database (Dapper): Fallback if cache is empty.
* Real-time Listener: A background task that uses Npgsql to listen for NOTIFY events and invalidates the cache.

```
dotnet add package Dapper
dotnet add package Npgsql
```
#### The Service Implementation
```csharp
using Dapper;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(string key);
}

public class FeatureFlagService : IFeatureFlagService
{
    private readonly string _connectionString;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FeatureFlagService> _logger;

    public FeatureFlagService(IConfiguration config, IMemoryCache cache, ILogger<FeatureFlagService> logger)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")!;
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> IsEnabledAsync(string key)
    {
        // 1. Try Memory Cache
        if (_cache.TryGetValue(key, out bool isEnabled)) return isEnabled;

        // 2. Fallback to Database using Dapper
        using var connection = new NpgsqlConnection(_connectionString);
        isEnabled = await connection.QueryFirstOrDefaultAsync<bool>(
            "SELECT is_enabled FROM feature_flags WHERE key = @key", new { key });

        // 3. Store in Cache (with a safety polling fallback of 5 minutes)
        _cache.Set(key, isEnabled, TimeSpan.FromMinutes(5));
        
        return isEnabled;
    }
}
```
#### The Real-Time Listener (Background Service)
This background worker stays connected to PostgreSQL and clears the cache the moment a flag changes.
```csharp
public class FeatureFlagListener : BackgroundService
{
    private readonly string _connectionString;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FeatureFlagListener> _logger;

    public FeatureFlagListener(IConfiguration config, IMemoryCache cache, ILogger<FeatureFlagListener> logger)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")!;
        _cache = cache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(stoppingToken);
                
                // Register the listener
                using var cmd = new NpgsqlCommand("LISTEN feature_flag_changed", conn);
                await cmd.ExecuteNonQueryAsync(stoppingToken);

                conn.Notification += (o, e) =>
                {
                    _logger.LogInformation("Flag changed: {FlagKey}. Invalidating cache.", e.Payload);
                    _cache.Remove(e.Payload); // Payload is the flag 'key' from the trigger
                };

                while (!stoppingToken.IsCancellationRequested)
                {
                    // Wait for notifications (this keeps the connection idle/efficient)
                    await conn.WaitAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Listener lost connection. Retrying in 5s...");
                await Task.Delay(5000, stoppingToken); // Reconnection strategy
            }
        }
    }
}
```
#### Program.cs
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IFeatureFlagService, FeatureFlagService>();
builder.Services.AddHostedService<FeatureFlagListener>();

var app = builder.Build();

app.MapGet("/test-feature", async (IFeatureFlagService featureService) =>
{
    return await featureService.IsEnabledAsync("BetaFeature") 
        ? Results.Ok("Feature Active!") 
        : Results.NotFound("Feature Disabled.");
});

app.Run();
```

* Performance: After the first request, every subsequent check is a RAM lookup (microseconds).
* Scalability: The database is only queried once per flag (until it changes or expires).
* Freshness: If you run UPDATE feature_flags SET is_enabled = false WHERE key = 'BetaFeature'; in your SQL tool, the app updates instantly without a restart.
* Reliability: If the LISTEN connection drops, the IMemoryCache still has a 5-minute sliding expiration as a polling fallback.

#### Minimal API Implementation
```csharp
app.MapPost("/admin/flags", async (string key, bool enabled, IConfiguration config) =>
{
    using var connection = new NpgsqlConnection(config.GetConnectionString("DefaultConnection"));
    
    // UPSERT: Create if doesn't exist, Update if it does
    const string sql = @"
        INSERT INTO feature_flags (key, is_enabled) 
        VALUES (@key, @enabled)
        ON CONFLICT (key) DO UPDATE SET is_enabled = @enabled;";
    
    await connection.ExecuteAsync(sql, new { key, enabled });
    
    return Results.Accepted($"/admin/flags/{key}", new { key, enabled });
});

app.MapGet("/admin/flags", async (IConfiguration config) =>
{
    using var connection = new NpgsqlConnection(config.GetConnectionString("DefaultConnection"));
    var flags = await connection.QueryAsync("SELECT * FROM feature_flags");
    return Results.Ok(flags);
});
```
Testing the Real-Time Update
You can test the "Polling Fallback" and "Real-time" aspects easily:
* Test Real-time: Call GET /test-feature. It will return "Feature Active".
* Run this SQL manually in your database tool (like pgAdmin or psql):
`UPDATE feature_flags SET is_enabled = false WHERE key = 'BetaFeature';`
Call `GET /test-feature` again immediately. It will return "Feature Disabled" without you having to wait for a cache timeout.


#### Conventional API Controller Setup
```csharp
using Microsoft.AspNetCore.Mvc;
using Dapper;
using Npgsql;

[ApiController]
[Route("api/[controller]")]
public class FeatureFlagsController : ControllerBase
{
    private readonly IFeatureFlagService _featureService;
    private readonly string _connectionString;

    public FeatureFlagsController(IFeatureFlagService featureService, IConfiguration config)
    {
        _featureService = featureService;
        _connectionString = config.GetConnectionString("DefaultConnection")!;
    }

    // GET: api/featureflags/test/BetaFeature
    [HttpGet("test/{key}")]
    public async Task<IActionResult> GetFeatureStatus(string key)
    {
        var isEnabled = await _featureService.IsEnabledAsync(key);
        return isEnabled ? Ok(new { status = "Active" }) : NotFound(new { status = "Disabled" });
    }

    // GET: api/featureflags (Admin)
    [HttpGet]
    public async Task<IActionResult> GetAllFlags()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var flags = await connection.QueryAsync("SELECT * FROM feature_flags");
        return Ok(flags);
    }

    // POST: api/featureflags (Admin Management)
    [HttpPost]
    public async Task<IActionResult> UpdateFlag([FromQuery] string key, [FromQuery] bool enabled)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        const string sql = @"
            INSERT INTO feature_flags (key, is_enabled) 
            VALUES (@key, @enabled)
            ON CONFLICT (key) DO UPDATE SET is_enabled = @enabled;";
        
        await connection.ExecuteAsync(sql, new { key, enabled });
        return AcceptedAtAction(nameof(GetFeatureStatus), new { key }, new { key, enabled });
    }
}
```
#### Updated Program.cs
```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. Add services to the container
builder.Services.AddControllers(); // Required for Controllers
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. Register our Custom Services
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IFeatureFlagService, FeatureFlagService>();
builder.Services.AddHostedService<FeatureFlagListener>();

var app = builder.Build();

// 3. Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

// 4. Map the Controllers
app.MapControllers(); 

app.Run();
```
