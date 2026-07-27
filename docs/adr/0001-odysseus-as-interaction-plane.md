# ADR-0001 — Odysseus como Interaction Plane

## Status

Accepted

## Contexto

O OCAB precisa de uma camada de interação com o usuário. Existem alternativas:

* Construir uma UI própria.
* Adotar o Odysseus como plano de interação conversacional.
* Integrar diretamente com um IDE.

Construir uma UI própria aumenta o escopo e duplica a experiência conversacional que o Odysseus já oferece. Integrar diretamente com um IDE vincula o OCAB a um fornecedor e reduz reuso. Adotar o Odysseus aproveita a interface conversacional existente e o mantém responsável por memória, pesquisa, planejamento e composição de contexto.

## Decisão

O Odysseus é o plano de interação oficial do OCAB. O Coding Agent Bridge não implementa UI. Toda comunicação com o usuário ocorre via Odysseus, usando MCP Streamable HTTP (ver ADR-0002).

## Alternativas consideradas

* **UI própria:** rejeitada — escopo elevado, duplicação de esforço.
* **Integração direta com IDE:** rejeitada — acoplamento a fornecedor, cobertura limitada.
* **Múltiplos canais:** rejeitada para o MVP — adiciona complexidade sem ganho claro no início.

## Consequências positivas

* Redução do escopo do bridge.
* Reuso da camada conversacional existente.
* Concentração da responsabilidade de interação no Odysseus.
* Padronização do contrato via MCP.

## Consequências negativas

* Acoplamento ao roadmap do Odysseus.
* Mudanças no contrato MCP exigem coordenação.

## Riscos

* Mudanças bruscas no Odysseus podem forçar revisão do contrato.
* Dependência operacional do Odysseus em incidentes.

## Impacto operacional

* Necessidade de monitorar versões compatíveis do Odysseus.
* Necessidade de documentar versão de contrato esperada.

## Impacto de segurança

* Toda a segurança do OCAB se sustenta sem depender da UI.
* O Odysseus é parte da fronteira de confiança (ver [`../security/trust-boundaries.md`](../security/trust-boundaries.md)).

## Critérios para revisitar

* Caso o Odysseus se torne inviável como interação primária.
* Caso a necessidade de múltiplos canais de interação se torne real e mensurável.
* Caso o contrato MCP se torne instável ou insuficiente.

## Referências relacionadas

* [`../architecture/system-context.md`](../architecture/system-context.md)
* [`../architecture/system-design.md`](../architecture/system-design.md)
* [`../project/vision.md`](../project/vision.md)
* [ADR-0002](0002-bridge-as-mcp-server.md)
