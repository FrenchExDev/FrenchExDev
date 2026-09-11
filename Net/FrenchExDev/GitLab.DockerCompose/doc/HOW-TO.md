# GitLab.DockerCompose -- Configuration Reference

Complete reference for GitLab Omnibus configuration via Docker Compose. All settings go into `GITLAB_OMNIBUS_CONFIG` (Ruby syntax) or a mounted `/etc/gitlab/gitlab.rb`.

---

## 1. External URL and HTTPS

```ruby
external_url 'https://gitlab.example.com'
```

### Let's Encrypt (automatic when URL uses `https://`)

```ruby
letsencrypt['enable'] = true
letsencrypt['contact_emails'] = ['admin@example.com']
letsencrypt['auto_renew'] = true
letsencrypt['auto_renew_hour'] = 0
letsencrypt['auto_renew_minute'] = 0
letsencrypt['auto_renew_day_of_month'] = "*/4"
```

### Manual Certificates

```ruby
letsencrypt['enable'] = false
nginx['ssl_certificate'] = "/etc/gitlab/ssl/gitlab.example.com.crt"
nginx['ssl_certificate_key'] = "/etc/gitlab/ssl/gitlab.example.com.key"
```

### Behind Reverse Proxy (Traefik)

```ruby
external_url 'https://gitlab.example.com'
nginx['listen_port'] = 80
nginx['listen_https'] = false
letsencrypt['enable'] = false
nginx['proxy_set_headers'] = {
  "X-Forwarded-Proto" => "https",
  "X-Forwarded-Ssl" => "on"
}
```

### NGINX SSL Tuning

```ruby
nginx['redirect_http_to_https'] = true
nginx['http2_enabled'] = true
nginx['hsts_max_age'] = 63072000
nginx['ssl_ciphers'] = "ECDHE-ECDSA-AES128-GCM-SHA256:..."
nginx['ssl_dhparam'] = "/etc/gitlab/ssl/dhparams.pem"
```

---

## 2. SMTP / Email

```ruby
gitlab_rails['smtp_enable'] = true
gitlab_rails['smtp_address'] = "smtp.example.com"
gitlab_rails['smtp_port'] = 587
gitlab_rails['smtp_user_name'] = "user@example.com"
gitlab_rails['smtp_password'] = "password"
gitlab_rails['smtp_domain'] = "example.com"
gitlab_rails['smtp_authentication'] = "login"
gitlab_rails['smtp_enable_starttls_auto'] = true
gitlab_rails['smtp_tls'] = false
gitlab_rails['smtp_ssl'] = false
gitlab_rails['smtp_openssl_verify_mode'] = 'peer'
gitlab_rails['smtp_pool'] = true

gitlab_rails['gitlab_email_from'] = 'gitlab@example.com'
gitlab_rails['gitlab_email_reply_to'] = 'noreply@example.com'
gitlab_rails['gitlab_email_display_name'] = 'GitLab'
```

### Common Providers

| Provider | Port | Auth | TLS |
|----------|------|------|-----|
| Gmail | 587 | login | STARTTLS |
| AWS SES | 587 | login | STARTTLS |
| SendGrid | 587 | login | STARTTLS |
| Office365 | 587 | login | STARTTLS |
| Mailgun | 587 | plain | STARTTLS |

---

## 3. Container Registry

```ruby
registry_external_url 'https://registry.example.com'
# or same domain, different port:
registry_external_url 'https://gitlab.example.com:5050'

registry['enable'] = true
gitlab_rails['registry_enabled'] = true
gitlab_rails['registry_api_url'] = 'http://localhost:5000'
```

### Behind Traefik

```ruby
registry_nginx['listen_port'] = 80
registry_nginx['listen_https'] = false
```

### S3 Storage Backend

```ruby
registry['storage'] = {
  's3' => {
    'accesskey' => 'KEY',
    'secretkey' => 'SECRET',
    'bucket' => 'registry-bucket',
    'region' => 'us-east-1'
  }
}
```

---

## 4. GitLab Pages

```ruby
pages_external_url 'https://pages.example.com'
gitlab_pages['enable'] = true
gitlab_pages['access_control'] = true

pages_nginx['listen_port'] = 80
pages_nginx['listen_https'] = false
```

---

## 5. PostgreSQL

### Bundled (default)

```ruby
postgresql['listen_address'] = 'localhost'
postgresql['port'] = 5432
postgresql['ssl'] = 'on'
```

### External

