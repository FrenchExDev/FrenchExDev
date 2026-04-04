# GitLab.DockerCompose

Docker Compose service contributor for self-hosted GitLab. Generates a fully configured `docker-compose.yml` fragment for GitLab CE/EE with optional companion services (external PostgreSQL, Redis, Runner, MinIO), Traefik integration, and structured `gitlab.rb` configuration via `GITLAB_OMNIBUS_CONFIG`.

**Status: design/specification phase** -- see [doc/](doc/) for architecture and configuration surface.

## What This Project Does

`GitLab.DockerCompose` implements `IComposeFileContributor` from `DockerCompose.Bundle` to emit a GitLab service block (and optional companions) into a composed Docker Compose file. It exposes the full GitLab Omnibus configuration surface as typed C# models with `[Builder]` pattern.

## GitLab Docker Image

GitLab ships as a single Omnibus container (`gitlab/gitlab-ee` or `gitlab/gitlab-ce`) that bundles:

| Internal Service | Role |
|-----------------|------|
| NGINX | Reverse proxy / TLS termination |
| Puma | Ruby application server |
| Sidekiq | Background job processor |
| Gitaly | Git RPC (repository access) |
| GitLab Shell | SSH access |
| GitLab Workhorse | Smart HTTP proxy (large files) |
| PostgreSQL | Database (can be externalized) |
| Redis | Cache/queues (can be externalized) |
| Container Registry | Docker image registry (optional) |
| GitLab Pages | Static site hosting (optional) |
| Prometheus | Monitoring (optional) |

Each bundled service can be disabled in favor of an external counterpart.

## Configuration Surface Summary

| Area | Key Settings |
|------|-------------|
| External URL / HTTPS | `external_url`, NGINX SSL, Let's Encrypt |
| SMTP / Email | 15+ smtp settings, 70+ provider examples |
| Container Registry | `registry_external_url`, S3/filesystem storage |
| GitLab Pages | `pages_external_url`, access control |
| PostgreSQL | Bundled tuning or external connection |
| Redis | Bundled tuning or external connection |
| Object Storage | Consolidated S3/MinIO/GCS/Azure for 9 bucket types |
| LDAP / Auth | Multi-provider LDAP, AD support |
| Backup | Path, retention, remote upload |
| Monitoring | Prometheus, exporters, Alertmanager |
| Puma | Workers, threads, memory limits |
| Sidekiq | Metrics, health checks, TLS |
| Gitaly | Storage paths, auth tokens, clustering |
| Logging | Per-service levels, rotation, syslog forwarding |

## Volumes (Mandatory)

| Container Path | Content |
|---------------|---------|
| `/etc/gitlab` | `gitlab.rb`, `gitlab-secrets.json`, SSL certs |
| `/var/log/gitlab` | All service logs |
| `/var/opt/gitlab` | Git repos, database, uploads, backups |

## Ports

| Port | Service |
|------|---------|
| 80/443 | HTTP/HTTPS (web UI) |
| 22 | SSH (Git) |
| 5050 | Container Registry |
| 8060 | GitLab Pages |

## Companion Services

| Service | Image | Role |
|---------|-------|------|
| PostgreSQL | `postgres:16-alpine` | External database |
| Redis | `redis:7-alpine` | External cache/queues |
| GitLab Runner | `gitlab/gitlab-runner:alpine` | CI/CD executor |
| MinIO | `minio/minio` | S3-compatible object storage |

## Documentation

- [ARCHITECTURE.md](doc/ARCHITECTURE.md) -- project structure, C# models, contributor pattern
- [HOW-TO.md](doc/HOW-TO.md) -- full configuration reference with examples
- [PHILOSOPHY.md](doc/PHILOSOPHY.md) -- design decisions, why typed config, why contributor pattern
