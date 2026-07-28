# Spec 013 — Antigravity Runner

## Status

In Discovery

## Resumo

Documenta objetivos, dependências e pontos a validar para o Antigravity Runner. **Não implementar nesta fase.** Esta spec não presume uma API que ainda não foi confirmada.

## Contexto

Antigravity é uma interface de automação cuja API ainda está em discovery. Sua integração prematura pode levar a retrabalho.

## Problema

Como integrar o Antigravity respeitando os princípios do OCAB sem assumir uma interface ainda instável?

## Objetivos

* Validar a interface disponível.
* Documentar capabilities esperadas.
* Iniciar com uso read-only.
* Manter adapter alinhado aos princípios do OCAB.

## Não objetivos

* Implementar nesta fase.
* Presumir API que ainda não foi confirmada.
* Substituir OpenCode ou Codex.

## Escopo funcional (planejado)

* Container `ocab-antigravity-runner`.
* Adapter no bridge implementando `IRunnerAdapter`.
* Integração inicial read-only.
* Suporte futuro a revisão de projetos legados.

## Pontos a validar (discovery)

* Interface de automação disponível.
* Mecanismo de autenticação.
* Modelo de execução (subprocesso, HTTP, etc.).
* Compatibilidade com workspaces isolados.
* Sandbox aplicável.
* Latência e limites.

## Capacidades esperadas

* `analyze` (read-only).
* `review` (read-only).

Capacidades de escrita **não** serão habilitadas até validação explícita.

## Dependências

* Spec 001 (Platform Foundation).
* Spec 005 (OpenCode Runner).
* ADR-0011.

## Riscos

* API instável ou indisponível.
* Limitações de sandbox que conflitem com isolamento.
* Mudança de modelo de execução durante discovery.

## Critérios de aceite (futuros)

* Adapter Antigravity implementa `IRunnerAdapter`.
* Execução read-only funciona em ambiente validado.
* Descoberta documentada com decisões registradas.
* Revisão sobre projetos legados suportada quando aplicável.

## Questões em aberto

* Interface exata.
* Política de autenticação.
* Casos de uso suportados (legado vs. greenfield).
* Sandbox e isolamento.

## Fora de escopo

* Implementação antes do Epic 5 do roadmap.
* Escrita até validação explícita.

## Estratégia de entrega (futura)

1. Discovery da interface.
2. Documentação de decisões.
3. Adapter esqueleto.
4. Integração read-only.
5. Validação em projeto piloto.
6. Suporte a revisão.
7. Testes de contrato.

## Referências relacionadas

* [ADR-0011](../adr/0011-antigravity-post-mvp.md)
* [`005-opencode-runner/spec.md`](005-opencode-runner/spec.md)
* [`../contracts/runner-adapter.md`](../contracts/runner-adapter.md)
