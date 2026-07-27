# Metrics

> **Status:** Proposed

Define métricas padronizadas expostas pelo OCAB.

## Princípios

* Formato Prometheus.
* Cardinalidade limitada.
* Rótulos permitidos: `repository_slug`, `agent_id`, `status`.
* `run_id` **não** permitido como rótulo.

## Métricas de execução

```text
agent_runs_total{repository, agent, type, access_mode, status}
agent_runs_active{repository, agent}
agent_runs_failed_total{repository, agent, reason}
agent_runs_cancelled_total{repository, agent}
agent_runs_timed_out_total{repository, agent}
agent_run_duration_seconds{repository, agent, status} (histogram)
agent_validation_duration_seconds{repository, command} (histogram)
```

## Métricas de política

```text
agent_policy_denials_total{repository, agent, reason}
agent_policy_evaluations_total{repository, phase, decision}
```

## Métricas de runner

```text
agent_runner_health{agent}
agent_runner_session_active{agent}
```

## Métricas de recursos

```text
agent_workspace_size_bytes{repository}
agent_artifact_size_bytes{repository, type}
agent_disk_used_bytes
agent_disk_available_bytes
```

## Métricas de fila

```text
agent_queue_depth{repository, access_mode}
agent_queue_oldest_age_seconds{repository, access_mode}
```

## Métricas de HTTP

```text
http_server_request_duration_seconds{path, method, status}
```

## Métricas internas

```text
dotnet_gc_collections_total
dotnet_threadpool_threads_count
```

## Buckets sugeridos (duração)

```text
[1, 5, 15, 30, 60, 120, 300, 600, 1800, 3600]
```

## Referências relacionadas

* [`../architecture/observability-architecture.md`](../architecture/observability-architecture.md)
* [`../specs/010-observability/spec.md`](../specs/010-observability/spec.md)
