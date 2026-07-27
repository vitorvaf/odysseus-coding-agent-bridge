# Events — Contrato de eventos

> **Status:** Proposed

Define o schema dos eventos registrados ao longo da execução.

## Modelo geral

```json
{
  "eventId": "evt_01JXYZ",
  "runId": "run_01JXYZ",
  "timestamp": "2026-07-27T15:00:00-03:00",
  "actor": "system",
  "type": "state.transition",
  "data": { }
}
```

## Tipos de evento

### `run.created`

```json
{
  "type": "run.created",
  "data": {
    "repository": "payment-hub",
    "agent": "opencode",
    "profile": "implementer",
    "runType": "Implement",
    "accessMode": "WorkspaceWrite"
  }
}
```

### `run.state_transition`

```json
{
  "type": "run.state_transition",
  "data": {
    "from": "ValidatingRequest",
    "to": "PreparingWorkspace",
    "reason": "policy.allow"
  }
}
```

### `policy.decision`

```json
{
  "type": "policy.decision",
  "data": {
    "phase": "pre-execution",
    "decision": "allow",
    "policyVersion": "v1",
    "reason": "agent_allowed_and_repo_writable"
  }
}
```

### `workspace.prepared`

```json
{
  "type": "workspace.prepared",
  "data": {
    "workspaceId": "ws_01JXYZ",
    "branch": "ocab/run_01JXYZ",
    "sizeBytes": 10485760
  }
}
```

### `workspace.cleaned`

```json
{
  "type": "workspace.cleaned",
  "data": {
    "workspaceId": "ws_01JXYZ",
    "reason": "retention_expired"
  }
}
```

### `runner.session_created`

```json
{
  "type": "runner.session_created",
  "data": {
    "agent": "opencode",
    "sessionId": "sess_abc"
  }
}
```

### `runner.event`

```json
{
  "type": "runner.event",
  "data": {
    "agent": "opencode",
    "raw": { "type": "message", "content": "..." }
  }
}
```

### `runner.session_cancelled`

```json
{
  "type": "runner.session_cancelled",
  "data": {
    "agent": "opencode",
    "sessionId": "sess_abc",
    "reason": "user_requested"
  }
}
```

### `validation.started`

```json
{
  "type": "validation.started",
  "data": {
    "command": "dotnet build"
  }
}
```

### `validation.completed`

```json
{
  "type": "validation.completed",
  "data": {
    "command": "dotnet build",
    "exitCode": 0,
    "durationMs": 12000,
    "timeout": false
  }
}
```

### `artifact.persisted`

```json
{
  "type": "artifact.persisted",
  "data": {
    "type": "diff.patch",
    "path": "runs/run_01JXYZ/diff.patch",
    "checksum": "sha256:...",
    "size": 4096
  }
}
```

### `report.built`

```json
{
  "type": "report.built",
  "data": {
    "reportRef": "ocab-artifacts://runs/run_01JXYZ/final-report.md"
  }
}
```

### `error.raised`

```json
{
  "type": "error.raised",
  "data": {
    "code": "workspace_prepare_failed",
    "message": "Clone failed",
    "phase": "PreparingWorkspace"
  }
}
```

### `audit.policy`

```json
{
  "type": "audit.policy",
  "data": {
    "phase": "execution",
    "decision": "deny",
    "policyVersion": "v1",
    "reason": "command_blocked: git push"
  }
}
```

## Invariantes

* Eventos são append-only.
* `eventId` único por evento.
* `runId` correlaciona todos os eventos de uma execução.
* Eventos sensíveis passam por redaction antes da gravação.

## Persistência

* Cada evento é gravado em `RunEvent` no PostgreSQL.
* Eventos também são serializados em `events.jsonl` no diretório da execução.

## Referências relacionadas

* [`mcp-tools.md`](mcp-tools.md)
* [`runner-adapter.md`](runner-adapter.md)
* [`../specs/003-run-lifecycle/spec.md`](../specs/003-run-lifecycle/spec.md)
* [`../data/conceptual-model.md`](../data/conceptual-model.md)
