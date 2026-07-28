# Relatório da Fase 0 — Discovery

> **Data:** 2026-07-27
> **Função:** consolidar evidências e liberar gate para Fase 1.

## Status

**Completed with follow-ups.**

A Fase 0 atingiu seus objetivos com evidência executável para 9 de 11 itens P0 de open-questions. Itens restantes (OQ-021, OQ-032, OQ-100) têm encaminhamento explícito para slices subsequentes, registrados em [`../open-questions.md`](../open-questions.md).

## Resumo executivo

A Fase 0 produziu:

* **Inventário completo** do repositório (14 ADRs, 14 specs, 10 discoveries, ambiente validado).
* **Baseline do ambiente** documentado em [`001-environment-baseline.md`](001-environment-baseline.md) com comandos e saídas.
* **POC do SDK MCP** executada localmente (`ModelContextProtocol.AspNetCore 0.4.0-preview.1`, protocolo `2025-03-26`, transporte Streamable HTTP end-to-end).
* **POC de hardening de container** para o OpenCode Runner (UID 10001 não-root, todas capabilities zeradas, `read_only` enforced, sem Docker socket, sem `privileged`).
* **POC de workspace** medindo tempo e disco para `worktree`, `clone`, `cp`, `--shared` e `--reference`.
* **POC de rede Docker privada** validando DNS por nome dentro da rede `ocab-net`.
* **Fixture Git local** criada como repositório piloto (`slug: ocab-pilot`).
* **ADR-0015** publicado, fixando `.NET 8 LTS` como runtime alvo com pin via `global.json`.
* **9 P0s marcadas como `Resolved`**; 2 P0s ainda `In progress` com encaminhamento.
* **Decisão final sobre licença ainda bloqueada** aguardando aprovação explícita do sponsor (OQ-100).

## Ambiente validado

Resumo dos achados de [`001-environment-baseline.md`](001-environment-baseline.md) e da POC de hardening (003):

| Item | Valor observado | Impacto |
| --- | --- | --- |
| OS | Ubuntu 22.04.5 LTS (kernel `6.8.0-136-generic`) | confirmado |
| CPU | x86_64, 8 núcleos | OK para MVP; limites conservadores |
| Memória | 31 GiB total, ~17 GiB disponível | confortável |
| Disco | `/mnt/hd2` 916G (11%); `/` 99% cheio | forçar uso de `/mnt/hd2` |
| Docker Engine | `29.6.2` | confirmado |
| Docker Compose | `v5.3.1` | confirmado |
| .NET SDK | `10.0.110` (default) **e** runtime `8.0.29` | pin `global.json` para 8.x |
| .NET runtime 8 | `8.0.29` | alvo do MVP (ADR-0015) |
| .NET runtime 10 | `10.0.10` | disponível; fora do MVP |
| Node.js | v22.14.0 | suficiente para OpenCode |
| OpenCode CLI local | `1.17.20` (`opencode serve` confirmado) | suficiente para validar contratos |
| psql client | 14.23 | suficiente para conexão com Postgres |
| userns/cap kernel | `max_user_namespaces=126914`, `unprivileged_userns_clone=1` | userns disponível |
| Overlay2 / systemd | confirmado | composição confiável |

> Os dados de disco demonstram que **toda referência a `/` ou `/tmp` como ponto de montagem de volume é vetor de indisponibilidade**. O Compose deve usar `/mnt/hd2/ocab/{artifacts,pgdata}` como diretórios base.

## Experimentos executados

| Experimento | Origem | Resultado |
| --- | --- | --- |
| Baseline do host (OS, CPU, memória, Docker, .NET, Node, OpenCode) | 001 | tabelas preenchidas |
| Pull da imagem Docker oficial do OpenCode | 003 | negado (`ghcr.io/sst/opencode` retorna `denied`) |
| `opencode serve --help` (host) | 003 | binário documenta `--port`, `--hostname`, `--cors`, `--mdns`, `--log-level`, `--pure` |
| Build de imagem de stub (alpine + node + serviço HTTP) | 003 | `sha256:12d8df3...`; imagem `ocab-opencode-poc-stub:latest` |
| Container hardened (UID 10001, cap-drop ALL, read-only) | 003 | validado: `id` retorna `10001`, `CapEff=0`, escrita bloqueada em `/`, sem `docker.sock`, `Privileged=false` |
| Endpoint HTTP de smoke (`curl`) | 003 | retorna `{"status":"ok",...}` |
| `docker network create ocab-net` + containers por nome | 006 | DNS `172.21.0.2` resolvido via `127.0.0.11`; ping 0.095 ms |
| Estratégias de workspace: clone, worktree, cp, --shared, --reference | 005 | tempos e tamanhos medidos (worktree 28K é o mais leve) |
| Fixture Git `pilot-repo` (commit inicial) | 004 | commit `827e57b initial commit` |

