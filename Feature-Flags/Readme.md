# Implement Feature Flag Service in ASP.NET Core 8 Application

#### Create table to store feature flags
```sql
CREATE TABLE feature_flags (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) UNIQUE NOT NULL,
    is_enabled BOOLEAN NOT NULL DEFAULT false,
    last_updated TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

```
