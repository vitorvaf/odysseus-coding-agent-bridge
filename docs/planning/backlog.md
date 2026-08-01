# Backlog

> **Status:** Proposed

Backlog hierárquico priorizado em formato Epic → Capability → Slice → Task.

## Estratégia

* Priorizar fatias verticais.
* Cada fatia entrega valor observável.
* Não começar pela implementação simultânea de todos os runners.
* A primeira fatia é o fluxo read-only com OpenCode.

---

## EPIC 0 — Discovery (Milestone 0)

### Capability 0.1 — Discovery técnico

#### Slice 0.1.1 — Environment baseline

**ID:** SLICE-DISCOVERY-001

**Título:** Registrar baseline de ambiente.

**Objetivo:** documentar versões e capacidades do host.

**Motivação:** sem baseline, nenhuma decisão de runtime é segura.

**Dependências:** nenhuma.

**Escopo:**

* OS, kernel, arquitetura.
* Docker Engine e Compose.
* Filesystem e capabilities.
* .NET SDK, Git, utilitários.
* PostgreSQL alvo.
* OpenCode e Odysseus disponíveis.

**Fora de escopo:** qualquer decisão de arquitetura.

**Critérios de aceite:**

* [`../docs/discovery/001-environment-baseline.md`](../docs/discovery/001-environment-baseline.md) preenchido.
* Tabela de versões publicada.
* Sem segredos em nenhum registro.

**Documentação afetada:** docs/discovery/001, open-questions.

#### Slice 0.1.2 — MCP SDK evaluation

**ID:** SLICE-DISCOVERY-002

**Título:** Escolher SDK MCP para .NET.

**Objetivo:** fundamentar escolha de pacote.

**Dependências:** Slice 0.1.1.

**Documentação afetada:** docs/discovery/002, open-questions OQ-001.

#### Slice 0.1.3 — OpenCode container PoC

**ID:** SLICE-DISCOVERY-003

**Título:** Provar OpenCode Server em container descartável.

**Objetivo:** validar viabilidade do runner antes do vertical.

**Dependências:** Slice 0.1.1.

**Documentação afetada:** docs/discovery/003, open-questions OQ-020, OQ-021.

#### Slice 0.1.4 — Repository pilot selection

**ID:** SLICE-DISCOVERY-004

**Título:** Escolher e cadastrar repositório piloto.

**Objetivo:** ter alvo concreto para os primeiros slices.

**Dependências:** Slice 0.1.3.

**Documentação afetada:** docs/discovery/004, examples/repositories, open-questions OQ-010.

#### Slice 0.1.5 — Workspace strategy evaluation

**ID:** SLICE-DISCOVERY-005

**Título:** Escolher estratégia de workspace.

**Objetivo:** fundamentar Epic 2 (Capability 2.1 — Workspace isolado).

**Dependências:** Slice 0.1.4.

**Documentação afetada:** docs/discovery/005, open-questions OQ-011 a OQ-015.

#### Slice 0.1.6 — Authentication and networking

**ID:** SLICE-DISCOVERY-006

**Título:** Validar autenticação e rede Docker.

**Objetivo:** fundamentar Epic 1.

**Dependências:** Slice 0.1.1.

**Documentação afetada:** docs/discovery/006, open-questions OQ-030, OQ-032.

#### Slice 0.1.7 — Process execution evaluation

**ID:** SLICE-DISCOVERY-007

**Título:** Avaliar estratégia de subprocesso.

**Objetivo:** fundamentar policy engine e validation pipeline.

**Dependências:** Slice 0.1.1.

**Documentação afetada:** docs/discovery/007, open-questions OQ-005.

#### Slice 0.1.8 — Phase 0 report

**ID:** SLICE-DISCOVERY-REPORT

**Título:** Consolidar relatório do Milestone 0 (Discovery).

**Objetivo:** liberar gate para Epic 1.

**Dependências:** Slices 0.1.1 a 0.1.7.

**Documentação afetada:** docs/discovery/phase-0-report.md, risk-register, ADRs conforme necessário.

