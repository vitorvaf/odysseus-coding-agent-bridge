# Modelo Relacional

> **Status:** Proposed

Mapeia o modelo conceitual para tabelas PostgreSQL. **Não inclui migrations reais.** Apenas contratos conceituais.

## Convenções

* Identificadores como `id` (string, formato ULID/UUID).
* Timestamps em UTC.
* Colunas `createdAt`, `updatedAt`, `deletedAt` quando aplicável.
* `metadata` como `JSONB`.

## Tabelas

### `repositories`

| Coluna | Tipo | Constraints |
| --- | --- | --- |
| `id` | uuid | PK |
| `slug` | text | UNIQUE, NOT NULL, CHECK (regex) |
| `display_name` | text | NOT NULL |
| `url_canonical` | text | NOT NULL |
| `default_branch` | text | NOT NULL |
| `writable` | boolean | NOT NULL DEFAULT false |
| `allowed_agents` | text[] | NOT NULL |
| `validations` | jsonb | NOT NULL DEFAULT '[]' |
| `policies` | jsonb | NOT NULL DEFAULT '{}' |
| `created_at` | timestamptz | NOT NULL |
| `updated_at` | timestamptz | NOT NULL |
| `archived_at` | timestamptz | NULL |

Índices:

* `repositories_slug_idx` UNIQUE (`slug`).
* `repositories_archived_idx` (`archived_at`).

### `agents`

| Coluna | Tipo | Constraints |
| --- | --- | --- |
| `id` | text | PK |
| `display_name` | text | NOT NULL |
| `enabled` | boolean | NOT NULL DEFAULT true |
| `image_ref` | text | NOT NULL |
| `created_at` | timestamptz | NOT NULL |
| `updated_at` | timestamptz | NOT NULL |

### `agent_capabilities`

| Coluna | Tipo | Constraints |
| --- | --- | --- |
| `agent_id` | text | FK → `agents.id` |
| `capability` | text | NOT NULL |

PK composta: (`agent_id`, `capability`).

### `runs`

| Coluna | Tipo | Constraints |
| --- | --- | --- |
| `id` | text | PK |
| `repository_id` | uuid | FK → `repositories.id` |
| `agent_id` | text | FK → `agents.id` |
| `profile` | text | NULL |
| `run_type` | text | NOT NULL |
| `access_mode` | text | NOT NULL CHECK in (`ReadOnly`, `WorkspaceWrite`) |
| `base_reference` | text | NOT NULL |
| `prompt` | text | NOT NULL |
| `status` | text | NOT NULL |
| `created_at` | timestamptz | NOT NULL |
| `started_at` | timestamptz | NULL |
| `finished_at` | timestamptz | NULL |
| `timeout_seconds` | integer | NOT NULL |
| `idempotency_key` | text | NULL |
| `retention_days` | integer | NOT NULL DEFAULT 30 |
| `diagnostic` | boolean | NOT NULL DEFAULT false |
| `error_code` | text | NULL |
| `error_message` | text | NULL |
| `metadata` | jsonb | NOT NULL DEFAULT '{}' |

Índices:

* `runs_status_idx` (`status`).
* `runs_repository_status_idx` (`repository_id`, `status`) — concorrência.
* `runs_created_idx` (`created_at`).
* `runs_idempotency_idx` UNIQUE (`idempotency_key`) WHERE `idempotency_key IS NOT NULL`.

### `run_events`

| Coluna | Tipo | Constraints |
| --- | --- | --- |
| `id` | uuid | PK |
| `run_id` | text | FK → `runs.id` |
| `timestamp` | timestamptz | NOT NULL |
| `actor` | text | NOT NULL |
| `type` | text | NOT NULL |
| `from_state` | text | NULL |
| `to_state` | text | NULL |
| `reason` | text | NULL |
| `data` | jsonb | NOT NULL DEFAULT '{}' |

Índices:

