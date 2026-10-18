# Production Implementation Plan: Event Sourced Domain Events & MassTransit Integration

This document outlines a detailed architecture and implementation plan for event-sourcing domain events in .NET microservices using **Marten** (PostgreSQL) and **SqlStreamStore** (SQL Server, MySQL, or PostgreSQL) while publishing external integration contracts via **MassTransit**.

---

## 1. Architectural Principles & Boundaries

In a clean Domain-Driven Design (DDD) + Event-Driven Architecture (EDA):

1. **Domain Events (Internal):**
   * Capture fine-grained state changes within an Aggregate boundary (e.g., `OrderCreatedDomainEvent`, `PaymentReceivedDomainEvent`).
   * Stored in the local event store (Marten or SqlStreamStore).
   * **Never** published directly across microservice boundaries to prevent tight coupling.

2. **Integration Events (External):**
   * Represent official public contracts between microservices (e.g., `OrderSubmittedIntegrationEvent`).
   * Published to a central message broker (RabbitMQ, Azure Service Bus) via **MassTransit**.
   * Processed asynchronously by external consumer services.

3. **Dual-Write Avoidance:**
   * Saving to the local event store and publishing to the message broker must occur in a transactional, fault-tolerant manner.
   * Handled via **Outbox Pattern** or **Event Store Daemon/Subscription Hooks**.

---

## 2. Solution Directory Structure

```text
src/
├── BuildingBlocks/
│   ├── EventSourcing.Abstractions/
│   │   ├── IDomainEvent.cs
│   │   └── IAggregateRoot.cs
│   └── EventSourcing.MassTransit/
│       └── OutboxExtensions.cs
├── Services/
│   ├── MartenService/                     # Dedicated PostgreSQL Implementation
│   │   ├── MartenService.Domain/
│   │   │   └── Aggregates/
│   │   │       └── Order.cs
│   │   ├── MartenService.Infrastructure/
│   │   │   └── Projections/
│   │   │       └── IntegrationEventPublisherDaemon.cs
│   │   └── MartenService.Api/
│   │       └── Program.cs
│   └── SqlStreamStoreService/             # Database-Agnostic Implementation
│       ├── SqlStreamService.Domain/
│       │   └── Aggregates/
│       │       └── Order.cs
│       ├── SqlStreamService.Infrastructure/
│       │   └── Subscriptions/
│       │       └── SqlStreamStoreCatchUpWorker.cs
│       └── SqlStreamService.Api/
│           └── Program.cs
└── docker-compose.yml
```

---

## 3. Implementation A: Marten (PostgreSQL)

### 3.1. Overview
Marten leverages PostgreSQL’s `JSONB` capabilities to provide document storage and event sourcing. It handles projections and event streaming out of the box.

### 3.2. Code Setup

#### Domain Model (`Order.cs`)
```csharp
namespace MartenService.Domain.Aggregates;

public record OrderCreatedDomainEvent(Guid OrderId, string CustomerId, decimal Amount);
public record OrderPaidDomainEvent(Guid OrderId, DateTime PaidAt);

public class Order
{
    public Guid Id { get; private set; }
    public string CustomerId { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public bool IsPaid { get; private set; }

    public Order() { }

    // Apply methods construct state from events (Live Aggregation)
    public void Apply(OrderCreatedDomainEvent @event)
    {
        Id = @event.OrderId;
        CustomerId = @event.CustomerId;
        Amount = @event.Amount;
    }

    public void Apply(OrderPaidDomainEvent @event)
    {
        IsPaid = true;
    }
}
```

#### MassTransit Integration Daemon (`IntegrationEventPublisherDaemon.cs`)
```csharp
using Marten.Events.Projections;
using MassTransit;
using MartenService.Domain.Aggregates;

namespace MartenService.Infrastructure.Projections;

public record OrderSubmittedIntegrationEvent(Guid OrderId, decimal TotalAmount);

public class IntegrationEventPublisherProjection : EventProjection
{
    private readonly IPublishEndpoint _publishEndpoint;

    public IntegrationEventPublisherProjection(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async Task Project(OrderCreatedDomainEvent @event, CancellationToken ct)
    {
        // Map domain event to external contract and publish to MassTransit broker
        var integrationEvent = new OrderSubmittedIntegrationEvent(@event.OrderId, @event.Amount);
        await _publishEndpoint.Publish(integrationEvent, ct);
    }
}
```

#### Multi-Pod & API Configuration (`Program.cs`)
```csharp
using Marten;
using Marten.Events.Daemon.Resiliency;
using Marten.Events.Projections;
using MassTransit;
using MartenService.Infrastructure.Projections;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Marten Event Store
builder.Services.AddMarten(options =>
{
    options.Connection(builder.Configuration.GetConnectionString("Postgres")!);
    options.Projections.Add<IntegrationEventPublisherProjection>(ProjectionLifecycle.Async);
})
// CRITICAL FOR MULTI-POD SCALING:
// HotCold daemon mode uses Postgres advisory locks to ensure ONLY ONE POD runs the Async Daemon.
.AddAsyncDaemon(DaemonMode.HotCold);

// 2. Configure MassTransit with Bus Outbox
builder.Services.AddMassTransit(x =>
{
    x.AddMartenOutbox(); // Integrates MassTransit Outbox with Marten session
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });
    });
});

var app = builder.Build();
app.Run();
```

