# Extension #7: IEC61499.2 Deployment Manager

## Purpose

One-click deploy to targets: Docker containers, VMs, bare-metal, Kubernetes. Hot deploy (online changes without downtime). Bulk deployment. Rollback. Communicates with the always-running IEC61499.2 Agent on edge devices.

## Lifecycle phase

Deploy

## Architecture

```
vscode/iec61499-deployment-manager/
+-- package.json
+-- src/
|   +-- extension.ts
|   +-- DeploymentTreeProvider.ts      Sidebar tree: devices, status, history
|   +-- DeploymentPanel.ts             Webview: deploy wizard, progress, logs
|   +-- AgentClient.ts                 gRPC/REST client to IEC61499.2 Agent
|   +-- BuildPipeline.ts              dotnet publish → docker build → push
|   +-- ComposeGenerator.ts            Generate docker-compose.yml from topology
|   +-- EnvGenerator.ts                Generate .env from deployment payload
|   +-- RollbackManager.ts             Track versions, rollback to previous
|   +-- BulkDeployer.ts                Deploy to multiple devices in parallel
```

## package.json

```json
{
  "contributes": {
    "viewsContainers": {
      "activitybar": [{
        "id": "iec61499-deployment",
        "title": "IEC61499 Deployment",
        "icon": "resources/deploy.svg"
      }]
    },
    "views": {
      "iec61499-deployment": [
        { "id": "iec61499.deployTargets", "name": "Deploy Targets" },
        { "id": "iec61499.deployHistory", "name": "Deployment History" }
      ]
    },
    "commands": [
      { "command": "iec61499.deploy.build", "title": "Build & Publish Image" },
      { "command": "iec61499.deploy.deploy", "title": "Deploy to Device" },
      { "command": "iec61499.deploy.deployAll", "title": "Deploy to All Devices" },
      { "command": "iec61499.deploy.rollback", "title": "Rollback Deployment" },
      { "command": "iec61499.deploy.hotDeploy", "title": "Hot Deploy (Online Change)" },
      { "command": "iec61499.deploy.status", "title": "Check Device Status" }
    ]
  }
}
```

## Deployment workflow

### Step 1: Build

```
[Build & Publish Image]
  1. dotnet publish -c Release --self-contained -r linux-musl-x64
  2. docker build -t {registry}/{project}:{version} .
  3. docker push {registry}/{project}:{version}
```

Progress shown in webview panel with live terminal output.

### Step 2: Deploy payload

Extension constructs a deployment payload from the topology:

```typescript
interface DeploymentPayload {
  targetDevice: string;           // device address
  imageTag: string;               // registry/project:version
  composeVariables: Record<string, string>;  // env vars
  resourceConfig: {
    resource: string;
    applications: string[];
    cpu: string;                  // e.g., "0.5"
    memory: string;               // e.g., "256m"
  }[];
  opcUa?: {
    port: number;
    certificate: string;          // base64 cert
  };
}
```

### Step 3: Send to Agent

```
Extension  ──── gRPC/REST ────>  IEC61499.2 Agent (on device)
                                   │
                                   ├── Writes docker-compose.yml
                                   ├── Writes .env
                                   ├── Writes config files
                                   └── Runs: docker compose up -d --pull
```

### Step 4: Monitor result

Agent streams back:
- Container pull progress
- Container start status
- Health check results
- Logs from first 30 seconds

## Tree view

```
▼ Deploy Targets
  ▼ EdgeController_01 (192.168.1.10) [● Online]
    Current: myplant:v1.2.3
    Uptime: 14d 3h
    Status: 3/3 containers healthy
  ▼ PLC_02 (192.168.1.20) [● Online]
    Current: myplant:v1.2.2
    Status: 2/2 containers healthy
  ▼ TestBench_03 (192.168.1.30) [○ Offline]

▼ Deployment History
  2026-03-23 14:30  EdgeController_01  v1.2.3  ✓ Success
  2026-03-22 09:15  EdgeController_01  v1.2.2  ✓ Success
  2026-03-20 16:45  PLC_02             v1.2.2  ✓ Success
  2026-03-19 11:00  EdgeController_01  v1.2.1  ✗ Rolled back
```

## Hot deploy (online changes)

For config-only changes (no new image):
1. Extension detects only `.env` or compose variable changes
2. Sends lightweight payload (no image tag change)
3. Agent runs `docker compose up -d` (no `--pull`)
4. Only affected containers restart

## Rollback

1. Agent stores last N deployment states (compose files + .env)
2. Extension sends `{ action: "rollback", version: "v1.2.2" }`
3. Agent restores previous compose file and runs `docker compose up -d --pull`

## Integration with other extensions

- Reads device topology from Extension #4 (Topology Manager)
- Reports device status to Extension #9 (Runtime Monitor)
- Reports deployment events to Extension #11 (Diagnostics)
- Uses certificates from Extension #8 (Security Manager)

## Dependencies

- `@grpc/grpc-js` or `node-fetch` -- Agent communication
- Child process calls to `dotnet`, `docker`

## Testing

- Unit: ComposeGenerator produces valid docker-compose.yml
- Unit: EnvGenerator produces valid .env
- Unit: RollbackManager tracks versions correctly
- Integration: build → deploy to local Agent → verify containers running
