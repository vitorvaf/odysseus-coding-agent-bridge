# ADR-0008 — Aprovação humana obrigatória

## Status

Accepted

## Contexto

Ações irreversíveis (push, merge, criação de pull request, deploy) mudam o estado do mundo. As alternativas:

* Permitir que o bridge execute essas ações automaticamente após validação.
* Exigir aprovação humana explícita antes de cada ação irreversível.

A primeira opção aumenta a velocidade, mas transfere risco para o agente e elimina a barreira de revisão. A segunda preserva a decisão humana sobre mudanças reais.

## Decisão

Push, merge, criação de pull request e deploy são exclusivamente manuais. O bridge prepara a alteração (em workspace isolado), valida, registra e devolve um relatório. A aplicação da alteração é decisão humana, registrada em `Approval`.

## Alternativas consideradas

* **Automação total:** rejeitada — risco operacional e de governança.
* **Automação condicional com timeout:** rejeitada — complexidade sem ganho claro no MVP.
* **Aprovação parcial (ex.: push automatizado, merge manual):** rejeitada — inconsistente e confusa.

## Consequências positivas

* Barreira explícita contra mudanças irreversíveis.
* Auditabilidade clara.
* Compatibilidade com governança e compliance.

## Consequências negativas

* Atrito adicional no fluxo.
* Necessidade de UX clara para aprovação.

## Riscos

* Aprovações humanas podem se tornar gargalo se a frequência for alta.
* Aprovações mecânicas (sem revisão real) podem virar rotina.

## Impacto operacional

* Necessidade de fluxo claro para aprovação.
* Necessidade de registrar aprovações em `Approval`.

## Impacto de segurança

* Reduz superfície de ataque por push automatizado para upstream.
* Mantém cadeia de custódia humana.

## Critérios para revisitar

* Caso processos formais de governança exijam modos diferentes.
* Caso surja requisito explícito de automação supervisionada.

## Referências relacionadas

* [`../specs/003-run-lifecycle/spec.md`](../specs/003-run-lifecycle/spec.md)
* [`../security/threat-model.md`](../security/threat-model.md)
* [`../security/command-policy.md`](../security/command-policy.md)
* [`../data/conceptual-model.md`](../data/conceptual-model.md)
* [ADR-0005](0005-isolated-workspaces.md)
