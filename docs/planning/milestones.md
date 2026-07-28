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

* Fase 0 concluída.
* [`../docs/discovery/phase-0-report.md`](../docs/discovery/phase-0-report.md) publicado.
* Gate da Fase 1 fechado.
* ADRs atualizadas conforme resultados.
* Repositório piloto cadastrado.

> **Nenhuma implementação de produção pode iniciar antes deste marco.**

## M1 — Foundation pronta

* Fase 1 concluída.
* Bridge responde em `/health`, `/ready`, `/metrics`.
* PostgreSQL em execução.
* Logs estruturados.

## M2 — Vertical read-only com OpenCode

* Fases 2, 3 e 4 concluídas.
* Execução read-only via MCP com OpenCode (contrato mínimo).
* Ferramentas MCP completas com autenticação, paginação e idempotência.
* Relatório padronizado.

## M3 — Workspace Write com validação

* Fases 5, 6 e 7 concluídas.
* Execução workspace-write com diff e validações.
* Policy engine rígido.

## M4 — Operação assistida

* Fase 10 concluída.
* Backup e restore operacionais.
* Retenção e limpeza ativas.

## M5 — Codex integrado

* Fase 8 concluída.
* Codex como executor e revisor.

## M6 — Antigravity integrado

* Fase 9 concluída.
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
