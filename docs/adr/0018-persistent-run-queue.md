# ADR-0018 — Persistent Run Queue with Awaitable Execution Coordination

## Status

Proposed

## Contexto

O `RunDispatcher.CreateAsync` (introduzido em slice 1.1.3) insere um Run com `status = Pending` no Postgres e dispara `_ = Task.Run(() => ExecuteAsync(...))` em **fire-and-forget**. Esse padrão, embora simples, deixa três pontos críticos sem cobertura arquitetural:

1. **Cancelamento e timeout** dependem de um `CancellationToken` que ninguém observa depois do `Task.Run` retornar; em testes E2E (`SLICE-STAB-002`, `discovery/013`), a exception lançada durante o `StreamEventsAsync` (OpenCode sem provedor LLM) ficou pendurada até o container Postgres ser disposed — o que prova que o ciclo de cancelamento não é determinístico.

2. **Reconciliação após restart** do bridge: Runs com `status = Running` ficam órfãos. Sem um worker que repick-up, o `WaitForTerminalAsync` em clientes MCP nunca retorna.

3. **Observabilidade e auditoria**: testes determinísticos precisam de uma forma observável de aguardar o terminal state. Hoje o cliente HTTP `/v1/runs` retorna imediatamente após inserir; nada informa que a execução está em andamento, falhou ou foi cancelada.

A `discovery/013-opencode-end-to-end-smoke.md` documenta esses sintomas e foi a evidência para esta ADR. A `discovery/012-opencode-contract-spike.md` resolveu o contrato HTTP do OpenCode, mas não cobre o lifecycle do lado do bridge.

## Decisão

Adotar a abstração `IRunExecutionCoordinator` com a seguinte topologia:

```
┌────────────────────────┐    ┌────────────────────────┐
│ /v1/runs POST          │    │ /mcp run_create        │
│ /mcp run_create         │    │ (MCP Streamable HTTP)  │
└──────────┬─────────────┘    └──────────┬─────────────┘
           │ enqueue (insert Pending)        │
           ▼                                 ▼
        ┌─────────────────────────────────────┐
        │ Postgres runs table (fila)            │
        │ status = Pending → Running → terminal│
        └─────────────────┬───────────────────┘
                          │ poll (500 ms)
                          ▼
        ┌─────────────────────────────────────┐
        │ RunQueueWorker (BackgroundService)  │
        │ - claims Pending rows (FOR UPDATE    │
        │   SKIP LOCKED)                        │
        │ - opens CancellationTokenSource per  │
        │   run                                 │
        │ - tracks in registry (CTS + TCS)     │
        │ - delegates execution to scope'd     │
        │   RunDispatcher                       │
        │ - persists terminal state on exit    │
        └─────────────────┬───────────────────┘
                          │
                          ▼
        ┌─────────────────────────────────────┐
        │ IRunnerAdapter (OpenCode real)       │
        │ - StartSessionAsync                   │
        │ - SendPromptAsync                      │
        │ - StreamEventsAsync                    │
        └─────────────────────────────────────┘
```

### API pública

```csharp
public interface IRunExecutionCoordinator
{
    Task EnqueueAsync(
        Guid runId,
        CancellationToken cancellationToken);

    Task<RunTerminalResult> WaitForTerminalStateAsync(
        Guid runId,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    Task<bool> CancelAsync(
        Guid runId,
        string reason,
        CancellationToken cancellationToken);
}
```

### Implementação: `RunExecutionCoordinator`

