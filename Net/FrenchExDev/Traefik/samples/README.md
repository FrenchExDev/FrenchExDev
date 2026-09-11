# Traefik samples

Realistic Traefik configuration files used by `RealisticRoundTripTests` to
catch edge cases the minimal fixtures in the test project don't reach.

## Files

- `realistic-dynamic.yaml` — dynamic config with HTTP + TCP routers, multiple
  middleware types (basic auth, strip prefix, rate limit, headers), weighted
  load balancing with health checks, and TLS options. Hand-written based on
  patterns from the [Traefik docs](https://doc.traefik.io/traefik/).

## How to use

```csharp
var yaml = File.ReadAllText("samples/realistic-dynamic.yaml");
var result = TraefikSerializer.TryDeserializeDynamic(yaml);
```
