# Discovery 007 — Process Execution Evaluation

> **Status:** Completed (with POC deferida)
> **Bloqueia:** Fase 1
> **Prioridade:** P0
> **Data:** 2026-07-27

## Objetivo

Avaliar como o bridge controlará subprocessos (validações, comandos auxiliares, adapters de runner) e propor a estratégia de lifecycle, sinais, buffers e cancelamento que será materializada no slice 1.1.1 e na spec 006.

## Cenários cobertos

| Cenário | Resultado esperado |
| --- | --- |
| Processo termina em 5 s com exit 0 | OK, exit code propagado |
| Processo termina em 5 s com exit 1 | exit code capturado e `Run.Failed` registrado |
| Processo não termina em `defaultTimeoutSeconds` | SIGTERM, captura de buffer parcial, SIGKILL de fallback |
| Processo ignora SIGTERM | SIGKILL após `graceSeconds` |
| Processo gera `n` MB de stdout | buffer aplicado, truncamento documentado |
| Container recebe SIGTERM | processos filhos terminam antes do container |
| Bridge reinicia durante execução | reconciliação (spec 003 RL-FR-015) |

## Estratégia recomendada

### Camada base: `System.Diagnostics.Process` (.NET)

| Aspecto | Decisão |
| --- | --- |
| Plataforma | Linux (Ubuntu 22.04 confirmado em [`001-environment-baseline.md`](001-environment-baseline.md)) |
| Inicialização | `Process.Start(new ProcessStartInfo { ... })` |
| Captura de stdout | redirecionar para `Process.StandardOutput.BaseStream` + `StreamReader.ReadAsync` |
| Captura de stderr | redirecionar para `Process.StandardError.BaseStream` |
| Cancelamento cooperativo | primeiro via HTTP/API (adapter runner); depois SIGTERM/SIGKILL |
| Timeout | `CancellationTokenSource` em conjunto com timer interno |
| Buffer máximo | por processo (default 10 MB stdout, 10 MB stderr) — configurável |
| Encoding | UTF-8 sem BOM |
| Environment variável | filtragem via allowlist no `ProcessStartInfo` |
| Working directory | validado contra o workspace (rejeitar `..`) |
| Argumentos | validados contra `security/command-policy.md` (catalog de comandos permitidos) |

### Sinais e ordenação

1. Solicitação de cancelamento chega ao `ProcessSupervisor`.
2. O supervisor dispara o sinal configurado (HTTP `POST /cancel` ou `kill -TERM`).
3. Espera por `cancelGraceSeconds` (default 30).
4. Se o processo ainda está vivo, `kill -KILL` e nova espera de 5 s.
5. Captura final de buffer parcial e loga como `process_force_killed`.

### Process group

Todo subprocesso é iniciado em novo process group (`ProcessStartInfo.CreateNoWindow = false` por padrão, mas o supervisor cria o process group via `setsid` wrapper script) para que sinais sejam entregues ao grupo inteiro, não apenas ao líder. Quando o signal é enviado, todos os filhos do grupo são afetados.

### JSONL ou eventos estruturados

* Cada subprocesso emite **uma linha JSON por evento** quando em modo `streaming` (ex.: `validation` em spec 008). Esse será o canal primário do runner para o bridge.
* Para validações e comandos auxiliares locais, `stdout`/`stderr` permanecem em texto bruto, capturados em buffer.

### Processos órfãos

* **Bridge → runner:** a comunicação é via HTTP. Cancelamento é via `POST /cancel`. Se o runner não responder, o bridge marca o processo como `unresponsive` e, periodicamente, executa `process cleanup` baseado em lock por `runId`.

### Restart do bridge

* O bridge mantém estado durável no PostgreSQL (spec 003).
* Em restart, o `RunReconciler` consulta execuções em estados não terminais e age conforme RL-FR-015.

### Restart do runner

* O runner é gerenciado externamente (ADR-0006), portanto seu restart é responsabilidade do operador via Compose. Em caso de indisponibilidade, execuções em `Running` migram para `Failed` (`runner_unavailable`) após health check falhar por `runnerHealthTimeoutSeconds`.

