# Discovery — Fase 0

> **Status:** Planned
> **Escopo:** exclusivamente documental e de prova técnica em ambiente descartável.
> **Não inclui:** código de produção, migrations, runner definitivo, deploy.

A Fase 0 do roadmap é composta por **discovery técnico** que antecede qualquer implementação. Seu objetivo é produzir **evidências** que sustentem as decisões bloqueantes listadas em [`../open-questions.md`](../open-questions.md) e em [`../planning/roadmap.md`](../planning/roadmap.md).

## Princípios

* **Sem código de produção.** Apenas scripts e comandos descartáveis.
* **Sem persistência real.** Containers descartáveis, dados voláteis.
* **Sem credenciais reais.** Tokens e segredos são placeholders.
* **Sem acoplamento ao repositório OCAB.** Evidências ficam em `/tmp/opencode/discovery-*` ou similar.

## Estrutura

```text
docs/discovery/
├── README.md
├── 001-environment-baseline.md
├── 002-mcp-sdk-evaluation.md
├── 003-opencode-container-poc.md
├── 004-repository-pilot-selection.md
├── 005-workspace-strategy-evaluation.md
├── 006-authentication-and-networking.md
├── 007-process-execution-evaluation.md
├── 008-resource-limits-baseline.md
├── 009-security-discovery.md
├── 010-discovery-decisions.md
└── phase-0-report.md
```

## Sumário dos discovery

| ID | Título | Bloqueia | Prioridade |
| --- | --- | --- | --- |
| 001 | Environment baseline | Todas as fases | P0 |
| 002 | MCP SDK evaluation | Fase 1 | P0 |
| 003 | OpenCode container PoC | Fase 3 | P0 |
| 004 | Repository pilot selection | Fase 3 | P0 |
| 005 | Workspace strategy evaluation | Fase 5 | P0 |
| 006 | Authentication and networking | Fase 1 | P0 |
| 007 | Process execution evaluation | Fase 1 | P0 |
| 008 | Resource limits baseline | Fase 1 | P0 |
| 009 | Security discovery | Fase 1 / 7 | P0 |
| 010 | Discovery decisions consolidation | — | — |

## Gate para iniciar a Fase 1

A Fase 1 (Foundation) só começa quando os itens abaixo estiverem **fechados** com evidência documentada:

* [ ] Nome do projeto confirmado.
* [ ] .NET 8 confirmado (ou versão posterior justificada).
* [ ] SDK MCP escolhido (com ADR ou discovery report).
* [ ] OpenCode executado com sucesso em container descartável.
* [ ] Forma de autenticação do OpenCode confirmada.
* [ ] Repositório piloto escolhido e cadastrado.
* [ ] Estratégia inicial de workspace escolhida.
* [ ] Estratégia de subprocesso escolhida.
* [ ] Rede Docker validada (DNS interno, sem Docker socket).
* [ ] Limites iniciais de CPU e memória definidos.
* [ ] ADRs atualizados com os resultados.
* [ ] Riscos de Discovery registrados em [`../planning/risk-register.md`](../planning/risk-register.md).
* [ ] `phase-0-report.md` publicado com sign-off.

## Como executar um discovery

1. Ler o discovery correspondente.
2. Executar os comandos em ambiente descartável.
3. Capturar saídas e artefatos em `/tmp/opencode/discovery-<id>/`.
4. Atualizar o discovery com resultados e conclusões.
5. Quando o discovery estiver completo, marcar o item no gate.

## Como registrar evidências

* Saídas de comando em blocos `bash`.
* Tabelas com versões e capacidades.
* Decisões com link para ADR ou nota técnica.
* Sem credenciais reais em nenhum lugar.

## Referências relacionadas

* [`../planning/roadmap.md`](../planning/roadmap.md)
* [`../planning/backlog.md`](../planning/backlog.md)
* [`../open-questions.md`](../open-questions.md)
* [`../planning/risk-register.md`](../planning/risk-register.md)