> **Gate para Epic 1:** todos os 13 itens do gate definidos em [`../docs/discovery/README.md`](../docs/discovery/README.md#gate-para-iniciar-o-epic-1) devem estar fechados antes de iniciar Slice 1.1.1.

---

## EPIC 1 — MVP Read-Only

### Capability 1.1 — Vertical slice read-only com OpenCode

#### Slice 1.1.1 — Foundation da plataforma

**ID:** SLICE-FOUNDATION-001

**Título:** Subir Coding Agent Bridge com health checks e persistência básica.

**Objetivo:** ter o bridge respondendo e persistindo `Run`.

**Motivação:** sem base, nenhuma fatia posterior funciona.

**Dependências:** gate do Milestone 0 (Discovery).

**Escopo:**

* Solução .NET 8 (versão confirmada no Discovery 001).
* Compose mínimo.
* Health checks.
* Logs estruturados.
* Tabela `runs`.

**Fora de escopo:**

* Runners reais.
* Validações complexas.

**Critérios de aceite:**

* `docker compose up` levanta bridge e postgres.
* `/health` e `/ready` respondem.
* `Run` pode ser inserido e consultado.

**Validações:**

* Unitários: configuração.
* Contrato: schema de `run_create` mínimo.

**Riscos:** mínimos.

**Documentação afetada:** specs 001, 003.

#### Slice 1.1.2 — Repository Registry mínimo

**ID:** SLICE-REPO-REGISTRY-001

**Título:** Cadastrar e listar repositórios por slug.

**Objetivo:** permitir referência a repositórios por slug em `run_create`.

**Motivação:** entrada de execução depende de repositório válido.

**Dependências:** Slice 1.1.1.

**Escopo:**

* Tabela `repositories`.
* Seed inicial (incluindo repositório piloto do Discovery 004).
* `repositories_list`.

**Fora de escopo:** políticas por repositório.

**Critérios de aceite:**

* `repositories_list` retorna repositórios cadastrados.
* `run_create` com slug inexistente retorna 404.

**Validações:**

* Unitários: validação de slug.
* Contrato: `repositories_list`.

**Riscos:** mínimos.

**Documentação afetada:** spec 002.

#### Slice 1.1.3 — OpenCode Read-Only Vertical Slice

**ID:** SLICE-OPENCODE-READONLY-VS

**Título:** Despachar execução read-only via MCP com contrato mínimo.

**Objetivo:** primeiro vertical realmente utilizável.

**Motivação:** entregar a primeira fatia observável do OCAB (Slice 1.1.3 do Epic 1).

**Dependências:** Slices 1.1.1 e 1.1.2, mais PoC validada em [`../docs/discovery/003-opencode-container-poc.md`](../docs/discovery/003-opencode-container-poc.md).

**Escopo:**

* Adapter OpenCode.
* Sessão e prompt.
* Eventos.
* Cancelamento.
* Timeout.
* Ferramentas MCP mínimas:
  * `repositories_list`
  * `run_create`
  * `run_get`
  * `run_cancel`
  * `run_report`
* Relatório no contrato de `run_report`.

**Fora de escopo:** workspace-write, validações complexas, ferramentas MCP adicionais.

**Critérios de aceite:**

* `run_create` (ReadOnly) retorna `runId` e termina em `Completed`.
* `run_report` contém summary, scope, filesChanged (vazio), findings, artifacts.
* Repositório original não é alterado.
* `run_cancel` funciona.
* Cliente MCP (ou Odysseus) consegue fluxo completo.

**Validações:**

* Integração: OpenCode real em container.
* Contrato: schema de eventos.
* Segurança: runner não root, sem Docker socket.

**Riscos:** instabilidade do OpenCode.

**Documentação afetada:** specs 003, 004, 005, contracts/runner-adapter.

#### Slice 1.1.4 — MCP Contract Completion

**ID:** SLICE-MCP-COMPLETION-001

**Título:** Completar contrato MCP e endurecer aspectos transversais.

**Objetivo:** finalizar ferramentas e qualidades contratuais.

**Motivação:** Slice 1.1.4 do Epic 1.

**Dependências:** Slice 1.1.3.

**Escopo:**

* `agents_list`.
* `run_diff` (modos `summary`, `stat`, `patch`).
* `review_create`.
* Paginação padronizada.
* Erros padronizados.
* Autenticação Bearer.
* Idempotência em `run_create`.
* Limites de payload.
* Versionamento de contrato.
* Headers de correlação.

**Fora de escopo:** SSE streaming, multi-tenant.

**Critérios de aceite:**

* Todas as ferramentas listadas funcionais.
* Erros seguem [`../docs/contracts/errors.md`](../docs/contracts/errors.md).
* Autenticação rejeita chamadas sem token.
* Idempotência validada.
* Limites respeitados.
* Header `X-OCAB-Contract-Version` presente.

**Validações:**

* Contrato: schemas.
* Segurança: auth.

**Documentação afetada:** spec 004, contracts/mcp-tools, contracts/errors.

---

### Capability 1.2 — Estabilização da Fase 1

#### Slice 1.2.1 — Align OpenCode Upstream Contract

**ID:** SLICE-STAB-002

**Título:** Alinhar o `OpenCodeAdapter` ao OpenAPI emitido pela versão fixada do OpenCode.

**Objetivo:** Selecionar e fixar uma versão compatível do OpenCode, alinhar o `OpenCodeAdapter` ao OpenAPI publicado por essa versão e comprovar o lifecycle read-only completo por testes e smoke test real.

**Motivação:** o adapter atual e a Spec 005 assumem rotas no plural (`/sessions`, `/sessions/{id}/prompt`, `/sessions/{id}/cancel`) que o OpenCode Server real não implementa — as rotas oficiais estão no singular (`/session`, `/session/{id}/prompt_async`, `/session/{id}/abort`, `/event`, `/global/health`). Sem alinhamento, o caminho principal `cliente MCP → bridge → OpenCode → repositório piloto` não funciona end-to-end.

**Dependências:** Slice 1.1.4 (MCP Contract Completion); commits `2d5932c` e `f3e08f2` da stabilization gate inicial (qualidade + descoberta).

**Escopo:**

* Contract spike comparativo entre OpenCode v1.17.20 (já validado em host) e v1.18.8 (release oficial de 2026-07-28) — captura de OpenAPI em `/doc`, validação de rotas de sessão, prompt, cancelamento, eventos, autenticação, content-types, códigos HTTP.
* Decisão de versão alvo via ADR-0017 (`Status: Proposed` → `Accepted` somente após smoke test ponta a ponta verde).
* Atualização da Spec 005 para refletir a versão fixada, lifecycle, autenticação, cancelamento, timeout, eventos, erros, content-types aceitos, comportamento diante de HTML inesperado e estratégia de upgrade.
* Correção do `poc/opencode-container/Dockerfile` para fail-fast (`SHELL ["/bin/bash", "-o", "pipefail", "-c"]`, `set -eux`, `command -v opencode`, `opencode --version` no build, sem bind-mount do binário do host).
* Atualização do `src/OcabBridge.Api/Adapters/OpenCodeAdapter.cs` para usar exclusivamente rotas comprovadas pelo OpenAPI, enviar Basic Auth via `OPENCODE_SERVER_PASSWORD`, validar `Content-Type`, rejeitar HTML como `UpstreamContractMismatch`, suportar cancelamento, aplicar timeout, correlacionar sessão e `runId`, normalizar erros sem vazar corpo sensível e registrar versão upstream + checksum do contrato.
* Substituição dos `PlaceholderTests` por testes de contrato e integração reais em `tests/OcabBridge.ContractTests/` e `tests/OcabBridge.IntegrationTests/`.
* Smoke test ponta a ponta documentado em `docs/discovery/012-…` cobrindo: cliente MCP → `run_create` → bridge → OpenCode fixado → sessão real → prompt read-only → eventos → relatório; também `run_cancel`, timeout, runner indisponível, credencial inválida e resposta incompatível.

**Fora de escopo:**

* Atualização do `OpenCodeAdapter` para workspace-write (escopo do Epic 2 / SLICE-WORKSPACE-001).
* Code generation a partir do OpenAPI sem antes validar o spike — a decisão entre client manual, gerado ou sidecar TS fica registrada na ADR-0017.
* Push, merge, abertura ou atualização de PR.

**Bloqueia:**

* Fechamento da `OQ-200` (Estabilização do Epic 1).
* Fechamento da `OQ-201` (contract drift entre `OpenCodeAdapter` e OpenCode Server real).
* Início do `SLICE-WORKSPACE-001` (Slice 2.1.1 — Workspace Manager).

**Critérios de aceite (gate completo):**

* [ ] Target version fixada com tag + digest quando disponível.
* [ ] OpenAPI capturado em `/doc` da versão fixada.
* [ ] `ADR-0017` com `Status: Accepted`.
* [ ] Spec 005 atualizada referenciando o OpenAPI capturado.
* [ ] Dockerfile fail-fast (build aborta se install falhar; `command -v opencode` e `opencode --version` validados no build).
* [ ] Runner não root, `cap_drop: ALL`, sem Docker socket, sem bind-mount do binário do host.
* [ ] `OpenCodeAdapter` alinhado às rotas comprovadas, com Basic Auth, validação de `Content-Type`, rejeição de HTML como `UpstreamContractMismatch` e normalização de erros.
* [ ] Testes de contrato reais cobrindo health, sessão, prompt, cancel, eventos, Basic Auth, desserialização JSON, rejeição de HTML e normalização de 401/404/409/429/5xx, timeout e cancelamento.
* [ ] Testes de integração reais cobrindo subida do Compose, health do runner, criação de sessão via bridge, prompt read-only, persistência de eventos, cancelamento, timeout, restart do runner e repositório piloto inalterado.
* [ ] Smoke test documentado com evidência ponta a ponta.
* [ ] CI verde em todos os lanes.
* [ ] `OQ-200` fechada com referência ao smoke test.
* [ ] `OQ-201` fechada com referência ao smoke test.

**Validações:**

* Unitários: adapter (rotas, headers, desserialização, erros, timeout).
* Contrato: HTTP contra o OpenCode fixado no runner (Testcontainers ou runner local).
* Integração: bridge + Postgres + OpenCode real via docker compose.
* Segurança: Basic Auth enviado, credenciais fora do repo, runner não root.

**Riscos:**

* Mudança de comportamento entre versões do OpenCode exige revisitar `ADR-0017`.
* Versões `:latest` ou sem digest podem introduzir drift silencioso — política fixada em tag + digest.
* Sidecar TypeScript adiciona dependência runtime Node não justificada no MVP — `ADR-0017` deve justificar a escolha entre client manual, gerado e sidecar.

**Documentação afetada:** este backlog, `docs/adr/0017-pin-opencode-version.md`, `docs/specs/005-opencode-runner/spec.md`, `docs/discovery/012-…` (smoke test), `docs/open-questions.md` (fechamento de `OQ-200` e `OQ-201`).

#### Slice 1.2.2 — Deterministic E2E Lifecycle and Awaitable Dispatch

**ID:** SLICE-STAB-003

**Título:** Tornar a execução do Run awaitable, observável e deterministicamente validável ponta a ponta.

**Objetivo:** Eliminar `Task.Run` fire-and-forget do `RunDispatcher`, introduzir `IRunExecutionCoordinator` com fila persistente no Postgres + `BackgroundService` worker + registry em memória de execuções ativas, e validar end-to-end com provedor LLM determinístico que o ciclo completo Pending → Running → Completed (com persistência do relatório), o cancelamento real (Pending/Running → Cancelled) e o timeout real (Running → TimedOut) funcionam.

**Motivação:** O gate parcial da `SLICE-STAB-002` deixou dois pontos críticos sem cobertura ponta a ponta: (a) a execução completa de um prompt read-only não foi comprovada por limitação ambiental (sem provedor LLM); (b) o `Task.Run` fire-and-forget do `RunDispatcher.CreateAsync` dificulta cancelamento, timeout, reconciliação e testes determinísticos. Esses dois pontos atingem o **núcleo do sistema** — execução real e lifecycle observável — e não podem ser diferidos: são pré-condição da escrita em workspace.

**Dependências:** `SLICE-STAB-002` (commit `1b75800`).

**Escopo:**

* `IRunExecutionCoordinator` com `EnqueueAsync(runId, ct)`, `WaitForTerminalAsync(runId, timeout, ct)` e `CancelAsync(runId, reason, ct)`. Implementação usa `runs` table como fila persistente, `BackgroundService` (`RunQueueWorker`) que faz poll de `Pending` runs e executa, registry em memória apenas para execuções ativas (com `CancellationTokenSource` por run e `TaskCompletionSource<RunTerminalResult>` por run para `WaitForTerminalAsync`).
* `RunDispatcher` refatorado: `CreateAsync` apenas persiste Run com `status = Pending` e retorna `runId`; a execução é invocada pelo `RunQueueWorker` no escopo de `IServiceScope`. Toda exceção observada resulta em estado terminal conhecido (`Failed`); cancelamento e timeout são propagados via `CancellationTokenSource` associado à execução.
* `Run` model ganha `TimeoutSeconds` (default configurável) e `ResultJson` (já existe) carregando o relatório final.
* Provedor LLM determinístico para E2E: serviço HTTP mínimo (compatível com OpenAI / `chat/completions`) que responde de modo previsível (resposta concluída, lenta, bloqueada-para-cancelamento, erro de provider, resposta inválida). O OpenCode real continua no caminho; apenas a inferência é determinística.
* Fixtures atualizadas para iniciar OpenCode real + provedor mock em portas separadas, e configurar o OpenCode via `opencode.json` para usar o mock como provider customizado.
* `Bridge` ganha cliente HTTP para o provedor LLM e para o OpenCode; configuração via `Ocab:OpenCodeUrl` (já existe), `Ocab:OpenCodeProviderUrl` (novo) e `Ocab:OpenCodeProviderKey` (novo).
* Atualização de Spec 003 (`run-lifecycle`) e Spec 005 (`opencode-runner`) para refletir ownership observável e fila persistente.
* Atualização de `docs/testing/integration-tests.md` e `docs/testing/acceptance-tests.md` para cobrir completion, cancelamento e timeout E2E com fixture determinística.

**Fora de escopo:**

* Escrita em workspace (`SLICE-WORKSPACE-001`) — ainda não autorizada.
* Code generation ou client gerado para o OpenCode — diferido para revisão futura.
* Push, merge, abertura ou atualização de PR — ainda não autorizados.

**Bloqueia:**

* Fechamento definitivo de `OQ-200` e `OQ-201` (status parcial atual, requer E2E ponta a ponta verde).
* Autorização de `SLICE-WORKSPACE-001` (Epic 2).

**Critérios de aceite (gate completo):**

* [ ] Sem `Task.Run` fire-and-forget não observado em `RunDispatcher` ou em código adjacente de execução.
* [ ] Execuções ativas possuem ownership claro (registry em memória com `CancellationTokenSource` por run).
* [ ] Prompt read-only real termina em `Completed` com relatório final persistido.
* [ ] Resposta final do runner é persistida em `runs.result`.
* [ ] Eventos reais (não apenas `session_started`) são persistidos em `run_events`.
* [ ] `run_cancel` chega à execução ativa e termina em `Cancelled` com `POST /session/{id}/abort` chamado no OpenCode.
* [ ] Timeout configurável termina em `TimedOut` com cancelamento upstream.
* [ ] Erro do provedor (5xx ou resposta inválida) termina em `Failed` com `runner_unavailable` normalizado.
* [ ] Não existem processos órfãos após os testes (OpenCode runner, provedor mock e bridge parados corretamente).
* [ ] Origem Git permanece inalterada (`git rev-parse HEAD` antes/depois).
* [ ] Testes locais verdes (14/14 existentes permanecem; novos testes E2E adicionados).
* [ ] `OQ-200` e `OQ-201` permanecem fechadas com referência à evidência E2E.
* [ ] Novo follow-up (se houver) documentado em `docs/open-questions.md` como não bloqueante.

**Validações:**

* **Unitários** (em `OcabBridge.UnitTests`):
  * Transições de cancelamento (Pending → Cancelling → Cancelled; Running → Cancelling → Cancelled; idempotência).
  * Transições de timeout (Running → TimedOut com persistência do evento).
  * Corrida entre completion e cancelamento (completion vence → status final `Completed`; cancelamento chega tarde → sem transição retroativa).
  * Corrida entre timeout e completion (idem).
  * Exceção não observada no worker → `Failed` terminal garantido.
  * Cancelamento idempotente (cancelar 2× não muda estado pós-terminal).
* **Contrato** (em `OcabBridge.ContractTests`):
  * `POST /session/{id}/abort` é chamado quando cancelamento é sinalizado.
  * `CancellationToken` é propagado para o adapter e para o `HttpClient`.
  * Timeout HTTP do `HttpClient` não é confundido com timeout do Run (são independentes).
  * Resposta final é interpretada como `Completed` (evento `done` no SSE ou provider retorna `done` no JSON).
  * Erro do provedor é normalizado em `runner_unavailable` (5xx) ou `runner_contract_mismatch` (HTML/inválido).
* **Integração** (em `OcabBridge.IntegrationTests`):
  * `RunQueueWorker` consome Run com `status = Pending` da fila persistente.
  * Estado é persistido em Postgres (`runs.status`) e sobrevive a restart do bridge.
  * `WaitForTerminalAsync` retorna o estado terminal após transição.
  * Restart/reconciliação não deixa Run indefinido em `Running` (worker retoma execuções órfãs).
  * `CancelAsync` chega à execução ativa via `CancellationTokenSource` registrado.
* **E2E real** (em `OcabBridge.IntegrationTests`, fixture determinística):
  * Prompt read-only termina em `Completed` com resposta persistida.
  * Cancelamento termina em `Cancelled` com `POST /abort` chamado.
  * Timeout termina em `TimedOut`.
  * Erro do provedor termina em `Failed`.
  * Origem Git permanece intacta (`git rev-parse HEAD` antes/depois).
  * Nenhum processo fica ativo após os testes.

**Riscos:**

* Provedor LLM determinístico pode divergir de provedores reais (OpenAI/Anthropic) em respostas de streaming. Mitigação: o fixture valida apenas o protocolo HTTP, não a qualidade do modelo; o adapter continua agnóstico ao provider.
* Restart do bridge durante execução pode deixar Run em `Running` sem owner. Mitigação: o `RunQueueWorker` retoma Runs com `status = Running` que tenham `updated_at` mais antigo que `timeout` (reconciliação).
* Concorrência entre múltiplos workers (em deploy com réplicas) pode levar a execução duplicada. Mitigação: `TryClaimPendingAsync` usa `SELECT ... FOR UPDATE SKIP LOCKED` (Postgres) para garantir claim atômico.

**Impacto operacional:**

* Bridge passa a depender de `Ocab:OpenCodeProviderUrl` e `Ocab:OpenCodeProviderKey` na configuração; placeholders `__SET_ME__` no `appsettings.json` e `compose.yaml`.
* `compose.yaml` ganha um novo serviço `ocab-opencode-provider` (imagem base `node:20-alpine` ou similar) com o provedor determinístico; porta exposta para o `ocab-opencode-runner`.
* Dockerfile do provider adicionado em `poc/opencode-provider/` com fail-fast consistente com o do runner.
* Postgres ganha coluna `timeout_seconds` (default 300) na tabela `runs` (migration `V006__add_run_timeout.sql`).

**Impacto de segurança:**

* Provider key é placeholder `__SET_ME__` real lido de env var; nunca commitada.
* Sem Docker socket; sem bind-mount; sem `privileged`; capabilities zeradas no provider (mesmo padrão do runner).
* Logs do provider passam por redaction.

**Critérios para revisitar:**

* Adoção de `LISTEN/NOTIFY` do Postgres para eliminar o poll loop (atualmente 500 ms).
* Substituição do `TaskCompletionSource` em memória por Redis ou stream distribuído se o bridge rodar em múltiplas réplicas.

**Documentação afetada:** este backlog, `docs/adr/0018-persistent-run-queue.md`, `docs/discovery/014-deterministic-e2e-lifecycle.md`, `docs/specs/003-run-lifecycle/spec.md`, `docs/specs/005-opencode-runner/spec.md`, `docs/testing/integration-tests.md`, `docs/testing/acceptance-tests.md`, `db/migrations/V006__add_run_timeout.sql`, `poc/opencode-provider/Dockerfile`, `compose.yaml`, `src/OcabBridge.Api/Application/IRunExecutionCoordinator.cs` (novo), `src/OcabBridge.Api/Application/RunExecutionCoordinator.cs` (novo), `src/OcabBridge.Api/Application/RunQueueWorker.cs` (novo), `src/OcabBridge.Api/Application/RunDispatcher.cs` (refactor), `src/OcabBridge.Api/Domain/Run.cs` (novo campo `TimeoutSeconds`), `src/OcabBridge.Api/Infrastructure/Persistence/RunRepository.cs` (novos métodos).

## EPIC 2 — MVP Workspace-Write

### Capability 2.1 — Workspace isolado

#### Slice 2.1.1 — Workspace Manager

**ID:** SLICE-WORKSPACE-001

**Título:** Criar e limpar workspaces isolados.

**Objetivo:** permitir alterações em workspace dedicado.

**Motivação:** execução workspace-write exige isolamento.

**Dependências:** Slice 1.1.3.

**Documento de escopo:** [`../docs/discovery/016-workspace-manager-scope.md`](../docs/discovery/016-workspace-manager-scope.md).
**Documento operacional da slice:** [`../docs/planning/slices/SLICE-WORKSPACE-001.md`](../docs/planning/slices/SLICE-WORKSPACE-001.md).

**Escopo:**

* Criação de volume.
* Montagem read-only na origem.
* Branch de execução.
* Cleanup.

**Fora de escopo:** snapshots.

**Critérios de aceite:**

* Workspace criado para execução workspace-write.
* Origem read-only.
* Cleanup após retenção.

**Validações:**

* Integração: criação e limpeza.
* Segurança: sem escrita na origem.

**Riscos:** disco.

**Documentação afetada:** spec 007.

#### Slice 2.1.2 — Git diff e status

**ID:** SLICE-DIFF-001

**Título:** Coletar Git diff e status.

**Objetivo:** fornecer diff ao relatório.

**Motivação:** relatório precisa mostrar alterações.

**Dependências:** Slice 2.1.1.

**Escopo:**

* `git status` e `git diff` no workspace.
* Persistência como artefato.
* `run_diff` MCP.

**Fora de escopo:** merge, push.

**Critérios de aceite:**

* `run_diff` retorna `summary`, `stat`, `patch`.
* Patch > 1 MB vira referência a artefato.

**Validações:**

* Integração: git real.

**Riscos:** baixo.

**Documentação afetada:** spec 009, contratos.

#### Slice 2.1.3 — Validation Pipeline

**ID:** SLICE-VALIDATION-001

**Título:** Executar comandos de validação por repositório.

**Objetivo:** validar alterações antes do relatório final.

**Motivação:** garantir que alterações não quebram testes/lint.

**Dependências:** Slice 2.1.2.

**Escopo:**

* Comandos declarativos.
* Timeout por comando.
* Captura de stdout/stderr.
* Status agregado.

**Fora de escopo:** comandos condicionais avançados.

**Critérios de aceite:**

* Validações executadas em sequência.
* Falha não confunde com erro de infraestrutura.
* Status final reflete validação.

**Validações:**

* Integração: comandos reais.

**Riscos:** timeout inadequado.

**Documentação afetada:** spec 008.

### Capability 2.2 — Hardening de segurança

#### Slice 2.2.1 — Policy Engine

**ID:** SLICE-POLICY-001

**Título:** Aplicar política determinística em todas as fases.

**Objetivo:** bloquear comandos sensíveis e auditar decisões.

**Motivação:** ADR-0007.

**Dependências:** Slice 1.1.3.

**Escopo:**

* Avaliação pré-execução.
* Avaliação por comando.
* Auditoria em `policy_decisions`.

**Fora de escopo:** políticas dinâmicas.

**Critérios de aceite:**

* `git push` bloqueado.
* Path traversal bloqueado.
* Decisões registradas.

**Validações:**

* Segurança.

**Riscos:** listas desatualizadas.

**Documentação afetada:** spec 006.

#### Slice 2.2.2 — Limites de recursos e auditoria

**ID:** SLICE-RESOURCE-LIMITS-001

**Título:** Aplicar limites de CPU/memória/PIDs e audit.

**Objetivo:** prevenir abuso.

**Motivação:** ADR-0006.

**Dependências:** Slice 2.2.1.

**Escopo:**

* Compose com limites.
* Auditoria completa.

**Fora de escopo:** alertas.

**Critérios de aceite:**

* Runner não executa como root.
* Limites aplicados.
* Métricas expostas.

**Validações:**

* Segurança.

**Riscos:** baixo.

**Documentação afetada:** specs 011, 014.

---

## EPIC 3 — Operação

### Capability 3.1 — Operação contínua

#### Slice 3.1.1 — Backup e restore

**ID:** SLICE-BACKUP-001

**Título:** Implementar backup automatizado e restore testado.

**Objetivo:** garantir recuperação.

**Motivação:** ADR-0012.

**Dependências:** Slice 2.2.2.

**Escopo:**

* Backup diário.
* Restore testado em staging.

**Fora de escopo:** backup remoto.

**Critérios de aceite:**

* Backup executado e validado.
* Restore recupera estado.

**Validações:**

* Integração.

**Riscos:** corrupção de backup.

**Documentação afetada:** spec 014.

#### Slice 3.1.2 — Retenção e limpeza

**ID:** SLICE-RETENTION-001

**Título:** Aplicar retenção configurável e limpeza agendada.

**Objetivo:** controlar uso de disco.

**Motivação:** ADR-0012.

**Dependências:** Slice 3.1.1.

**Escopo:**

* Cron interna.
* Lock por `runId`.

**Fora de escopo:** limpeza de logs.

**Critérios de aceite:**

* Execuções expiradas removidas.
* Logs de limpeza.

**Validações:**

* Integração.

**Riscos:** baixo.

**Documentação afetada:** spec 014.

---

## EPIC 4 — Codex Runner (pós-MVP)

### Capability 4.1 — Integração Codex

#### Slice 4.1.1 — Discovery e adapter Codex

**ID:** SLICE-CODEX-001

**Título:** Validar Codex CLI e implementar adapter.

**Objetivo:** adicionar Codex como executor/revisor.

**Motivação:** ADR-0010.

**Dependências:** M3.

**Escopo:**

* Discovery.
* Adapter.

**Fora de escopo:** revisão avançada.

**Critérios de aceite:**

* Adapter Codex implementa `IRunnerAdapter`.
* Execução read-only e workspace-write funcionam.

**Validações:**

* Integração.

**Riscos:** mudanças de CLI.

**Documentação afetada:** spec 012.

---

## EPIC 5 — Antigravity (pós-MVP)

### Capability 5.1 — Discovery e integração Antigravity

#### Slice 5.1.1 — Discovery da interface

**ID:** SLICE-ANTIGRAVITY-DISCOVERY-001

**Título:** Validar interface disponível do Antigravity.

**Objetivo:** fundamentar integração.

**Motivação:** ADR-0011.

**Dependências:** M3.

**Escopo:**

* Documentação de descoberta.

**Fora de escopo:** implementação.

**Critérios de aceite:**

* Decisões registradas em ADR.

**Validações:**

* Não aplicável nesta fase.

**Riscos:** interface indisponível.

**Documentação afetada:** spec 013.

#### Slice 5.1.2 — Adapter Antigravity read-only

**ID:** SLICE-ANTIGRAVITY-001

**Título:** Integrar Antigravity em modo read-only.

**Objetivo:** executar análise read-only.

**Motivação:** ADR-0011.

**Dependências:** Slice 5.1.1.

**Escopo:**

* Adapter.
* Execução read-only.

**Fora de escopo:** escrita.

**Critérios de aceite:**

* Execução read-only funciona.
* Revisão sobre projetos legados suportada.

**Validações:**

* Integração.

**Riscos:** mudanças de interface.

**Documentação afetada:** spec 013.

---

## Critérios de aceite do MVP

1. Cliente MCP consegue listar repositórios autorizados.
2. Cliente MCP consegue listar agentes e capacidades.
3. Execução read-only pode ser criada.
4. Execução recebe identificador único.
5. Estado pode ser consultado.
6. Execução pode ser cancelada.
7. Timeout encerra a execução.
8. Histórico permanece após restart.
9. Repositório original não é alterado.
10. Execução de escrita utiliza workspace isolado.
11. Diff pode ser consultado.
12. Validações configuradas são executadas.
13. Logs e artefatos relacionados ao `runId`.
14. Caminho físico arbitrário rejeitado.
15. Path traversal rejeitado.
16. Symlink escape rejeitado.
17. Repositório read-only não aceita escrita.
18. Agente não autorizado rejeitado.
19. `git push` não pode ser executado.
20. Runner não executa como root.
21. Nenhum serviço possui Docker socket.
22. Segredos não aparecem em prompts ou logs.
23. Execuções concorrentes de escrita não compartilham workspace.
24. Falha de validação não é confundida com falha de infraestrutura.
25. Relatório final segue contrato padronizado.

## Próximo slice recomendado

**Estabilização do Epic 1 (gate atual) — fechada.**

A PR #3 (`STAB-005A.3: Complete OpenCode bridge run via session messages`) foi mergeada em `phase-1-mvp` no commit `6e21573f` (head do `stab-005a` em `d1001d3`). Completion real provado via `GET /session/{id}/message`; `GET /event` é pump SSE auxiliar. Cenários `RealOpenCodePoc` `cancel`/`timeout`/`provider-error` permanecem como follow-up fora da STAB-005A.3.

O próximo slice de implementação é o **SLICE-WORKSPACE-001 (Slice 2.1.1 — Workspace Manager)**, definido neste backlog sob `Epic 2 / Capability 2.1 — Workspace isolado`. A OQ-200 está `Resolved` e a transição do Epic 1 para o Epic 2 está liberada. O escopo e a estratégia da slice estão versionados em [`../docs/discovery/016-workspace-manager-scope.md`](../docs/discovery/016-workspace-manager-scope.md) e em [`../docs/planning/slices/SLICE-WORKSPACE-001.md`](../docs/planning/slices/SLICE-WORKSPACE-001.md) (fase de planejamento — implementação não iniciada).

O Milestone 0 (Discovery) já está fechado conforme [`../docs/discovery/phase-0-report.md`](../docs/discovery/phase-0-report.md); o gate para iniciar o Epic 1 também já foi superado (slices 1.1.1 a 1.1.4 entregues). O gate de estabilização foi superado pelo merge da PR #3; Epic 2 / SLICE-WORKSPACE-001 está liberado para início.
