# Backlog

> **Status:** Proposed

Backlog hierárquico priorizado em formato Epic → Capability → Slice → Task.

## Estratégia

* Priorizar fatias verticais.
* Cada fatia entrega valor observável.
* Não começar pela implementação simultânea de todos os runners.
* A primeira fatia é o fluxo read-only com OpenCode.

---

## EPIC 1 — MVP Read-Only

### Capability 1.1 — Vertical slice read-only com OpenCode

#### Slice 1.1.1 — Fundação da plataforma

**ID:** SLICE-FOUNDATION-001

**Título:** Subir Coding Agent Bridge com health checks e persistência básica.

**Objetivo:** ter o bridge respondendo e persistindo `Run`.

**Motivação:** sem base, nenhuma fatia posterior funciona.

**Dependências:** nenhuma.

**Escopo:**

* Solução .NET 8.
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
* Seed inicial.
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

#### Slice 1.1.3 — OpenCode Adapter read-only

**ID:** SLICE-OPENCODE-READONLY-001

**Título:** Despachar execução read-only para OpenCode Runner e receber resultado.

**Objetivo:** vertical completo read-only.

**Motivação:** validar o fluxo MCP → bridge → runner → relatório.

**Dependências:** Slices 1.1.1 e 1.1.2.

**Escopo:**

* Adapter OpenCode.
* Sessão e prompt.
* Eventos.
* Cancelamento.
* Timeout.
* `run_report` simplificado.

**Fora de escopo:** workspace-write, validações complexas.

**Critérios de aceite:**

* `run_create` (ReadOnly) retorna `runId` e termina em `Completed`.
* `run_report` contém summary, scope, filesChanged (vazio), findings, artifacts.
* Repositório original não é alterado.
* `run_cancel` funciona.

**Validações:**

* Integração: OpenCode real em container.
* Contrato: schema de eventos.

**Riscos:** instabilidade do OpenCode.

**Documentação afetada:** specs 003, 004, 005.

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

Slice 1.1.1 — Fundação da plataforma, precedido por qualquer descoberta documental pendente (revisão da Fase 0).
