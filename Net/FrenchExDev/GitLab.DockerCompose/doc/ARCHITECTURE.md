# GitLab.DockerCompose -- Architecture

## 1. Overview

GitLab.DockerCompose is a `DockerCompose.Bundle` contributor that emits a fully configured GitLab service (and optional companions) into a Docker Compose file. Configuration is expressed as typed C# models using the `[Builder]` pattern, then rendered into `GITLAB_OMNIBUS_CONFIG` environment variable entries.

**Status: design phase.** This document describes the planned architecture.

---

## 2. Project Decomposition

```
GitLab.DockerCompose/
  src/
    FrenchExDev.Net.GitLab.DockerCompose/        IComposeFileContributor + config models
  test/
    FrenchExDev.Net.GitLab.DockerCompose.Tests/   Unit tests
```

---

## 3. Dependency Graph

```
DockerCompose.Bundle
  ↑
GitLab.DockerCompose  →  Builder (for typed config models)
  ↑
HomeLab               →  (consumer: orchestrates contributors)
```

---

## 4. GitLab Omnibus Container Architecture

The `gitlab/gitlab-ee` (or `gitlab/gitlab-ce`) image is an all-in-one container managed by `runit` (process supervisor) and configured via `gitlab-ctl reconfigure`.

### Internal Process Tree

```
runit (PID 1)
 ├── nginx           (reverse proxy, ports 80/443)
 ├── puma            (Rails app server, socket/8080)
 ├── sidekiq         (background jobs, port 8082 metrics)
 ├── gitaly          (Git RPC, port 8075)
 ├── gitlab-shell    (SSH, port 22)
 ├── gitlab-workhorse (smart HTTP proxy)
 ├── postgresql      (database, port 5432) -- optional, can be external
 ├── redis           (cache/queues, port 6379) -- optional, can be external
 ├── registry        (container registry, port 5000) -- optional
 ├── gitlab-pages    (static hosting, port 8090) -- optional
 ├── prometheus      (monitoring, port 9090) -- optional
 ├── alertmanager    -- optional
 ├── node-exporter   -- optional
 └── logrotate
```

Each service can be individually enabled/disabled and configured.

### Configuration Flow

```
GITLAB_OMNIBUS_CONFIG (env var, Ruby syntax)
  ↓
/etc/gitlab/gitlab.rb (file, Ruby syntax)
  ↓
gitlab-ctl reconfigure (Chef converge)
  ↓
Service configs generated (/var/opt/gitlab/...)
  ↓
runit restarts affected services
```

`GITLAB_OMNIBUS_CONFIG` is evaluated BEFORE `gitlab.rb` -- it's the Docker-native config mechanism.

---

## 5. Docker Compose Service Model

### 5.1 Core GitLab Service

```yaml
services:
  gitlab:
    image: gitlab/gitlab-ee:latest
    hostname: gitlab.example.com
    restart: always
    shm_size: '256m'                              # required for Prometheus
    environment:
      GITLAB_OMNIBUS_CONFIG: |
        external_url 'https://gitlab.example.com'
        # ... all gitlab.rb settings
      GITLAB_ROOT_PASSWORD: changeme              # first run only
    volumes:
      - gitlab-config:/etc/gitlab
      - gitlab-logs:/var/log/gitlab
      - gitlab-data:/var/opt/gitlab
    ports:
      - "80:80"
      - "443:443"
      - "22:22"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost/-/health"]
      interval: 30s
      timeout: 10s
      retries: 5
      start_period: 300s                          # GitLab takes 3-5 min to boot
    deploy:
      resources:
        limits:
          memory: 8G                              # minimum 4G, 8G recommended
```

### 5.2 Companion Services (Optional)

```yaml
  postgresql:
    image: postgres:16-alpine
    restart: always
    environment:
      POSTGRES_DB: gitlabhq_production
      POSTGRES_USER: gitlab
      POSTGRES_PASSWORD: ${GITLAB_DB_PASSWORD}
    volumes:
      - postgresql-data:/var/lib/postgresql/data

  redis:
    image: redis:7-alpine
    restart: always
    command: redis-server --requirepass ${GITLAB_REDIS_PASSWORD}
    volumes:
      - redis-data:/data

  gitlab-runner:
    image: gitlab/gitlab-runner:alpine
    restart: always
    volumes:
      - runner-config:/etc/gitlab-runner
      - /var/run/docker.sock:/var/run/docker.sock
    depends_on:
      gitlab:
        condition: service_healthy

  minio:
    image: minio/minio:latest
    restart: always
    command: server /data --console-address ":9001"
    environment:
      MINIO_ROOT_USER: ${MINIO_ROOT_USER}
      MINIO_ROOT_PASSWORD: ${MINIO_ROOT_PASSWORD}
    volumes:
      - minio-data:/data
```

---

## 6. Health Endpoints

| Endpoint | Purpose | Response |
|----------|---------|----------|
| `/-/health` | Liveness | `GitLab OK` (200) or 503 |
| `/-/liveness` | App server alive | `{"status":"ok"}` |
| `/-/readiness` | Ready for traffic | JSON with component status |
| `/-/readiness?all=1` | Deep check (DB, Redis, Gitaly) | JSON with all deps |

Use `/-/health` for Docker HEALTHCHECK (simplest, fastest).

---

## 7. Volumes

| Volume | Container Path | Content | Critical |
|--------|---------------|---------|----------|
| `gitlab-config` | `/etc/gitlab` | `gitlab.rb`, `gitlab-secrets.json`, SSL certs, trusted-certs | Yes -- secrets |
| `gitlab-logs` | `/var/log/gitlab` | All service logs (runit-managed) | No -- regenerated |
| `gitlab-data` | `/var/opt/gitlab` | Git repos, DB, uploads, registry, backups, pages | Yes -- all data |

All three must be persisted. `gitlab-config` contains `gitlab-secrets.json` which is unrecoverable if lost (encrypts DB columns).

---

## 8. Port Map

| Port | Service | Notes |
|------|---------|-------|
| 80 | HTTP (NGINX) | Main web UI |
| 443 | HTTPS (NGINX) | TLS-terminated web UI |
| 22 | SSH (GitLab Shell) | Git SSH; often remapped to 2224 |
| 5050 | Container Registry | Same-domain registry default |
| 5000 | Registry internal | Not exposed externally |
| 8060 | GitLab Pages | Pages daemon |
| 9090 | Prometheus | Internal monitoring |
| 8080 | Puma metrics | Internal |
| 8082 | Sidekiq metrics | Internal |

---

## 9. C# Model Design (Planned)

Each configuration area maps to a `[Builder]`-annotated model:

```
GitLabConfig                        (root)
  ├── ExternalUrlConfig             (URL, HTTPS, Let's Encrypt)
  ├── NginxConfig                   (listen port, SSL, proxy headers)
  ├── SmtpConfig                    (SMTP settings)
  ├── RegistryConfig                (container registry)
  ├── PagesConfig                   (GitLab Pages)
  ├── PostgresqlConfig              (bundled or external)
  ├── RedisConfig                   (bundled or external)
  ├── ObjectStorageConfig           (consolidated S3/MinIO/GCS/Azure)
  ├── LdapConfig                    (authentication)
  ├── BackupConfig                  (path, retention, remote)
  ├── PumaConfig                    (workers, threads, memory)
  ├── SidekiqConfig                 (metrics, health)
  ├── GitalyConfig                  (storage, auth)
  ├── PrometheusConfig              (monitoring)
  └── LoggingConfig                 (levels, rotation, forwarding)
```

Each model renders to `gitlab.rb` Ruby syntax via a `GitLabRbRenderer`.
