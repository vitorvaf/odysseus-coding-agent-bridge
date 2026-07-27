# Observability Architecture

> **Status:** Accepted

## Pilares

* **Logs** estruturados.
* **Métricas** de aplicação e de negócio.
* **Traces** distribuídos.
* **Correlação por `runId`**.
* **Redaction** automática.

## Sinais por componente

| Componente | Logs | Métricas | Traces |
| --- | --- | --- | --- |
| Bridge | Todos eventos da execução | `agent_runs_*`, `agent_policy_denials_total`, latência MCP | `mcp.request`, `run.create`, `policy.evaluate` |
| OpenCode Runner | Sessão, eventos do agente | `agent_runner_health`, latência de execução | `runner.execute`, `runner.event` |
| Codex Runner | Sessão, eventos do agente | idem | idem |
| PostgreSQL | Erros, slow queries | Conexões, queries, locks | — |

## Correlação

Todo evento carrega:

```text
run_id
repository_slug
agent_id
profile
run_type
phase
```

Spans filhos herdam o `trace_id` do span raiz da execução.

## Coleta e exportação

* OpenTelemetry como API padrão.
* Exportador OTLP para coletor (opcional).
* Exportador Prometheus para scraping local (opcional).
* Logs em JSON com timestamps UTC.

## Redaction

Antes de gravar qualquer log, evento ou artefato:

* Cabeçalhos HTTP sensíveis são removidos.
* URLs com credenciais são mascaradas.
* Tokens, API keys e senhas são removidos por padrão.
* Saída de comando é filtrada por regras configuráveis.

## Cardinalidade

* Métricas devem usar rótulos de baixa cardinalidade.
* `runId` não deve aparecer como rótulo de métrica — apenas como rótulo de log/trace.
* Tags como `repository_slug` podem aparecer em métricas, desde que a allowlist seja pequena.

## Saúde

* Endpoint HTTP `/health` no bridge.
* Readiness: dependências externas acessíveis.
* Liveness: processo respondendo.

Para detalhes, ver [`../specs/010-observability/spec.md`](../specs/010-observability/spec.md) e [`../operations/health-checks.md`](../operations/health-checks.md).

## Decisões relacionadas

* ADR-0013 (rede Docker privada).

## Referências relacionadas

* [`../specs/010-observability/spec.md`](../specs/010-observability/spec.md)
* [`../operations/logging.md`](../operations/logging.md)
* [`../operations/metrics.md`](../operations/metrics.md)
* [`../operations/tracing.md`](../operations/tracing.md)
* [`../security/secrets-management.md`](../security/secrets-management.md)
