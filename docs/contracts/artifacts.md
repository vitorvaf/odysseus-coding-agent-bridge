# Artifacts — Contrato de artefatos

> **Status:** Proposed

Define estrutura, naming, tipos, referências e ciclo de vida dos artefatos.

## Localização

Todos os artefatos residem sob:

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
        <command>.log
      workspace-snapshot.tar.gz  (opcional)
```

## Tipos suportados

| Tipo | Localização | Descrição |
| --- | --- | --- |
| `events.jsonl` | `runs/<runId>/` | Eventos append-only. |
| `diff.patch` | `runs/<runId>/` | Diff Git unificado. |
| `stdout.log` | `runs/<runId>/` | Saída padrão do runner. |
| `stderr.log` | `runs/<runId>/` | Saída de erro do runner. |
| `final-report.md` | `runs/<runId>/` | Relatório final. |
| `validation/<command>.log` | `runs/<runId>/validation/` | Log de validação. |
| `test-results.xml` | `runs/<runId>/validation/` | Resultado de testes (JUnit). |
| `coverage.xml` | `runs/<runId>/validation/` | Cobertura. |
| `workspace-snapshot.tar.gz` | `runs/<runId>/` | Snapshot opcional do workspace. |

## Referência

Forma:

```text
ocab-artifacts://runs/<runId>/<path-relativo>
```

ou objeto JSON:

```json
{
  "runId": "run_01JXYZ",
  "type": "diff.patch",
  "path": "runs/run_01JXYZ/diff.patch",
  "checksum": "sha256:...",
  "size": 4096,
  "mimeType": "text/x-diff",
  "truncated": false
}
```

## Sanitização

* Path validado para impedir traversal.
* Nome de arquivo sanitizado (`[a-zA-Z0-9._-]`).
* Comando de validação não pode injetar caminhos arbitrários.

## Checksum

* Algoritmo: SHA-256.
* Formato: `sha256:<hex>`.
* Calculado no momento da gravação, em streaming.

## Tamanho

* Limite padrão por arquivo: 50 MB.
* Limite total por execução: 1 GB.
* Exceder limite retorna erro `artifact_too_large` ou trunca com `truncated=true`, conforme config.

## Redaction

* Antes de gravação, aplicar filtros de redaction.
* Antes de download, aplicar redaction novamente.
* Logs de validação são tratados com cuidado redobrado.

## Retenção

* Default: 30 dias.
* Configurável por execução via `retentionDays`.
* Modo `diagnostic` preserva o volume além do default.

## Download

* Via endpoint autenticado.
* Referência opaca.
* Streaming com `Content-Length` e `ETag` baseado em checksum.

## Deleção

* Por rotina agendada.
* Por comando manual do operador (com auditoria).
* Lock por `runId`.

## Modelo de dados

Ver [`../data/conceptual-model.md`](../data/conceptual-model.md) e a entidade `Artifact`.

## Referências relacionadas

* [`run-report.md`](run-report.md)
* [`events.md`](events.md)
* [`../specs/009-artifact-management/spec.md`](../specs/009-artifact-management/spec.md)
* [`../security/secrets-management.md`](../security/secrets-management.md)
