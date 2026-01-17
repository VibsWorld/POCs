# HttpClient + HttpClientFactory vs Refit.HttpClientFactory
* Status: Await consultation with others
* Date: 2026-01-17

## Context
Choosing between `Refit.HttpClientFactory` vs. raw `HttpClient with HttpClientFactory` for production-grade API requests in ASP.NET Core 8 or above

## Decision
Refit.HttpClientFactory for its productivity, maintainability, and built-in resilience features

## Consequences
 [Refit](https://github.com/reactiveui/refit) is a REST library that turns HTTP APIs into live interfaces, supporting registration via ASP.NET Core's IHttpClientFactory.

**Pros**
- **Declarative:** API contracts via interfaces and attributes minimize code.
- **Strong Typing:** Auto-generates strongly-typed clients for requests/responses.
- **Polly Integration:** Attach Polly policies globally or per-client.
- **Rate Limiting:** Plug in custom handlers for rate-limiting, retry, etc.
- **Testability:** Interfaces are simple to mock in unit tests.

**Known Cons**
- **Abstraction Overhead:** Less control for advanced/custom scenarios.
- **Debugging:** Attribute or serialization errors can be subtler to trace.
- **Dependency:** API calls depend on Refit’s abstractions.

**Conveniences**
- **Serialization:** Handles JSON and others out of the box; easy to customize.
- **Error Handling:** Built-in extensible error propagation.
- **Integration:** Seamless with IHttpClientFactory and DI.

## Alternatives considered
### Use `HttpClient` with `IHttpClientFactory`
**Pros**
- **Full Control:** Total customization over requests, message handlers, serialization, etc.
- **Integration:** Easily integrates with libraries like Polly for resilience and rate-limiting.
- **Flexibility:** Use typed, named, or anonymous clients as needed.
- **Maturity:** First-class, widely adopted in .NET Core ecosystem.

**Known Cons**
- **Boilerplate:** More code required for serialization, mapping, error handling.
- **Verbosity:** Less concise than interface-driven libraries.

**Conveniences**
- **Http Resilience:** Complete Polly support for retries, circuit breaker, timeouts, and more.
- **Rate Limiting:** Supported through DelegatingHandlers or Polly policies.
- **Authentication, Logging, Tracing:** Easily implemented via handlers.

## Feature Comparison Table

| Feature                   | Refit.HttpClientFactory     | HttpClient + IHttpClientFactory  |
|---------------------------|----------------------------|----------------------------------|
| Developer Productivity    | High (attributes/interfaces)| Medium-Low (manual code)        |
| Typed API Mapping         | Yes                        | Manual                          |
| Request/Response Mapping  | Automatic                  | Manual                          |
| Custom Headers            | Attributes or handler      | Manual                          |
| Http Resilience (Polly)   | Yes (via handlers)         | Yes (via handlers)              |
| Rate Limiting             | Handler/Polly              | Handler/Polly                   |
| Extensibility             | Good                       | Very High                       |
| Error Handling            | Built-in/Extensible        | Manual                          |
| Debuggability             | Moderate                   | Full control                    |



