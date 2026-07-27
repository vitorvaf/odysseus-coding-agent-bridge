# ADR-0009 — OpenCode como primeiro runner

## Status

Accepted

## Contexto

O MVP precisa validar o fluxo vertical completo (cliente MCP → bridge → runner → relatório). Existem três candidatos a primeiro runner: OpenCode, Codex, Antigravity. As alternativas:

* Começar por OpenCode.
* Começar por Codex.
* Começar por Antigravity.
* Suportar todos desde o início.

Começar por todos gera risco de uma base instável e atrapalha a iteração. OpenCode oferece uma API HTTP documentada e empacotável em container, com capacidade de prompt, planejamento e execução. Codex e Antigravity entram depois.

## Decisão

O primeiro runner do MVP é o OpenCode. A integração é detalhada em [`../specs/005-opencode-runner/spec.md`](../specs/005-opencode-runner/spec.md). Codex e Antigravity entram nas Fases 8 e 9 do roadmap.

## Alternativas consideradas

* **Codex primeiro:** rejeitada — exige decisões de subprocesso e subprocess management menos maduras no MVP.
* **Antigravity primeiro:** rejeitada — interface ainda em discovery (ADR-0011).
* **Suportar todos desde o início:** rejeitada — amplia escopo e risco.

## Consequências positivas

* Foco vertical claro.
* Validação precoce do fluxo completo.
* Aproveita API HTTP do OpenCode Server.

## Consequências negativas

* OpenCode pode evoluir e exigir manutenção do adapter.
* A escolha pode influenciar indevidamente o design do contrato comum.

## Riscos

* Mudança breaking no OpenCode pode exigir revisão do adapter.
* Bloqueio se a documentação do OpenCode estiver incompleta.

## Impacto operacional

* Necessidade de monitorar a versão do OpenCode Server.
* Necessidade de plano de contingência caso o OpenCode se torne indisponível.

## Impacto de segurança

* Mesmas garantias do ADR-0007 aplicadas ao OpenCode.
* Validação de comandos e recursos conforme [`../security/command-policy.md`](../security/command-policy.md).

## Critérios para revisitar

* Caso o OpenCode se mostre inviável para o uso esperado.
* Caso surja evidência de que outro agente deve ser priorizado.

## Referências relacionadas

* [`../specs/005-opencode-runner/spec.md`](../specs/005-opencode-runner/spec.md)
* [`../contracts/runner-adapter.md`](../contracts/runner-adapter.md)
* [ADR-0003](0003-agent-adapter-boundary.md)
* [ADR-0010](0010-codex-second-runner.md)
* [ADR-0011](0011-antigravity-post-mvp.md)
