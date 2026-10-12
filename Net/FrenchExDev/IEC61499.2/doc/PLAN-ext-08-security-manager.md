# Extension #8: IEC61499.2 Security Manager

## Purpose

OPC-UA certificate management. Auto-generate TLS certificates for device communication. Audit trail of deployments. Secret management (connection strings, tokens).

## Lifecycle phase

Secure

## Architecture

```
vscode/iec61499-security-manager/
+-- package.json
+-- src/
|   +-- extension.ts
|   +-- SecurityTreeProvider.ts        Sidebar: certs, secrets, audit log
|   +-- CertificateManager.ts         Generate/renew/revoke OPC-UA certs
|   +-- SecretStore.ts                 Encrypted secret storage (per-device)
|   +-- AuditLogger.ts                Track who deployed what, when
|   +-- webview/
|       +-- CertificateDashboard.tsx   Cert status, expiry, chain visualization
|       +-- SecretEditor.tsx           Manage secrets per device/resource
|       +-- AuditLog.tsx               Searchable deployment audit trail
```

## package.json

```json
{
  "contributes": {
    "views": {
      "iec61499-topology": [{
        "id": "iec61499.security",
        "name": "Security"
      }]
    },
    "commands": [
      { "command": "iec61499.security.generateCert", "title": "Generate OPC-UA Certificate" },
      { "command": "iec61499.security.renewCert", "title": "Renew Certificate" },
      { "command": "iec61499.security.revokeCert", "title": "Revoke Certificate" },
      { "command": "iec61499.security.addSecret", "title": "Add Secret" },
      { "command": "iec61499.security.viewAudit", "title": "View Audit Log" }
    ]
  }
}
```

## Certificate workflow

1. Generate root CA (once per system) using `openssl` or `mkcert`
2. Per device: generate device certificate signed by root CA
3. Certificates stored in workspace `.certs/` directory (git-ignored)
4. On deploy, Extension #7 includes cert in deployment payload
5. Agent installs cert in OPC-UA server config

## Secret management

- Secrets encrypted at rest using VSCode's `SecretStorage` API
- Per-device secrets: DB connection strings, API keys, MQTT credentials
- Secrets injected as env vars in the `.env` file during deployment
- Never stored in source control

## Audit log

```
2026-03-23 14:30  user@company  DEPLOY   EdgeController_01  v1.2.3  ✓
2026-03-23 14:29  user@company  CERT     EdgeController_01  renewed (expires 2027-03-23)
2026-03-22 09:15  user@company  DEPLOY   EdgeController_01  v1.2.2  ✓
2026-03-20 16:45  admin@company SECRET   PLC_02             DB_CONN updated
```

Stored as append-only JSON file in workspace `.audit/` directory.

## Dependencies

- Child process: `openssl` or `mkcert`
- VSCode `SecretStorage` API
- `react`, `react-dom` for webview panels

## Testing

- Unit: CertificateManager generate/verify/revoke
- Unit: SecretStore encrypt/decrypt roundtrip
- Unit: AuditLogger append/query
- Integration: generate cert → deploy with cert → verify OPC-UA connection