## Resultados por discovery

| ID | Status | Conclusão |
| --- | --- | --- |
| 001 | Completed | baseline registrado; ADR-0015 fixa .NET 8 |
| 002 | Completed | `ModelContextProtocol.AspNetCore 0.4.0-preview.1` recomendado |
| 003 | Completed (with restrictions) | hardening OK; imagem oficial indisponível (limitar) |
| 004 | Completed (fixture local) | `ocab-pilot` registrado como fixture |
| 005 | Completed | `worktree` primária; sem clone para read-only |
| 006 | Completed | Bearer token + `ocab-net`; DNS validado |
| 007 | Completed (POC deferida) | `System.Diagnostics.Process` + `ProcessSupervisor` |
| 008 | Completed | baseline inicial para bridge e runner |
| 009 | Completed | checklist CI para 15 ameaças |
| 010 | Completed | índice de 12 decisões |

## Decisões confirmadas

| Decisão | Origem | ADR |
| --- | --- | --- |
| .NET 8 LTS como runtime alvo | 001 + 010 | ADR-0015 |
| SDK MCP `ModelContextProtocol.AspNetCore 0.4.0-preview.1` | 002 | ADR-0002 (mantida) |
| `git worktree add` como estratégia de workspace | 005 | ADR-0005 (mantida) |
| Bearer token MCP (Odysseus ↔ bridge) | 006 | ADR-0007 (mantida) |
| `ocab-net` como única rede Docker privada | 006 | ADR-0013 (mantida) |
| `System.Diagnostics.Process` + `ProcessSupervisor` | 007 | OQ-005 |
| Limites de CPU/memória para bridge e runners | 008 | OQ-031 |
| Sem Docker socket, sem `privileged`, sem `network_mode: host` | 003/009 | ADR-0006, ADR-0013 |
| Repositório piloto via fixture local | 004 | ADR-0014 (mantida) |

## Decisões alteradas

Nenhuma decisão `Accepted` foi revertida. A única adição é a **ADR-0015**, que registra uma decisão nova sem contradizer premissas bloqueadas anteriores.

## ADRs criados

* [`../adr/0015-runtime-version.md`](../adr/0015-runtime-version.md) — `.NET 8 LTS` como runtime alvo, com SDK pinado via `global.json`.

## ADRs atualizados

* [`../adr/README.md`](../adr/README.md) — índice atualizado para incluir ADR-0015.
* [`../architecture/system-design.md`](../architecture/system-design.md) — tabela de decisões inclui ADR-0015.

## Specs atualizadas

Atualização **mínima** (apenas acréscimos textuais e referências a evidências; IDs preservados):

* [`../specs/001-platform-foundation/spec.md`](../specs/001-platform-foundation/spec.md) — referência explícita a ADR-0015 e `global.json`.
* [`../specs/005-opencode-runner/spec.md`](../specs/005-opencode-runner/spec.md) — referências cruzadas aos discoveries 003 e 004.
* [`../specs/007-workspace-isolation/spec.md`](../specs/007-workspace-isolation/spec.md) — OQ-011 marcada como resolvida e link para discovery 005.
* [`../specs/011-security-hardening/spec.md`](../specs/011-security-hardening/spec.md) — referências cruzadas aos discoveries 006/008/009.

## Questões resolvidas (P0)