* **`EnqueueAsync(runId)`**: idempotente. Verifica que o Run está em `Pending`; caso contrário, no-op. O worker reclama via `SELECT … FOR UPDATE SKIP LOCKED` (Postgres advisory lock nativo). Múltiplos workers (réplicas do bridge) podem coexistir sem execução duplicada.
* **`WaitForTerminalStateAsync(runId, timeout)`**: fast-path via `TaskCompletionSource<RunTerminalResult>` registrado no registry em memória; fallback para polling do Postgres com intervalo 200 ms (até `timeout`). Sempre respeita `CancellationToken` do chamador.
* **`CancelAsync(runId, reason)`**: se a execução está ativa no registry, aciona o `CancellationTokenSource` correspondente (propaga para o adapter e para o `HttpClient.Timeout`). Se a execução já terminou, transição idempotente no DB (não regressão: `Cancelled` após `Completed` é rejeitado pelo invariante de máquina de estados).
* **Registry em memória**: `ConcurrentDictionary<Guid, ActiveExecution>` onde `ActiveExecution` contém `CancellationTokenSource` e `TaskCompletionSource<RunTerminalResult>`. Não é fonte de verdade — o DB é — mas permite wake-up imediato para testes determinísticos e para clientes MCP que querem SSE/streaming.
* **Cleanup**: ao terminar (qualquer caminho — `Completed`, `Failed`, `Cancelled`, `TimedOut`, exception não observada), o worker remove a entrada do registry e persiste o estado terminal antes de retornar.

### Persistência é a fonte da verdade

* `runs.status` no Postgres é o source of truth. `WaitForTerminalStateAsync` cai para polling se o TCS em memória não estiver registrado (cenário: cliente MCP de uma réplica diferente, ou bridge reiniciado entre `Enqueue` e `Wait`).
* Reinício do bridge: `RunQueueWorker.StartAsync` faz scan de Runs em `Pending` ou `Running` (órfãos com `updated_at` antigo > timeout) e os retoma. Reconciliação por timeout, não por heartbeat (mais simples e suficiente para MVP).
* Migration `V006__add_run_timeout.sql` adiciona coluna `timeout_seconds INT NOT NULL DEFAULT 300` em `runs`.

### Por que fila persistente no Postgres e não RabbitMQ/Redis

* MVP não tem dependência externa além do Postgres (que já é pré-requisito do bridge).
* Lista de Runs pendentes é naturalmente modelada como `SELECT … FROM runs WHERE status = 'Pending' AND …`; polling a 500 ms é trivial em Postgres e não satura carga.
* `FOR UPDATE SKIP LOCKED` é nativo do Postgres e dispensa locking distribuído.
* Quando o produto crescer além de MVP, a fila persistente pode ser substituída por um broker dedicado; até lá, **não vale a complexidade** (ver Critérios para revisitar abaixo).

## Alternativas consideradas

* **TaskCompletionSource em memória como única fonte de verdade** — rejeitada: qualquer restart do bridge ou deployment derrubaria a TCS; clientes MCP em outras réplicas não conseguiriam `WaitForTerminalStateAsync`.
* **Heartbeat + watchdog** — rejeitada para o MVP: adiciona complexidade de tracking de liveness e reconciliation mais complexa (precisa de coordenação de leases); o scan periódico por Runs órfãos é mais simples.
* **RabbitMQ / Redis Streams** — rejeitada para o MVP: adiciona dependência operacional e nova superfície de segurança (TLS, auth, backup) sem ganho claro até escala > 100 runs/min.
* **Manter `Task.Run` mas adicionar `await`-friendly wrapper** — rejeitada: a exception não observada ainda é silenciosa; o ciclo de cancelamento ainda depende do chamador manter a referência ao `Task` (que não está exposta via interface).

## Consequências positivas

* **Determinismo em testes**: `WaitForTerminalStateAsync` retorna o estado terminal em vez de retornar null quando o tempo expira.
* **Cancelamento real**: `CancellationToken` é propagado para o adapter e para o `HttpClient`; `POST /session/{id}/abort` é chamado quando o `CancellationToken` aciona durante o stream SSE.
* **Reconciliação**: restart do bridge retoma Runs órfãos via scan periódico.
* **Observabilidade**: o registry em memória permite tracing de quantas execuções estão ativas em qualquer momento (métrica Prometheus `ocab_runs_active`).

## Consequências negativas

* Latência adicional de até 500 ms entre `EnqueueAsync` e o início da execução (intervalo de poll do worker). Mitigação: aceita para MVP; substituível por `LISTEN/NOTIFY` do Postgres quando necessário.
* Complexidade operacional maior (worker como `BackgroundService` precisa de graceful shutdown, timeouts, tratamento de deadlock no `FOR UPDATE SKIP LOCKED`). Mitigação: testes de stress e chaos no smoke test E2E.
* `WaitForTerminalStateAsync` pode retornar `TimedOut` mesmo que a execução continue rodando em background (worker ainda ativo). Mitigação: o test documenta explicitamente que `TimedOut` é **terminal sob a perspectiva do chamador**; o worker continua até persistir o estado real, que sobrescreve o snapshot do timeout.

