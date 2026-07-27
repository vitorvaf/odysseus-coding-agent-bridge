# Acceptance Tests

> **Status**: Proposed

Define testes de aceitação que validam o MVP ponta a ponta.

## Fluxo 1 — Análise read-only

```mermaid
sequenceDiagram
    participant T as Test
    participant B as Bridge
    participant O as OpenCode Runner
    T->>B: run_create (ReadOnly)
    B->>O: dispatch
    O->>B: result
    B-->>T: run_report
```

### Critérios

* `runId` retornado.
* Estado final `Completed`.
* Relatório contém summary, filesChanged (vazio em ReadOnly), validations, findings.
* Repositório original não foi alterado.

## Fluxo 2 — Implementação workspace-write

```mermaid
sequenceDiagram
    participant T as Test
    participant B as Bridge
    participant W as Workspace
    participant O as OpenCode Runner
    participant V as Validation
    T->>B: run_create (WorkspaceWrite)
    B->>W: preparar workspace
    W-->>B: ok
    B->>O: dispatch
    O-->>B: result
    B->>V: executar validações
    V-->>B: ValidationResults
    B-->>T: run_report
```

### Critérios

* Workspace isolado criado.
* Diff disponível.
* Validações executadas.
* Repositório original não alterado.
* Estado final conforme validação.

## Fluxo 3 — Cancelamento

```mermaid
sequenceDiagram
    participant T as Test
    participant B as Bridge
    participant O as OpenCode Runner
    T->>B: run_create
    B->>O: dispatch
    T->>B: run_cancel
    B->>O: cancel
    O-->>B: ack
    B-->>T: estado Cancelled
```

### Critérios

* Estado final `Cancelled`.
* Eventos `runner.session_cancelled` registrados.

## Fluxo 4 — Timeout

* Idempotência: `run_create` repetido retorna mesma execução.

### Critérios

* `run_create` com `idempotencyKey` repetido retorna mesmo `runId`.
* Com payload divergente retorna 409.

## Ambiente

* Compose local em CI.
* Repositório de teste.
* Token dedicado.

## Critérios de aceite globais

* Todos os 25 critérios de aceite do MVP passam.

## Referências relacionadas

* [`strategy.md`](strategy.md)
* [`../planning/backlog.md`](../planning/backlog.md)
* [`../specs/003-run-lifecycle/spec.md`](../specs/003-run-lifecycle/spec.md)
