# ADR-0011 — Antigravity após o MVP

## Status

Accepted

## Contexto

Antigravity é uma interface de automação cuja API ainda não foi totalmente validada. Integrá-lo prematuramente pode levar a retrabalho.

## Decisão

Antigravity entra na Fase 9 do roadmap, apenas após discovery da interface disponível. A documentação atual registra objetivos, dependências e pontos a validar em [`../specs/013-antigravity-runner/spec.md`](../specs/013-antigravity-runner/spec.md), sem assumir uma API específica.

## Alternativas consideradas

* **Antigravity no MVP:** rejeitada — risco alto, evidência insuficiente.
* **Antigravity como único runner:** rejeitada — viola estratégia de diversidade.
* **Pular Antigravity:** rejeitada — abre possibilidade futura se viável.

## Consequências positivas

* Redução de risco por integração prematura.
* Possibilidade de validar premissas antes de implementar.

## Consequências negativas

* Atraso na disponibilização para usuários.
* Necessidade de discovery dedicado.

## Riscos

* Mudança no plano do Antigravity pode alterar a integração planejada.
* Falta de clareza sobre sandbox e autenticação.

## Impacto operacional

* Necessidade de um esforço de discovery na Fase 9.
* Necessidade de plano de validação antes de integrar.

## Impacto de segurança

* Aplica ADR-0007 e ADR-0005 quando a integração for iniciada.
* Necessidade de revisar caso a caso.

## Critérios para revisitar

* Caso a interface do Antigravity se torne pública e estável.
* Caso o roadmap indique antecipação por requisito de produto.

## Referências relacionadas

* [`../specs/013-antigravity-runner/spec.md`](../specs/013-antigravity-runner/spec.md)
* [ADR-0003](0003-agent-adapter-boundary.md)
* [ADR-0009](0009-opencode-first-runner.md)
* [ADR-0010](0010-codex-second-runner.md)