```ruby
postgresql['enable'] = false

gitlab_rails['db_adapter'] = 'postgresql'
gitlab_rails['db_encoding'] = 'utf8'
gitlab_rails['db_host'] = 'postgresql'
gitlab_rails['db_port'] = 5432
gitlab_rails['db_username'] = 'gitlab'
gitlab_rails['db_password'] = 'secret'
gitlab_rails['db_database'] = 'gitlabhq_production'
```

---

## 6. Redis

### Bundled (default)

```ruby
redis['maxmemory'] = '2gb'
redis['maxmemory_policy'] = 'allkeys-lru'
redis['tcp_timeout'] = 60
```

### External

```ruby
redis['enable'] = false

gitlab_rails['redis_host'] = 'redis'
gitlab_rails['redis_port'] = 6379
gitlab_rails['redis_password'] = 'secret'
```

---

## 7. Object Storage (Consolidated)

Single credential for all GitLab features. Works with AWS S3, MinIO, GCS, Azure.

```ruby
gitlab_rails['object_store']['enabled'] = true
gitlab_rails['object_store']['proxy_download'] = false
gitlab_rails['object_store']['connection'] = {
  'provider' => 'AWS',
  'region' => 'us-east-1',
  'aws_access_key_id' => 'KEY',
  'aws_secret_access_key' => 'SECRET',
  'endpoint' => 'http://minio:9000',       # MinIO only
  'path_style' => true                      # MinIO only
}
gitlab_rails['object_store']['objects']['artifacts']['bucket'] = 'gitlab-artifacts'
gitlab_rails['object_store']['objects']['lfs']['bucket'] = 'gitlab-lfs'
gitlab_rails['object_store']['objects']['uploads']['bucket'] = 'gitlab-uploads'
gitlab_rails['object_store']['objects']['packages']['bucket'] = 'gitlab-packages'
gitlab_rails['object_store']['objects']['external_diffs']['bucket'] = 'gitlab-mr-diffs'
gitlab_rails['object_store']['objects']['terraform_state']['bucket'] = 'gitlab-terraform-state'
gitlab_rails['object_store']['objects']['ci_secure_files']['bucket'] = 'gitlab-ci-secure-files'
gitlab_rails['object_store']['objects']['dependency_proxy']['bucket'] = 'gitlab-dependency-proxy'
gitlab_rails['object_store']['objects']['pages']['bucket'] = 'gitlab-pages'
```

### Provider Connection Variants

| Provider | Key Fields |
|----------|-----------|
| AWS S3 | `provider: AWS`, `region`, `aws_access_key_id`, `aws_secret_access_key` |
| MinIO | Same as AWS + `endpoint` + `path_style: true` |
| GCS | `provider: Google`, `google_project`, `google_json_key_location` |
| Azure | `provider: AzureRM`, `azure_storage_account_name`, `azure_storage_access_key` |

---

## 8. LDAP / Authentication

```ruby
gitlab_rails['ldap_enabled'] = true
gitlab_rails['ldap_servers'] = {
  'main' => {
    'label' => 'LDAP',
    'host' => 'ldap.example.com',
    'port' => 636,
    'uid' => 'sAMAccountName',
    'base' => 'dc=example,dc=com',
    'encryption' => 'simple_tls',
    'bind_dn' => 'cn=admin,dc=example,dc=com',
    'password' => 'bindpassword',
    'verify_certificates' => true,
    'active_directory' => true,
    'user_filter' => '(memberOf=cn=gitlab-users,ou=groups,dc=example,dc=com)',
    'attributes' => {
      'username' => 'uid',
      'email' => ['mail', 'userPrincipalName'],
      'name' => 'displayName',
      'first_name' => 'givenName',
      'last_name' => 'sn'
    }
  }
}
```

Supports multiple providers (add `'secondary' => { ... }`).

---

## 9. Backup

```ruby
gitlab_rails['backup_path'] = '/var/opt/gitlab/backups'
gitlab_rails['backup_keep_time'] = 604800               # 7 days
gitlab_rails['manage_backup_path'] = true
gitlab_rails['backup_upload_remote_directory'] = 's3-backup-bucket'
```

Commands:
- `gitlab-backup create` -- full backup
- `gitlab-ctl backup-etc` -- config backup (`/etc/gitlab/config_backup/`)

---

## 10. Puma (Web Server)

```ruby
puma['worker_processes'] = 4
puma['min_threads'] = 4
puma['max_threads'] = 4
puma['per_worker_max_memory_mb'] = 1200
puma['worker_timeout'] = 60

# Memory-constrained (disable clustering):
puma['worker_processes'] = 0
```

---

## 11. Sidekiq

