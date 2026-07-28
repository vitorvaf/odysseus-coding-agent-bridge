# Integration Tests

> **Status:** Proposed

Define testes de integração com dependências reais (limitadas).

## PostgreSQL

* Testcontainers.
* Schema aplicado via migrations.
* Cada teste usa schema isolado ou transações revertidas.

## Filesystem

* Diretório temporário por teste.
* Permissões POSIX testadas.

## Repositório Git

* Repositório de teste com fixture.
* Commits controlados.

## OpenCode Server

* Container dedicado em CI.
* Sessões descartáveis.
* Eventos capturados.
* **Provider LLM determinístico** (compatível com OpenAI `/v1/chat/completions`) servindo respostas previsíveis (`normal`, `slow`, `blocked`, `error`, `invalid`) para validar o ciclo read-only completo sem dependência de credencial paga. Configurado em `poc/opencode-provider/` e referenciado em `poc/opencode-container/opencode.json` via `provider.custom.<name>.baseURL`. Ver [ADR-0018 § Provider determinístico](../adr/0018-persistent-run-queue.md#decisão) e [Discovery 014](../discovery/014-deterministic-e2e-lifecycle.md).

## Cancelamento

* Execuções longas canceladas.
* Verificação de estado terminal `Cancelled`.
* `POST /session/{id}/abort` chamado no OpenCode real.
* `CancellationTokenSource` no coordinator propaga para o adapter e para o `HttpClient`.
* Cancelamento em estado terminal (`Cancelled`, `Completed`, `Failed`, `TimedOut`) é idempotente.
* `runner_unresponsive` registrado se o runner não responder em `cancelGraceSeconds` (default 30 s).

## Timeout

* Execuções com timeout curto (default 300 s, configurável por execução).
* Verificação de estado terminal `TimedOut`.
* `CancellationTokenSource` com `CancelAfter(timeoutSeconds)` é acionado quando o timer expira.
* O adapter chama `POST /session/{id}/abort` no OpenCode real.
* O `HttpClient.Timeout` (5 min, configurado em `Program.cs`) é independente do timeout do Run; testes explicitamente validam que timeout do Run não é confundido com timeout HTTP.

## Validação

* Comandos com sucesso.
* Comandos com falha.
* Comandos bloqueados (provider determinístico retorna SSE stream que segura a conexão aberta até cancelamento externo).
* Erro do provider (5xx) normalizado como `runner_unavailable`.
* Resposta inválida (`text/html` em vez de `application/json`) normalizada como `runner_contract_mismatch` (`UpstreamContractMismatch`).

## Concorrência

* Múltiplas execuções no mesmo repositório.
* Lock aplicado.
* **`RunQueueWorker` consome Runs `Pending` da fila persistente** (`runs` table); múltiplos workers podem coexistir via `SELECT … FOR UPDATE SKIP LOCKED`.
* `IRunExecutionCoordinator.WaitForTerminalStateAsync` retorna o estado terminal real (não `null` em timeout); polling do Postgres (200 ms) como fallback se a entrada do registry em memória foi limpa.

## Reconciliation após restart

* Bridge reiniciado retoma Runs órfãos via `RunQueueWorker.StartAsync`.
* Runs em `Running` cuja `updated_at` seja ≤ `timeoutSeconds` / 2 são retomadas com novo `CancellationTokenSource`.
* Runs em `Running` cuja `updated_at` seja > `timeoutSeconds` / 2 são marcadas como `TimedOut` (`reconciler` ator).

## Backup/Restore

* Backup de banco real.
* Restore em banco limpo.

## Cleanup

* Limpeza remove artefatos esperados.
* Lock por `runId`.
* **Após os testes E2E, nenhum processo órfão** (OpenCode runner, provedor LLM determinístico, bridge) deve estar ativo. Verificado via `pgrep` em script de cleanup ou assertion `Process.GetProcesses()`.

## Ferramentas

* Testcontainers.
* Docker CLI em CI.
* Bash scripts para setup.
* **Provedor LLM determinístico customizado** (HTTP em `poc/opencode-provider/`) controlado via `/control/scenario` para alternar entre `normal`, `slow`, `blocked`, `error` e `invalid`.

## Referências relacionadas

* [`strategy.md`](strategy.md)
* [`../specs/014-operations/spec.md`](../specs/014-operations/spec.md)