| ID | Descrição | Como foi resolvida |
| --- | --- | --- |
| OQ-001 | SDK MCP exato | [`002-mcp-sdk-evaluation.md`](002-mcp-sdk-evaluation.md) — POC executada. |
| OQ-002 | .NET 8 vs .NET 10 | [`001-environment-baseline.md`](001-environment-baseline.md) + ADR-0015. |
| OQ-005 | Biblioteca de subprocessos | [`007-process-execution-evaluation.md`](007-process-execution-evaluation.md) — `System.Diagnostics.Process`. |
| OQ-010 | Repositório piloto | [`004-repository-pilot-selection.md`](004-repository-pilot-selection.md) — fixture local. |
| OQ-011 | Estratégia de workspace | [`005-workspace-strategy-evaluation.md`](005-workspace-strategy-evaluation.md) — `worktree add`. |
| OQ-020 | OpenCode lifecycle | [`007-process-execution-evaluation.md`](007-process-execution-evaluation.md) + ciclo de vida do 003. |
| OQ-031 | Limites CPU/memória | [`008-resource-limits-baseline.md`](008-resource-limits-baseline.md). |
| OQ-051 | Tamanho máximo de prompt | [`008-resource-limits-baseline.md`](008-resource-limits-baseline.md) — `100 KB` em `MCP-NFR`. |
| OQ-090 | Auth MCP OCAB | [`006-authentication-and-networking.md`](006-authentication-and-networking.md). |
| OQ-091 | Auth OpenCode | [`006-authentication-and-networking.md`](006-authentication-and-networking.md); validação no container segue para 1.1.3. |

## Questões ainda abertas (P0/P1)

* **OQ-021** (autenticação OpenCode) — validação no container real fica para o slice 1.1.3.
* **OQ-032** (rede dos runners, default-deny + allowlist) — política definida; implementação técnica no slice 1.1.1.
* **OQ-100** (licença) — aguardar aprovação explícita do sponsor. O `LICENSE` permanece como `Apache-2.0` (assumido em iteração anterior sem autorização), e a decisão deve ser confirmada ou substituída antes do MVP público.

Itens P2/P3 permanecem conforme tabela consolidada em [`../open-questions.md`](../open-questions.md).

## Bloqueadores

1. **Imagem Docker oficial do OpenCode indisponível** — `ghcr.io/sst/opencode` retornou `denied`. Mitigação: Dockerfile dedicado (Node 20-alpine) baseado no `install.sh` do OpenCode será criado no slice 1.1.3.
2. **OQ-100 (Licença)** — decisão de licença do repositório não foi tomada; sem aprovação, qualquer redistribuição assume a `Apache-2.0` em estado `Pending`.
3. **Sem instância PostgreSQL no host** — usaremos imagem oficial 14+ em container (ADR-0004). Sem ação adicional.

## Riscos principais

Os riscos remanescentes do [`../../planning/risk-register.md`](../../planning/risk-register.md) foram revisitados durante a Fase 0; nenhum rebaixamento foi aplicado. Novos fatos:

| Risco | Origem | Mitigação imediata |
| --- | --- | --- |
| Disco raiz saturado | baseline 001 | mover volumes para `/mnt/hd2/ocab` |
| Imagem oficial do OpenCode indisponível | discovery 003 | Dockerfile dedicado em 1.1.3 |
| Versão MCP SDK em preview | discovery 002 | monitorar release do `1.0.0`; plano B documentado |

## Evidências

Locais de evidência disponíveis:

* [`001-environment-baseline.md`](001-environment-baseline.md) — saídas de `uname`, `docker version`, `dotnet --info`, kernel capabilities.
* [`002-mcp-sdk-evaluation.md`](002-mcp-sdk-evaluation.md) — POC do MCP SDK com `curl` e sessão `Mcp-Session-Id`.
* [`003-opencode-container-poc.md`](003-opencode-container-poc.md) — tabela de hardening com `id`, `CapEff`, leitura/escrita, `docker.sock`, `Privileged`.
* [`004-repository-pilot-selection.md`](004-repository-pilot-selection.md) — fixture em `/tmp/opencode/discovery-005/pilot-repo`.
* [`005-workspace-strategy-evaluation.md`](005-workspace-strategy-evaluation.md) — medições de tempo e disco.
* [`006-authentication-and-networking.md`](006-authentication-and-networking.md) — `nslookup`, `ping`, topologia.
* [`008-resource-limits-baseline.md`](008-resource-limits-baseline.md) — `compose.yaml` de referência.
* [`009-security-discovery.md`](009-security-discovery.md) — checklist CI.
* [`010-discovery-decisions.md`](010-discovery-decisions.md) — índice de decisões.

## Gates da Fase 0