```ruby
sidekiq['metrics_enabled'] = true
sidekiq['listen_address'] = 'localhost'
sidekiq['listen_port'] = 8082
sidekiq['health_checks_enabled'] = true
sidekiq['health_checks_listen_address'] = 'localhost'
sidekiq['health_checks_listen_port'] = 8092
sidekiq['log_format'] = 'json'
```

---

## 12. Gitaly

```ruby
gitaly['configuration'] = {
  listen_addr: '0.0.0.0:8075',
  auth: { token: 'gitaly-secret-token' },
  storage: [
    { name: 'default', path: '/var/opt/gitlab/git-data/repositories' },
    { name: 'secondary', path: '/mnt/data/git-data/repositories' }
  ],
  logging: { level: 'warn', format: 'json' }
}
```

---

## 13. Monitoring (Prometheus)

```ruby
prometheus['enable'] = true
prometheus['listen_address'] = 'localhost:9090'

node_exporter['enable'] = true
alertmanager['enable'] = true
```

---

## 14. Logging

```ruby
logging['svlogd_size'] = 200 * 1024 * 1024
logging['svlogd_num'] = 30
logging['logrotate_frequency'] = "daily"
logging['logrotate_rotate'] = 30
logging['logrotate_compress'] = "compress"

# Per-service levels
gitlab_rails['env'] = { "GITLAB_LOG_LEVEL" => "warn" }
registry['log_level'] = 'info'
sidekiq['log_format'] = 'json'

# Syslog forwarding
logging['udp_log_shipping_host'] = '1.2.3.4'
logging['udp_log_shipping_port'] = 1514
```

---

## 15. Traefik Integration

### GitLab NGINX Config (HTTP only, Traefik terminates TLS)

```ruby
external_url 'https://gitlab.example.com'
nginx['listen_port'] = 80
nginx['listen_https'] = false
letsencrypt['enable'] = false
nginx['proxy_set_headers'] = {
  "X-Forwarded-Proto" => "https",
  "X-Forwarded-Ssl" => "on"
}
```

### Traefik Labels

```yaml
labels:
  # Web UI
  - "traefik.enable=true"
  - "traefik.docker.network=traefik-public"
  - "traefik.http.routers.gitlab.rule=Host(`gitlab.example.com`)"
  - "traefik.http.routers.gitlab.entrypoints=websecure"
  - "traefik.http.routers.gitlab.tls.certresolver=letsencrypt"
  - "traefik.http.services.gitlab.loadbalancer.server.port=80"

  # Container Registry (separate subdomain)
  - "traefik.http.routers.registry.rule=Host(`registry.example.com`)"
  - "traefik.http.routers.registry.entrypoints=websecure"
  - "traefik.http.routers.registry.tls.certresolver=letsencrypt"
  - "traefik.http.services.registry.loadbalancer.server.port=5005"

  # SSH (TCP router)
  - "traefik.tcp.routers.gitlab-ssh.rule=HostSNI(`*`)"
  - "traefik.tcp.routers.gitlab-ssh.entrypoints=ssh"
  - "traefik.tcp.routers.gitlab-ssh.service=gitlab-ssh-svc"
  - "traefik.tcp.services.gitlab-ssh-svc.loadbalancer.server.port=22"
```

GitLab must join both the Traefik network (proxy) and a backend network (internal services).

---

## 16. GitLab Runner

Runners are a **separate service** (`gitlab/gitlab-runner:alpine`), not configured in `gitlab.rb`.

```yaml
gitlab-runner:
  image: gitlab/gitlab-runner:alpine
  restart: always
  volumes:
    - runner-config:/etc/gitlab-runner
    - /var/run/docker.sock:/var/run/docker.sock
  depends_on:
    gitlab:
      condition: service_healthy
```

Registration:
```bash
docker exec gitlab-runner gitlab-runner register \
  --url https://gitlab.example.com \
  --token <runner-token>
```

Config lives at `/etc/gitlab-runner/config.toml`.

---

## 17. Docker Compose Requirements

| Setting | Value | Why |
|---------|-------|-----|
| `shm_size` | `256m` | Prometheus shared memory (default 64m too small) |
| `restart` | `always` | Self-healing |
| `start_period` | `300s` | GitLab takes 3-5 min to boot |
| Memory limit | `8G` | Minimum 4G, 8G recommended for production |

---

## 18. Environment Variables

| Variable | Purpose | Notes |
|----------|---------|-------|
| `GITLAB_OMNIBUS_CONFIG` | All `gitlab.rb` settings | Ruby syntax, evaluated before file |
| `GITLAB_ROOT_PASSWORD` | Initial root password | First run only |
| `GITLAB_ROOT_EMAIL` | Initial root email | First run only |
| `TZ` | Timezone | e.g. `Europe/Paris` |
