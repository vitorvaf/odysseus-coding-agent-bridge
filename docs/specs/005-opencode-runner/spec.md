# Spec 005 — OpenCode Runner

## Status

Proposed

## Resumo

Define a integração entre o Coding Agent Bridge e o OpenCode Server, incluindo criação de sessão, envio de prompt, acompanhamento de eventos, cancelamento, timeout, captura de resultado e testes de contrato.

## Contexto

O OpenCode é o primeiro runner do MVP. Sua API HTTP precisa ser encapsulada por um adapter que implementa `IRunnerAdapter`.

## Problema

Como integrar o OpenCode Server de forma isolada, cancelável, observável e compatível com o contrato comum?

## Objetivos

* Criar adapter OpenCode.
* Estabelecer contrato HTTP entre bridge e OpenCode.
* Garantir cancelamento e timeout.
* Coletar eventos estruturados.
* Manter testes de contrato.

## Não objetivos

* Implementar o OpenCode Server.
* Substituir o OpenCode por outro executor no MVP.

## Escopo funcional

* Container `ocab-opencode-runner` baseado em imagem oficial do OpenCode Server.
* Adapter no bridge implementando `IRunnerAdapter`.
* Endpoints:
  * `POST /sessions` — criar sessão.
  * `POST /sessions/{id}/prompt` — enviar prompt.
  * `GET /sessions/{id}/events` — acompanhar eventos.
  * `POST /sessions/{id}/cancel` — cancelar sessão.
  * `GET /health` — health check.
* Autenticação por token interno.
* Timeouts configuráveis.
* Persistência de eventos em `RunEvent`.

## Requisitos funcionais

* **OCR-FR-001** O adapter deve implementar `IRunnerAdapter`.
* **OCR-FR-002** O adapter deve criar sessão antes de enviar prompt.
* **OCR-FR-003** O adapter deve enviar prompt exatamente uma vez por sessão.
* **OCR-FR-004** O adapter deve consumir eventos estruturados do OpenCode.
* **OCR-FR-005** O adapter deve permitir cancelamento cooperativo.
* **OCR-FR-006** O adapter deve respeitar timeout configurado.
* **OCR-FR-007** O adapter deve retornar resultado final padronizado para o bridge.
* **OCR-FR-008** O adapter deve validar saúde do OpenCode antes de despachar.
* **OCR-FR-009** O adapter deve emitir evento `runner_unhealthy` se OpenCode falhar health check.

## Requisitos não funcionais

* **OCR-NFR-001** Container deve executar como usuário não root.
* **OCR-NFR-002** Sem Docker socket.
* **OCR-NFR-003** Limites de CPU e memória configurados.
* **OCR-NFR-004** Health check deve responder em menos de 500 ms.

## Atores e componentes envolvidos

* Bridge.
* OpenCode Runner.
* OpenCode Server.
* Workspace Manager.

## Casos de uso

* Criar sessão read-only.
* Criar sessão workspace-write.
* Acompanhar eventos.
* Cancelar sessão.
* Tratar timeout.

## Fluxos principais

* Despacho: bridge valida saúde → cria sessão → envia prompt → acompanha eventos → recebe resultado.
* Cancelamento: bridge envia cancel → confirma com runner → atualiza estado.

## Fluxos de erro

* OpenCode indisponível → execução `Failed` com `runner_unavailable`.
* Sessão falha ao criar → execução `Failed` com `session_create_failed`.
* Cancelamento sem ACK → `runner_unresponsive`.

## Capacidades suportadas no MVP

* `plan`
* `implement`
* `review`
* `document`

## Capacidades fora do MVP

* Execução paralela multi-agent.
* Sessões persistentes de longa duração entre execuções.

## Contratos

* [`../contracts/runner-adapter.md`](../contracts/runner-adapter.md) — interface comum.
* [`../contracts/events.md`](../contracts/events.md) — eventos.

## Modelo de dados afetado

* `RunEvent` recebe eventos do runner.
* `RunnerHealth` registra saúde observada.

## Segurança

* Token interno armazenado em arquivo montado.
* Sem Docker socket.
* Limites de recursos.
* Logs passam por redaction.

## Observabilidade

* Métricas `agent_runner_health`.
* Logs estruturados por evento.
* Spans por fase (`runner.dispatch`, `runner.execute`).

## Estratégia de testes

* Contrato: validar mapping entre eventos OpenCode e `RunEvent`.
* Integração: subir OpenCode real em container e validar fluxo.
* Segurança: ausência de Docker socket, root, etc.
* Falhas: simular indisponibilidade.

## Critérios de aceite

* **OCR-AC-001** Dado um OpenCode Server saudável, quando o adapter criar sessão, recebe `sessionId`.
* **OCR-AC-002** Dado um prompt enviado, o adapter acompanha eventos até `done` ou `error`.
* **OCR-AC-003** Dado um cancelamento solicitado, o runner confirma cancelamento em menos de 30 s.
* **OCR-AC-004** Dado um timeout expirado, o runner é sinalizado e a execução transita para `TimedOut`.
* **OCR-AC-005** Dado um OpenCode indisponível, a execução vai para `Failed` com `runner_unavailable`.
* **OCR-AC-006** O runner não executa como root.
* **OCR-AC-007** O runner não monta Docker socket.

## Dependências

* Spec 001 (Platform Foundation).
* Spec 003 (Run Lifecycle).
* Spec 006 (Policy Engine).
* ADR-0009.

## Riscos

* Mudança breaking na API do OpenCode pode exigir revisão do adapter.
* Instabilidade do OpenCode Server durante o MVP pode atrasar validação.

## Decisões relacionadas

* [ADR-0009](../adr/0009-opencode-first-runner.md)
* [ADR-0003](../adr/0003-agent-adapter-boundary.md)

## Questões em aberto

* Autenticação interna exata.
* Política de retries em falhas transitórias.
* Mapeamento exato de eventos OpenCode para `RunEvent`.

## Fora de escopo

* Suporte a múltiplas sessões simultâneas no mesmo runner.
* Sessões persistentes entre execuções.

## Estratégia de entrega incremental

1. Adapter esqueleto.
2. Health check.
3. Criação de sessão.
4. Envio de prompt.
5. Consumo de eventos.
6. Cancelamento.
7. Timeout.
8. Mapeamento de eventos para `RunEvent`.
9. Testes de contrato.
