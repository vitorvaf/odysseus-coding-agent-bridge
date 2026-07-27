# Spec 004 — MCP Contract

## Status

Proposed

## Resumo

Define o contrato MCP Streamable HTTP exposto pelo Coding Agent Bridge, incluindo ferramentas, schemas, códigos de erro, autenticação, paginação, limites, idempotência e versionamento.

## Contexto

O Odysseus precisa de um contrato estável para criar e acompanhar execuções. O contrato MCP é a interface externa do bridge.

## Problema

Como expor as funcionalidades do bridge de forma padronizada, auditável e evolutiva?

## Objetivos

* Listar ferramentas MCP iniciais.
* Padronizar schemas de entrada e saída.
* Padronizar códigos de erro.
* Suportar paginação e limites.
* Suportar idempotência.
* Versionar contrato.

## Não objetivos

* Suportar outros protocolos além de MCP.
* Expor endpoints administrativos via MCP.

## Escopo funcional

* Ferramentas MCP:
  * `repositories_list`
  * `agents_list`
  * `run_create`
  * `run_get`
  * `run_cancel`
  * `run_report`
  * `run_diff`
  * `review_create`
* Schemas JSON de entrada e saída.
* Autenticação via token.
* Limites por tool (ex.: tamanho máximo de prompt).
* Idempotência em `run_create`.
* Versionamento `v1`.

## Requisitos funcionais

* **MCP-FR-001** Todas as ferramentas devem ser expostas via MCP Streamable HTTP.
* **MCP-FR-002** `repositories_list` deve retornar lista paginada de repositórios.
* **MCP-FR-003** `agents_list` deve retornar lista de agentes disponíveis.
* **MCP-FR-004** `run_create` deve criar execução e retornar `runId` e estado inicial.
* **MCP-FR-005** `run_get` deve retornar estado atual e metadados da execução.
* **MCP-FR-006** `run_cancel` deve solicitar cancelamento e retornar estado atual.
* **MCP-FR-007** `run_report` deve retornar relatório estruturado (contrato separado).
* **MCP-FR-008** `run_diff` deve retornar diff em modo `summary`, `stat` ou `patch`.
* **MCP-FR-009** `review_create` deve criar revisão independente baseada em execução de origem.
* **MCP-FR-010** Todas as ferramentas devem aceitar header `Authorization: Bearer <token>`.
* **MCP-FR-011** Token inválido deve retornar `401 unauthorized`.
* **MCP-FR-012** Filtros por `repository` e `agent` devem ser suportados em `run_get` (listagem).
* **MCP-FR-013** `run_diff` deve retornar referência de artefato quando o patch exceder limite configurável.

## Requisitos não funcionais

* **MCP-NFR-001** Latência P95 das tools deve ser inferior a 300 ms (excluindo `run_create`).
* **MCP-NFR-002** `run_create` deve responder em menos de 1 segundo mesmo quando execução não inicia imediatamente.
* **MCP-NFR-003** Logs de MCP devem omitir payloads sensíveis (token, prompt completo) ou aplicar redaction.
* **MCP-NFR-004** Toda tool deve retornar `trace_id` e `runId` quando aplicável.
* **MCP-NFR-005** Contrato deve ter versão semântica (`v1`) e mudanças incompatíveis exigem nova versão.
* **MCP-NFR-006** Rate limit deve ser aplicado por token (default 60 req/min).

## Atores e componentes envolvidos

* Odysseus (cliente MCP).
* Bridge (servidor MCP).
* Policy Engine.

## Casos de uso

* Listar repositórios.
* Listar agentes.
* Criar execução.
* Acompanhar execução.
* Cancelar execução.
* Obter relatório.
* Obter diff.
* Criar revisão.

## Ferramentas

### `repositories_list`

* Entrada: `{ pagination?: { cursor?, limit? } }`.
* Saída: `{ repositories: [...], nextCursor? }`.

### `agents_list`

* Entrada: vazio.
* Saída: `{ agents: [...] }`.

### `run_create`

* Entrada: ver [`../contracts/run-request.md`](../contracts/run-request.md).
* Saída: `{ runId, status, createdAt }`.

### `run_get`

* Entrada: `{ runId }`.
* Saída: `{ runId, status, repository, agent, profile, runType, accessMode, baseReference, createdAt, startedAt?, finishedAt?, timeoutSeconds, errorCode?, errorMessage? }`.

### `run_cancel`

* Entrada: `{ runId, reason }`.
* Saída: `{ runId, status }`.

### `run_report`

