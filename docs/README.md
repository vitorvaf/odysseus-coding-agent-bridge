# docs — Índice navegável

Documentação do Odysseus Coding Agent Bridge (OCAB). Cada documento abaixo está vinculado a uma das áreas do projeto.

## Estado de entrega

Status do MVP baseado em [`planning/roadmap.md`](planning/roadmap.md) e [`planning/milestones.md`](planning/milestones.md).

| Milestone / Epic | Status | Evidência |
| --- | --- | --- |
| Milestone 0 — Discovery | **Concluído** | [`discovery/phase-0-report.md`](discovery/phase-0-report.md); commits `62e9bcf`, `95d8f04` |
| Epic 1 — MVP Read-Only (Slices 1.1.1–1.1.4) | **Concluído com follow-ups** | smoke test contra stack real; commits `f6ded6e`, `ff26974`, `d65fc5c`, `da4c3b7`, `95d8f04`; stabilization gate em curso |
| Epic 2 — MVP Workspace-Write (Slices 2.1.1–2.2.2) | Pendente | bloqueia em `specs/007-workspace-isolation` + `003-run-lifecycle` diferidos |
| Epic 3 — Operations | Pendente | pós-MVP (ADR-0012) |
| Epic 4 — Codex Runner | Pendente | pós-MVP (ADR-0010) |
| Epic 5 — Antigravity | Pendente | pós-MVP (ADR-0011) |

### Slices Phase 1 entregues

| Slice | Commit | Conteúdo verificado |
| --- | --- | --- |
| 1.1.1 — Foundation | `f6ded6e` | bridge ASP.NET 8 + health + bearer auth + runs |
| 1.1.2 — Repository Registry | `ff26974` | `repositories` table + `repositories_list` + slug validation |
| 1.1.3 — OpenCode Read-Only VS | `d65fc5c` | adapter + dispatcher + MCP + runner container |
| 1.1.4 — MCP Contract Completion | `da4c3b7` | `agents_list`, `run_diff`, `review_create`, idempotência, paginação, `X-OCAB-Contract-Version`, `X-OCAB-Trace-Id` |
| fix(phase1): review fixes | `95d8f04` | correções de Dapper + Dockerfile + Postgres entrypoint |

### Diferido / pendente

* **spec 003 — máquina de estados**: shippamos 7 dos 14 estados (`Pending`, `Running`, `Completed`, `Cancelling`, `Cancelled`, `Failed`, `TimedOut`). Os 7 restantes (`ValidatingRequest`, `PreparingWorkspace`, `ValidatingResult`, `ReviewRequired`, `CompletedWithValidationErrors`, `ChangesRequested`, `Rejected`) entram em Phase 2/3.
* **OpenCode Runner (binário real)**: o build do container em `poc/opencode-container/` testou primitivos de hardening, mas a imagem oficial `ghcr.io/sst/opencode` retornou `denied` neste ambiente. O `Dockerfile` tem fallback documentado (instalador oficial + bind-mount do binário do host). Validação ponta-a-ponta fica para ambiente com acesso ao registry.
* **OQ-100 — LICENSE**: `LICENSE` permanece untracked aguardando confirmação do sponsor (Apache-2.0 assumido em iteração anterior sem autorização).

## Mapa

### Projeto
* [`project/vision.md`](project/vision.md) — visão de produto e motivação.
* [`project/project-charter.md`](project/project-charter.md) — autorização, partes interessadas e governança.
* [`project/scope.md`](project/scope.md) — escopo do MVP, restrições obrigatórias e fora de escopo.
* [`project/stakeholders.md`](project/stakeholders.md) — personas e agentes envolvidos.
* [`project/principles.md`](project/principles.md) — princípios de engenharia.

### Arquitetura
* [`architecture/system-context.md`](architecture/system-context.md) — contexto do sistema (C4 nível 1).
* [`architecture/container-architecture.md`](architecture/container-architecture.md) — containers e topologia Docker.
* [`architecture/component-architecture.md`](architecture/component-architecture.md) — componentes do bridge.
* [`architecture/deployment-architecture.md`](architecture/deployment-architecture.md) — estratégia de deploy.
* [`architecture/runtime-flows.md`](architecture/runtime-flows.md) — fluxos de execução.
* [`architecture/data-architecture.md`](architecture/data-architecture.md) — modelo de dados e persistência.
* [`architecture/security-architecture.md`](architecture/security-architecture.md) — arquitetura de segurança.
* [`architecture/observability-architecture.md`](architecture/observability-architecture.md) — observabilidade.
* [`architecture/system-design.md`](architecture/system-design.md) — consolidação das decisões arquiteturais.