## Riscos

* **Deadlock no `FOR UPDATE SKIP LOCKED`**: se a transação demorar mais que o `timeout_seconds`, outros workers pulam a linha e ela fica órfã. Mitigação: transação curta (apenas `UPDATE runs SET status = 'Running' WHERE run_id = …`); retry no próximo poll.
* **Concorrência entre múltiplos workers**: `SKIP LOCKED` resolve claim duplicado; mas se dois workers pegarem o mesmo run por bug, o `status = 'Running'` check no adapter detecta. Mitigação: invariante explícito `WHERE status = 'Pending'` na UPDATE de claim.
* **Timeout do `HttpClient` confundido com timeout do Run**: hoje o `HttpClient.Timeout = 5 min` no Program.cs é independente do `Run.TimeoutSeconds`. A ADR não altera essa configuração; testes E2E explicitamente separam os dois.

## Impacto operacional

* `compose.yaml` adiciona serviço `ocab-opencode-provider` com imagem deterministic-provider (vide `poc/opencode-provider/Dockerfile`).
* `compose.yaml` adiciona env vars `Ocab__OpenCodeProviderUrl` e `Ocab__OpenCodeProviderKey` no `ocab-bridge`.
* `appsettings.json` ganha `Ocab.OpenCodeProviderUrl` (default `http://ocab-opencode-provider:14302`) e `Ocab.OpenCodeProviderKey` (placeholder `__SET_ME__`).
* Migration `db/migrations/V006__add_run_timeout.sql` aplicada automaticamente via `compose.yaml` mount em `db/init/`.
* Métrica Prometheus `ocab_runs_active` (gauge) exposta em `/metrics`.

## Impacto de segurança

* Provider key lida de env var, nunca commitada.
* Sem Docker socket, sem `privileged`, `cap_drop: ALL` no provider (mesmo padrão do runner).
* Logs do provider passam por redaction no Program.cs.
* `CancellationToken` é local ao processo (não distribuído); sem superfície de ataque adicional.

## Critérios para revisitar

* Substituir poll loop por `LISTEN/NOTIFY` do Postgres quando a cadência de Runs passar de 10/min.
* Mover o registry em memória para Redis ou stream distribuído se o bridge rodar em múltiplas réplicas ativas simultaneamente.
* Adicionar métricas de duração por estado (histograma) para observabilidade de gargalos.

## Referências relacionadas

* [`../discovery/013-opencode-end-to-end-smoke.md`](../discovery/013-opencode-end-to-end-smoke.md) — evidência do problema.
* [`../discovery/014-deterministic-e2e-lifecycle.md`](../discovery/014-deterministic-e2e-lifecycle.md) — evidência do fix E2E.
* [`../specs/003-run-lifecycle/spec.md`](../specs/003-run-lifecycle/spec.md) — Spec 003 atualizada.
* [`../specs/005-opencode-runner/spec.md`](../specs/005-opencode-runner/spec.md) — Spec 005 atualizada.
* [`../open-questions.md`](../open-questions.md) — `OQ-200` (Estabilização do Epic 1) e `OQ-201` (contract drift) referenciam esta ADR.
* [`../planning/backlog.md`](../planning/backlog.md) — `SLICE-STAB-003` (Slice 1.2.2) implementa esta ADR.
* [`0009-opencode-first-runner.md`](0009-opencode-first-runner.md) — ADR original que decidiu OpenCode como primeiro runner.
* [`0017-pin-opencode-version.md`](0017-pin-opencode-version.md) — ADR que pinou `v1.18.8` e o contrato HTTP, pré-requisito para esta ADR.

## Histórico

* **2026-07-28:** Aceita como `Proposed` em stabilization gate da Fase 1 (Epic 1 → Epic 2). Promoverá para `Accepted` quando os 13 critérios de aceite da `SLICE-STAB-003` forem satisfeitos com testes E2E verdes ponta a ponta.
