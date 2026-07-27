# Glossário

> **Status:** Proposed

Define termos técnicos usados no OCAB.

## Termos

### Bridge

Coding Agent Bridge. Servidor MCP que controla o ciclo de vida da execução.

### Run

Uma execução de uma tarefa por um agente em um repositório.

### Runner

Container separado que executa um agente específico (OpenCode, Codex, Antigravity).

### Workspace

Diretório isolado usado por uma execução para alterações.

### Adapter

Implementação de `IRunnerAdapter` para um agente específico.

### Slug

Identificador único de um repositório no formato `^[a-z0-9][a-z0-9-]{0,63}$`.

### AccessMode

Modo de acesso da execução: `ReadOnly` ou `WorkspaceWrite`.

### Profile

Configuração de execução por agente (ex.: `implementer`, `reviewer`).

### Idempotency Key

Chave fornecida pelo cliente para deduplicação de execuções.

### Artifact

Arquivo persistido sob `runs/<runId>/` referenciado pelo banco.

### RunEvent

Registro append-only de um evento da execução.

### Policy Decision

Registro de uma decisão do motor de políticas.

### MCP

Model Context Protocol. Protocolo usado pelo Odysseus para chamar ferramentas.

### Streamable HTTP

Transporte MCP baseado em HTTP streaming.

### OpenTelemetry

Padrão de observabilidade usado pelo bridge.

### ULID/UUID

Identificadores únicos usados no banco.

### Approval

Registro de aprovação humana para uma execução.

### Validation Result

Resultado de um comando de validação.

### Diagnostic

Modo em que a execução preserva o workspace além da retenção padrão.

## Siglas

* OCAB — Odysseus Coding Agent Bridge.
* MCP — Model Context Protocol.
* ADR — Architecture Decision Record.
* NFR — Non-Functional Requirement.
* FR — Functional Requirement.
* RPO — Recovery Point Objective.
* RTO — Recovery Time Objective.
* SBOM — Software Bill of Materials.
* OTLP — OpenTelemetry Protocol.
* POSIX — Portable Operating System Interface.

## Referências relacionadas

* [`assumptions.md`](assumptions.md)
* [`open-questions.md`](open-questions.md)
