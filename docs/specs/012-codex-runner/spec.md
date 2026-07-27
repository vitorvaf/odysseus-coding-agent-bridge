# Spec 012 — Codex Runner

## Status

Proposed (Pós-MVP)

## Resumo

Documenta a evolução prevista do Codex Runner. **Não implementar nesta fase.** Esta spec descreve objetivos, dependências, plano de discovery e integrações futuras.

## Contexto

Codex é o segundo agente previsto no roadmap. Sua CLI (`codex exec`) é uma alternativa ao OpenCode Server. Diferentemente do OpenCode, Codex é tipicamente invocado como subprocesso.

## Problema

Como integrar o Codex sem acoplá-lo ao OpenCode e respeitando as garantias comuns de isolamento, política e auditoria?

## Objetivos

* Fornecer adapter Codex.
* Permitir uso como executor (workspace-write) e como revisor (read-only).
* Aplicar política comum.
* Coletar eventos estruturados.

## Não objetivos

* Implementar nesta fase.
* Substituir o OpenCode.

## Escopo funcional (planejado)

* Container `ocab-codex-runner` baseado em imagem oficial do Codex.
* Adapter no bridge implementando `IRunnerAdapter`.
* Execução via `codex exec`.
* Captura de saída estruturada.
* Cancelamento via sinal.
* Sandbox do Codex aplicada quando compatível.

## Capacidades previstas

* `plan`
* `implement`
* `review`
* `document`

## Pontos a validar (discovery)

* Forma exata de captura de eventos.
* Política de autenticação.
* Compatibilidade com sandbox local.
* Compatibilidade com workspaces isolados.
* Limites de subprocessos.

## Dependências

* Spec 001 (Platform Foundation).
* Spec 005 (OpenCode Runner) — modelo de adapter.
* ADR-0010.

## Riscos

* Mudanças no CLI do Codex podem exigir revisão.
* Particularidades de subprocess management.

## Critérios de aceite (futuros)

* Adapter Codex implementa `IRunnerAdapter`.
* Execução read-only funciona.
* Execução workspace-write funciona.
* Revisão independente funciona.
* Cancelamento e timeout funcionam.

## Questões em aberto

* Autenticação do Codex.
* Política de retries.
* Sandbox mode preferida.

## Fora de escopo

* Implementação antes da Fase 8 do roadmap.

## Estratégia de entrega (futura)

1. Discovery da CLI.
2. Adapter esqueleto.
3. Execução read-only.
4. Execução workspace-write.
5. Cancelamento.
6. Revisão independente.
7. Testes de contrato.

## Referências relacionadas

* [ADR-0010](../adr/0010-codex-second-runner.md)
* [`005-opencode-runner/spec.md`](005-opencode-runner/spec.md)
* [`../contracts/runner-adapter.md`](../contracts/runner-adapter.md)
