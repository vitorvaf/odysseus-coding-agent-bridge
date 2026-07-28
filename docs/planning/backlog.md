# Backlog

> **Status:** Proposed

Backlog hierárquico priorizado em formato Epic → Capability → Slice → Task.

## Estratégia

* Priorizar fatias verticais.
* Cada fatia entrega valor observável.
* Não começar pela implementação simultânea de todos os runners.
* A primeira fatia é o fluxo read-only com OpenCode.

---

## EPIC 0 — Discovery (Fase 0)

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

**Objetivo:** fundamentar Fase 5.

**Dependências:** Slice 0.1.4.

**Documentação afetada:** docs/discovery/005, open-questions OQ-011 a OQ-015.

#### Slice 0.1.6 — Authentication and networking

**ID:** SLICE-DISCOVERY-006

**Título:** Validar autenticação e rede Docker.

**Objetivo:** fundamentar Fase 1.

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

**Título:** Consolidar relatório da Fase 0.

**Objetivo:** liberar gate para Fase 1.

**Dependências:** Slices 0.1.1 a 0.1.7.

**Documentação afetada:** docs/discovery/phase-0-report.md, risk-register, ADRs conforme necessário.

> **Gate para Fase 1:** todos os 13 itens do gate definidos em [`../docs/discovery/README.md`](../docs/discovery/README.md#gate-para-iniciar-a-fase-1) devem estar fechados antes de iniciar Slice 1.1.1.

---

## EPIC 1 — MVP Read-Only

### Capability 1.1 — Vertical slice read-only com OpenCode

#### Slice 1.1.1 — Foundation da plataforma

**ID:** SLICE-FOUNDATION-001

**Título:** Subir Coding Agent Bridge com health checks e persistência básica.

**Objetivo:** ter o bridge respondendo e persistindo `Run`.

**Motivação:** sem base, nenhuma fatia posterior funciona.

**Dependências:** gate da Fase 0.

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

**Motivação:** entregar a primeira fatia observável do OCAB (Fase 3 do roadmap).

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

**Motivação:** Fase 4 do roadmap.

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

## EPIC 2 — MVP Workspace-Write

### Capability 2.1 — Workspace isolado

#### Slice 2.1.1 — Workspace Manager

**ID:** SLICE-WORKSPACE-001

**Título:** Criar e limpar workspaces isolados.

**Objetivo:** permitir alterações em workspace dedicado.

**Motivação:** execução workspace-write exige isolamento.

**Dependências:** Slice 1.1.3.

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

**Nenhuma implementação deve ser iniciada.**

A próxima entrega é **exclusivamente documental e de discovery técnico** da Fase 0:

* Slice 0.1.1 — Environment baseline.
* Slice 0.1.2 — MCP SDK evaluation.
* Slice 0.1.3 — OpenCode container PoC.
* Slice 0.1.4 — Repository pilot selection.
* Slice 0.1.5 — Workspace strategy evaluation.
* Slice 0.1.6 — Authentication and networking.
* Slice 0.1.7 — Process execution evaluation.
* Slice 0.1.8 — Phase 0 report.

A Fase 1 (Foundation) só inicia quando o gate definido em [`../docs/discovery/README.md`](../docs/discovery/README.md#gate-para-iniciar-a-fase-1) estiver fechado.
