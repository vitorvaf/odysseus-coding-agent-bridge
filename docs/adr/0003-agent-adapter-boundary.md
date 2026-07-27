# ADR-0003 — Adapters independentes por agente

## Status

Accepted

## Contexto

Cada agente de desenvolvimento (OpenCode, Codex, Antigravity) tem sua própria interface, protocolo e modelo de execução. Existem duas alternativas arquiteturais:

* Bridge monolítico com lógica específica por agente espalhada.
* Adapters independentes por agente, atrás de uma interface comum.

A primeira abordagem leva a acoplamento, complexidade crescente e dificuldade de teste. A segunda permite isolar mudanças por agente e evoluir cada runner em seu próprio ritmo.

## Decisão

Cada agente possui seu próprio adapter, implementado como um módulo separado dentro do bridge, atrás de uma interface comum `IRunnerAdapter`. O contrato entre bridge e runner é definido em [`../contracts/runner-adapter.md`](../contracts/runner-adapter.md).

## Alternativas consideradas

* **Bridge monolítico:** rejeitada — alto acoplamento, baixa testabilidade.
* **Serviço externo por agente:** rejeitada para o MVP — aumenta complexidade operacional sem ganho claro.
* **Plugar por injeção de script:** rejeitada — sem ganho de segurança ou testabilidade.

## Consequências positivas

* Isolamento de mudanças entre agentes.
* Testabilidade individual por adapter.
* Possibilidade de implementar runners adicionais sem alterar o core do bridge.
* Fronteiras claras de responsabilidade.

## Consequências negativas

* Mais arquivos e mais testes a manter.
* Necessidade de harmonizar contratos entre adapters.

## Riscos

* Divergência de comportamento entre adapters se o contrato comum for ambíguo.
* Sobrecarga de manutenção se houver muitos adapters em paralelo.

## Impacto operacional

* Cada adapter tem seu próprio teste de contrato.
* Cada adapter pode ter configuração e segredos próprios.

## Impacto de segurança

* O contrato comum impõe que decisões sensíveis (cancelamento, timeout, validação) permaneçam no bridge.
* Adapter não executa ações fora do permitido pela política.

## Critérios para revisitar

* Caso o número de adapters se torne inviável de manter.
* Caso surja um padrão único de fato no ecossistema.

## Referências relacionadas

* [`../architecture/component-architecture.md`](../architecture/component-architecture.md)
* [`../contracts/runner-adapter.md`](../contracts/runner-adapter.md)
* [`../specs/005-opencode-runner/spec.md`](../specs/005-opencode-runner/spec.md)
* [`../specs/012-codex-runner/spec.md`](../specs/012-codex-runner/spec.md)
* [`../specs/013-antigravity-runner/spec.md`](../specs/013-antigravity-runner/spec.md)