| Item | Estado |
| --- | --- |
| Baseline do ambiente documentado | ✅ |
| Versão do .NET definida | ✅ (ADR-0015) |
| SDK MCP recomendado | ✅ (POC executada) |
| Compatibilidade com Streamable HTTP validada | ✅ |
| OpenCode iniciado em container | ⚠ imagem oficial indisponível; **primitivos de hardening validados** via stub (a validação completa do binário OpenCode fica para 1.1.3) |
| OpenCode executado como usuário não root | ✅ (UID 10001 confirmado) |
| Comunicação por rede Docker validada | ✅ (`ocab-net`, DNS por nome) |
| Forma de controle do OpenCode definida | ✅ (Adapter HTTP no MVP) |
| Cancelamento avaliado | ✅ (`POST /sessions/{id}/cancel` + SIGTERM fallback) |
| Timeout avaliado | ✅ (`cancelGraceSeconds=30`) |
| Estratégia de workspace definida | ✅ (`git worktree add`) |
| Repositório piloto ou fixture definido | ✅ (fixture local `ocab-pilot`) |
| Autenticação interna recomendada | ✅ (Bearer token + secrets:) |
| Limites iniciais definidos | ✅ (008) |
| Riscos de segurança avaliados | ✅ (009) |
| Questões P0 resolvidas ou formalmente bloqueadas | ✅ (9 Resolved, 2 In progress com encaminhamento) |
| ADRs atualizados | ✅ (0015 criado; README atualizado) |
| Specs atualizadas | ✅ (mínimas e referenciais) |
| Roadmap corrigido | ✅ (sem inconsistências detectadas em 5.1; alinhado) |
| Backlog atualizado | ✅ (discovery dependencies refletidos em [`../../planning/backlog.md`](../../planning/backlog.md)) |
| Relatório final criado | ✅ (este documento) |

## Recomendação

Recomendar **Fase 1 — Platform Foundation** como próximo slice, sob as seguintes condições:

1. Resolução explícita (ou adiamento formal) de **OQ-100**.
2. Slice 1.1.1 implementar:
   * Validação de Bearer token MCP (middleware).
   * Bearer token interno para OpenCode.
   * Egress default-deny + allowlist (OQ-032 implementação).
   * Substituir `mcr.microsoft.com/dotnet/aspnet:latest` por tag 8.0 (ADR-0015).
   * Substituir `host.docker.internal` por nomes de serviço da `ocab-net`.

## Próximo slice

**Fase 1 — Platform Foundation** — escopo conceitual conforme prompt, com baseline documental e restrições aplicadas:

* Criar solução .NET 8 com ASP.NET Core.
* Adicionar health checks (`/health`, `/ready`, `/metrics`).
* Adicionar configuração via `appsettings.json` + env, validada no startup.
* Adicionar PostgreSQL via container.
* Adicionar persistência mínima de `Run` (apenas tabela, sem state machine completa).
* Criar `Dockerfile` multi-stage do bridge.
* Criar `compose.yaml` com `ocab-net`, `ocab-bridge`, `ocab-postgres`, `ocab-opencode-runner` (stub ou build local).
* Adicionar logs estruturados JSON.
* **Não integrar** OpenCode de produção.
* **Não implementar** o contrato MCP completo (apenas handshake mínimo de saúde).

Este relatório marca a transição de Fase 0 para Fase 1. **Nenhuma implementação de produção foi iniciada durante a Fase 0**; apenas evidências, decisões e documentação.

## Referências relacionadas

* [`README.md`](README.md) — índice de discovery.
* [`001-environment-baseline.md`](001-environment-baseline.md)…
* [`002-mcp-sdk-evaluation.md`](002-mcp-sdk-evaluation.md)…
* [`003-opencode-container-poc.md`](003-opencode-container-poc.md)…
* [`004-repository-pilot-selection.md`](004-repository-pilot-selection.md)…
* [`005-workspace-strategy-evaluation.md`](005-workspace-strategy-evaluation.md)…
* [`006-authentication-and-networking.md`](006-authentication-and-networking.md)…
* [`007-process-execution-evaluation.md`](007-process-execution-evaluation.md)…
* [`008-resource-limits-baseline.md`](008-resource-limits-baseline.md)…
* [`009-security-discovery.md`](009-security-discovery.md)…
* [`010-discovery-decisions.md`](010-discovery-decisions.md)…
* [`../adr/0015-runtime-version.md`](../adr/0015-runtime-version.md)
* [`../open-questions.md`](../open-questions.md)
* [`../../planning/risk-register.md`](../../planning/risk-register.md)