---

## 4. Implementation B: SqlStreamStore (SQL Server / MySQL / Postgres)

### 4.1. Overview
`SqlStreamStore` is a low-level engine-agnostic library. It supports SQL Server (`MsSqlStreamStore`), MySQL (`MySqlStreamStore`), PostgreSQL (`PostgresStreamStore`), or In-Memory.

### 4.2. Code Setup

#### Stream Writer & Event Appender (`SqlStreamService.cs`)
```csharp
using SqlStreamStore;
using SqlStreamStore.Streams;
using System.Text.Json;

namespace SqlStreamService.Infrastructure;

public record OrderPlacedDomainEvent(Guid OrderId, string CustomerId, decimal Amount);

public class SqlStreamStoreService
{
    private readonly IStreamStore _streamStore;

    public SqlStreamStoreService(IStreamStore streamStore)
    {
        _streamStore = streamStore;
    }

    public async Task AppendDomainEventAsync(Guid orderId, object @event, CancellationToken ct)
    {
        var streamId = new StreamId($"order-{orderId}");
        var eventType = @event.GetType().Name;
        var eventJson = JsonSerializer.Serialize(@event);

        var message = new NewStreamMessage(
            messageId: Guid.NewGuid(),
            type: eventType,
            jsonData: eventJson
        );

        await _streamStore.AppendToStream(streamId, ExpectedVersion.Any, new[] { message }, ct);
    }
}
```

#### Catch-up Subscription Worker (`SqlStreamStoreCatchUpWorker.cs`)
```csharp
using SqlStreamStore;
using SqlStreamStore.Streams;
using MassTransit;
using System.Text.Json;
using Microsoft.Extensions.Hosting;

namespace SqlStreamService.Infrastructure.Subscriptions;

public record OrderShippedIntegrationEvent(Guid OrderId);

public class SqlStreamStoreCatchUpWorker : BackgroundService
{
    private readonly IStreamStore _streamStore;
    private readonly IPublishEndpoint _publishEndpoint;

    public SqlStreamStoreCatchUpWorker(IStreamStore streamStore, IPublishEndpoint publishEndpoint)
    {
        _streamStore = streamStore;
        _publishEndpoint = publishEndpoint;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Subscribe to All Streams to catch up on new domain events
        _streamStore.SubscribeToAll(
            continueAfterPosition: Position.Start,
            streamMessageReceived: async (subscription, message, ct) =>
            {
                if (message.Type == nameof(OrderPlacedDomainEvent))
                {
                    var jsonData = await message.GetJsonData(ct);
                    var domainEvent = JsonSerializer.Deserialize<OrderPlacedDomainEvent>(jsonData);
                    
                    if (domainEvent != null)
                    {
                        // Map and dispatch via MassTransit
                        await _publishEndpoint.Publish(new OrderShippedIntegrationEvent(domainEvent.OrderId), ct);
                    }
                }
            });

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
```

---

## 5. Multi-Pod Kubernetes (AKS) Scaling Strategy

When scaling services across $N$ pods, the key considerations are:

### A. MassTransit Consumers (Horizontal Auto-Scaling)
* **Status:** Out-of-the-box safe.
* **Mechanism:** MassTransit consumers connect to broker queues using competing consumers. Broker balances message delivery across all active pod replicas. No duplicate execution occurs.

### B. Marten Async Daemon (Leader Election)
* **Status:** Requires `DaemonMode.HotCold`.
* **Mechanism:** Marten uses PostgreSQL advisory locks to designate **one active leader pod** to process projections and publish integration events. Secondary pods stay on standby. If the leader pod crashes, a secondary pod claims the advisory lock within seconds.

### C. SqlStreamStore Subscriptions (Distributed Locking)
* **Status:** Requires distributed locking mechanism across pods.
* **Mechanism:** Wrap the `SubscribeToAll` background execution inside a distributed lock (e.g., using `Medallion.Threading.Postgres` or `Medallion.Threading.SqlServer`):

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    // Ensure only one pod runs this catch-up subscription worker at a time
    await using var @lock = await _distributedLockProvider.AcquireLockAsync("sqlstreamstore-reader-lock", cancellationToken: stoppingToken);

    _streamStore.SubscribeToAll(...);
    await Task.Delay(Timeout.Infinite, stoppingToken);
}
```

---

## 6. Verification & Production Checklist

1. **Transaction Boundaries:** Verify that domain state updates and domain event appending occur inside a single local transaction.
2. **Idempotency:** Ensure MassTransit consumers implement message deduplication or idempotent processing using the `MessageId`.
3. **Database Indexing:** Ensure `Position` or event timestamp columns in SQL Server/MySQL have adequate indexes for fast stream polling.


Outline
Raw Markdown Mode
Table of Contents is not available in raw markdown view.

0 words  •  0 characters