* Entrada: `{ runId }`.
* Saída: relatório estruturado conforme [`../contracts/run-report.md`](../contracts/run-report.md).

### `run_diff`

* Entrada: `{ runId, mode: "summary" | "stat" | "patch" }`.
* Saída: conforme mode. `patch` pode retornar referência a artefato.

### `review_create`

* Entrada: `{ sourceRunId, agent?, profile? }`.
* Saída: `{ reviewRunId, status, createdAt }`.

## Códigos de erro

Padrão em [`../contracts/errors.md`](../contracts/errors.md).

Exemplos:

* `400 invalid_slug`
* `400 invalid_payload`
* `401 unauthorized`
* `403 agent_not_allowed`
* `403 repository_not_writable`
* `404 repository_not_found`
* `404 run_not_found`
* `409 idempotency_conflict`
* `409 invalid_state_transition`
* `422 validation_failed`
* `429 rate_limited`
* `500 internal_error`
* `503 dependency_unavailable`

## Autenticação

* Token Bearer no header `Authorization`.
* Validação no middleware MCP.
* Token armazenado em arquivo montado, não em código.

## Paginação

* Cursor opaco em `nextCursor`.
* `limit` default 50, máximo 200.

## Limites

* Tamanho máximo de prompt: 100 KB.
* Tamanho máximo de resposta de `run_diff` em modo `patch`: 1 MB (acima disso vira referência a artefato).
* Tamanho máximo de `payload`: 2 MB.

## Idempotência

* `run_create` aceita `idempotencyKey`.
* Repetição com mesma chave e mesmo payload retorna mesma execução.
* Repetição com mesma chave e payload divergente retorna `409 idempotency_conflict`.

## Versionamento

* Versão atual: `v1`.
* Header `X-OCAB-Contract-Version` retornado em todas as respostas.
* Mudanças incompatíveis exigem `v2` paralelo.

## Respostas compactas

* `run_get` retorna apenas campos essenciais.
* Relatórios grandes são entregues via `run_report`, com truncamento e referência a artefato.

## Contratos relacionados

* [`../contracts/mcp-tools.md`](../contracts/mcp-tools.md)
* [`../contracts/run-request.md`](../contracts/run-request.md)
* [`../contracts/run-report.md`](../contracts/run-report.md)
* [`../contracts/errors.md`](../contracts/errors.md)
* [`../contracts/events.md`](../contracts/events.md)

## Modelo de dados afetado

* `Run` é a fonte primária.
* `PolicyDecision` é referenciada em auditoria.
* `Approval` é referenciada em `review_create`.

## Segurança

* Token validado em todas as chamadas.
* Redaction em logs de payload.
* Rate limit por token.
* Validação rigorosa de schemas.

## Observabilidade

* Spans por chamada MCP.
* Métricas por tool.
* Logs com `trace_id`.

## Estratégia de testes

* Contrato: validar schemas.
* Integração: chamadas ponta a ponta com cliente MCP.
* Segurança: token inválido, rate limit.

## Critérios de aceite

* **MCP-AC-001** Quando um cliente MCP válido chamar `repositories_list`, recebe lista paginada.
* **MCP-AC-002** Quando chamar `run_create` com payload válido, recebe `runId` em menos de 1 segundo.
* **MCP-AC-003** Quando chamar `run_create` com `idempotencyKey` repetida e mesmo payload, recebe mesma execução.
* **MCP-AC-004** Quando chamar `run_diff` em modo `patch` e patch > 1 MB, recebe referência a artefato.
* **MCP-AC-005** Quando chamar tool com token inválido, recebe `401 unauthorized`.
* **MCP-AC-006** Quando chamar tool com rate limit excedido, recebe `429 rate_limited`.

## Dependências

* Spec 001 (Platform Foundation).
* Spec 003 (Run Lifecycle).

## Riscos

* Mudança no padrão MCP pode exigir revisão.
* Versionamento incorreto pode quebrar clientes.

## Decisões relacionadas

* [ADR-0002](../adr/0002-bridge-as-mcp-server.md)

## Questões em aberto

* Política de rate limit exata.
* Suporte a SSE streaming para eventos em tempo real.

## Fora de escopo

* Autenticação avançada (OAuth, OIDC).
* Multi-tenant.

## Estratégia de entrega incremental

1. Esqueleto MCP server.
2. Schemas JSON.
3. `repositories_list` e `agents_list`.
4. `run_create`, `run_get`, `run_cancel`.
5. `run_report`.
6. `run_diff`.
7. `review_create`.
8. Idempotência e versionamento.
