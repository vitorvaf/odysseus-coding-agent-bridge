# Spec 005 — OpenCode Runner

## Status

Proposed (atualizada em 2026-07-28 pelas `SLICE-STAB-002` para refletir o OpenAPI fixado em `v1.18.8` — ver [ADR-0017](../adr/0017-pin-opencode-version.md) e [Discovery 012](../discovery/012-opencode-contract-spike.md); e novamente pela `SLICE-STAB-003` para incluir o caminho de provider customizado determinístico — ver [ADR-0018](../adr/0018-persistent-run-queue.md) e [Discovery 014](../discovery/014-deterministic-e2e-lifecycle.md)). Promover para `Accepted` após smoke test ponta a ponta verde com provedor determinístico.

## Resumo

Define a integração entre o Coding Agent Bridge e o OpenCode Server, incluindo criação de sessão, envio de prompt assíncrono, acompanhamento de eventos via SSE, cancelamento, timeout, captura de resultado, autenticação Basic, detecção de contract drift e testes de contrato.

## Contexto

O OpenCode é o primeiro runner do MVP. Sua API HTTP precisa ser encapsulada por um adapter que implementa `IRunnerAdapter`. A versão alvo é `v1.18.8` (fixada em [ADR-0017](../adr/0017-pin-opencode-version.md)) e o contrato efetivo é o OpenAPI 3.1.0 publicado em runtime pelo servidor em `GET /doc`, capturado em [Discovery 012](../discovery/012-opencode-contract-spike.md). A `SLICE-STAB-003` complementa este contexto com um **provider LLM determinístico** que permite validar o ciclo completo read-only (`Pending → Running → Completed`) end-to-end sem depender de credenciais de provedor pago; ver [Discovery 014](../discovery/014-deterministic-e2e-lifecycle.md) e [ADR-0018 § Provider determinístico](../adr/0018-persistent-run-queue.md#decisão).

## Problema

Como integrar o OpenCode Server de forma isolada, cancelável, observável, pinada por versão e digest, protegida contra contract drift e compatível com o contrato comum?

## Objetivos

* Criar adapter OpenCode alinhado à família `/session/*` (singular) do OpenAPI fixado.
* Estabelecer contrato HTTP entre bridge e OpenCode com version pinning (tag + SHA-256).
* Garantir cancelamento via `POST /session/{sessionID}/abort` e timeout propagado.
* Coletar eventos estruturados via `GET /event` (SSE) ou `GET /api/session/{sessionID}/event`.
* Manter testes de contrato contra o OpenAPI emitido pelo runner real.
* Detectar contract drift (resposta `text/html` quando esperado `application/json`) e falhar alto como `UpstreamContractMismatch`.

## Não objetivos

* Implementar o OpenCode Server.
* Substituir o OpenCode por outro executor no MVP.
* Adotar a família `/api/session/*` (mais nova e mais granular) — diferida para revisão futura.
* Sidecar TypeScript usando o SDK oficial — rejeitado por adicionar dependência runtime Node.

## Escopo funcional

* Container `ocab-opencode-runner` baseado em `node:20-alpine` com binário `opencode v1.18.8` (musl) baixado do GitHub release `anomalyco/opencode`, com SHA-256 fixado em `ARG`, fail-fast (`SHELL pipefail`, `set -eux`, `command -v opencode`, `opencode --version`).
* Adapter no bridge implementando `IRunnerAdapter`, reescrito para a família `/session/*` (singular) e para enviar Basic Auth via `OPENCODE_SERVER_PASSWORD`.
* Endpoints (alinhados ao OpenAPI fixado; veja `Discovery 012` para a evidência completa):
  * `GET /global/health` — liveness (substitui `/health`, que cai em SPA fallback `text/html`).
  * `POST /session` — criar sessão.
  * `POST /session/{sessionID}/prompt_async` — enviar prompt assíncrono (compatível com a máquina de estados persistente do bridge).
  * `GET /event` — stream SSE global (compatível com adapter atual; pode evoluir para `GET /api/session/{sessionID}/event` por sessão).
  * `POST /session/{sessionID}/abort` — cancelar sessão.
  * `GET /session/status` — status global.
* Autenticação Basic com usuário fixo `opencode` e senha via `OPENCODE_SERVER_PASSWORD` (fora do repo).
* `UpstreamContractMismatch` quando `Content-Type` vier `text/html` (route SPA fallback) ou schema JSON divergir do OpenAPI fixado.
* Timeouts configuráveis em `HttpClient.Timeout` no adapter.
* Persistência de eventos em `RunEvent`.
* Healthcheck do container via `wget --spider http://127.0.0.1:4096/global/health`.

## Requisitos funcionais

* **OCR-FR-001** O adapter deve implementar `IRunnerAdapter`.
* **OCR-FR-002** O adapter deve criar sessão antes de enviar prompt.
* **OCR-FR-003** O adapter deve enviar prompt exatamente uma vez por sessão.
* **OCR-FR-004** O adapter deve consumir eventos estruturados do OpenCode.
* **OCR-FR-005** O adapter deve permitir cancelamento cooperativo.
* **OCR-FR-006** O adapter deve respeitar timeout configurado.
* **OCR-FR-007** O adapter deve retornar resultado final padronizado para o bridge.
* **OCR-FR-008** O adapter deve validar saúde do OpenCode antes de despachar.
* **OCR-FR-009** O adapter deve emitir evento `runner_unhealthy` se OpenCode falhar health check.

## Requisitos não funcionais

* **OCR-NFR-001** Container deve executar como usuário não root.
* **OCR-NFR-002** Sem Docker socket.
* **OCR-NFR-003** Limites de CPU e memória configurados.
* **OCR-NFR-004** Health check deve responder em menos de 500 ms.

## Atores e componentes envolvidos

* Bridge.
* OpenCode Runner.
* OpenCode Server.
* Workspace Manager.

## Casos de uso

* Criar sessão read-only.
* Criar sessão workspace-write.
* Acompanhar eventos.
* Cancelar sessão.
* Tratar timeout.

## Fluxos principais

* Despacho: bridge valida `/global/health` → `POST /session` → `POST /session/{sessionID}/prompt_async` → consome `GET /event` (SSE) → recebe evento terminal → padroniza para `RunEvent`.
* Cancelamento: bridge envia `POST /session/{sessionID}/abort` → confirma com runner → atualiza estado da `Run`.

## Fluxos de erro

* OpenCode indisponível → execução `Failed` com `runner_unavailable`.
* Sessão falha ao criar (`POST /session` retorna 401/5xx ou HTML) → execução `Failed` com `session_create_failed`.
* Resposta com `Content-Type: text/html` em endpoint que deveria devolver `application/json` → execução `Failed` com `runner_contract_mismatch` (`UpstreamContractMismatch` registrado no `RunEvent`).
* Cancelamento sem ACK em `cancelGraceSeconds` (default 30 s) → `runner_unresponsive` e fallback para `docker stop` no container (ciclo de vida gerenciado externamente, conforme [ADR-0006](../adr/0006-no-docker-socket.md)).
* 401 → runner requer `OPENCODE_SERVER_PASSWORD` configurado ou senha divergente entre bridge e compose; execução `Failed` com `runner_auth_failed`.
* 404 (sessão inexistente) → execução `Failed` com `session_not_found`.
* 409 (conflito, ex.: sessão já em estado terminal) → execução `Failed` com `session_conflict`.
* 429 (rate limit) → retry com backoff conforme [OQ-024](../open-questions.md); se exceder `maxRetries`, execução `Failed` com `runner_rate_limited`.
* 5xx → execução `Failed` com `runner_unavailable` (não distinguir ainda entre transient e permanente; ver [OQ-024](../open-questions.md)).

## Capacidades suportadas no MVP

* `plan`
* `implement`
* `review`
* `document`

## Capacidades fora do MVP

* Execução paralela multi-agent.
* Sessões persistentes de longa duração entre execuções.

## Contratos

* [`../contracts/runner-adapter.md`](../contracts/runner-adapter.md) — interface comum.
* [`../contracts/events.md`](../contracts/events.md) — eventos.

## Modelo de dados afetado

* `RunEvent` recebe eventos do runner.
* `RunnerHealth` registra saúde observada.

## Segurança

* Autenticação Basic com `OPENCODE_SERVER_PASSWORD` (variável de ambiente; placeholder `__SET_ME__` no `compose.yaml`; valor real em arquivo não versionado).
* Sem Docker socket (mantido conforme [ADR-0006](../adr/0006-no-docker-socket.md)).
* Limites de recursos (`cpus`, `memory`, `pids_limit`) no `compose.yaml`.
* `cap_drop: ALL`, `no-new-privileges`, `read_only` rootfs, `user: "10001:10001"`.
* Logs passam por redaction; `UpstreamContractMismatch` registra tipo MIME e primeiros bytes da resposta para diagnóstico, mas corpo sensível não é logado.
* Pin de versão por tag + SHA-256 do asset binário impede execução de binário não auditado.

## Observabilidade

* Métricas `agent_runner_health`.
* Logs estruturados por evento.
* Spans por fase (`runner.dispatch`, `runner.execute`).

## Estratégia de testes

* Contrato: validar mapping entre eventos OpenCode e `RunEvent`.
* Integração: subir OpenCode real em container e validar fluxo.
* Segurança: ausência de Docker socket, root, etc.
* Falhas: simular indisponibilidade.

## Critérios de aceite

* **OCR-AC-001** Dado um OpenCode Server saudável (`GET /global/health` retorna `{"healthy":true,"version":"1.18.8"}`), quando o adapter criar sessão via `POST /session`, recebe `sessionId` (`ses_…`) e slug.
* **OCR-AC-002** Dado um prompt enviado via `POST /session/{sessionID}/prompt_async`, o adapter acompanha eventos via `GET /event` (SSE) até `done` ou `error` (evento terminal).
* **OCR-AC-003** Dado um cancelamento solicitado via `POST /session/{sessionID}/abort`, o runner confirma cancelamento em menos de 30 s.
* **OCR-AC-004** Dado um timeout expirado, o runner é sinalizado e a execução transita para `TimedOut`.
* **OCR-AC-005** Dado um OpenCode indisponível, a execução vai para `Failed` com `runner_unavailable`.
* **OCR-AC-006** O runner não executa como root.
* **OCR-AC-007** O runner não monta Docker socket.
* **OCR-AC-008** Toda resposta com `Content-Type: text/html` em endpoint que deveria devolver `application/json` é rejeitada como `UpstreamContractMismatch`; a execução transita para `Failed` com `runner_contract_mismatch`.
* **OCR-AC-009** A versão upstream e o checksum do contrato são registrados em cada `Run` (campos `UpstreamVersion` e `ContractChecksum`).
* **OCR-AC-010** 401 (sem Basic Auth ou senha incorreta) é normalizado para `runner_auth_failed`; 404 (`session_not_found`), 409 (`session_conflict`), 429 (`runner_rate_limited`) e 5xx (`runner_unavailable`) são normalizados analogamente.
* **OCR-AC-011** Smoke test ponta a ponta (cliente MCP → `run_create` → bridge → OpenCode → sessão real → prompt read-only → eventos → relatório; também `run_cancel`, timeout, runner indisponível, credencial inválida, resposta incompatível) é executado e a evidência é publicada em `docs/discovery/013-...`.
* **OCR-AC-012** Provider customizado determinístico (compatível com OpenAI `/v1/chat/completions`) é configurado via `opencode.json` em `provider.custom.<name>.baseURL` apontando para serviço HTTP local; o adapter continua agnóstico ao provider e o ciclo completo read-only (`Pending → Running → Completed` com resposta persistida) é validado sem dependência de credencial paga. O OpenCode real permanece no caminho; apenas a inferência é determinística. Coberto por `tests/OcabBridge.IntegrationTests/DeterministicCoordinatorTests` (vide `Discovery 014`).

## Dependências

* Spec 001 (Platform Foundation).
* Spec 003 (Run Lifecycle).
* Spec 006 (Policy Engine).
* ADR-0009.

## Riscos

* Mudança breaking na API do OpenCode pode exigir revisão do adapter.
* Instabilidade do OpenCode Server durante o MVP pode atrasar validação.

## Decisões relacionadas

* [ADR-0009](../adr/0009-opencode-first-runner.md) — OpenCode como primeiro runner (não substituída).
* [ADR-0003](../adr/0003-agent-adapter-boundary.md) — adapters independentes por agente.
* [ADR-0017](../adr/0017-pin-opencode-version.md) — pin de versão `v1.18.8` e contrato HTTP utilizado.

## Questões em aberto

* Política de retries em falhas transitórias (`OQ-024`).
* Mapeamento exato de eventos OpenCode para `RunEvent` (campos ainda não normalizados).
* Adoção futura da família `/api/session/*` (mais granular) — registrada como revisão futura, sem OQ aberta.

## Referências complementares

* [`../discovery/003-opencode-container-poc.md`](../discovery/003-opencode-container-poc.md) — POC de hardening de container.
* [`../discovery/004-repository-pilot-selection.md`](../discovery/004-repository-pilot-selection.md) — repositório piloto usado pela primeira fatia vertical.

## Fora de escopo

* Suporte a múltiplas sessões simultâneas no mesmo runner.
* Sessões persistentes entre execuções.

## Estratégia de entrega incremental

1. Adapter esqueleto.
2. Health check.
3. Criação de sessão.
4. Envio de prompt.
5. Consumo de eventos.
6. Cancelamento.
7. Timeout.
8. Mapeamento de eventos para `RunEvent`.
9. Testes de contrato.
