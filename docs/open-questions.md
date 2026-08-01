# Questões em Aberto

> **Status:** Active

Questões que ainda não foram resolvidas com evidência. Cada item possui ID, prioridade, fase que bloqueia, responsável sugerido, método de validação, resultado esperado, status atual e ADR relacionado.

## Legenda

* **Prioridade:**
  * `P0` — bloqueia início da Foundation.
  * `P1` — bloqueia o primeiro vertical slice (Slice 1.1.3).
  * `P2` — necessário antes do MVP.
  * `P3` — pós-MVP ou evolução.
* **Bloqueia:** fase do roadmap afetada.
* **Resultado:** artefato esperado ao fechar (ADR, discovery report, decisão registrada).
* **Status:** `Open`, `In progress`, `Resolved`, `Blocked`.

## Plataforma e SDK

| ID | Título | Descrição | Bloqueia | Prioridade | Responsável sugerido | Método de validação | Resultado esperado | Status | ADR relacionado |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| OQ-001 | SDK MCP exato | Pacote .NET para MCP Server com Streamable HTTP, autenticação e tools | Epic 1 | P0 | Arquiteto | POC + matriz comparativa em `docs/discovery/002-mcp-sdk-evaluation.md` | ADR atualizado ou substituto | Resolved | 0002 |
| OQ-002 | .NET 8 vs .NET 10 | Ambiente tem .NET 8 e 10; ADR diz "baseline .NET 8" | Epic 1 | P0 | Arquiteto | Discovery 001 + ADR `0015-runtime-version.md` | Decisão registrada | Resolved | 0015 |
| OQ-003 | Nome definitivo do projeto | "OCAB" como sigla de trabalho | Milestone 0 | P0 | Sponsor | Aprovação explícita | Decisão em `project/project-charter.md` | Open | — |
| OQ-004 | Mecanismo final de fila | ADR-0004 fixa PostgreSQL; revisar sob evidência | Epic 1 | P1 | Arquiteto | Benchmark com Testcontainers | ADR mantido ou substituto | Open | 0004 |
| OQ-005 | Biblioteca de subprocessos | Como o bridge controlará processos | Epic 1 | P0 | Plataforma | POC em `docs/discovery/007-process-execution-evaluation.md` | Decisão registrada | Resolved | — |
| OQ-006 | Geração de IDs | ULID vs UUID | Epic 1 | P2 | Plataforma | Testes de ordenação | Decisão registrada | Open | — |
| OQ-090 | Auth MCP do OCAB | Bearer vs mTLS vs OIDC | Epic 1 | P0 | Segurança | POC em `docs/discovery/006-authentication-and-networking.md` | ADR substituto | Resolved | 0002, 0007 |
| OQ-091 | Auth OpenCode | Basic (observado na source v1.18.7) vs outras opções | Slice 1.1.3 | P0 | Plataforma | Discovery 003 + validação no container | Decisão registrada | Resolved | — |
| OQ-201 | Contract drift entre `OpenCodeAdapter` e OpenCode Server real | `src/OcabBridge.Api/Adapters/OpenCodeAdapter.cs` esperava JSON em `/sessions`, `/sessions/{id}/prompt`, `/sessions/{id}/cancel`. OpenCode Server real v1.17.20 retornava HTML SPA nessas rotas | Epic 1 → Epic 2 | P0 | Plataforma | Adapter alinhado à família singular `/session/*` conforme OpenAPI fixado em `v1.18.8`; contract tests 10/10 verdes contra runner real | Adapter alinhado, runner fixado, contract drift eliminado | Resolved | 0017 |

## Repositórios e workspaces

| ID | Título | Descrição | Bloqueia | Prioridade | Responsável sugerido | Método de validação | Resultado esperado | Status | ADR relacionado |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| OQ-010 | Repositório piloto | Sandbox dedicado, não usar projetos sensíveis | Slice 1.1.3 | P0 | Operador | Criar fixture local em `poc/fixtures/pilot-repo` | Slug cadastrado e validado | Resolved | 0014 |
| OQ-011 | Estratégia de workspace | Clone, worktree, copy, --reference | Epic 2 | P0 | Plataforma | POC em `docs/discovery/005-workspace-strategy-evaluation.md` | ADR substituto | Resolved | 0005 |
| OQ-012 | Submodules | Tratamento | Epic 2 | P2 | Plataforma | Teste em fixture com submódulo | Estratégia documentada | Open | — |
| OQ-013 | Git LFS | Tratamento | Epic 2 | P2 | Plataforma | Teste em fixture com LFS | Estratégia documentada | Open | — |
| OQ-014 | Alterações locais | Detecção e tratamento | Epic 2 | P2 | Plataforma | Teste em fixture suja | Estratégia documentada | Open | — |
| OQ-015 | Monorepos | Estratégia futura | Operação | P3 | Plataforma | Análise | Estratégia documentada | Open | — |

