# Runtime Flows

> **Status:** Accepted

## Visão

Documenta os fluxos de execução mais importantes do Coding Agent Bridge. Cada fluxo é descrito em sequência e referenciado pelas specs correspondentes.

## Fluxos

### 1. Listagem de repositórios

```mermaid
sequenceDiagram
    participant O as Odysseus
    participant B as Bridge
    participant DB as PostgreSQL
    O->>B: repositories_list
    B->>B: Auth + Authorization
    B->>DB: SELECT repositories
    DB-->>B: Lista
    B-->>O: { repositories: [...] }
```

### 2. Listagem de agentes

```mermaid
sequenceDiagram
    participant O as Odysseus
    participant B as Bridge
    participant DB as PostgreSQL
    O->>B: agents_list
    B->>DB: SELECT agents
    DB-->>B: Lista
    B-->>O: { agents: [...] }
```

### 3. Criação de run read-only

```mermaid
sequenceDiagram
    participant O as Odysseus
    participant B as Bridge
    participant DB as PostgreSQL
    participant W as Workspace Manager
    participant R as Runner
    participant FS as Artifact Storage

    O->>B: run_create (ReadOnly)
    B->>B: ValidarRequest
    B->>B: Policy.evaluate
    B->>DB: INSERT Run (Pending)
    B->>DB: UPDATE Run (ValidatingRequest)
    B->>B: ValidarRequest OK
    B->>DB: UPDATE Run (PreparingWorkspace)
    B->>W: preparar workspace read-only
    W-->>B: workspace pronto
    B->>DB: UPDATE Run (Running)
    B->>R: dispatch (sessão)
    R-->>B: eventos
    R-->>B: resultado
    B->>FS: persistir artefatos
    B->>DB: UPDATE Run (ValidatingResult)
    B->>DB: UPDATE Run (Completed)
    B-->>O: run_get status final
```

### 4. Criação de run workspace-write

```mermaid
sequenceDiagram
    participant O as Odysseus
    participant B as Bridge
    participant W as Workspace Manager
    participant R as Runner
    participant V as Validation Pipeline
    participant FS as Artifact Storage
    participant DB as PostgreSQL

    O->>B: run_create (WorkspaceWrite)
    B->>B: ValidarRequest
    B->>B: Policy.evaluate
    B->>DB: INSERT Run (Pending)
    B->>W: criar branch de execução
    W-->>B: workspace pronto
    B->>R: dispatch (sessão)
    R-->>B: eventos
    R-->>B: resultado
    B->>V: executar validações
    V-->>B: resultados
    B->>FS: persistir diff + logs + artefatos
    B->>DB: UPDATE Run (ValidatingResult)
    alt validação OK
        B->>DB: UPDATE Run (Completed)
    else validação com falhas
        B->>DB: UPDATE Run (CompletedWithValidationErrors)
    end
    B-->>O: run_get + run_report
```

### 5. Cancelamento

```mermaid
sequenceDiagram
    participant O as Odysseus
    participant B as Bridge
    participant R as Runner
    participant DB as PostgreSQL

    O->>B: run_cancel
    B->>B: Idempotência
    B->>DB: UPDATE Run (Cancelling)
    B->>R: cancel
    R-->>B: ACK
    B->>DB: UPDATE Run (Cancelled)
    B-->>O: run_get (Cancelled)
```

### 6. Timeout

```mermaid
sequenceDiagram
    participant B as Bridge
    participant R as Runner
    participant DB as PostgreSQL

    Note over B: timer expira
    B->>DB: UPDATE Run (Cancelling)
    B->>R: cancel
    R-->>B: ACK
    B->>DB: UPDATE Run (TimedOut)
```

### 7. Validação

```mermaid
sequenceDiagram
    participant B as Bridge
    participant V as Validation Pipeline
    participant FS as Artifact Storage

    B->>V: executar comandos
    V->>V: comando 1
    V->>V: comando 2
    V-->>B: ValidationResult[]
    B->>FS: persistir artefatos de validação
```

### 8. Revisão independente

```mermaid
sequenceDiagram
    participant O as Odysseus
    participant B as Bridge
    participant R as Runner (revisor)

    O->>B: review_create (origemRunId)
    B->>B: ValidarRequest
    B->>B: Policy.evaluate (read-only)
    B->>B: preparar workspace read-only
    B->>R: dispatch (sessão de revisão)
    R-->>B: findings
    B->>B: gerar relatório
    B-->>O: review_report
```

### 9. Aplicação de alteração aprovada (manual)

```mermaid
sequenceDiagram
    participant Op as Operador humano
    participant W as Workspace
    participant Git as Repositório remoto

    Op->>W: revisar diff e validações
    Op->>W: git checkout branch-execucao
    Op->>W: validar localmente
    Op->>Git: push manual + PR + merge manual
```

## Estados e transições

Ver [`../specs/003-run-lifecycle/spec.md`](../specs/003-run-lifecycle/spec.md) para a máquina de estados completa.

## Erros e exceções

* Erros de validação de entrada são retornados ao cliente sem alterar estado da execução.
* Erros de infraestrutura disparam estado `Failed` e emitem evento de incidente.
* Erros de política disparam estado `Rejected` ou `Cancelled` conforme o momento.
* Cancelamento incompleto é tratado pelo [`../specs/007-workspace-isolation/spec.md`](../specs/007-workspace-isolation/spec.md) e reconciliação após restart.

## Referências relacionadas

* [`system-design.md`](system-design.md)
* [`../specs/003-run-lifecycle/spec.md`](../specs/003-run-lifecycle/spec.md)
* [`../specs/005-opencode-runner/spec.md`](../specs/005-opencode-runner/spec.md)
* [`../specs/006-policy-engine/spec.md`](../specs/006-policy-engine/spec.md)
* [`../contracts/runner-adapter.md`](../contracts/runner-adapter.md)
