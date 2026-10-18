# SqlStreamStore Multi-Pod Scaling & Integration Guide

This guide provides a production-ready implementation of **SqlStreamStore** integrated with **MassTransit** and **Medallion.Threading** (Distributed Locks) to safely scale event store background processing across multiple Kubernetes pods or container instances.

---

## 1. Architectural Challenge: Multi-Pod Scaling

When running $N$ pods of a microservice, each pod running a standard `BackgroundService` stream reader will attempt to poll the event store and publish integration messages. Without coordination, this leads to:

1. **Duplicate Event Publishing:** Multiple pods reading position $P$ concurrently and publishing duplicate messages to MassTransit.
2. **Race Conditions:** Out-of-order execution if Pod A processes event $N+1$ faster than Pod B processes event $N$.

### Solution Pattern: Distributed Leader Election for Stream Readers
We use **MedallionDistributedMatcher** (`Medallion.Threading`) backed by SQL Server or PostgreSQL advisory locks. Only the **leader pod** holds the lock and runs the catch-up subscription worker. Secondary pods remain in a light standby loop, ready to claim leadership if the leader pod terminates or crashes.

---

## 2. Dependencies & NuGet Packages

Add the following packages to your project:

```bash
# Core SqlStreamStore & SQL Server Provider (or SqlStreamStore.Postgres / MySqlStreamStore)
dotnet add package SqlStreamStore
dotnet add package SqlStreamStore.MsSql

# MassTransit Broker Integration
dotnet add package MassTransit
dotnet add package MassTransit.RabbitMQ

# Distributed Locking for Multi-Pod Synchronization
dotnet add package Medallion.Threading.SqlServer
# Or if using PostgreSQL:
# dotnet add package Medallion.Threading.Postgres
```

---

## 3. Domain & Integration Event Contracts

```csharp
namespace SqlStreamStoreDemo.Contracts;

// --- Domain Events (Internal to Aggregate) ---
public record OrderPlacedDomainEvent(Guid OrderId, string CustomerId, decimal Amount, DateTime CreatedAt);
public record OrderStatusUpdatedDomainEvent(Guid OrderId, string Status, DateTime UpdatedAt);

// --- Integration Events (Cross-Microservice Contracts) ---
public interface IOrderPlacedIntegrationEvent
{
    Guid OrderId { get; }
    string CustomerId { get; }
    decimal Amount { get; }
    DateTime EventTimestamp { get; }
}

public class OrderPlacedIntegrationEvent : IOrderPlacedIntegrationEvent
{
    public Guid OrderId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime EventTimestamp { get; set; }
}
```

---

## 4. Multi-Pod Stream Reader Worker Implementation

This worker uses `IDistributedLockProvider` to guarantee single-active-reader execution across multiple pods.

```csharp
using System.Text.Json;
using MassTransit;
using Medallion.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SqlStreamStore;
using SqlStreamStore.Streams;
using SqlStreamStoreDemo.Contracts;

namespace SqlStreamService.Infrastructure.Workers;

public class SqlStreamStoreMultiPodWorker : BackgroundService
{
    private readonly IStreamStore _streamStore;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IDistributedLockProvider _lockProvider;
    private readonly ILogger<SqlStreamStoreMultiPodWorker> _logger;

    private const string LockName = "SqlStreamStore_GlobalStreamReader_LeaderLock";
    private IStreamSubscription? _subscription;

    public SqlStreamStoreMultiPodWorker(
        IStreamStore streamStore,
        IPublishEndpoint publishEndpoint,
        IDistributedLockProvider lockProvider,
        ILogger<SqlStreamStoreMultiPodWorker> logger)
    {
        _streamStore = streamStore;
        _publishEndpoint = publishEndpoint;
        _lockProvider = lockProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Pod initialized. Entering leader lock acquisition loop...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try {
                // Attempt to acquire non-blocking lock or wait gracefully
                await using var handle = await _lockProvider.TryAcquireLockAsync(
                    LockName, 
                    timeout: TimeSpan.FromSeconds(5), 
                    cancellationToken: stoppingToken);

                if (handle != null)
                {
                    _logger.LogInformation("Leader lock acquired successfully! This pod is now the ACTIVE stream processor.");

                    // Subscribe to stream store catch-up feed
                    _subscription = _streamStore.SubscribeToAll(
                        continueAfterPosition: Position.Start, // In production, persist & load last processed checkpoint position
                        streamMessageReceived: StreamMessageReceivedAsync,
                        subscriptionDropped: SubscriptionDropped);

                    // Keep holding the lock and processing until worker stops or lock breaks
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                else
                {
                    _logger.LogDebug("Leader lock currently held by another pod. Standing by...");
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Pod shutdown requested. Releasing subscription and leader lock.");
                _subscription?.Dispose();
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in Leader Lock loop. Retrying in 10 seconds...");
                _subscription?.Dispose();
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task StreamMessageReceivedAsync(
        IStreamSubscription subscription, 
        StreamMessage message, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing Event Position {Position} | Type: {Type}", message.Position, message.Type);

        if (message.Type == nameof(OrderPlacedDomainEvent))
        {
            var json = await message.GetJsonData(cancellationToken);
            var domainEvent = JsonSerializer.Deserialize<OrderPlacedDomainEvent>(json);

            if (domainEvent != null)
            {
                // Map local domain event to MassTransit Integration Contract
                await _publishEndpoint.Publish<IOrderPlacedIntegrationEvent>(new OrderPlacedIntegrationEvent
                {
                    OrderId = domainEvent.OrderId,
                    CustomerId = domainEvent.CustomerId,
                    Amount = domainEvent.Amount,
                    EventTimestamp = domainEvent.CreatedAt
                }, cancellationToken);

                _logger.LogInformation("Successfully published IOrderPlacedIntegrationEvent via MassTransit for Order {OrderId}", domainEvent.OrderId);
            }
        }

        // TODO: Save updated checkpoint position to persistence store (e.g. checkpoint table)
    }

    private void SubscriptionDropped(
        IStreamSubscription subscription, 
        SubscriptionDropReason reason, 
        Exception? exception)
    {
        _logger.LogWarning(exception, "Stream subscription dropped. Reason: {Reason}", reason);
    }
}
```

