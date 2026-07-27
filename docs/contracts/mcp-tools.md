# MCP Tools — Contrato

> **Status:** Proposed

Define as ferramentas MCP expostas pelo Coding Agent Bridge, schemas JSON, exemplos, erros e invariantes.

## Versão do contrato

* Versão atual: `v1`.
* Header de resposta: `X-OCAB-Contract-Version: v1`.

## Ferramentas

| Tool | Descrição |
| --- | --- |
| `repositories_list` | Lista repositórios cadastrados. |
| `agents_list` | Lista agentes disponíveis. |
| `run_create` | Cria execução. |
| `run_get` | Consulta estado de execução. |
| `run_cancel` | Solicita cancelamento. |
| `run_report` | Retorna relatório estruturado. |
| `run_diff` | Retorna diff da execução. |
| `review_create` | Cria revisão independente. |

## Autenticação

* Header: `Authorization: Bearer <token>`.
* Token armazenado em arquivo montado, nunca em código.
* Token inválido retorna `401 unauthorized`.

## Paginação

* Cursor opaco retornado em `nextCursor`.
* `limit` default 50, máximo 200.

## Rate limit

* Default: 60 req/min por token.
* Excedido retorna `429 rate_limited`.

## Limites

* Tamanho máximo de prompt: 100 KB.
* Tamanho máximo de payload: 2 MB.
* Tamanho máximo de `run_diff` em `patch`: 1 MB (acima disso vira referência a artefato).

## Idempotência

* `run_create` aceita `idempotencyKey`.
* Repetição com mesma chave e mesmo payload retorna mesma execução.
* Repetição com mesma chave e payload divergente retorna `409 idempotency_conflict`.

## `repositories_list`

### Entrada

```json
{
  "pagination": { "cursor": "opaque", "limit": 50 },
  "filter": { "writable": true, "allowedAgent": "opencode" }
}
```

### Saída

```json
{
  "repositories": [
    {
      "slug": "payment-hub",
      "displayName": "Payment Hub",
      "defaultBranch": "dev",
      "writable": true,
      "allowedAgents": ["opencode", "codex"]
    }
  ],
  "nextCursor": null
}
```

## `agents_list`

### Entrada

```json
{}
```

### Saída

```json
{
  "agents": [
    {
      "id": "opencode",
      "displayName": "OpenCode",
      "enabled": true,
      "capabilities": ["plan", "implement", "review", "document"]
    }
  ]
}
```

## `run_create`

### Entrada

Ver [`run-request.md`](run-request.md).

### Saída

```json
{
  "runId": "run_01JXYZ",
  "status": "Pending",
  "createdAt": "2026-07-27T15:00:00-03:00",
  "contractVersion": "v1"
}
```

## `run_get`

### Entrada

```json
{
  "runId": "run_01JXYZ"
}
```

### Saída

```json
{
  "runId": "run_01JXYZ",
  "status": "Completed",
  "repository": "payment-hub",
  "agent": "opencode",
  "profile": "implementer",
  "runType": "Implement",
  "accessMode": "WorkspaceWrite",
  "baseReference": "dev",
  "createdAt": "2026-07-27T15:00:00-03:00",
  "startedAt": "2026-07-27T15:00:05-03:00",
  "finishedAt": "2026-07-27T15:08:42-03:00",
  "timeoutSeconds": 1800,
  "errorCode": null,
  "errorMessage": null
}
```

## `run_cancel`

### Entrada

```json
{
  "runId": "run_01JXYZ",
  "reason": "Cancelado pelo usuário"
}
```

### Saída

```json
{
  "runId": "run_01JXYZ",
  "status": "Cancelling"
}
```

## `run_report`

### Entrada

```json
{
  "runId": "run_01JXYZ"
}
```

### Saída

Ver [`run-report.md`](run-report.md).

## `run_diff`

### Entrada

```json
{
  "runId": "run_01JXYZ",
  "mode": "patch"
}
```

### Saída — `summary`

```json
{
  "filesChanged": 4,
  "insertions": 120,
  "deletions": 35
}
```

### Saída — `stat`

```json
{
  "files": [
    { "path": "src/PaymentHub.Api/Controllers/OrdersController.cs", "insertions": 80, "deletions": 10 }
  ]
}
```

### Saída — `patch` (resposta completa)

```json
{
  "patch": "diff --git a/... b/...\n..."
}
```

### Saída — `patch` (acima do limite)

```json
{
  "artifactRef": {
    "runId": "run_01JXYZ",
    "type": "diff.patch",
    "checksum": "sha256:...",
    "size": 1234567
  }
}
```

## `review_create`

### Entrada

```json
{
  "sourceRunId": "run_01JXYZ",
  "agent": "opencode",
  "profile": "reviewer"
}
```

### Saída

```json
{
  "reviewRunId": "run_01JABC",
  "status": "Pending",
  "createdAt": "2026-07-27T15:10:00-03:00"
}
```

## Erros padronizados

Ver [`errors.md`](errors.md).

## Versionamento

* Mudanças incompatíveis exigem `v2`.
* Mudanças compatíveis (novo campo opcional) permanecem em `v1`.
* Cliente pode enviar `X-OCAB-Contract-Version: v1` para fixar versão.

## Referências relacionadas

* [`run-request.md`](run-request.md)
* [`run-report.md`](run-report.md)
* [`runner-adapter.md`](runner-adapter.md)
* [`events.md`](events.md)
* [`errors.md`](errors.md)
* [`../specs/004-mcp-contract/spec.md`](../specs/004-mcp-contract/spec.md)
