# Milestones — Marcos

> **Status:** Proposed

Define marcos do projeto.

## M0 — Especificação aceita

* 14 ADRs publicadas.
* 14 specs publicadas.
* 7 discovery reports estruturados.
* Roadmap, backlog e risk register prontos.
* Diagramas revisados.
* Questões em aberto classificadas.

## M0.1 — Discovery concluído

* Milestone 0 (Discovery) concluído.
* [`../docs/discovery/phase-0-report.md`](../docs/discovery/phase-0-report.md) publicado.
* Gate do Epic 1 fechado.
* ADRs atualizadas conforme resultados.
* Repositório piloto cadastrado.

> **Nenhuma implementação de produção pode iniciar antes deste marco.**

## M1 — Foundation pronta (Epic 1 — Slices 1.1.1 a 1.1.4)

* Slices 1.1.1 a 1.1.4 do Epic 1 concluídos (Foundation, Repository Registry, OpenCode Read-Only VS, MCP Contract Completion).
* Bridge responde em `/health`, `/ready`, `/metrics`.
* PostgreSQL em execução.
* Logs estruturados.
* Stabilization gate ativo (validação OpenCode real, testes automatizados, CI, licença Apache-2.0, renomeação Milestone/Epic/Slice).

## M2 — Vertical read-only com OpenCode (Epic 1 completo)

* Epic 1 completo (Slices 1.1.1 a 1.1.4).
* Execução read-only via MCP com OpenCode (contrato mínimo).
* Ferramentas MCP completas com autenticação, paginação e idempotência.
* Relatório padronizado.
* Smoke test contra container real do OpenCode executado e evidência registrada em [`../discovery/011-opencode-real-validation.md`](../discovery/011-opencode-real-validation.md).

## M3 — Workspace Write com validação (Epic 2 completo)

* Epic 2 completo (Slices 2.1.1, 2.1.2, 2.1.3, 2.2.1, 2.2.2).
* Execução workspace-write com diff e validações.
* Policy engine rígido.

## M4 — Operação assistida (Epic 3)

* Epic 3 completo (Slices 3.1.1 e 3.1.2).
* Backup e restore operacionais.
* Retenção e limpeza ativas.

## M5 — Codex integrado (Epic 4)

* Epic 4 completo (Slice 4.1.1).
* Codex como executor e revisor.

## M6 — Antigravity integrado (Epic 5)

* Epic 5 completo (Slices 5.1.1 e 5.1.2).
* Antigravity em modo read-only.

## Critérios de marco

Cada marco deve ter:

* Validação técnica.
* Sign-off de patrocinador.
* Documentação atualizada.

## Referências relacionadas

* [`roadmap.md`](roadmap.md)
* [`backlog.md`](backlog.md)
* [`definition-of-done.md`](definition-of-done.md)
* [`../docs/discovery/phase-0-report.md`](../docs/discovery/phase-0-report.md)