### ADRs
* [`adr/README.md`](adr/README.md) — índice.
* ADRs `0001`–`0016` em [`adr/`](adr/).

### Especificações
* [`specs/001-platform-foundation/spec.md`](specs/001-platform-foundation/spec.md) — fundação da plataforma.
* [`specs/002-repository-registry/spec.md`](specs/002-repository-registry/spec.md) — registro de repositórios.
* [`specs/003-run-lifecycle/spec.md`](specs/003-run-lifecycle/spec.md) — máquina de estados da execução.
* [`specs/004-mcp-contract/spec.md`](specs/004-mcp-contract/spec.md) — contrato MCP do bridge.
* [`specs/005-opencode-runner/spec.md`](specs/005-opencode-runner/spec.md) — OpenCode Runner.
* [`specs/006-policy-engine/spec.md`](specs/006-policy-engine/spec.md) — motor de políticas.
* [`specs/007-workspace-isolation/spec.md`](specs/007-workspace-isolation/spec.md) — isolamento de workspaces.
* [`specs/008-validation-pipeline/spec.md`](specs/008-validation-pipeline/spec.md) — pipeline de validação.
* [`specs/009-artifact-management/spec.md`](specs/009-artifact-management/spec.md) — gestão de artefatos.
* [`specs/010-observability/spec.md`](specs/010-observability/spec.md) — observabilidade.
* [`specs/011-security-hardening/spec.md`](specs/011-security-hardening/spec.md) — endurecimento de segurança.
* [`specs/012-codex-runner/spec.md`](specs/012-codex-runner/spec.md) — Codex Runner (pós-MVP).
* [`specs/013-antigravity-runner/spec.md`](specs/013-antigravity-runner/spec.md) — Antigravity Runner (pós-MVP).
* [`specs/014-operations/spec.md`](specs/014-operations/spec.md) — operações e operação contínua.

### Contratos
* [`contracts/mcp-tools.md`](contracts/mcp-tools.md) — ferramentas MCP expostas.
* [`contracts/run-request.md`](contracts/run-request.md) — contrato de `run_create`.
* [`contracts/run-report.md`](contracts/run-report.md) — contrato de `run_report`.
* [`contracts/runner-adapter.md`](contracts/runner-adapter.md) — contrato entre bridge e runners.
* [`contracts/events.md`](contracts/events.md) — eventos de execução.
* [`contracts/artifacts.md`](contracts/artifacts.md) — estrutura de artefatos.
* [`contracts/errors.md`](contracts/errors.md) — erros padronizados.

### Dados
* [`data/conceptual-model.md`](data/conceptual-model.md) — entidades conceituais.
* [`data/relational-model.md`](data/relational-model.md) — mapeamento relacional.
* [`data/lifecycle-and-retention.md`](data/lifecycle-and-retention.md) — retenção e ciclo de vida.
* [`data/migrations-strategy.md`](data/migrations-strategy.md) — estratégia de migrations.

### Segurança
* [`security/threat-model.md`](security/threat-model.md) — modelagem de ameaças.
* [`security/trust-boundaries.md`](security/trust-boundaries.md) — fronteiras de confiança.
* [`security/secrets-management.md`](security/secrets-management.md) — gestão de segredos.
* [`security/command-policy.md`](security/command-policy.md) — política de comandos.
* [`security/container-hardening.md`](security/container-hardening.md) — endurecimento de containers.
* [`security/incident-response.md`](security/incident-response.md) — resposta a incidentes.

