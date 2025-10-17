 Proposal \- Using **Marten ORM framework** to leverage benefits of [Postgres JSONB framework](https://www.postgresql.org/docs/current/datatype-json.html) to implement a unique flexibility of `No-SQL Document database` along with `RDBMS’s Transactional ACID compliance`.  

⬅️ Please navigate document tabs on the LEFT or click links below ⬅️

1. [Marten Vs Other ORMs](MartenPlayground#marten-vs-other-orms)  
2. [Optimizing JSONB GET operations](?tab=t.kdyz6836gnhs)  
3. [Where NOT to use Marten](?tab=t.kr7nyp6b8eu3)  
4. [Marten Vs Eventstore](?tab=t.tyqynef560lc)   
5. [Event Source Aggregating](?tab=t.1ua32w8nyrvu) 

Site link \- [https://martendb.io/](https://martendb.io/) 

Nuget Package Info \- [`https://www.nuget.org/packages/Marten/`](https://www.nuget.org/packages/Marten/)

Sample Repository for POC   
[https://github.com/VibsWorld/POCs/tree/main/Databases/MartenPlayground](https://github.com/VibsWorld/POCs/tree/main/Databases/MartenPlayground) 

A wonderful way to get start via Freight Shipping Service \- [https://martendb.io/tutorials/getting-started.html](https://martendb.io/tutorials/getting-started.html) 

### Marten Vs Other ORMs
When saving data in a .NET application using PostgreSQL, `Marten` offers several significant advantages over `Dapper` by providing a higher level of abstraction and **embracing a document database approach** which relates more closely with `Domain Driven Design`. 

While Dapper excels at high-performance querying and giving developers **direct SQL control**, Marten simplifies the act of data persistence, reducing boilerplate code and development overhead.

**Main Advantages over Dapper** *(Not discussing here ENTITY Framework pros/cons intentionally business has already decided to use Dapper over EF for now)*

----------------------------------------------------------------------------
#### Unit of Work and Automatic Change Tracking

Marten's `IDocumentSession` implements the `Unit of Work` pattern, which is one of its most powerful features for data persistence. It automatically tracks changes to your C\# objects, figures out what needs to be inserted or updated, and batches all operations into a single transaction when you call `SaveChangesAsync()`  
**Ref:** [https://www.codemag.com/Article/2205051/Fast-Application-Persistence-with-Marten](https://www.codemag.com/Article/2205051/Fast-Application-Persistence-with-Marten)   
With Marten:  
You simply load an object, modify it, and tell the session to save the changes. Marten handles the rest.

**Example 1**
```csharp
// Assumes 'session' is an injected IDocumentSession
var user = await session.LoadAsync<User>(userId);
user.LastLogin = DateTime.UtcNow;
user.Address = "New Address";

session.Store(user); // Stages the user object for an update
await session.SaveChangesAsync(); // A single transaction commits changes
```

As `user` goes the registration process then we keep updating the field

```csharp
user.status = "Active";
user.Shipment.Status = "Pickup Up";
session.store(user);
await session.SaveCHangesAsync();
```

Marten uses PostgreSQL's `INSERT ... ON CONFLICT DO UPDATE` under the hood to perform upsert i.e if you provide user**.**Id then its an `Update` else an `Insert`. 

We use a session (`LightweightSession`) to interact with the database. *This pattern is similar to an EF Core DbContext or a NHibernate session*. The session is a unit of work; we save changes at the end (which wraps everything in a DB transaction).Calling `Store`(user) tells Marten to stage that document for saving. `SaveChangesAsync()` actually **commits** it to PostgreSQL.

However, one **drawback** of the document-only approach is that we lose the historical changes. Each update overwrites the previous state. If we later want to know when a shipment was picked up or delivered, we have those timestamps, but what if we need more detail or want an audit trail? We might log or archive old versions, but that gets complex. ***This is where [event sourcing comes in](https://martendb.io/tutorials/evolve-to-event-sourcing.html)***. Instead of just storing the final state, we capture each state change as an event.

**Example 2** *(More Advanced that involves to and fro operation)*
```csharp
var customer = new Customer
    {
        Name = "Acme",
        Region = "North America",
        Class = "first"
    };
     
    // Marten has "upsert", insert, and update semantics
    session.Insert(customer);
     
    // Partial updates to a range of Customer documents
    // by a LINQ filter
    session.Patch<Customer>(x => x.Region == "EMEA")
        .Set(x => x.Class, "First");
 
    // Both the above operations happen in one 
    // ACID transaction
    await session.SaveChangesAsync();
```
**With Dapper:**  
You are responsible for everything. You must manually write the UPDATE SQL statement, create a database connection, and execute the command. This requires more code and is more prone to error if you forget to update a field in your SQL string.

For example
```csharp
// Assumes 'connection' is an IDbConnection
var user = await connection.QuerySingleAsync<User>("SELECT data FROM users WHERE id = @Id", new { Id = userId });
user.LastLogin = DateTime.UtcNow;
user.Address = "New Address";

await connection.ExecuteAsync(
    "UPDATE users SET data = @Data WHERE id = @Id",
    new { Data = user, Id = userId } // Assumes the 'user' object can be serialized to JSON
);
```
#### Seamless Document-Based Persistence

Marten leverages PostgreSQL's powerful `JSONB` capabilities to store your C\# objects as documents. **This eliminates the** **`object-relational impedance mismatch`**, meaning you don't have to flatten complex objects into a relational table structure. You can save rich, nested objects directly.

Advantage: You work with your domain objects naturally without writing mapping code. Dapper, while it can work with JSON, is fundamentally designed for a relational world, requiring you to manage the serialization and mapping between your objects and table columns yourself.

**Note:**  
We’ll discuss Indexation and GET performance related issues and their resolutions later in the document.  

#### Automatic Schema Management and Evolution
Marten can manage your database schema for you. Based on your C\# classes, **it can automatically create the necessary tables, functions, and indexes on the fly** *(at the same it gives you control over Data Annotations like we had in Entity Framework to change some properties like changing name of Identity column)*. As your classes evolve (e.g., you add a new property), Marten can automatically update the database schema to reflect those changes.  
Ref: [https://jeremydmiller.com/2024/08/29/why-and-how-marten-is-a-great-document-database](https://jeremydmiller.com/2024/08/29/why-and-how-marten-is-a-great-document-database) 

**Advantage**: This dramatically speeds up development and simplifies migrations. With Dapper, schema management is entirely manual. You must write and maintain all the Data Definition Language (DDL) scripts (CREATE TABLE, ALTER TABLE, etc.) yourself.

#### Built-in Event Sourcing Engine	

For applications designed with an Event Sourcing architecture, Marten is an unparalleled choice. It's not just a document database library; **it's also a full-featured event store**. Saving data means appending events to a stream, which Marten handles natively.

With Marten:  
Saving a series of events is a first-class operation.

```csharp
var userRegistered = new UserRegistered(userId, "test@example.com");
var userNameUpdated = new UserNameUpdated(userId, "Test User");

// Appending events is the primary way of saving state
session.Events.Append(userId, userRegistered, userNameUpdated);
await session.SaveChangesAsync();
```
Advantage: With Dapper, you would have to build the entire event sourcing infrastructure from the ground up—designing tables for event streams, managing concurrency, and handling event serialization. **This is a massive undertaking that Marten provides out of the box**.

Ref: [https://falberthen.github.io/posts/ecommerceddd-pt4/](https://falberthen.github.io/posts/ecommerceddd-pt4/)  
---

#### Simplified "Upsert" and Bulk Insert Operations

Marten's `Store()` method conveniently handles both `INSERT` and `UPDATE` operations "**upsert**". It checks if a document with the given ID exists and performs the correct database command. Dapper requires you to write logic to differentiate between new and existing records.  
Ref: [https://martendb.io/documents/storing\#upsert-with-store](https://martendb.io/documents/storing#upsert-with-store) 

Additionally, Marten provides a highly efficient `BulkInsert()` method that leverages `PostgreSQL's COPY command` for loading large amounts of data quickly, abstracting away the complexity of this operation.  
Ref: [https://martendb.io/documents/storing\#bulk-loading](https://martendb.io/documents/storing#bulk-loading) 

---

Tracking and Logging

`Marten` utilizes `ISessionFactory` which we can override to create our own traces.   
[https://martendb.io/diagnostics.html\#session-specific-logging](https://martendb.io/diagnostics.html#session-specific-logging) 

Telemetry Statics  
[https://martendb.io/otel.html\#open-telemetry-and-metrics](https://martendb.io/otel.html#open-telemetry-and-metrics) 
```csharp
using var store = DocumentStore.For(opts =>
{
    opts.Connection("some connection string");

    // Track Marten connection usage
    opts.OpenTelemetry.TrackConnections = TrackLevel.Normal;
});
```
#### Other Advantages

* Excellent Documentation Support and navigation which is a typical USP for .NET ecosystem.   
* Paid Support also available (Like in Masstransit)

## Other Considerations

If you registered Marten with `AddMarten()` under `ServiceCollection`, the `IDocumentSession` and `IQuerySession` services are registered with a **Scoped lifetime** by default. You should just inject a session directly in most cases. `IDocumentStore` is [registered](https://martendb.io/tutorials/getting-started.html#storing-and-retrieving-documents-with-marten) with Singleton scope, but you'll rarely need to interact with that service. 

