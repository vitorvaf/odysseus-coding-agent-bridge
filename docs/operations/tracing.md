# Tracing

> **Status:** Proposed

Define a estratégia de tracing distribuído do OCAB.

## Padrão

* OpenTelemetry.
* Exportador OTLP (configurável).
* Amostragem: 100% no MVP.

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

## Atributos padrão

```text
service.name
service.version
deployment.environment
run.id
run.type
run.access_mode
repository.slug
agent.id
agent.profile
```

## Correlação

* `trace_id` propaga do MCP até os runners.
* `run.id` é atributo em todos os spans da execução.

## Saída

* OTLP HTTP/gRPC.
* Endpoint configurável.

## Retenção

* Traces: 7 dias (configurável).
* Apenas metadados em logs estruturados após esse período.

## Testes

* Cada span deve aparecer em uma execução de exemplo.
* Atributos devem estar presentes.

## Referências relacionadas

* [`../architecture/observability-architecture.md`](../architecture/observability-architecture.md)
* [`../specs/010-observability/spec.md`](../specs/010-observability/spec.md)
