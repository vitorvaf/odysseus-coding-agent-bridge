# Discovery 010 — Consolidação de Decisões

> **Status:** Completed
> **Bloqueia:** —
> **Prioridade:** —
> **Data:** 2026-07-27

Este documento funciona como **índice de decisões** registradas durante a Fase 0. Não duplica o conteúdo integral das ADRs; cada item aponta para o documento de origem.

## Tabela de decisões

| ID | Decisão | Evidência | Alternativas | Impacto | ADR / Spec / OQ | Status | Fase afetada |
| --- | --- | --- | --- | --- | --- | --- | --- |
| D-001 | Runtime alvo: **.NET 8 LTS** com SDK pinado via `global.json` | [`001-environment-baseline.md`](001-environment-baseline.md); conflito de SDK 10.0.110 (default) vs 8.0.29 (alvo); compatibilidade do MCP SDK com `net8.0` confirmada em [`002-mcp-sdk-evaluation.md`](002-mcp-sdk-evaluation.md) | manter só .NET 10; coexistência sem pin | pin de versão evita flutuação de builds; .NET 8 LTS reduz risco de breaking change | ADR `0015-runtime-version.md` (a criar) + OQ-002 | Resolved | Fase 1 |
| D-002 | SDK MCP: **`ModelContextProtocol.AspNetCore` 0.4.0-preview.1** | POC real em `002-mcp-sdk-evaluation.md` cobriu `initialize`, `tools/list`, `tools/call`, header `Mcp-Session-Id`, transporte Streamable HTTP | outros pacotes (McpHttpServer, McpSdk.Adapter.StreamableHttpServer, Elarion.AspNetCore.Mcp); plano B (manual sobre `HttpContext`) | preview oficial do C# SDK; protocolo `2025-03-26` end-to-end; compatibilidade com `net8.0` confirmada | ADR-0002 (Accepted) + OQ-001 | Resolved | Fase 1 |
| D-003 | Workspace strategy: **`git worktree add` (primária)**; sem workspace para read-only | Medições reais em [`005-workspace-strategy-evaluation.md`](005-workspace-strategy-evaluation.md); spec 007 invariantes atendidas | clone local, cp -a, clone --shared, clone --reference | menor uso de disco; branch automática; cleanup determinístico | ADR-0005 + spec 007 + OQ-011 | Resolved | Fase 5 |
| D-004 | Auth interna: **Bearer Token** (Odysseus → bridge, bridge → runner) | [`006-authentication-and-networking.md`](006-authentication-and-networking.md) | mTLS, OIDC, basic | simplicidade; suficiente para MVP local; evolução para mTLS fora de escopo | OQ-090 + OQ-091 + ADR-0007 | Resolved | Fase 1 |
| D-005 | Rede Docker privada: **`ocab-net`**, sem `host.docker.internal`, sem `host: networking` | POC em 006 com `docker network create ocab-net` validou DNS por nome; alpine resolveu `ocab-test-a` em `172.21.0.2` | `network_mode: host`, portas publicadas no host | atende ADR-0013 | ADR-0013 | Resolved | Fase 1 |
| D-006 | Controle de processos: **`System.Diagnostics.Process`** com `ProcessSupervisor` | [`007-process-execution-evaluation.md`](007-process-execution-evaluation.md); consensual com .NET nativo | wrappers externos, bash puro | sem dependência extra; controle fino sobre PGID e sinais | OQ-005 + spec 006 | Resolved | Fase 1 |
| D-007 | Limites iniciais de recursos | [`008-resource-limits-baseline.md`](008-resource-limits-baseline.md); host tem 8 CPUs / 31 GiB | sem limite (host crash) | define baseline conservador; revisão após Fase 3 | OQ-031 + spec 011 | Resolved | Fase 1 |
| D-008 | Container OpenCode: **stub hardened** validado | [`003-opencode-container-poc.md`](003-opencode-container-poc.md); UID 10001, `CapEff` 0, read-only, sem Docker socket, sem `privileged` | imagem oficial indisponível (`ghcr.io/sst/opencode` denied); `ghcr.io/sst/opencode-server` não publicada | primitivos de hardening validados; Dockerfile canônico fica para slice 1.1.3 | OQ-020 + spec 005 | Partial | Fase 3 |
| D-009 | Repositório piloto: **fixture local** `ocab-pilot` | [`004-repository-pilot-selection.md`](004-repository-pilot-selection.md); fixture criada em `/tmp/opencode/discovery-005/pilot-repo` | usar projeto real (rejeitado por ADR-0014 + restrições obrigatórias) | sem risco de danificar projeto real | OQ-010 | Resolved | Fase 3 |
| D-010 | Sem Docker socket, sem `privileged`, sem `pid`/`ipc` compartilhado | [`009-security-discovery.md`](009-security-discovery.md) | montar socket (rejeitado) | atende ADR-0006 | ADR-0006 + spec 011 | Resolved | Fase 1 |
| D-011 | Validação de path traversal, symlink escape, prompt injection | [`009-security-discovery.md`](009-security-discovery.md) | validação manual | reduzido a checklist CI | WI-FR-005 / WI-FR-006 / ADR-0007 | Resolved | Fase 7 |
| D-012 | `.NET 8` é a versão runtime alvo; SDK pode estar em 10 no host mas pinado via `global.json` em `8.x` | [`001-environment-baseline.md`](001-environment-baseline.md) | coexistência sem pin | builds reprodutíveis | ADR `0015-runtime-version.md` | Resolved | Fase 1 |

## ADRs a criar/atualizar durante a Fase 0

| ID | Título | Ação | Origem |
| --- | --- | --- | --- |
| `0015-runtime-version.md` | .NET 8 como runtime alvo | **Criar** | D-001 |
| `0002-bridge-as-mcp-server.md` | — | **Mantida** (não houve breaking change) | D-002 |

## Itens não decididos nesta Fase (forwarded para Slices)

* **Estratégia final de egress** (default deny via iptables vs proxy). Forwarded para slice 1.1.1.
* **Imagem Docker oficial do OpenCode**. Forwarded para slice 1.1.3; enquanto isso, o adapter usa o stub validado ou um Dockerfile dedicado.
* **Políticas dinâmicas por repositório**. Forwarded para Fase 7.
* **SBOM/scanning**. Forwarded para Fase 10.

## Próximo passo

* Esta consolidação é copiada, em forma resumida, para o `phase-0-report.md`.
