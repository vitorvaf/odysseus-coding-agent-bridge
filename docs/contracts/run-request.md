# run_create — Contrato de requisição

> **Status:** Proposed

Define o schema de entrada e os invariantes de `run_create`.

## Schema

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "ocab://contracts/run-request.schema.json",
  "title": "RunRequest",
  "type": "object",
  "required": ["repository", "agent", "type", "accessMode", "baseReference", "prompt"],
  "properties": {
    "repository": {
      "type": "string",
      "pattern": "^[a-z0-9][a-z0-9-]{0,63}$"
    },
    "agent": {
      "type": "string",
      "enum": ["opencode", "codex", "antigravity"]
    },
    "profile": {
      "type": "string",
      "description": "Perfil de execução. Varia por agente."
    },
    "type": {
      "type": "string",
      "enum": ["Analyze", "Implement", "Refactor", "Document", "Review", "Custom"]
    },
    "accessMode": {
      "type": "string",
      "enum": ["ReadOnly", "WorkspaceWrite"]
    },
    "baseReference": {
      "type": "string",
      "description": "Branch ou commit de referência."
    },
    "prompt": {
      "type": "string",
      "maxLength": 102400
    },
    "runValidation": {
      "type": "boolean",
      "default": true
    },
    "validationOverride": {
      "type": "array",
      "items": { "type": "string" }
    },
    "timeoutSeconds": {
      "type": "integer",
      "minimum": 60,
      "maximum": 86400,
      "default": 1800
    },
    "retentionDays": {
      "type": "integer",
      "minimum": 1,
      "maximum": 365,
      "default": 30
    },
    "diagnostic": {
      "type": "boolean",
      "default": false
    },
    "idempotencyKey": {
      "type": "string",
      "minLength": 8,
      "maxLength": 128
    }
  },
  "additionalProperties": false
}
```

## Exemplo

```json
{
  "repository": "payment-hub",
  "agent": "opencode",
  "profile": "implementer",
  "type": "Implement",
  "accessMode": "WorkspaceWrite",
  "baseReference": "dev",
  "prompt": "Implemente o slice descrito.",
  "runValidation": true,
  "timeoutSeconds": 1800,
  "idempotencyKey": "client-2026-07-27-001"
}
```

## Invariantes

* `repository` deve existir na allowlist.
* `agent` deve estar em `Repository.allowedAgents`.
* `accessMode = WorkspaceWrite` exige `Repository.writable = true`.
* `timeoutSeconds` deve respeitar limites mínimo e máximo.
* `prompt` deve passar por redaction antes da gravação.
* `idempotencyKey` é opcional; quando presente, deve seguir o contrato de idempotência.

## Erros específicos

| Código | Quando |
| --- | --- |
| `400 invalid_slug` | Slug inválido |
| `400 invalid_payload` | Payload inválido |
| `403 repository_not_writable` | Tentativa de escrita em repo read-only |
| `403 agent_not_allowed` | Agente não permitido |
| `404 repository_not_found` | Slug inexistente |
| `409 idempotency_conflict` | Mesma chave, payload divergente |

## Resposta

```json
{
  "runId": "run_01JXYZ",
  "status": "Pending",
  "createdAt": "2026-07-27T15:00:00-03:00"
}
```

## Referências relacionadas

* [`../specs/004-mcp-contract/spec.md`](../specs/004-mcp-contract/spec.md)
* [`../specs/003-run-lifecycle/spec.md`](../specs/003-run-lifecycle/spec.md)
* [`mcp-tools.md`](mcp-tools.md)
