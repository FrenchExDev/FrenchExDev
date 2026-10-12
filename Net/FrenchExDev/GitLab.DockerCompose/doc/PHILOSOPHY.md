# GitLab.DockerCompose -- Philosophy

## 1. Why a Typed Configuration Layer?

GitLab Omnibus exposes 500+ settings via `gitlab.rb` (Ruby syntax). In raw Docker Compose, these are stuffed into a `GITLAB_OMNIBUS_CONFIG` string -- untyped, unvalidated, easy to get wrong silently.

`GitLab.DockerCompose` wraps this surface in C# models with `[Builder]` pattern:
- **Compile-time safety** -- typos and invalid combinations caught before deployment
- **IntelliSense** -- discoverability without reading docs
- **Validation** -- required fields, port ranges, URL formats, mutual exclusions (e.g., bundled vs external PostgreSQL)
- **Composability** -- models can be merged, overridden, serialized to YAML

The models render to `gitlab.rb` Ruby syntax at generation time. The developer never writes Ruby.

---

## 2. Why `IComposeFileContributor`?

Same pattern as `Traefik.DockerCompose`: each infrastructure concern is a contributor that emits its service block into a shared Docker Compose file. Benefits:

- **Single responsibility** -- GitLab knows GitLab; Traefik knows Traefik; the orchestrator composes them
- **Testable** -- each contributor can be unit-tested in isolation (emit YAML, assert structure)
- **Composable** -- HomeLab picks which contributors to activate based on `config-homelab.yaml`
- **No copy-paste** -- no manually maintained `docker-compose.yml` that drifts from the typed models

---

## 3. Omnibus: Embrace the Monolith, Externalize What Matters

GitLab's Omnibus container is deliberately monolithic -- one image runs everything. This is pragmatic for small/medium deployments (homelab, small team). The design philosophy here:

- **Default to bundled** -- PostgreSQL, Redis, Prometheus all run inside the container out of the box
- **Externalize when needed** -- flip a boolean (`postgresql['enable'] = false`) and point to an external service
- **Model both paths** -- the C# config models support bundled tuning AND external connection, with validation that prevents mixing (e.g., can't set `redis['maxmemory']` when `redis['enable'] = false`)

This mirrors real-world GitLab usage: start simple, externalize as you scale.

---

## 4. Traefik as TLS Terminator

GitLab bundles NGINX with Let's Encrypt support. Behind Traefik, this is redundant. The pattern:

1. Disable HTTPS in GitLab's NGINX (`listen_https = false`, `listen_port = 80`)
2. Disable Let's Encrypt (`letsencrypt['enable'] = false`)
3. Set proxy headers (`X-Forwarded-Proto: https`, `X-Forwarded-Ssl: on`)
4. Let Traefik handle TLS termination and certificate management

This avoids double TLS, simplifies certificate management (one place: Traefik), and keeps GitLab's internal networking plain HTTP.

The same pattern applies to Registry, Pages, and Mattermost subdomains -- each gets its own Traefik router but talks HTTP internally.

---

## 5. Companion Services as Opt-In

Not every deployment needs external PostgreSQL, Redis, MinIO, or a Runner. The contributor pattern makes these opt-in:

```yaml
# config-homelab.yaml
compose:
  gitlab: true
  gitlab_external_db: false      # use bundled PostgreSQL
  gitlab_external_redis: false   # use bundled Redis
  gitlab_runner: true            # add Runner service
  gitlab_minio: false            # no object storage
```

Each companion is a separate contributor (or a flag on the GitLab contributor) that emits its own service block and adjusts the GitLab config accordingly.

---

## 6. Health-First Orchestration

GitLab takes 3-5 minutes to boot. Companion services (Runner, MinIO bucket init) must wait. The design:

- GitLab service has a `healthcheck` using `/-/health` with generous `start_period` (300s)
- Dependents use `depends_on: { gitlab: { condition: service_healthy } }`
- No polling scripts, no sleep hacks -- Docker Compose handles the sequencing

---

## 7. Secrets Management

`gitlab-secrets.json` (in `/etc/gitlab/`) encrypts database columns. If lost, encrypted data is unrecoverable. Design implications:

- The `gitlab-config` volume is treated as critical (same tier as `gitlab-data`)
- Passwords and tokens are injected via environment variables or Docker secrets, never hardcoded in models
- The C# models use placeholder references (`${GITLAB_DB_PASSWORD}`) that resolve at deploy time
