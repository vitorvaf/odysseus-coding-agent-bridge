# ADRs — Architecture Decision Records

> **Status:** Active

Este diretório contém as decisões arquiteturais do OCAB. Cada ADR é uma premissa bloqueada salvo nova ADR substituta.

## Formato

Todo ADR segue o template definido em [`../../AGENTS.md`](../../AGENTS.md) e no prompt:

```markdown
# ADR-NNNN — Título

## Status

Accepted

## Contexto

## Decisão

## Alternativas consideradas

## Consequências positivas

## Consequências negativas

## Riscos

## Impacto operacional

## Impacto de segurança

## Critérios para revisitar

## Referências relacionadas
```

## Índice

| ADR | Título | Status |
| --- | --- | --- |
| [0001](0001-odysseus-as-interaction-plane.md) | Odysseus como Interaction Plane | Accepted |
| [0002](0002-bridge-as-mcp-server.md) | Coding Agent Bridge como servidor MCP | Accepted |
| [0003](0003-agent-adapter-boundary.md) | Adapters independentes por agente | Accepted |
| [0004](0004-postgresql-as-initial-queue.md) | PostgreSQL como persistência e fila inicial | Accepted |
| [0005](0005-isolated-workspaces.md) | Workspaces isolados | Accepted |
| [0006](0006-no-docker-socket.md) | Sem Docker socket | Accepted |
| [0007](0007-policy-enforcement-outside-model.md) | Políticas críticas fora do modelo | Accepted |
| [0008](0008-human-approval-required.md) | Aprovação humana obrigatória | Accepted |
| [0009](0009-opencode-first-runner.md) | OpenCode como primeiro runner | Accepted |
| [0010](0010-codex-second-runner.md) | Codex como segundo runner | Accepted |
| [0011](0011-antigravity-post-mvp.md) | Antigravity após o MVP | Accepted |
| [0012](0012-filesystem-artifact-storage.md) | Artefatos no filesystem | Accepted |
| [0013](0013-private-docker-network.md) | Rede Docker privada | Accepted |
| [0014](0014-repository-slug-registry.md) | Repositórios identificados por slug | Accepted |
| [0015](0015-runtime-version.md) | .NET 8 LTS como runtime alvo | Accepted |
| [0016](0016-apache-2.0-license.md) | Licença Apache-2.0 do repositório | Accepted |
| [0017](0017-pin-opencode-version.md) | Pin OpenCode Version and Generated HTTP Contract | Accepted |
| [0018](0018-persistent-run-queue.md) | Persistent Run Queue with Awaitable Execution Coordination | Proposed |

## Como propor nova ADR

1. Não reverta ADR aceita silenciosamente.
2. Crie nova ADR com referência à anterior.
3. Atualize [`../open-questions.md`](../open-questions.md) durante a análise.
4. Atualize specs afetadas e arquitetura.
5. Atualize este índice.

## Como propor revisão de ADR

1. Criar nova ADR substituta.
2. Listar motivadores.
3. Avaliar impacto operacional e de segurança.
4. Atualizar referências cruzadas em specs, contratos e diagramas.
