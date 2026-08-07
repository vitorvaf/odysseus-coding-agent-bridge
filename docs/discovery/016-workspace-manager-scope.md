# Discovery 016 — Workspace Manager Scope (SLICE-WORKSPACE-001)

> **Status:** Proposed
> **Bloqueia:** início da implementação do Workspace Manager (Slice 2.1.1 — Epic 2 / Capability 2.1).
> **Prioridade:** P0
> **Data:** 2026-08-01
> **Branch:** `slice-workspace-001` (a partir de `origin/phase-1-mvp` no commit `85449a8`)

## 1. Objetivo

Documentar o **escopo e a estratégia da SLICE-WORKSPACE-001** (primeira slice do Epic 2 — MVP
Workspace-Write) **antes de qualquer alteração em código de produção**. Este discovery é o
primeiro artefato versionado da slice e estabelece os contratos, invariantes e fronteiras que
orientam a futura implementação do `Workspace Manager`.

> **Importante:** este commit **não implementa** o `Workspace Manager`. Nenhum arquivo de
> código (`src/**`), nenhuma migration (`db/migrations/**`) e nenhum artefato executável é
> alterado por esta slice. O objetivo é apenas **versionar o planejamento aprovado em chat**,
> transformando o rascunho aprovado em documento de discovery autoritativo.

## 2. Contexto

### 2.1 Estado de proveniência

* A stabilization gate `STAB-005A` / `OQ-200` foi fechada e mesclada em `origin/phase-1-mvp`
  no commit `85449a8 docs: close STAB-005A gate and reopen workspace planning`.
* A PR `#3` (`STAB-005A.3: Complete OpenCode bridge run via session messages`,
  commit `6e21573f`) foi mergeada, comprovando completion real via
  `GET /session/{id}/message` (terminal authority) e mantendo `GET /event` como pump SSE
  auxiliar de `RunEvent`. Os cenários `RealOpenCodePoc` `cancel` / `timeout` / `provider-error`
  permanecem como follow-up **fora** do escopo desta slice.
* A `SLICE-WORKSPACE-001` está autorizada a iniciar; este discovery é o **passo 0** da slice.

### 2.2 Documentos de referência

| Documento | Papel |
| --- | --- |
| [`../specs/007-workspace-isolation/spec.md`](../specs/007-workspace-isolation/spec.md) | Spec de isolamento de workspace (base normativa). |
| [`../specs/003-run-lifecycle/spec.md`](../specs/003-run-lifecycle/spec.md) | Máquina de estados; introduz `ValidatingRequest` e `PreparingWorkspace`. |
| [`../specs/005-opencode-runner/spec.md`](../specs/005-opencode-runner/spec.md) | Adapter `IRunnerAdapter`; **não** deve ser alterado por esta slice. |
| [`../specs/006-policy-engine/spec.md`](../specs/006-policy-engine/spec.md) | Policy Engine; pode ser invocado, mas a tabela `policy_decisions` ainda não existe. |
| [`../adr/0005-isolated-workspaces.md`](../adr/0005-isolated-workspaces.md) | ADR-base para workspaces isolados. **Não invalidar.** |
| [`../adr/0014-repository-slug-registry.md`](../adr/0014-repository-slug-registry.md) | Identificação de repositórios por slug. |
| [`../adr/0018-persistent-run-queue.md`](../adr/0018-persistent-run-queue.md) | Fila persistente; provê `IRunExecutionCoordinator` consumido pelo `Workspace Manager`. |
| [`../discovery/005-workspace-strategy-evaluation.md`](../discovery/005-workspace-strategy-evaluation.md) | Estratégia primária `git worktree add`; secundária leitura direta da origem read-only. |
| [`../discovery/011-opencode-real-validation.md`](../discovery/011-opencode-real-validation.md) | Limites de validação real do OpenCode já cobertos. |

## 3. Problema

