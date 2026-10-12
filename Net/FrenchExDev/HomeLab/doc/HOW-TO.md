# HomeLab -- Developer Guide (HOW-TO)

**Status: design phase.** This document describes the planned CLI usage. See [PLAN.md](PLAN.md) for implementation details.

---

## Table of Contents

1. [Bootstrap a Lab](#1-bootstrap-a-lab)
2. [Build Packer Images](#2-build-packer-images)
3. [Manage Vagrant Boxes](#3-manage-vagrant-boxes)
4. [Provision VMs](#4-provision-vms)
5. [Deploy Services](#5-deploy-services)
6. [Configure DNS](#6-configure-dns)
7. [Configure TLS](#7-configure-tls)
8. [Configure GitLab](#8-configure-gitlab)
9. [Full Pipeline](#9-full-pipeline)

---

## 1. Bootstrap a Lab

```bash
homelab init --name test-lab --acme-name frenchexdev --acme-tld lab
cd test-lab
```

Creates:
- `config-homelab.yaml` -- main configuration
- `.vscode/settings.json` -- JSON schema mappings for intellisense

---

## 2. Build Packer Images

```bash
# Generate Packer HCL2 project
homelab packer init --distro alpine --version 3.21 --kind dockerhost

# Build the image
homelab packer build
```

Options: `--cpus`, `--memory`, `--disk-size`, `--box-version`, `--output`.

---

## 3. Manage Vagrant Boxes

```bash
# Add box from local .box file
homelab box add --name frenchexdev/alpine-3.21-dockerhost \
  --box-file ./packer/output-vagrant/*.box

# Publish to self-hosted registry
homelab box publish --name frenchexdev/alpine-3.21-dockerhost \
  --registry-url http://registry.frenchexdev.lab:8080 \
  --version 1.0.0
```

---

## 4. Provision VMs

```bash
# Generate Vos config
homelab vos init --box frenchexdev/alpine-3.21-dockerhost \
  --instance-name main-01 --memory 2048 --cpus 4

# VM lifecycle
homelab vos up
homelab vos status
homelab vos ssh main-01
homelab vos ssh-command main-01 "docker ps"
homelab vos halt
homelab vos destroy --force
```

---

## 5. Deploy Services

```bash
# Generate Docker Compose stack (Traefik + GitLab)
homelab compose init --traefik --gitlab --gitlab-runner \
  --domain frenchexdev.lab

# Deploy to VM via Docker TCP
homelab compose deploy main-01

# Tear down
homelab compose down main-01
```

Services are deployed via `DOCKER_HOST=tcp://{vm-ip}:2375`.

---

## 6. Configure DNS

```bash
# Local hosts file
homelab dns add gitlab.frenchexdev.lab 192.168.56.10

# PiHole API
homelab dns add gitlab.frenchexdev.lab 192.168.56.10 \
  --pihole-url http://pihole.local --pihole-token abc123

# List entries
homelab dns list

# Remove
homelab dns remove gitlab.frenchexdev.lab
```

---

## 7. Configure TLS

```bash
# Generate CA + certificates
homelab tls init --provider native --domain frenchexdev.lab \
  --ca-name "HomeLab CA"

# Install certs to services
homelab tls install

# Trust CA on host machine
homelab tls trust
```

Providers: `native` (OpenSSL-based) or `mkcert` (mkcert binary).

---

## 8. Configure GitLab

```bash
# Wait for GitLab to be ready and configure
homelab gitlab configure --url https://gitlab.frenchexdev.lab \
  --admin-password --wait-timeout 300

# Register a CI runner
homelab gitlab runner register --url https://gitlab.frenchexdev.lab \
  --runner-name runner-01 --executor docker --docker-image alpine:latest

# Check status
homelab gitlab status
```

---

## 9. Full Pipeline

Complete bootstrap to CI in 10 steps:

```bash
# 0. Bootstrap
homelab init --name test-lab --acme-name frenchexdev --acme-tld lab
cd test-lab

# 1-2. Packer image
homelab packer init
homelab packer build

# 3. Register box
homelab box add --local

# 4-5. VMs
homelab vos init
homelab vos up

# 6. DNS + TLS
homelab dns add gitlab.frenchexdev.lab 192.168.56.10
homelab tls init --provider native
homelab tls install

# 7. Services
homelab compose init --traefik --gitlab
homelab compose deploy

# 8. GitLab
homelab gitlab configure
homelab gitlab runner register

# 9. Verify
homelab gitlab status
```

Each step is a standalone CLI command -- E2E tests execute this exact sequence.
