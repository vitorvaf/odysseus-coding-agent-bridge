# ADR-0002 — Coding Agent Bridge como servidor MCP

## Status

Accepted

## Contexto

O bridge precisa de um protocolo de comunicação com o Odysseus. As opções incluem:

* REST/JSON próprio.
* gRPC.
* MCP Streamable HTTP.

MCP é o protocolo usado pelo Odysseus e por outros agentes compatíveis. Adotar REST próprio aumenta o atrito de integração. gRPC adiciona ferramentas sem ganho claro de produtividade. MCP Streamable HTTP aproveita o ecossistema existente e o suporte nativo do Odysseus.

## Decisão

O Coding Agent Bridge expõe suas ferramentas exclusivamente via MCP Streamable HTTP. A versão e o transporte são especificados em [`../contracts/mcp-tools.md`](../contracts/mcp-tools.md).

## Alternativas consideradas

* **REST/JSON próprio:** rejeitada — exige cliente e servidor customizados, fora do ecossistema.
* **gRPC:** rejeitada — exige ferramentas extras e não é o padrão do Odysseus.
* **MCP stdio:** rejeitada — incompatível com isolamento de containers.

## Consequências positivas

* Reuso imediato do cliente MCP do Odysseus.
* Contrato padronizado e versionável.
* Suporte nativo a streams de eventos.
* Compatibilidade com futuros clientes MCP.

## Consequências negativas

* Dependência da estabilidade do protocolo MCP.
* Necessidade de versionar contratos explicitamente.

## Riscos

* Mudanças incompatíveis no MCP podem quebrar o bridge.
* Limites do protocolo podem exigir adaptações.

## Impacto operacional

* Necessidade de monitorar latência e taxa de chamadas.
* Necessidade de documentar versão mínima do MCP suportada.

## Impacto de segurança

* Autenticação e autorização devem ser aplicadas na camada MCP.
* Redaction obrigatória de payloads sensíveis.

## Critérios para revisitar

* Caso o MCP mostre limitações críticas não resolvidas.
* Caso surja um padrão superior amplamente adotado pelo ecossistema.
* Caso o transporte precise mudar por questões operacionais.

## Referências relacionadas

* [`../architecture/system-context.md`](../architecture/system-context.md)
* [`../architecture/system-design.md`](../architecture/system-design.md)
* [`../contracts/mcp-tools.md`](../contracts/mcp-tools.md)
* [`../specs/004-mcp-contract/spec.md`](../specs/004-mcp-contract/spec.md)
* [ADR-0001](0001-odysseus-as-interaction-plane.md)