A slice 2.1.1 do backlog (`SLICE-WORKSPACE-001`) é descrita em alto nível no
[`../planning/backlog.md`](../planning/backlog.md#slice-211--workspace-manager), mas não
estabelece, em texto autoritativo:

* O **formato exato** e a **validação por componentes** do branch de execução.
* A **distinção** entre `ValidatingRequest` (esta slice) e o futuro `Validation Pipeline`
  (slice 2.1.3).
* A estratégia para **bloqueios mínimos** enquanto a tabela `policy_decisions` não existe.
* O **escopo de retries** permitido a uma execução workspace-write no MVP.
* A **postura explícita** quanto a submodules, Git LFS e alterações locais (OQ-012, OQ-013,
  OQ-014).
* A estratégia de **retries** (OQ-024) limitada a operações idempotentes de setup/cleanup.

Este discovery **fixa** esses pontos, transformando decisões de chat em documento
versionado, sem ampliar nem invalidar nenhuma ADR existente.

## 4. Decisões registradas (escopo 2.1.1)

### 4.1 Identificadores

* **`runId` é `Guid` (RFC 4122), não ULID.** Convenção herdada de
  `IRunExecutionCoordinator` (ver `src/OcabBridge.Api/Application/IRunExecutionCoordinator.cs`)
  e mantida na slice 2.1.1. Os exemplos em texto usam o formato abreviado
  `<runId:Guid>` (ex.: `8c4f1a62-7e10-4f01-8c4d-7c4f9b3a1e22`).
* **`runType` e demais enums** permanecem conforme
  [`../contracts/run-request.md`](../contracts/run-request.md).

### 4.2 Branch de execução

* **Formato canônico:** `agent/<repository-slug>/<guid>`.
* **Componentes:**
  1. `agent` — literal fixo.
  2. `<repository-slug>` — slug cadastrado (ADR-0014), validado pelo
     `Repository` allowlist antes da criação do workspace.
  3. `<guid>` — `Guid.ToString("D")` (formato `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`,
     36 caracteres, lowercase) derivado de `runId`.

#### 4.2.1 Validação por componentes

O `Workspace Manager` deve validar **cada componente** separadamente antes de aceitar o
nome da branch. Esta validação é estrutural e não depende do agente executor:

| Componente | Regra de validação | Falha |
| --- | --- | --- |
| `agent` | Igualdade exata com a literal `agent`. | `400 invalid_branch_component` |
| `<repository-slug>` | `^[a-z0-9][a-z0-9-]{0,63}$` (mesmo regex de `repositories.slug`) **e** presença na allowlist. | `404 repository_not_found` |
| `<guid>` | `Guid.TryParse(..., GuidStyles.None)` com `GuidFormat.D` (8-4-4-4-12) **e** conversão sem perda para `Guid` igual ao `runId` da execução. | `400 invalid_run_id` |
| Quantidade de componentes | Exatamente 3 após `split('/')`. | `400 invalid_branch_format` |

A validação por componentes existe porque o nome da branch vira parte de uma referência
versionada e porque a composicionalidade permite falhar com motivo específico por
componente (observabilidade e correção assistida).

### 4.3 `ValidatingRequest` — elegibilidade estrutural, **não** pipeline de validação

`ValidatingRequest` é introduzido pela Spec 003 como estado de máquina imediatamente
anterior a `PreparingWorkspace`. Na slice 2.1.1 ele significa:

* **Elegibilidade estrutural** da requisição — semântica, **não** execução.
* Verificações realizadas:
  * Slug existe e está ativo (`Repository.archivedAt IS NULL`).
  * `Repository.allowedAgents` contém o `agentId` solicitado.
  * `Repository.writable = true` quando `accessMode = WorkspaceWrite`.
  * `baseReference` é um ref válido para a estratégia de workspace escolhida
    (branch ou SHA presente na origem).
  * `timeoutSeconds` dentro dos limites do contrato.
  * `idempotencyKey` válido quando fornecido (sem colisão divergente).
* **Não inclui** a execução de `validations` declarativas do repositório
  (`validations: [...]` em `examples/repositories.example.yaml`). Essas continuam
  diferidas para a slice 2.1.3 — `SLICE-VALIDATION-001` — que implementa o
  *Validation Pipeline* propriamente dito.

A separação é importante para evitar acoplamento prematuro: a slice 2.1.1 entrega o
**Workspace Manager**; a slice 2.1.3 entrega o **Validation Pipeline**. Misturá-los
violaria a estratégia de entrega incremental registrada em
[`../specs/007-workspace-isolation/spec.md`](../specs/007-workspace-isolation/spec.md#estratégia-de-entrega-incremental).

### 4.4 Auditoria de bloqueios antes de `policy_decisions`

* A tabela `policy_decisions` é definida no
  [`../data/relational-model.md`](../data/relational-model.md#policy_decisions), mas a
  slice 2.1.1 **não cria** a tabela nem a migration correspondente. Isso é responsabilidade
  da slice 2.2.1 (`SLICE-POLICY-001`).
* Enquanto `policy_decisions` não existir, **todos os bloqueios** mínimos que esta slice
  precisar registrar (rejeição por slug inválido, agente não permitido, modo de acesso
  incompatível, repositório sujo, presença de submodules, presença de Git LFS, branch
  inválida, `baseReference` não resolvido) são gravados como **eventos** em
  `run_events` com `actor = 'workspace_manager'` e `type = 'workspace.rejected'`,
  incluindo `decision`, `reason` e `metadata` no campo `data`.
* Quando a slice 2.2.1 materializar `policy_decisions`, o `Workspace Manager` deve
  ser atualizado para gravar lá os eventos já com semântica de decisão. Esta evolução
  **não** é escopo de 2.1.1; é registrada como follow-up em
  [`../open-questions.md`](../open-questions.md#repositórios-e-workspaces) por
  referenciamento desta slice.

### 4.5 Escopo de retries (OQ-024)

* **Escopo desta slice:** retries só são permitidos em operações **idempotentes** de
  *setup* e *cleanup* do workspace:
  * `git worktree add` (com cleanup prévio em caso de falha intermediária).
  * `git worktree remove` (com fallback `git worktree prune`).
  * Remoção do diretório temporário sob `runs/<runId>/workspace`.
* **Fora do escopo desta slice:**
  * Retry da execução do agente (`IRunnerAdapter.*`). O adapter não implementa
    retry nesta slice.
  * Retry de `POST /session/*` no OpenCode, mesmo diante de 429/5xx. Esses cenários
    permanecem como follow-up da `SLICE-STAB-003` e da Spec 005
    (`OCR-AC-012` parcialmente implementado).
* Justificativa: o retry do agente é uma decisão arquitetural transversal que afeta o
  adapter, o `IRunExecutionCoordinator` e o `Policy Engine`. Tratar retry do agente
  dentro de 2.1.1 violaria o princípio de **não ampliar escopo** registrado em
  [`../../AGENTS.md`](../../AGENTS.md) e forçaria nova ADR (potencialmente
  `ADR-0019` — ver §4.7).

### 4.6 Postura explícita sobre OQ-012, OQ-013, OQ-014

| OQ | Tema | Postura da SLICE-WORKSPACE-001 | Mecanismo |
| --- | --- | --- | --- |
| `OQ-012` | Submodules | **Rejeitar explicitamente** execuções workspace-write cujo repositório contenha `.gitmodules` ativo na `baseReference` ou cuja origem tenha submodules populados. | `git ls-files --stage` em conjunto com leitura de `.gitmodules`; em caso positivo, evento `workspace.rejected` com `decision = 'deny'`, `reason = 'submodule_not_supported'` e estado final `Rejected`. Permanece `Open` até suporte futuro. |
| `OQ-013` | Git LFS | **Rejeitar explicitamente** execuções workspace-write quando a origem contiver atributos `filter=lfs` rastreados em `baseReference`. | `git check-attr filter --stdin` (ou equivalente) sobre os arquivos rastreados; em caso positivo, evento `workspace.rejected` com `reason = 'lfs_not_supported'` e estado `Rejected`. Permanece `Open` até suporte futuro. |
| `OQ-014` | Alterações locais na origem | **Rejeitar explicitamente** execuções workspace-write se a origem (`Repository.urlCanonical`) estiver com working tree sujo na `baseReference` (inclui `untracked`, `modified`, `staged`, mas **não** confere com o estado do worktree gerenciado pelo bridge). | `git status --porcelain` no repositório bare/materializado do bridge; em caso de saída não-vazia, `workspace.rejected` com `reason = 'origin_dirty'` e estado `Rejected`. |

Essas posturas são **fixadas** por esta slice; reverter qualquer uma exige ADR substituta
(ver §4.7).

### 4.7 ADR-0019 — quando (e quando **não**) criar

* **Não criar `ADR-0019` como parte desta slice de planejamento.** A slice não invalida
  `ADR-0005` (workspaces isolados) e não introduz decisão arquitetural transversal.
* **Gatilhos para criar `ADR-0019`** em iteração futura desta mesma slice:
  1. Decidir método de preparação diferente de `git worktree add`
     (ex.: `git clone`, `cp -a`, `--reference` ou `--shared`) por motivo
     arquitetural novo.
  2. Adotar estratégia de **cleanup automático** disparado por evento externo
     (ex.: `LISTEN/NOTIFY`, cron) que afete o ciclo de vida do bridge.
  3. Adotar qualquer mecanismo de **retry do agente** (fora do escopo desta slice,
     ver §4.5).
  4. Criar a tabela `policy_decisions` (responsabilidade da slice 2.2.1, mas cuja
     omissão aqui é proposital).
* Enquanto nenhum desses gatilhos for disparado, a slice é **compatível** com as ADRs
  existentes e não exige substituição.

## 5. Fronteiras e escopo da slice (2.1.1)

### 5.1 Dentro do escopo

* **Planejamento versionado** (este commit): discovery `016-workspace-manager-scope.md`
  e `docs/planning/slices/SLICE-WORKSPACE-001.md`.
* **Atualizações leves** de links em:
  * `docs/planning/backlog.md` (referência ao discovery e à slice operacional).
  * `docs/planning/roadmap.md` (referência ao discovery e à slice operacional).
  * `docs/open-questions.md` (registro da estratégia adotada para OQ-012, OQ-013,
    OQ-014 e OQ-024; **status permanece `Open`**).
  * `docs/README.md` (link para o discovery e para a slice operacional).
  * `docs/discovery/README.md` (link para o discovery 016).
* **Nada mais.**

### 5.2 Fora do escopo (registrado explicitamente)

* Implementação do `WorkspaceManager` (nova classe / novo serviço em
  `src/OcabBridge.Api/Application/`).
* Alteração de `IRunnerAdapter`, `OpenCodeAdapter`, `RunDispatcher`,
  `RunExecutionCoordinator`, `RunQueueWorker`.
* Nova migration (ex.: `V007__...`); em particular:
  * Nenhuma alteração em `runs`, `run_events`, `repositories`, `agents`.
  * Nenhuma criação de `policy_decisions`, `workspaces`, `workspace_locks`.
* Criação de `ADR-0019` ou de qualquer ADR substituta.
* Composição / edição do `compose.yaml` para adicionar serviço novo ou bind-mount.
* Smoke tests ou suítes de teste de produção (código em `tests/**`).
* Push da branch para `origin` (esta etapa termina com commit local).

## 6. Riscos reconhecidos

| Risco | Mitigação |
| --- | --- |
| Risco de drift entre este discovery e a futura implementação. | Recompilar o `definition-of-done.md` específico da slice operacional antes de iniciar a fase de código; revisar a cada commit de implementação. |
| Risco de sobreposição com `SLICE-POLICY-001` (slice 2.2.1) — em especial a tabela `policy_decisions`. | Bloqueios da 2.1.1 ficam **apenas** em `run_events`; quando 2.2.1 for autorizada, a migração para `policy_decisions` é registrada como follow-up e **não** retroage a auditoria antiga. |
| Risco de descoberta tardia de corner case em submodules/LFS. | Detector é executado no `ValidatingRequest` (estado da máquina, ver §4.3) — falha rápida sem alocar workspace. |
| Risco de conflitar com o cenário `RealOpenCodePoc` ainda aberto. | Cenários de cancel/timeout/provider-error são explicitamente fora do escopo (ver §4.5). |

## 7. Critérios de aceite deste discovery

* [x] Branch `slice-workspace-001` criada a partir de `origin/phase-1-mvp` sem
      rebase, squash ou force push.
* [x] Este arquivo criado em `docs/discovery/016-workspace-manager-scope.md`.
* [x] Slice operacional criado em
      `docs/planning/slices/SLICE-WORKSPACE-001.md`.
* [x] Nenhum arquivo de produção (`src/**`, `db/migrations/**`, `tests/**`,
      `compose.yaml`, `Dockerfile*`, `appsettings*.json`) alterado.
* [x] `git status --short` mostra apenas arquivos `docs/**` novos/alterados.
* [x] Commit local com mensagem `docs: define Workspace Manager slice scope`.
* [x] Push **não** executado — aguardando confirmação explícita.

## 8. Próximos passos (após autorização de implementação)

1. Criar `src/OcabBridge.Api/Application/WorkspaceManager.cs` (esqueleto) e a
   interface `IWorkspaceManager` em slice de **implementação** separada, com
   migration dedicada.
2. Adicionar tabela `workspaces` (id, runId, repositoryId, path, branch,
   sizeBytes, status, createdAt, updatedAt, finishedAt) e, opcionalmente,
   `workspace_locks` para serialização por `repositoryId`.
3. Integrar o `IWorkspaceManager` ao `IRunExecutionCoordinator` durante a
   transição `Pending → ValidatingRequest → PreparingWorkspace → Running`.
4. Adicionar testes unitários para validação de branch por componentes e para
   detecção de submodules/LFS/origem suja.
5. Atualizar `open-questions.md` e mover OQ-012 / OQ-013 / OQ-014 para
   `Resolved` somente após evidência automatizada.

Esses passos estão **fora do escopo deste commit** e exigirão nova autorização
(provavelmente via nova slice ou ADR).

## 9. Referências relacionadas

* [`../../AGENTS.md`](../../AGENTS.md) — regras inegociáveis.
* [`../specs/007-workspace-isolation/spec.md`](../specs/007-workspace-isolation/spec.md)
* [`../specs/003-run-lifecycle/spec.md`](../specs/003-run-lifecycle/spec.md)
* [`../specs/005-opencode-runner/spec.md`](../specs/005-opencode-runner/spec.md)
* [`../specs/006-policy-engine/spec.md`](../specs/006-policy-engine/spec.md)
* [`../adr/0005-isolated-workspaces.md`](../adr/0005-isolated-workspaces.md)
* [`../adr/0014-repository-slug-registry.md`](../adr/0014-repository-slug-registry.md)
* [`../adr/0018-persistent-run-queue.md`](../adr/0018-persistent-run-queue.md)
* [`../discovery/005-workspace-strategy-evaluation.md`](../discovery/005-workspace-strategy-evaluation.md)
* [`../planning/backlog.md`](../planning/backlog.md)
* [`../planning/roadmap.md`](../planning/roadmap.md)
* [`../planning/slices/SLICE-WORKSPACE-001.md`](../planning/slices/SLICE-WORKSPACE-001.md)
* [`../open-questions.md`](../open-questions.md)
