# Health Checks

> **Status:** Proposed

Define os health checks do bridge e dos serviços correlatos.

## Endpoints

| Endpoint | Propósito | Resposta |
| --- | --- | --- |
| `GET /health` | Liveness | `200 OK` se processo responde. |
| `GET /ready` | Readiness | `200 OK` se dependências OK. |
| `GET /metrics` | Prometheus | Texto em formato Prometheus. |

## Liveness

* Verifica apenas processo.
* Sem dependências externas.
* Resposta em menos de 1 s.

## Readiness

* Verifica:
  * PostgreSQL acessível.
  * Configuração válida.
  * Volumes montados.
* Resposta em menos de 1 s.

## Health check de runner

* Endpoint HTTP do OpenCode Server: `GET /health`.
* Bridge consulta periodicamente.
* Resultado registrado em `RunnerHealth`.

## Frequência

* Liveness: contínuo (orquestrador externo).
* Readiness: a cada 10 s.
* Runner: a cada 30 s.

## Alertas

* Liveness falhando: reinício do container.
* Readiness falhando: alerta operacional.
* Runner unhealthy: execução de novos runs bloqueada.

## Testes

* Matar postgres → readiness retorna 503.
* Restaurar postgres → readiness retorna 200.

## Referências relacionadas

* [`../architecture/observability-architecture.md`](../architecture/observability-architecture.md)
* [`../specs/010-observability/spec.md`](../specs/010-observability/spec.md)
* [`troubleshooting.md`](troubleshooting.md)
