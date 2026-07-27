# Errors — Contrato de erros

> **Status:** Proposed

Padroniza os códigos de erro e o formato das respostas de erro.

## Formato

```json
{
  "error": {
    "code": "invalid_slug",
    "message": "Slug contains invalid characters",
    "details": {
      "field": "repository",
      "value": "../etc/passwd"
    },
    "traceId": "trace_01JXYZ",
    "runId": null
  }
}
```

## Códigos

### Validação (4xx)

| Código | HTTP | Descrição |
| --- | --- | --- |
| `invalid_payload` | 400 | Payload não atende schema. |
| `invalid_slug` | 400 | Slug inválido. |
| `invalid_state_transition` | 409 | Transição não permitida. |
| `idempotency_conflict` | 409 | Mesma chave com payload divergente. |
| `unauthorized` | 401 | Token ausente ou inválido. |
| `forbidden` | 403 | Acesso negado. |
| `repository_not_found` | 404 | Slug inexistente. |
| `run_not_found` | 404 | runId inexistente. |
| `agent_not_allowed` | 403 | Agente não permitido no repositório. |
| `repository_not_writable` | 409 | Tentativa de escrita em repo read-only. |
| `rate_limited` | 429 | Limite de taxa excedido. |
| `artifact_too_large` | 413 | Artefato excede limite. |

### Infraestrutura (5xx)

| Código | HTTP | Descrição |
| --- | --- | --- |
| `internal_error` | 500 | Erro inesperado. |
| `dependency_unavailable` | 503 | Dependência externa indisponível. |
| `runner_unavailable` | 503 | Runner sem saúde. |
| `workspace_prepare_failed` | 500 | Falha ao preparar workspace. |
| `session_create_failed` | 500 | Falha ao criar sessão. |
| `repository_unavailable` | 503 | Repositório remoto indisponível. |
| `runner_unresponsive` | 504 | Runner sem resposta ao cancelamento. |
| `disk_full` | 507 | Disco cheio. |
| `permission_denied` | 500 | Falha de permissão POSIX. |

### Validação de pipeline

| Código | HTTP | Descrição |
| --- | --- | --- |
| `validation_blocked` | 422 | Comando bloqueado pela política. |
| `validation_failed` | 422 | Comando retornou código não zero. |
| `validation_timeout` | 422 | Comando excedeu timeout. |

### Política

| Código | HTTP | Descrição |
| --- | --- | --- |
| `policy_denied` | 403 | Política recusou a operação. |

## Detalhes

* `details` é opcional e estruturado por código.
* `traceId` correlaciona com traces.
* `runId` é incluído quando aplicável.

## Mensagens

* Mensagens não devem conter segredos.
* Mensagens podem conter dados técnicos (slug, comando), mas nunca credenciais.

## Logs

* Todo erro é logado com nível adequado.
* Erros 5xx geram alerta.

## Referências relacionadas

* [`mcp-tools.md`](mcp-tools.md)
* [`run-request.md`](run-request.md)
* [`run-report.md`](run-report.md)
* [`../specs/004-mcp-contract/spec.md`](../specs/004-mcp-contract/spec.md)
