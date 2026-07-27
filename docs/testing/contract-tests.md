# Contract Tests

> **Status:** Proposed

Define testes de contrato para validar a conformidade com schemas e APIs.

## MCP

* Validação de schema JSON em cada tool.
* Validação de respostas válidas.
* Idempotência verificada.
* Erros padronizados.

## Runner Adapter

* Adapter OpenCode implementado e testado contra OpenCode Server real.
* Adapter Codex (futuro) testado contra CLI.
* Adapter Antigravity (futuro) testado após discovery.

## Eventos

* Cada tipo de evento validado contra schema.
* Eventos sensíveis passam por redaction em testes.

## Artefatos

* Path válido.
* Path inválido rejeitado.
* Checksum consistente.

## Erros

* Cada código de erro testado.
* Formato padronizado respeitado.

## Estratégia

* Schemas versionados em `contracts/schemas/`.
* Snapshots gerados a partir de execuções reais.
* Diff de schema bloqueia PR incompatível.

## Ferramentas

* JsonSchema.NET.
* Pact (para MCP) — opcional.
* Snapshot testing com Verify.

## Referências relacionadas

* [`strategy.md`](strategy.md)
* [`../contracts/mcp-tools.md`](../contracts/mcp-tools.md)
* [`../contracts/events.md`](../contracts/events.md)
* [`../contracts/errors.md`](../contracts/errors.md)
* [`../contracts/artifacts.md`](../contracts/artifacts.md)
