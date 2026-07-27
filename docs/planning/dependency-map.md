# Dependency Map — Mapa de dependências entre fases

> **Status:** Proposed

Mapeia dependências entre fases do roadmap.

## Dependências

```mermaid
flowchart LR
    P0[Fase 0] --> P1[Fase 1]
    P1 --> P2[Fase 2]
    P2 --> P3[Fase 3]
    P3 --> P4[Fase 4]
    P4 --> P5[Fase 5]
    P5 --> P6[Fase 6]
    P6 --> P7[Fase 7]
    P7 --> P8[Fase 8]
    P8 --> P9[Fase 9]
    P9 --> P10[Fase 10]
```

## Dependências detalhadas

| Fase | Depende de | Bloqueia |
| --- | --- | --- |
| 0 | — | 1 |
| 1 | 0 | 2, 3, 4 |
| 2 | 1 | 3, 4, 5 |
| 3 | 2 | 4, 5 |
| 4 | 3 | 5 |
| 5 | 4 | 6 |
| 6 | 5 | 7 |
| 7 | 6 | 8, 9 |
| 8 | 7 | 10 |
| 9 | 7 | 10 |
| 10 | 8, 9 | — |

## Dependências entre ADRs

| ADR | Bloqueia/decide |
| --- | --- |
| 0001 | Interação com Odysseus |
| 0002 | Contrato MCP |
| 0003 | Arquitetura de adapters |
| 0004 | Persistência |
| 0005 | Workspace isolation |
| 0006 | Hardening de containers |
| 0007 | Policy engine |
| 0008 | Aprovações humanas |
| 0009 | Vertical OpenCode |
| 0010 | Codex |
| 0011 | Antigravity |
| 0012 | Artefatos |
| 0013 | Rede Docker |
| 0014 | Slug registry |

## Caminho crítico

1. ADR-0001, 0002, 0004, 0006, 0007, 0014 desbloqueiam Fase 1.
2. ADR-0005, 0009 desbloqueiam Fases 2 e 3.
3. ADR-0008 permeia todas as fases.

## Bloqueios potenciais

* Mudança de MCP pode forçar revisão das Fases 1, 3, 4.
* Mudança no OpenCode pode forçar revisão da Fase 3.
* Discovery do Antigravity pode atrasar Fase 9.

## Referências relacionadas

* [`roadmap.md`](roadmap.md)
* [`milestones.md`](milestones.md)
* [`backlog.md`](backlog.md)
