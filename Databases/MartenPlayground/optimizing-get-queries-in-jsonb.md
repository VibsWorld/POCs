### Optimizing JSONB GET operations
Consider the following scenario in Postgres SQL
```sql
-- Find all users who have a "country" field in JSON set to "GB"
SELECT * FROM users WHERE profile @> '{"country": "GB"}';
```
Even though this JSONB query will work right out of box but there will be performance overheads once we get rows over 100K. (pl refer <https://medium.com/hackernoon/how-to-query-jsonb-beginner-sheet-cheat-4da3aa5082a3> to learn how to build JSONB queries).
Example to use LINQ to perform GET operations for the JSON fields. 
```csharp
var openIssues = await session
    .Query<Issue>()
    .Where(x => x.IsOpen)
    .OrderByDescending(x => x.Opened)
    .Take(10)
    .ToListAsync().ConfigureAwait(false);

await using var querySession = store.QuerySession();

// Load by Id
var existingShipment = await querySession.LoadAsync<Shipment>(shipment.Id);
Console.WriteLine($"Loaded shipment {existingShipment!.Id} with status {existingShipment.Status}");

// Filter by destination
var shipmentsToChicago = await querySession
    .Query<Shipment>()
    .Where(x => x.Destination == "Chicago")
    .ToListAsync();
Console.WriteLine($"Found {shipmentsToChicago.Count} shipments to Chicago");

// Count active shipments per driver
var active = await querySession
    .Query<Shipment>()
    .CountAsync(x => x.AssignedDriverId == driver.Id && x.Status != "Delivered");
Console.WriteLine($"Driver {driver.Name} has {active} active shipments");
```
*Performance Optimizations Suggestions for JSONB Indexations*

If you frequently query by certain fields, consider duplicating them as indexed columns:
```csharp
opts.Schema.For<Shipment>().Duplicate(x => x.Status);
#pragma warning disable CS8603 // Possible null reference return.
opts.Schema.For<Shipment>().Duplicate(x => x.AssignedDriverId);
#pragma warning restore CS8603 // Possible null reference return.
```
This improves query performance by creating indexes on those columns outside the JSON.
As I'll show later in this article, it's not only possible to query from within the structured JSON data, but you can also add computed indexes in Marten that work within the stored JSON data.

(Note: If you want to avoid native postgres indexation SQLs that are listed ahead then you can simply use C# extension methods in the Marten framework to create indexes and skip the rest of the article. Refer here for the Marten Methods for indexations <https://martendb.io/documents/indexing/computed-indexes.html>)

#### GIN Indexes: The General-Purpose Choice
A **GIN** (Generalized Inverted Index) is the most powerful and common way to index JSONB data. It's designed to handle composite values where items (like keys or values in a JSON object) can appear many times. Think of it as creating an index on every key and value within the JSONB document.
This makes it ideal for queries that check for the existence of keys or key-value pairs.
Indexing the Entire JSONB Column
This is the most flexible approach. It allows you to efficiently query for any key or value anywhere in the JSON document.
```sql
CREATE INDEX idx_gin_my_data ON my_table USING GIN (jsonb_column);

---Example: Let's say you have a table users with a JSONB column named profile.
CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    profile JSONB
);

-- Create a GIN index on the 'profile' column
CREATE INDEX idx_gin_users_profile ON users USING GIN (profile);
```
This index will significantly speed up queries using these operators:
* `@>` (contains): Does the left JSON contain the right JSON?
* `?` (exists): Does the string exist as a top-level key?
* `?|` (exists any): Do any of the strings in the array exist as top-level keys?
* `?&` (exists all): Do all of the strings in the array exist as top-level keys?

#### B-Tree Indexes: For Specific Values & Sorting  
A standard B-tree index is perfect when we consistently query or sort by the value of a specific, known key path within your JSONB data. It can't index the whole object like GIN, but it's much faster for equality `(=)`, comparison `(<, >)`, and `ORDER BY` operations on a single, extracted value.

To create a B-tree index, we must index an expression that extracts the value as text using the ->> operator.
```sql
CREATE INDEX idx_btree_my_key ON my_table ((jsonb_column->>'key_name'));
--- Example: Using the same users table, if you frequently search for users by their email address stored inside the profile JSON.

-- Create a B-tree index on the 'email' value
CREATE INDEX idx_btree_users_email ON users ((profile->>'email'));
-This index is highly effective for queries like:
-- Find a specific user by email (very fast)
SELECT * FROM users WHERE profile->>'email' = 'example@email.com';

-- Get all users and sort them by their email address
SELECT * FROM users ORDER BY profile->>'email';
```
NESTED JSONB fields
```sql
-- Index the city inside a nested 'address' object
CREATE INDEX idx_btree_users_city ON users ((profile->'address'->>'city'));

-- This query will now be very fast
SELECT * FROM users WHERE profile->'address'->>'city' = 'Manchester';
```
#### **Which One Should I Use?** 
Here's a quick guide to help you decide:

* **Use a GIN index when:**  
  * You need to search for arbitrary keys or values within the `JSONB` document.  
  * Your queries are varied and you don't know which keys you'll be searching for ahead of time.  
  * You are primarily using the containment operator (`@>`) or key existence operators (`?`, `?|`, `?&`).  
* **Use a B-tree index on an expression when:**  
  * You frequently filter, compare (`=`, `<`, `>`), or sort by the value of a **single, specific key**.  
  * You need fast equality checks on a particular field (like a user's email or a status flag).  
  * The indexed value has high cardinality (many distinct values).