### Operações
* [`operations/deployment.md`](operations/deployment.md) — implantação.
* [`operations/configuration.md`](operations/configuration.md) — configuração por ambiente.
* [`operations/backup-and-restore.md`](operations/backup-and-restore.md) — backup e restore.
* [`operations/health-checks.md`](operations/health-checks.md) — health checks.
* [`operations/logging.md`](operations/logging.md) — logs.
* [`operations/metrics.md`](operations/metrics.md) — métricas.
* [`operations/tracing.md`](operations/tracing.md) — tracing.
* [`operations/retention-and-cleanup.md`](operations/retention-and-cleanup.md) — retenção e limpeza.
* [`operations/troubleshooting.md`](operations/troubleshooting.md) — troubleshooting.

### Testes
* [`testing/strategy.md`](testing/strategy.md) — estratégia.
* [`testing/test-pyramid.md`](testing/test-pyramid.md) — pirâmide de testes.
* [`testing/contract-tests.md`](testing/contract-tests.md) — testes de contrato.
* [`testing/integration-tests.md`](testing/integration-tests.md) — testes de integração.
* [`testing/security-tests.md`](testing/security-tests.md) — testes de segurança.
* [`testing/acceptance-tests.md`](testing/acceptance-tests.md) — testes de aceitação.

### Planejamento
* [`planning/roadmap.md`](planning/roadmap.md) — roadmap por fases.
* [`planning/dependency-map.md`](planning/dependency-map.md) — dependências entre fases.
* [`planning/milestones.md`](planning/milestones.md) — marcos.
* [`planning/backlog.md`](planning/backlog.md) — backlog hierárquico.
* [`planning/risk-register.md`](planning/risk-register.md) — risk register.
* [`planning/definition-of-done.md`](planning/definition-of-done.md) — definition of done.

### Diagramas
* [`diagrams/README.md`](diagrams/README.md) — índice de diagramas.

### Discovery (Milestone 0)
* [`discovery/README.md`](discovery/README.md) — índice do Milestone 0 (Discovery).
* [`discovery/001-environment-baseline.md`](discovery/001-environment-baseline.md)
* [`discovery/002-mcp-sdk-evaluation.md`](discovery/002-mcp-sdk-evaluation.md)
* [`discovery/003-opencode-container-poc.md`](discovery/003-opencode-container-poc.md)
* [`discovery/004-repository-pilot-selection.md`](discovery/004-repository-pilot-selection.md)
* [`discovery/005-workspace-strategy-evaluation.md`](discovery/005-workspace-strategy-evaluation.md)
* [`discovery/006-authentication-and-networking.md`](discovery/006-authentication-and-networking.md)
* [`discovery/007-process-execution-evaluation.md`](discovery/007-process-execution-evaluation.md)
* [`discovery/008-resource-limits-baseline.md`](discovery/008-resource-limits-baseline.md)
* [`discovery/009-security-discovery.md`](discovery/009-security-discovery.md)
* [`discovery/010-discovery-decisions.md`](discovery/010-discovery-decisions.md)
* [`discovery/phase-0-report.md`](discovery/phase-0-report.md)

### Globais
* [`open-questions.md`](open-questions.md) — questões em aberto.
* [`glossary.md`](glossary.md) — glossário.
* [`assumptions.md`](assumptions.md) — premissas e hipóteses.

## Como navegar por objetivo

| Quero entender... | Comece por |
| --- | --- |
| O problema e a visão | `project/vision.md`, `README.md` |
| O escopo do MVP | `project/scope.md`, `planning/roadmap.md` |
| As decisões tomadas | `adr/README.md` |
| O que está pendente para começar | `discovery/README.md`, `discovery/phase-0-report.md` |
| Como o sistema é estruturado | `architecture/system-design.md`, `architecture/container-architecture.md` |
| Como uma execução acontece | `specs/003-run-lifecycle/spec.md`, `contracts/mcp-tools.md` |
| Como os repositórios são protegidos | `specs/002-repository-registry/spec.md`, `specs/007-workspace-isolation/spec.md`, `security/threat-model.md` |
| Como um agente é executado | `specs/005-opencode-runner/spec.md`, `contracts/runner-adapter.md` |
| Como a observabilidade é feita | `architecture/observability-architecture.md`, `specs/010-observability/spec.md` |
| Como operar o sistema | `specs/014-operations/spec.md`, `operations/deployment.md` |
| O que precisa ser feito | `planning/backlog.md`, `planning/roadmap.md` |
| Questões ainda abertas | `open-questions.md` |
