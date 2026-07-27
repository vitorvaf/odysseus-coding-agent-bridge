# Data Architecture

> **Status:** Accepted

## Visão

Detalha a estratégia de persistência do OCAB. Cobre entidades conceituais, modelo relacional, ciclo de vida e estratégia de migrations.

## Princípios

* PostgreSQL é a única fonte de verdade para estado de execução, fila, eventos e auditoria.
* Artefatos grandes (logs, diffs, relatórios) vivem no filesystem.
* Toda referência no banco aponta para um caminho relativo dentro de `ocab-artifacts`.
* Toda referência inclui checksum, tamanho e tipo.
* Eventos são append-only e correlacionados por `runId`.

## Entidades principais

* `Repository` — repositório registrado por slug.
* `Agent` — agente disponível (OpenCode, Codex, Antigravity).
* `AgentCapability` — capacidades declaradas por agente.
* `Run` — execução de uma tarefa por um agente em um repositório.
* `RunEvent` — evento do ciclo de vida.
* `ValidationResult` — resultado de um comando de validação.
* `Artifact` — referência a arquivo no filesystem.
* `PolicyDecision` — decisão de política registrada.
* `Approval` — aprovação humana registrada.
* `RunnerHealth` — saúde observada de um runner.

Para o modelo conceitual detalhado, ver [`../data/conceptual-model.md`](../data/conceptual-model.md).

Para o mapeamento relacional, ver [`../data/relational-model.md`](../data/relational-model.md).

## Layout de artefatos

```text
ocab-artifacts/
  runs/
    <runId>/
      events.jsonl
      diff.patch
      stdout.log
      stderr.log
      final-report.md
      validation/
        build.log
        test.log
        test-results.xml
        coverage.xml
      workspace-snapshot.tar.gz
```

## Ciclo de vida

* **Execução:** artefatos ficam sob `runs/<runId>/` até expirar a retenção.
* **Retenção:** padrão 30 dias, configurável por ambiente.
* **Limpeza:** tarefa agendada remove artefatos expirados e marca `Run` como arquivado.

## Migrations

* Versionadas em `db/migrations/`.
* Aplicadas via ferramenta de migrations do EF Core ou script próprio.
* Aplicadas por job específico, não pelo startup da aplicação.
* Reversíveis quando possível; sem reversão silenciosa em produção.

## Backup

* Backup lógico via `pg_dump` agendado.
* Snapshot de volume opcional para restore rápido.
* Retenção de backup configurada por ambiente.

## Concorrência

* Locks pessimistas ou `SELECT FOR UPDATE` em transições críticas da máquina de estados.
* No máximo uma execução de escrita por repositório simultaneamente.
* Runs read-only podem coexistir, com limite opcional.

## Auditoria

* `PolicyDecision` registra ator, motivo e timestamp para cada decisão.
* `RunEvent` registra cada transição de estado.
* `Approval` registra aprovações humanas com timestamp.

## Dados sensíveis

* Segredos nunca são persistidos em texto puro.
* Prompts completos são registrados, mas passam por redaction antes da gravação.
* Logs de saída de comandos passam por redaction antes da gravação.

## Decisões relacionadas

* ADR-0004 (PostgreSQL como fila).
* ADR-0012 (artefatos no filesystem).
* ADR-0014 (slug de repositório).

## Referências relacionadas

* [`../data/conceptual-model.md`](../data/conceptual-model.md)
* [`../data/relational-model.md`](../data/relational-model.md)
* [`../data/lifecycle-and-retention.md`](../data/lifecycle-and-retention.md)
* [`../data/migrations-strategy.md`](../data/migrations-strategy.md)
* [`../security/secrets-management.md`](../security/secrets-management.md)
