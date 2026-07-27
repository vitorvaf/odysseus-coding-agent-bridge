# Troubleshooting

> **Status:** Proposed

Guia para diagnóstico de problemas comuns.

## Bridge não inicia

### Sintomas

* `docker compose up` falha.
* Logs com `fatal: configuration invalid`.

### Verificações

* `.env` preenchido (sem `__SET_ME__`).
* Arquivos de segredos presentes e legíveis.
* Permissões POSIX dos segredos (`0600`).
* Versão do Docker Engine.

## Readiness falhando

### Sintomas

* `/ready` retorna `503`.

### Verificações

* `docker compose ps` para postgres.
* Logs do postgres.
* `docker exec ocab-postgres pg_isready`.

## Execuções em loop

### Sintomas

* Execuções entram em fila e não progridem.

### Verificações

* `SELECT * FROM runs WHERE status IN ('Pending', 'ValidatingRequest', 'PreparingWorkspace') ORDER BY created_at`.
* Verificar `lockRepositoryId`.
* Logs do runner.

## Cancelamento incompleto

### Sintomas

* Execução fica em `Cancelling` por muito tempo.

### Verificações

* Logs do runner.
* Health check do runner.
* `runner_unresponsive` na timeline.

## Disco cheio

### Sintomas

* `disk_full` em logs.
* Execuções falham ao persistir artefato.

### Ações

* Limpar artefatos expirados manualmente.
* Aumentar volume.
* Revisar política de retenção.

## Segredo em log

### Sintomas

* Auditoria detecta segredo em log.

### Ações

1. Acionar [`../security/incident-response.md`](../security/incident-response.md).
2. Rotacionar segredo.
3. Atualizar filtro de redaction.
4. Documentar em ADR.

## Falha de redaction

### Sintomas

* Token presente em stdout.

### Ações

* Ajustar regex.
* Validar em testes automatizados.
* Reabrir execução de exemplo.

## Push bloqueado por policy

### Sintomas

* Execução termina com `policy_denied: git push`.

### Ações

* Confirmar que tentativa é legítima.
* Se sim, reforçar política (não desbloquear).
* Documentar.

## Bridge lento

### Sintomas

* P95 de MCP > 300 ms.

### Verificações

* Carga no postgres.
* Latência do Docker DNS.
* Logs de GC.
* Métricas `dotnet_gc_collections_total`.

## Referências relacionadas

* [`health-checks.md`](health-checks.md)
* [`logging.md`](logging.md)
* [`metrics.md`](metrics.md)
* [`../security/incident-response.md`](../security/incident-response.md)