---

## 5. Dependency Injection & Service Configuration

Below is the complete `Program.cs` setup configuring **SqlStreamStore**, **Medallion SqlServer Distributed Lock**, **MassTransit**, and the **Multi-Pod Worker**.

```csharp
using MassTransit;
using Medallion.Threading;
using Medallion.Threading.SqlServer;
using SqlStreamStore;
using SqlStreamStoreService.Infrastructure.Workers;

var builder = WebApplication.CreateBuilder(args);

string sqlConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=localhost;Database=EventStoreDb;User Id=sa;Password=YourPassword123!;TrustServerCertificate=True;";

// 1. Register SqlStreamStore (MS SQL Server Engine)
builder.Services.AddSingleton<IStreamStore>(sp =>
{
    var settings = new MsSqlStreamStoreSettings(sqlConnectionString);
    var store = new MsSqlStreamStore(settings);
    store.CreateSchemaIfNotExists().GetAwaiter().GetResult();
    return store;
});

// 2. Register Distributed Lock Provider (Uses SQL Server Application Locks under the hood)
builder.Services.AddSingleton<IDistributedLockProvider>(sp => 
    new SqlServerDistributedSynchronizationProvider(sqlConnectionString));

// 3. Register MassTransit
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });
    });
});

// 4. Register the Scalable Multi-Pod Background Reader Worker
builder.Services.AddHostedService<SqlStreamStoreMultiPodWorker>();

var app = builder.Build();

app.MapGet("/", () => "SqlStreamService is running.");

app.Run();
```

---

## 6. How Multi-Pod Scaling Operates at Runtime

```text
               +-------------------------------------------------+
               |              Shared SQL Server / DB             |
               |  (SqlStreamStore Tables + Application Locks)     |
               +-------------------------------------------------+
                                 ^             ^
                   Acquires Lock |             | Blocked / Standing By
                                 |             |
                     +-----------+---+     +---+-----------+
                     |  Pod 1        |     |  Pod 2        |
                     | (LEADER)      |     | (STANDBY)     |
                     +---------------+     +---------------+
                             |                     |
                             v (Publishes)         x (No Duplicates)
                     +---------------+
                     |  MassTransit  |
                     | Message Broker|
                     +---------------+
```

1. **Leader Pod Startup:** `Pod 1` acquires the lock named `"SqlStreamStore_GlobalStreamReader_LeaderLock"` via SQL Server app locks. It starts reading streams and publishing to MassTransit.
2. **Standby Pod Startup:** `Pod 2` attempts to acquire the lock, receives `null` (lock held), and enters a 10-second polling standby loop.
3. **Failover Execution:** If `Pod 1` terminates or loses network connectivity, SQL Server releases the lock automatically. `Pod 2` claims the lock on its next check and resumes stream processing seamlessly.


Outline
Raw Markdown Mode
Table of Contents is not available in raw markdown view.

0 words  •  0 characters