* `run_events_run_idx` (`run_id`, `timestamp`).
* `run_events_type_idx` (`type`).

### `validation_results`

| Coluna | Tipo | Constraints |
| --- | --- | --- |
| `id` | uuid | PK |
| `run_id` | text | FK → `runs.id` |
| `command` | text | NOT NULL |
| `exit_code` | integer | NULL |
| `started_at` | timestamptz | NOT NULL |
| `finished_at` | timestamptz | NULL |
| `duration_ms` | integer | NULL |
| `timeout` | boolean | NOT NULL DEFAULT false |
| `status` | text | NOT NULL |
| `artifacts` | jsonb | NOT NULL DEFAULT '[]' |

### `artifacts`

| Coluna | Tipo | Constraints |
| --- | --- | --- |
| `id` | uuid | PK |
| `run_id` | text | FK → `runs.id` |
| `type` | text | NOT NULL |
| `path` | text | NOT NULL |
| `checksum` | text | NOT NULL |
| `size` | bigint | NOT NULL |
| `mime_type` | text | NULL |
| `created_at` | timestamptz | NOT NULL |
| `deleted_at` | timestamptz | NULL |
| `truncated` | boolean | NOT NULL DEFAULT false |
| `metadata` | jsonb | NOT NULL DEFAULT '{}' |

Índices:

* `artifacts_run_type_idx` (`run_id`, `type`).
* `artifacts_deleted_idx` (`deleted_at`).

### `policy_decisions`

| Coluna | Tipo | Constraints |
| --- | --- | --- |
| `id` | uuid | PK |
| `run_id` | text | FK → `runs.id` |
| `phase` | text | NOT NULL |
| `actor` | text | NOT NULL |
| `policy_version` | text | NOT NULL |
| `decision` | text | NOT NULL |
| `reason` | text | NOT NULL |
| `timestamp` | timestamptz | NOT NULL |
| `metadata` | jsonb | NOT NULL DEFAULT '{}' |

Índices:

* `policy_decisions_run_idx` (`run_id`, `timestamp`).
* `policy_decisions_decision_idx` (`decision`).

### `approvals`

| Coluna | Tipo | Constraints |
| --- | --- | --- |
| `id` | uuid | PK |
| `run_id` | text | FK → `runs.id` |
| `actor` | text | NOT NULL |
| `decision` | text | NOT NULL |
| `reason` | text | NULL |
| `timestamp` | timestamptz | NOT NULL |

### `runner_health`

| Coluna | Tipo | Constraints |
| --- | --- | --- |
| `id` | uuid | PK |
| `agent_id` | text | FK → `agents.id` |
| `healthy` | boolean | NOT NULL |
| `reason` | text | NULL |
| `checked_at` | timestamptz | NOT NULL |

Índices:

* `runner_health_agent_idx` (`agent_id`, `checked_at`).

## Constraints adicionais

* `CHECK (slug ~ '^[a-z0-9][a-z0-9-]{0,63}$')` em `repositories`.
* `CHECK (status IN (...))` em `runs`.
* `CHECK (decision IN ('allow', 'deny', 'require_review', 'require_approval'))` em `policy_decisions`.

## Estratégia de locking

* `SELECT ... FOR UPDATE` em `runs` por `run_id` durante transições.
* `SELECT ... FOR UPDATE` em `runs` por `repository_id` para execuções de escrita em fila.

## Views (opcional)

* `runs_active` — execuções em estados não terminais.
* `runs_pending_validation` — execuções em `ValidatingResult`.
* `policy_decisions_recent` — decisões das últimas 24 h.

## Funções (opcional)

* `fn_next_run_for_repository(repo_id)` — próxima execução de escrita em fila.

## Referências relacionadas

* [`conceptual-model.md`](conceptual-model.md)
* [`lifecycle-and-retention.md`](lifecycle-and-retention.md)
* [`migrations-strategy.md`](migrations-strategy.md)
