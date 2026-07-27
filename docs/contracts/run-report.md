# run_report — Contrato de relatório

> **Status:** Proposed

Define o schema do relatório final de uma execução.

## Estrutura

```json
{
  "runId": "run_01JXYZ",
  "status": "Completed",
  "summary": "Implementação concluída com sucesso.",
  "scope": {
    "repository": "payment-hub",
    "baseReference": "dev",
    "branch": "ocab/run_01JXYZ",
    "filesChanged": 4,
    "insertions": 120,
    "deletions": 35
  },
  "filesChanged": [
    {
      "path": "src/PaymentHub.Api/Controllers/OrdersController.cs",
      "insertions": 80,
      "deletions": 10,
      "binary": false
    }
  ],
  "validations": [
    {
      "command": "dotnet build",
      "exitCode": 0,
      "durationMs": 12000,
      "timeout": false,
      "artifacts": [
        { "type": "build.log", "ref": "ocab-artifacts://runs/run_01JXYZ/validation/build.log" }
      ]
    }
  ],
  "findings": [
    {
      "severity": "info",
      "file": "src/PaymentHub.Api/Controllers/OrdersController.cs",
      "line": 42,
      "message": "Sugestão de extração de método."
    }
  ],
  "risks": [
    {
      "category": "compatibility",
      "description": "Mudança em assinatura pública pode afetar consumidores."
    }
  ],
  "pendingItems": [
    {
      "type": "review",
      "description": "Aguardando revisão humana."
    }
  ],
  "artifacts": [
    { "type": "diff.patch", "ref": "ocab-artifacts://runs/run_01JXYZ/diff.patch", "checksum": "sha256:...", "size": 4096 },
    { "type": "events.jsonl", "ref": "ocab-artifacts://runs/run_01JXYZ/events.jsonl", "checksum": "sha256:...", "size": 8192 },
    { "type": "final-report.md", "ref": "ocab-artifacts://runs/run_01JXYZ/final-report.md", "checksum": "sha256:...", "size": 2048 }
  ],
  "duration": {
    "startedAt": "2026-07-27T15:00:05-03:00",
    "finishedAt": "2026-07-27T15:08:42-03:00",
    "totalSeconds": 517
  },
  "statusInfo": {
    "current": "Completed",
    "previous": "ValidatingResult"
  },
  "approval": null
}
```

## Schema (resumo)

```json
{
  "type": "object",
  "required": ["runId", "status", "scope", "duration"],
  "properties": {
    "runId": { "type": "string" },
    "status": { "type": "string" },
    "summary": { "type": "string" },
    "scope": { "type": "object" },
    "filesChanged": { "type": "array" },
    "validations": { "type": "array" },
    "findings": { "type": "array" },
    "risks": { "type": "array" },
    "pendingItems": { "type": "array" },
    "artifacts": { "type": "array" },
    "duration": { "type": "object" },
    "statusInfo": { "type": "object" },
    "approval": { "type": ["object", "null"] }
  }
}
```

## Campos

| Campo | Tipo | Descrição |
| --- | --- | --- |
| `runId` | string | Identificador único da execução. |
| `status` | string | Estado final. |
| `summary` | string | Resumo em linguagem natural. |
| `scope` | object | Escopo da execução (repo, base, branch, contadores). |
| `filesChanged` | array | Arquivos alterados com contadores. |
| `validations` | array | Resultados das validações. |
| `findings` | array | Findings do runner. |
| `risks` | array | Riscos identificados. |
| `pendingItems` | array | Pendências. |
| `artifacts` | array | Referências a artefatos. |
| `duration` | object | Tempos da execução. |
| `statusInfo` | object | Histórico curto do estado. |
| `approval` | object? | Aprovação humana quando aplicável. |

## Invariantes

* Todo relatório é gerado após a execução atingir estado terminal.
* `summary` não inclui segredos.
* `artifacts` contém checksum e tamanho.
* `duration` é sempre preenchido.

## Referências relacionadas

* [`mcp-tools.md`](mcp-tools.md)
* [`artifacts.md`](artifacts.md)
* [`../specs/004-mcp-contract/spec.md`](../specs/004-mcp-contract/spec.md)
* [`../specs/009-artifact-management/spec.md`](../specs/009-artifact-management/spec.md)
