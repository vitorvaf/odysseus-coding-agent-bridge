# Discovery — Milestone 0

> **Status:** Planned
> **Escopo:** exclusivamente documental e de prova técnica em ambiente descartável.
> **Não inclui:** código de produção, migrations, runner definitivo, deploy.

O Milestone 0 (Discovery) do roadmap é composto por **discovery técnico** que antecede qualquer implementação. Seu objetivo é produzir **evidências** que sustentem as decisões bloqueantes listadas em [`../open-questions.md`](../open-questions.md) e em [`../planning/roadmap.md`](../planning/roadmap.md).

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
├── 011-opencode-real-validation.md
├── 012-opencode-contract-spike.md
├── 013-opencode-end-to-end-smoke.md
├── 014-deterministic-e2e-lifecycle.md
├── 015-opencode-real-provider-poc.md
├── 016-workspace-manager-scope.md
└── phase-0-report.md
```

## Sumário dos discovery

| ID | Título | Bloqueia | Prioridade |
| --- | --- | --- | --- |
| 001 | Environment baseline | Todas as fases | P0 |
| 002 | MCP SDK evaluation | Epic 1 | P0 |
| 003 | OpenCode container PoC | Slice 1.1.3 | P0 |
| 004 | Repository pilot selection | Slice 1.1.3 | P0 |
| 005 | Workspace strategy evaluation | Epic 2 | P0 |
| 006 | Authentication and networking | Epic 1 | P0 |
| 007 | Process execution evaluation | Epic 1 | P0 |
| 008 | Resource limits baseline | Epic 1 | P0 |
| 009 | Security discovery | Epic 1 / Epic 2 | P0 |
| 010 | Discovery decisions consolidation | — | — |
| 011 | OpenCode real validation | Epic 1 | P0 |
| 012 | OpenCode contract spike | Epic 1 | P0 |
| 013 | OpenCode end-to-end smoke | Epic 1 | P0 |
| 014 | Deterministic E2E lifecycle | Epic 1 → Epic 2 | P0 |
| 015 | OpenCode real provider PoC | Epic 1 → Epic 2 | P0 |
| 016 | Workspace Manager scope | Epic 2 (Slice 2.1.1) | P0 |

## Gate para iniciar o Epic 1

O Epic 1 (Foundation + Repository Registry + OpenCode Read-Only VS + MCP Contract Completion) só começa quando os itens abaixo estiverem **fechados** com evidência documentada:

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