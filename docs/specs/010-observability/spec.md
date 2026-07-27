# Spec 010 — Observability

## Status

Proposed

## Resumo

Define logs estruturados, métricas, traces, correlação, redaction e health checks.

## Contexto

Sem observabilidade consistente, fica difícil diagnosticar falhas, auditar decisões e operar a plataforma.

## Problema

Como garantir logs, métricas e traces úteis, correlacionados por `runId`, sem expor segredos?

## Objetivos

* Logs estruturados com campos mínimos.
* Métricas padronizadas.
* Traces correlacionados.
* Redaction automática.
* Health checks confiáveis.

## Não objetivos

* Fornecer dashboards prontos.
* Implementar alerting complexo.

## Escopo funcional

* Logs em JSON com redaction.
* Métricas Prometheus.
* Tracer OTLP.
* Endpoints `/health`, `/ready`, `/metrics`.
* Health checks de dependências.

## Requisitos funcionais

* **OB-FR-001** Logs devem incluir `timestamp`, `level`, `service`, `environment`, `trace_id`, `span_id`, `run_id` (quando aplicável).
* **OB-FR-002** Logs devem ser emitidos no stdout em formato JSON.
* **OB-FR-003** Métricas devem usar o formato Prometheus.
* **OB-FR-004** Traces devem usar OpenTelemetry.
* **OB-FR-005** Redaction deve ser aplicada antes da gravação de logs e artefatos.
* **OB-FR-006** Health check `/health` deve ser leve (liveness).
* **OB-FR-007** Readiness `/ready` deve verificar dependências (postgres).
* **OB-FR-008** Métricas devem ter cardinalidade limitada.

## Requisitos não funcionais

* **OB-NFR-001** Logs devem suportar até 10.000 entradas por execução sem degradação perceptível.
* **OB-NFR-002** Traces devem amostrar 100% no MVP.
* **OB-NFR-003** Endpoints de observabilidade não devem exigir autenticação interna.
* **OB-NFR-004** Métricas não devem incluir `runId` como rótulo.

## Atores e componentes envolvidos

* Bridge.
* Runners.
* PostgreSQL.
* Operador técnico.

## Casos de uso

* Investigar execução.
* Medir throughput.
* Configurar alerta.

## Campos mínimos de log

```text
timestamp
level
service
environment
trace_id
span_id
run_id
repository
agent
profile
run_type
phase
event_type
duration_ms
exit_code
policy
decision
```

## Métricas mínimas

```text
agent_runs_total
agent_runs_active
agent_runs_failed_total
agent_runs_cancelled_total
agent_runs_timed_out_total
agent_run_duration_seconds
agent_validation_duration_seconds
agent_policy_denials_total
agent_runner_health
agent_workspace_size_bytes
agent_artifact_size_bytes
agent_queue_depth
```

## Spans mínimos

```text
mcp.request
run.create
policy.evaluate
workspace.prepare
runner.dispatch
runner.execute
validation.execute
git.diff
artifact.persist
report.build
workspace.cleanup
```

## Redaction

Tokens, API keys, senhas, headers sensíveis, URLs com credenciais, conteúdo marcado como secreto.

Implementação:

* Filtro por padrão regex.
* Allowlist de campos sensíveis por configuração.
* Replay antes de gravação.

## Saúde

* `/health` liveness — processo responde.
* `/ready` readiness — postgres conectado.
* `/metrics` Prometheus.

## Cardinalidade

* `repository_slug` permitido como rótulo.
* `agent_id` permitido.
* `run_id` **não** permitido como rótulo de métrica.

## Contratos

* [`../contracts/events.md`](../contracts/events.md)

## Modelo de dados afetado

* Sem entidade nova. Observabilidade é infraestrutura.

## Segurança

* Redaction antes da gravação.
* Endpoints administrativos em porta isolada.

## Estratégia de testes

* Unitários: redaction, formato de log.
* Integração: coleta de métricas em ambiente real.
* Segurança: presença de segredo em log é falha.

## Critérios de aceite

* **OB-AC-001** Logs emitidos pelo bridge seguem schema mínimo.
* **OB-AC-002** Segredos não aparecem em logs.
* **OB-AC-003** Métricas expostas em `/metrics` são válidas para Prometheus.
* **OB-AC-004** Traces carregam `trace_id` em todos os spans filhos.
* **OB-AC-005** `/ready` retorna 200 somente quando postgres está acessível.

## Dependências

* Spec 001 (Platform Foundation).

## Riscos

* Cardinalidade alta pode derrubar Prometheus.
* Reconfiguration incorreta da redaction pode vazar segredos.

## Decisões relacionadas

* [ADR-0013](../adr/0013-private-docker-network.md)

## Questões em aberto

* Ferramenta de visualização (Grafana ou similar).
* Política de amostragem de traces no pós-MVP.

## Fora de escopo

* Alertas.
* Dashboards.

## Estratégia de entrega incremental

1. Logs estruturados.
2. Métricas básicas.
3. Tracer.
4. Redaction.
5. Health checks.
6. Métricas adicionais.