## Tabela de opções

| Opção | Plataforma | Cancelamento | Timeout | Buffer | Licença | Notas |
| --- | --- | --- | --- | --- | --- | --- |
| `System.Diagnostics.Process` (.NET nativo) | Linux + Windows | Sinal + `CancellationToken` | sim | manual (implementado pelo bridge) | MIT | primeira escolha |
| `MediatR`/Wrappers externos | Linux | varia | varia | varia | MIT/Apache-2.0 | não necessário no MVP |
| Wrapper em `bash` puro | Linux | sinal | sim | manual | n/a | descartado por falta de portabilidade Windows (fora de escopo) |

## Biblioteca recomendada

`System.Diagnostics.Process` com uma camada de `ProcessSupervisor` que encapsula:

* `Start(command, args, env, cwd, deadline)`
* `SendSignal(sig)`
* `WaitAsync(timeout, ct)`
* `EnsureKilled(gracePeriod)`
* `StreamOutputAsync(ct)` — expõe eventos estruturados via `IAsyncEnumerable<ProcessOutputChunk>`

> Justificativa: está disponível no runtime .NET 8 sem dependências externas, oferecendo controle fino sobre pid, sinais e streams.

## MVP — contrato de subprocesso

```csharp
public interface IProcessSupervisor
{
    Task<ProcessExecutionResult> ExecuteAsync(
        ProcessRequest request,
        CancellationToken cancellationToken);
}

public sealed record ProcessRequest(
    string Command,
    IReadOnlyList<string> Args,
    string? WorkingDirectory,
    IReadOnlyDictionary<string, string>? Environment,
    TimeSpan Timeout,
    TimeSpan CancelGrace,
    long MaxOutputBytes);

public sealed record ProcessExecutionResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    ProcessExitReason Reason, // Completed, Timeout, Cancelled, Failed
    TimeSpan Elapsed);
```

## Riscos identificados

| Risco | Mitigação |
| --- | --- |
| Processo órfão segurar recursos | `ProcessSupervisor` usa process group + SIGKILL de fallback + lock por `runId` |
| Buffer ilimitado consumir memória | limite por processo (`MaxOutputBytes`), truncamento logado |
| `kill` em PIDs errados | sempre via PGID ou `runId`, nunca PID arbitrário |
| Exit code ambíguo (Windows/Linux) | `Process.ExitCode` é inteiro; documentar range esperado |
| Encoding inconsistente | forçar UTF-8 sem BOM no `ProcessStartInfo.StandardOutputEncoding` |

## POC deferida (justificativa)

A POC local em .NET foi planejada para o slice 1.1.1 (Foundation). **Não foi executada na Fase 0** porque:

1. A Fase 0 produz descoberta documental + POCs de viabilidade ambiental.
2. A POC completa de `ProcessSupervisor` exige a solução ASP.NET Core já criada — o que é responsabilidade do slice 1.1.1.
3. A recomendação `System.Diagnostics.Process` é consensual com a base de conhecimento .NET e os critérios; execução local agrega pouco no nível de confiança.

> O critério de fechamento está, portanto, satisfeito pela recomendação argumentada. A validação executiva deve ser registrada no slide 1.1.1.

## Questões abertas afetadas

* **OQ-005** (biblioteca de subprocessos): `Resolved` — `System.Diagnostics.Process` + `ProcessSupervisor`.
* **OQ-024** (retries): permanece `Open`. Política de retries em falhas transitórias será definida na Fase 7.

## Próximo passo

* No slice 1.1.1, implementar `ProcessSupervisor` com cobertura de testes unitários.
* Adicionar ao `specs/006-policy-engine/spec.md` (e `008-validation-pipeline/spec.md` quando aplicável) a referência a `ProcessSupervisor.ExecuteAsync`.
* Confirmar em produção real durante o slice 1.1.3 (OpenCode Read-Only Vertical Slice).
