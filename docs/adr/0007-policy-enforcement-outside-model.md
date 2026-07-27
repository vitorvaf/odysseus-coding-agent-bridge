# ADR-0007 — Políticas críticas fora do modelo

## Status

Accepted

## Contexto

Decisões sensíveis (acesso a repositório, comandos executados, permissões de escrita, exfiltração) não devem depender apenas de prompts enviados ao modelo. As alternativas:

* Confiar em instruções de prompt.
* Implementar um motor de políticas determinístico fora do modelo.
* Combinação de ambos.

A literatura e a experiência mostram que prompts podem ser contornados por prompt injection. Decisões críticas precisam ser enforced por código determinístico, registradas e auditáveis.

## Decisão

O OCAB implementa um `Policy Engine` determinístico, fora do modelo. Toda decisão crítica passa por esse motor antes de ser aplicada. Decisões são registradas em `PolicyDecision` para auditoria. O modelo é responsável apenas por sugestões; a execução é governada pelo engine.

## Alternativas consideradas

* **Políticas via prompt:** rejeitada — insegura.
* **Apenas sandbox do runner:** parcial — não cobre permissões de repositório nem comandos fora do runner.
* **Apenas revisão humana:** insuficiente — não escala.

## Consequências positivas

* Segurança efetiva contra prompt injection.
* Decisões auditáveis e reproduzíveis.
* Independência entre evolução do modelo e políticas.

## Consequências negativas

* Necessidade de manter listas e regras atualizadas.
* Complexidade adicional no bridge.

## Riscos

* Listas desatualizadas podem bloquear operações legítimas.
* Falsos negativos podem passar comandos perigosos.

## Impacto operacional

* Necessidade de processo de revisão de políticas.
* Necessidade de testes do motor.

## Impacto de segurança

* Reduz superfície de ataque por prompt injection.
* Permite auditoria detalhada.

## Critérios para revisitar

* Caso surjam padrões robustos de sandboxing que dispensem parte das regras.
* Caso o motor se torne gargalo operacional.
* Caso auditoria revele padrões não cobertos pelas regras atuais.

## Referências relacionadas

* [`../specs/006-policy-engine/spec.md`](../specs/006-policy-engine/spec.md)
* [`../security/command-policy.md`](../security/command-policy.md)
* [`../security/threat-model.md`](../security/threat-model.md)
* [`../data/conceptual-model.md`](../data/conceptual-model.md)