## Runners

| ID | Título | Descrição | Bloqueia | Prioridade | Responsável sugerido | Método de validação | Resultado esperado | Status | ADR relacionado |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| OQ-020 | OpenCode lifecycle | Persistente vs por-Run vs efêmero | Slice 1.1.3 | P0 | Plataforma | Discovery 007 | Decisão registrada | Resolved | 0009 |
| OQ-021 | Autenticação OpenCode | Basic Auth confirmada na source v1.18.7 | Slice 1.1.3 | P0 | Segurança | Validação no container | Decisão registrada | In progress | — |
| OQ-022 | Autenticação Codex | A confirmar pós-MVP | Epic 4 | P1 | Plataforma | Discovery futuro | Estratégia definida | Open | — |
| OQ-023 | Interface Antigravity | Em discovery | Epic 5 | P0 | Plataforma | Discovery externo | Discovery report | Open | 0011 |
| OQ-024 | Retries | Política de retries em falhas transitórias | Epic 2 | P1 | Plataforma | Análise | Estratégia documentada | Open | — |

## Segurança

| ID | Título | Descrição | Bloqueia | Prioridade | Responsável sugerido | Método de validação | Resultado esperado | Status | ADR relacionado |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| OQ-030 | Rotação de credenciais | Procedimento | Operação | P1 | Segurança | Documentação | Procedimento em `operations/configuration.md` | Open | — |
| OQ-031 | Limites CPU/memória | Padrão por runner | Epic 1 | P0 | Plataforma | Discovery 008 | Valores definidos | Resolved | — |
| OQ-032 | Rede dos runners | Default deny + allowlist | Epic 1 | P0 | Segurança | Discovery 006 + 009 | Política definida | In progress | 0013 |
| OQ-033 | SBOM/scanning | Ferramenta e rotina | Operação | P2 | Segurança | Avaliação | Ferramenta escolhida | Open | — |
| OQ-034 | Pentest | Ferramenta e rotina | Operação | P2 | Segurança | Avaliação | Ferramenta escolhida | Open | — |

## Operações

| ID | Título | Descrição | Bloqueia | Prioridade | Responsável sugerido | Método de validação | Resultado esperado | Status | ADR relacionado |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| OQ-040 | Política de backup | Frequência, retenção, off-host | Operação | P1 | Operações | Documentação | Procedimento em `operations/backup-and-restore.md` | Open | — |
| OQ-041 | Backup remoto | Ferramenta | Operação | P2 | Operações | Avaliação | Decisão registrada | Open | — |
| OQ-042 | Retenção | 30 dias é razoável? | Operação | P2 | Operações | Análise de uso | Decisão registrada | Open | — |
| OQ-043 | Janela de manutenção | Definição | Operação | P2 | Operações | Análise | Janela definida | Open | — |
| OQ-044 | Visualização | Grafana ou similar | Operação | P2 | Operações | Avaliação | Decisão registrada | Open | — |
| OQ-045 | Alerta | Ferramenta | Operação | P2 | Operações | Avaliação | Decisão registrada | Open | — |

## Limites e tamanhos

| ID | Título | Descrição | Bloqueia | Prioridade | Responsável sugerido | Método de validação | Resultado esperado | Status | ADR relacionado |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| OQ-050 | Tamanho máximo de diff | Limite em `run_diff` | Slice 1.1.4 | P1 | Plataforma | Teste com patch grande | Valor em `specs/004-mcp-contract/spec.md` | Open | — |
| OQ-051 | Tamanho máximo de prompt | Limite em `run_create` | Epic 1 | P0 | Plataforma | Análise | Valor definido | Resolved | — |
| OQ-052 | Limite de eventos | Por execução | Operação | P2 | Plataforma | Análise | Valor definido | Open | — |
| OQ-053 | Tamanho máximo de artefato | Por arquivo/execução | Operação | P2 | Plataforma | Análise | Valor em `specs/009-artifact-management/spec.md` | Open | — |

## Sanitização

| ID | Título | Descrição | Bloqueia | Prioridade | Responsável sugerido | Método de validação | Resultado esperado | Status | ADR relacionado |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| OQ-060 | Sanitização de arquivos | Nomes e paths | Operação | P2 | Segurança | Teste | Estratégia documentada | Open | — |
| OQ-061 | Tratamento de binários | Diff e metadata | Operação | P2 | Plataforma | Análise | Estratégia documentada | Open | — |

## Revisão humana

| ID | Título | Descrição | Bloqueia | Prioridade | Responsável sugerido | Método de validação | Resultado esperado | Status | ADR relacionado |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| OQ-070 | Aplicação manual | Procedimento após aprovação | Operação | P0 | Operador | Documentação | Procedimento registrado | Open | 0008 |
| OQ-071 | Assinatura Git | Para execuções aprovadas | Operação | P2 | Operador | Análise | Decisão registrada | Open | — |

