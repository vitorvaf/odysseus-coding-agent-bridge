# Logging

> **Status:** Proposed

Define a estratégia de logs do OCAB.

## Formato

* JSON estruturado.
* Saída: stdout.
* Coletado externamente.

## Campos mínimos

```json
{
  "timestamp": "2026-07-27T15:00:00Z",
  "level": "info",
  "service": "ocab-bridge",
  "environment": "prod",
  "trace_id": "trace_01JXYZ",
  "span_id": "span_01JXYZ",
  "run_id": "run_01JXYZ",
  "repository": "payment-hub",
  "agent": "opencode",
  "profile": "implementer",
  "run_type": "Implement",
  "phase": "ValidatingRequest",
  "event_type": "run.state_transition",
  "duration_ms": 50,
  "exit_code": 0,
  "policy": "v1",
  "decision": "allow",
  "message": "Run transitioned from ValidatingRequest to PreparingWorkspace"
}
```

## Níveis

* `debug` — diagnóstico detalhado.
* `info` — eventos de ciclo de vida.
* `warn` — eventos anormais recuperáveis.
* `error` — falhas.
* `fatal` — falhas irrecuperáveis.

## Redaction

* Cabeçalhos HTTP sensíveis.
* URLs com credenciais.
* Tokens, API keys, senhas.
* Conteúdo marcado como secreto.
* Comando stdout que possa conter credenciais.

## Coleta

* Logs enviados para coletor externo.
* Aggregação por `run_id`.

## Cardinalidade

* `run_id` apenas em logs/traces.
* `repository_slug` permitido em métricas.

## Retenção

* Logs de aplicação: 30 dias (configurável).
* Logs de auditoria: conforme política legal.

## Ferramentas

* Bridge: logger com sink JSON.
* Runner: logger com sink JSON.
* Coletor: externo (OpenTelemetry collector ou similar).

## Referências relacionadas

* [`../architecture/observability-architecture.md`](../architecture/observability-architecture.md)
* [`../specs/010-observability/spec.md`](../specs/010-observability/spec.md)
* [`../security/secrets-management.md`](../security/secrets-management.md)
