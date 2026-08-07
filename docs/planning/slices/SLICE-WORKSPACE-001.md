# SLICE-WORKSPACE-001 — Workspace Manager (Slice 2.1.1)

> **Status:** Proposed (planejamento)
> **ID:** SLICE-WORKSPACE-001
> **Epic:** 2 — MVP Workspace-Write
> **Capability:** 2.1 — Workspace isolado
> **Branch:** `slice-workspace-001` (a partir de `origin/phase-1-mvp` @ `85449a8`)
> **Documento de escopo:** [`../../discovery/016-workspace-manager-scope.md`](../../discovery/016-workspace-manager-scope.md)
> **Data de início:** 2026-08-01
> **Data planejada de fim da fase de planejamento:** 2026-08-01 (este commit)

## 1. Resumo

A `SLICE-WORKSPACE-001` introduz o **Workspace Manager** do OCAB, o componente
responsável por criar, expor e limpar workspaces Git isolados por execução
workspace-write. Esta página documenta **a fase de planejamento** da slice
(única fase coberta por este commit). A fase de implementação será coberta
por commits subsequentes, **fora do escopo deste commit**, mediante nova
autorização.

> **Importante:** este commit não implementa o `Workspace Manager`. Nenhum
> código de produção é alterado; ver
> [`../../discovery/016-workspace-manager-scope.md` § 5](../../discovery/016-workspace-manager-scope.md#5-fronteiras-e-escopo-da-slice-211)
> para a lista explícita do que está dentro e fora do escopo.

## 2. Contexto

* O `OQ-200` está `Resolved` (PR `#3` mergeada em `origin/phase-1-mvp`,
  commit `6e21573f`), e a transição do Epic 1 → Epic 2 foi autorizada.
* A `SLICE-WORKSPACE-001` foi liberada para início conforme
  [`../backlog.md`](../backlog.md#slice-211--workspace-manager) e
  [`../roadmap.md`](../roadmap.md#slice-211--workspace-manager).
* O método primário para criação de workspace é `git worktree add` (ver
  [`../../discovery/005-workspace-strategy-evaluation.md` § Recomendação](../../discovery/005-workspace-strategy-evaluation.md#recomendação-para-o-mvp)).
* A máquina de estados do `Run` (ver
  [`../../specs/003-run-lifecycle/spec.md` § Estados](../../specs/003-run-lifecycle/spec.md#estados))
  introduz os estados `ValidatingRequest`, `PreparingWorkspace` e `Rejected`,
  que a slice 2.1.1 deve operar.

## 3. Dependências

| Tipo | Item | Estado |
| --- | --- | --- |
| Bloqueio prévio | `SLICE-STAB-003` (slice 1.2.2) — fila persistente + `IRunExecutionCoordinator` | Resolvido (PR #3 `6e21573f`) |
| Bloqueio prévio | `OQ-200` (estabilização do Epic 1) | Resolved |
| Bloqueio prévio | `OQ-201` (contract drift adapter/OpenCode) | Resolved |
| Spec normativa | `specs/007-workspace-isolation/spec.md` | Proposed |
| Spec normativa | `specs/003-run-lifecycle/spec.md` | Proposed |
| Spec adjacente | `specs/005-opencode-runner/spec.md` | Proposed (parcialmente implementada) |
| Spec adjacente | `specs/006-policy-engine/spec.md` | Proposed (não implementada nesta slice) |
| ADR-base | `ADR-0005` (workspaces isolados) | Accepted — **não invalidar** |
| ADR-base | `ADR-0014` (slug registry) | Accepted |
| ADR-base | `ADR-0018` (fila persistente) | Accepted |
| Discovery | `005-workspace-strategy-evaluation.md` | Completed |
| Discovery | `016-workspace-manager-scope.md` (este commit) | Proposed |

## 4. Estratégia da slice (decisões fechadas para 2.1.1)

Esta seção reproduz, em formato operacional, as decisões fechadas no
[`../../discovery/016-workspace-manager-scope.md` § 4](../../discovery/016-workspace-manager-scope.md#4-decisões-registradas-escopo-211).
Em caso de divergência, prevalece o discovery.

### 4.1 Identificadores

* `runId` é `Guid` (RFC 4122), não ULID.
* `runType` e demais enums seguem
  [`../../contracts/run-request.md`](../../contracts/run-request.md).

### 4.2 Branch de execução

* **Formato:** `agent/<repository-slug>/<guid>`.
* **Validação por componentes** (cada componente validado separadamente):

  | Componente | Regra | Falha |
  | --- | --- | --- |
  | `agent` | Igualdade exata com literal `agent` | `400 invalid_branch_component` |
  | `<repository-slug>` | `^[a-z0-9][a-z0-9-]{0,63}$` **e** presente na allowlist | `404 repository_not_found` |
  | `<guid>` | `Guid.TryParse` com `GuidStyles.None` em formato `D` (8-4-4-4-12) **e** igual ao `runId` | `400 invalid_run_id` |
  | Quantidade de `/` | Exatamente 2 após `split('/')` | `400 invalid_branch_format` |

* O `<guid>` é o `runId.ToString("D")` da execução. Não é permitido reaproveitar
  `runId`s (idempotência é por `idempotencyKey`, não por `runId`).

### 4.3 `ValidatingRequest` — elegibilidade estrutural

* A slice 2.1.1 introduz `ValidatingRequest` como **validação de elegibilidade**,
  não como *Validation Pipeline*.
* Verificações executadas (todas estruturais, sem execução de comandos do
  repositório):
  * Slug existe e está ativo (`Repository.archivedAt IS NULL`).
  * `Repository.allowedAgents` contém o `agentId` solicitado.
  * `Repository.writable = true` quando `accessMode = WorkspaceWrite`.
  * `baseReference` resolve para um ref válido na origem (branch ou SHA).
  * `timeoutSeconds` dentro de `[60, 86400]`.
  * `idempotencyKey`, quando fornecido, não colide com payload divergente.
  * Ausência de submodules, Git LFS e alterações locais (ver §4.4).
* O **Validation Pipeline** (comandos declarativos de `validations`) permanece
  na slice 2.1.3 — `SLICE-VALIDATION-001`.

### 4.4 Postura explícita — OQ-012, OQ-013, OQ-014

| OQ | Postura | Estado durante 2.1.1 |
| --- | --- | --- |
| `OQ-012` (submodules) | Rejeitar execuções com `.gitmodules` ou submodules populados na `baseReference`. | Permanece `Open`; bloqueio ativo. |
| `OQ-013` (Git LFS) | Rejeitar execuções com `filter=lfs` em arquivos rastreados na `baseReference`. | Permanece `Open`; bloqueio ativo. |
| `OQ-014` (alterações locais) | Rejeitar execuções quando a origem estiver suja na `baseReference`. | Permanece `Open`; bloqueio ativo. |

> **Importante:** as posturas são *bloqueios ativos* da slice 2.1.1, mas as OQs
> **não são marcadas como `Resolved`** neste commit. A transição para
> `Resolved` exige evidência automatizada (testes), que só virá na fase de
> implementação.

### 4.5 Auditoria de bloqueios

* A tabela `policy_decisions` é **responsabilidade da slice 2.2.1**
  (`SLICE-POLICY-001`) e **não** é criada por 2.1.1.
* Enquanto `policy_decisions` não existir, **todos os bloqueios** desta slice
  são registrados em `run_events` com:
  * `actor = 'workspace_manager'`.
  * `type = 'workspace.rejected'`.
  * `data.decision ∈ {'allow','deny'}`.
  * `data.reason` textual (uma das razões canônicas registradas em §4.4 ou
    outras definidas na fase de implementação).
  * `data.metadata` inclui `branch`, `repositorySlug`, `runId`, `baseReference`,
    e o componente que falhou (quando aplicável).

### 4.6 Retries (OQ-024)

* **Escopo desta slice:** retries **somente** em operações idempotentes de
  setup/cleanup do workspace:
  * `git worktree add` (com cleanup prévio em falha intermediária).
  * `git worktree remove` (com fallback `git worktree prune`).
  * Remoção de `runs/<runId>/workspace` (filesystem).
* **Fora do escopo:** retry do agente (`IRunnerAdapter.*`) e retry de
  requisições HTTP ao OpenCode. Esses cenários permanecem como follow-up da
  `SLICE-STAB-003` e da Spec 005 (`OCR-AC-012`).
* `OQ-024` permanece `Open`; a slice 2.1.1 **não** a fecha.

### 4.7 ADR-0019 — não criar nesta fase

* Esta slice não cria `ADR-0019`. A slice não invalida `ADR-0005` e não
  introduz decisão arquitetural transversal nova.
* Gatilhos para `ADR-0019` em iteração futura estão listados em
  [`../../discovery/016-workspace-manager-scope.md` § 4.7](../../discovery/016-workspace-manager-scope.md#47-adr-0019--quando-e-quando-não-criar).

## 5. Fase 1 — Planejamento (este commit)

### 5.1 Entregáveis

* `docs/discovery/016-workspace-manager-scope.md` (novo) — escopo autoritativo.
* `docs/planning/slices/SLICE-WORKSPACE-001.md` (novo, este arquivo) — operação
  da slice.
* Atualizações pontuais de links em:
  * `docs/planning/backlog.md`.
  * `docs/planning/roadmap.md`.
  * `docs/open-questions.md`.
  * `docs/README.md`.
  * `docs/discovery/README.md`.

### 5.2 Critérios de aceite da fase de planejamento

* [x] Branch `slice-workspace-001` existe e descende de `origin/phase-1-mvp`
      sem rebase/squash/force-push.
* [x] `docs/discovery/016-workspace-manager-scope.md` existe e tem
      `Status: Proposed`.
* [x] `docs/planning/slices/SLICE-WORKSPACE-001.md` existe e tem
      `Status: Proposed`.
* [x] Nenhum arquivo de produção (`src/**`, `db/migrations/**`, `tests/**`,
      `compose.yaml`, `Dockerfile*`, `appsettings*.json`) foi alterado.
* [x] `git status --short` lista apenas arquivos em `docs/**`.
* [x] `git diff --stat` mostra apenas adições em arquivos `docs/**`.
* [x] Commit local com mensagem `docs: define Workspace Manager slice scope`.
* [x] Push **não** foi executado.

### 5.3 Validação (nesta fase)

* `git status --short` — confirma arquivos alterados.
* `git diff --stat` — confirma ausência de diff em `src/**`,
  `db/migrations/**`, `tests/**`, `compose.yaml`, `Dockerfile*`,
  `appsettings*.json`.
* `git log --oneline -3` — confirma proveniência em `origin/phase-1-mvp`
  (`85449a8`).

### 5.4 Riscos da fase de planejamento

* Risco de divergência entre este documento e a futura implementação.
  Mitigação: este discovery + esta slice operacional são a **fonte
  autoritativa** até a fase de implementação; qualquer mudança é registrada
  via commit novo e, se afetar premissa bloqueada, via ADR substituta.

## 6. Fase 2 — Implementação (NÃO coberta por este commit)

A fase de implementação está **explicitamente fora do escopo** deste commit.
Quando autorizada, seguirá (no mínimo):

1. Migration dedicada (ex.: `V007__create_workspaces.sql`) criando
   `workspaces` e, opcionalmente, `workspace_locks` para serialização por
   `repositoryId`.
2. `src/OcabBridge.Api/Application/Workspaces/IWorkspaceManager.cs` (interface)
   e `WorkspaceManager.cs` (implementação).
3. Hook no `IRunExecutionCoordinator` (ou novo `IRunStateTransitionObserver`)
   para acionar `PreparingWorkspace` ao sair de `ValidatingRequest`.
4. Testes:
   * Unitários para validação de branch por componentes.
   * Unitários para detecção de submodules / LFS / origem suja.
   * Integração para `git worktree add` + `git worktree remove` idempotentes.
   * Contrato para evento `workspace.rejected` em `run_events`.
5. Atualização de Spec 003 (estados `ValidatingRequest`, `PreparingWorkspace`,
   `Rejected`) e Spec 007 (validação por componentes).
6. Smoke test ponta a ponta com OpenCode real (depende da autorização dos
   cenários `RealOpenCodePoc` ainda abertos — fora desta slice).
7. Mover OQ-012, OQ-013, OQ-014 para `Resolved` apenas após evidência
   automatizada (testes verdes).

Esses passos **não** fazem parte deste commit.

## 7. Fora do escopo (registro consolidado)

* Implementação de `WorkspaceManager` (fase 2).
* Alteração de `IRunnerAdapter`, `OpenCodeAdapter`, `RunDispatcher`,
  `RunExecutionCoordinator`, `RunQueueWorker`.
* Migrations novas.
* Criação de `ADR-0019` ou de qualquer ADR substituta.
* Composição / edição do `compose.yaml`.
* Push, merge ou criação de PR.
* Cenários `RealOpenCodePoc` `cancel` / `timeout` / `provider-error`
  (permanecem como follow-up da `SLICE-STAB-003` e da Spec 005).
* `Policy Engine` completo (slice 2.2.1).
* `Validation Pipeline` (slice 2.1.3).
* Codex Runner (Epic 4) e Antigravity (Epic 5).

## 8. Definition of Done — fase de planejamento

Adicionalmente ao
[`../definition-of-done.md` § Específico para documentação](../definition-of-done.md#específico-para-documentação):

* [x] Texto revisado e em português (exceto nomes próprios em inglês).
* [x] Links relativos válidos.
* [x] Nenhum diagrama Mermaid (não há nesta fase; se houver, deve renderizar).
* [x] Sem afirmações não verificadas — toda referência a commit/PR/branch é
      comprovável via `git` ou `gh`.
* [x] Marcadores de status aplicados: `Status: Proposed` em ambos os
      documentos.
* [x] ADRs existentes não invalidadas.
* [x] Nenhuma referência a `host.docker.internal`, WSL, `localhost` no host
      ou caminhos absolutos arbitrários foi introduzida.
* [x] Nenhum segredo real inserido.
* [x] Nenhuma alteração em `src/**`, `db/migrations/**`, `tests/**`,
      `compose.yaml`, `Dockerfile*`, `appsettings*.json`.

## 9. Referências relacionadas

* [`../../AGENTS.md`](../../AGENTS.md) — regras inegociáveis.
* [`../../discovery/016-workspace-manager-scope.md`](../../discovery/016-workspace-manager-scope.md) — escopo autoritativo da slice.
* [`../../specs/007-workspace-isolation/spec.md`](../../specs/007-workspace-isolation/spec.md) — spec normativa.
* [`../../specs/003-run-lifecycle/spec.md`](../../specs/003-run-lifecycle/spec.md) — máquina de estados.
* [`../../specs/005-opencode-runner/spec.md`](../../specs/005-opencode-runner/spec.md) — adapter do runner.
* [`../../specs/006-policy-engine/spec.md`](../../specs/006-policy-engine/spec.md) — policy engine (futuro).
* [`../../adr/0005-isolated-workspaces.md`](../../adr/0005-isolated-workspaces.md) — ADR-base.
* [`../../adr/0014-repository-slug-registry.md`](../../adr/0014-repository-slug-registry.md) — slug registry.
* [`../../adr/0018-persistent-run-queue.md`](../../adr/0018-persistent-run-queue.md) — fila persistente.
* [`../../discovery/005-workspace-strategy-evaluation.md`](../../discovery/005-workspace-strategy-evaluation.md) — estratégia de workspace.
* [`../../discovery/014-deterministic-e2e-lifecycle.md`](../../discovery/014-deterministic-e2e-lifecycle.md) — gate de estabilização.
* [`../../discovery/015-opencode-real-provider-poc.md`](../../discovery/015-opencode-real-provider-poc.md) — POC OpenCode real.
* [`../backlog.md`](../backlog.md) — entrada de backlog da slice.
* [`../roadmap.md`](../roadmap.md) — entrada de roadmap da slice.
* [`../definition-of-done.md`](../definition-of-done.md) — DoD.
* [`../../open-questions.md`](../../open-questions.md) — OQ-012, OQ-013, OQ-014, OQ-024, OQ-200.