## Observabilidade

| ID | Título | Descrição | Bloqueia | Prioridade | Responsável sugerido | Método de validação | Resultado esperado | Status | ADR relacionado |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| OQ-080 | Amostragem de traces | Pós-MVP | Operação | P2 | Operações | Avaliação | Decisão registrada | Open | — |
| OQ-081 | Ferramenta final | Prometheus + Grafana? OTLP-only? | Epic 1 | P1 | Operações | POC | Decisão registrada | Open | — |

## Governance

| ID | Título | Descrição | Bloqueia | Prioridade | Responsável sugerido | Método de validação | Resultado esperado | Status | ADR relacionado |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| OQ-100 | Licença do repositório | LICENSE está como Apache-2.0; foi decisão não autorizada | — | P1 | Sponsor | Aprovação explícita do sponsor | LICENSE confirmado ou substituído | Resolved | 0016 |
| OQ-200 | Estabilização do Epic 1 | PR `chore(stabilization)` deve fechar 4 pendências: validação OpenCode real ponta a ponta, rede de testes automatizados + CI, decisão de licença, correção de nomenclatura Milestone/Epic/Slice | Epic 1 → Epic 2 | P1 | Plataforma | Smoke test contra OpenCode real com provedor determinístico; pipeline verde; OQ-100 resolvida; docs atualizados | PR mergeado e gate considerado fechado | Resolved (PR #3 `STAB-005A.3: Complete OpenCode bridge run via session messages` mergeada em `phase-1-mvp` no commit `6e21573f`. Completion real provado via `GET /session/{id}/message` (terminal authority); `GET /event` permanece como pump SSE auxiliar de `RunEvent`. Cenários `RealOpenCodePoc` `cancel`/`timeout`/`provider-error` seguem como follow-up fora da STAB-005A.3. Transição para Epic 2 / SLICE-WORKSPACE-001 liberada. Evidência: [Discovery 015](discovery/015-opencode-real-provider-poc.md).) | 0017, 0018 |

## Próximos passos obrigatórios (P0)

* OQ-001 (SDK MCP) — Resolved.
* OQ-002 (.NET versão) — Resolved.
* OQ-005 (subprocessos) — Resolved.
* OQ-010 (repositório piloto) — Resolved.
* OQ-020 (OpenCode lifecycle) — Resolved.
* OQ-021 (autenticação OpenCode) — validação no container fica para slice 1.1.3.
* OQ-031 (limites) — Resolved.
* OQ-032 (rede) — política definida; implementação técnica no slice 1.1.1.
* OQ-051 (tamanho de prompt) — Resolved.
* OQ-090 (auth MCP) — Resolved.
* OQ-100 (licença) — aguardar autorização explícita do sponsor.

## Como fechar uma questão

1. Atualizar o `Status` para `In progress`.
2. Executar o método de validação.
3. Produzir o artefato esperado.
4. Atualizar ADR ou nota técnica.
5. Marcar `Status` como `Resolved` e adicionar referência cruzada.

## Status consolidado após Milestone 0

| ID | Status final |
| --- | --- |
| OQ-001 | Resolved |
| OQ-002 | Resolved |
| OQ-003 | Open (depende de aprovação do sponsor) |
| OQ-004 | Open |
| OQ-005 | Resolved |
| OQ-006 | Open |
| OQ-010 | Resolved |
| OQ-011 | Resolved |
| OQ-012 | Open |
| OQ-013 | Open |
| OQ-014 | Open |
| OQ-015 | Open |
| OQ-020 | Resolved |
| OQ-021 | In progress (validar em slice 1.1.3) |
| OQ-022 | Open |
| OQ-023 | Open |
| OQ-024 | Open |
| OQ-030 | Open (rotação manual no MVP) |
| OQ-031 | Resolved |
| OQ-032 | In progress (implementação em slice 1.1.1) |
| OQ-033 | Open |
| OQ-034 | Open |
| OQ-040 a OQ-045 | Open (Epic 3) |
| OQ-050 | Open |
| OQ-051 | Resolved |
| OQ-052 | Open |
| OQ-053 | Open |
| OQ-060, OQ-061 | Open |
| OQ-070 | Open |
| OQ-071 | Open |
| OQ-080, OQ-081 | Open |
| OQ-090 | Resolved |
| OQ-091 | Resolved (Basic via `OPENCODE_SERVER_PASSWORD` validado em discovery/011) |
| OQ-100 | Resolved (ADR-0016; Apache-2.0 aceito em 2026-07-28) |
| OQ-200 | Resolved (PR #3 `6e21573f`; completion real provado via `/session/{id}/message`; `GET /event` é pump auxiliar; cancel/timeout/provider-error permanecem como follow-up; Epic 2 / SLICE-WORKSPACE-001 liberado) |
| OQ-201 | Resolved (adapter alinhado, contract drift eliminado, contract tests 10/10 verdes) |