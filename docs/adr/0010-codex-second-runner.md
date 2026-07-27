# ADR-0010 — Codex como segundo runner

## Status

Accepted

## Contexto

Após o MVP, o OCAB deve suportar Codex como executor e/ou revisor independente. Codex CLI oferece capacidade de execução via subprocesso e revisão via leitura.

## Decisão

O Codex Runner é adicionado na Fase 8 do roadmap, após validação do fluxo vertical com OpenCode. Codex pode atuar como executor (workspace-write) ou como revisor (read-only) sobre execuções anteriores. A integração é detalhada em [`../specs/012-codex-runner/spec.md`](../specs/012-codex-runner/spec.md).

## Alternativas consideradas

* **Codex no MVP:** rejeitada — amplia escopo e atrasa a validação do vertical.
* **Codex apenas como revisor:** considerada — útil para revisão independente, mas execução também é desejada.
* **Pular Codex:** rejeitada — exclui um agente importante.

## Consequências positivas

* Maior diversidade de agentes no pós-MVP.
* Possibilidade de revisão independente.
* Maior robustez para casos de indisponibilidade de um runner.

## Consequências negativas

* Manutenção adicional do adapter.
* Necessidade de harmonizar contratos entre adapters.

## Riscos

* Mudanças na CLI do Codex podem exigir revisão.
* Particularidades de sandbox do Codex podem impor restrições ao workspace.

## Impacto operacional

* Necessidade de monitorar saúde do runner Codex.
* Necessidade de configuração de autenticação específica.

## Impacto de segurança

* Codex herda as garantias do ADR-0007.
* Aplicação dos limites de comando via policy engine.

## Critérios para revisitar

* Caso Codex CLI mude de forma incompatível.
* Caso a Fase 8 seja priorizada por requisito de produto.

## Referências relacionadas

* [`../specs/012-codex-runner/spec.md`](../specs/012-codex-runner/spec.md)
* [`../contracts/runner-adapter.md`](../contracts/runner-adapter.md)
* [ADR-0003](0003-agent-adapter-boundary.md)
* [ADR-0009](0009-opencode-first-runner.md)
