# Testing Strategy

> **Status:** Proposed

Define a estratégia de testes do OCAB, cobrindo testes unitários, de contrato, integração, segurança e aceitação.

## Objetivos

* Garantir correção da máquina de estados.
* Garantir isolamento e segurança.
* Validar contratos.
* Assegurar resiliência operacional.

## Princípios

* Testes determinísticos.
* Cobertura proporcional ao risco.
* Pirâmide de testes respeitada.
* Testes rápidos no MVP.

## Categorias

* **Unitários:** funções puras e regras.
* **Contrato:** schemas e APIs.
* **Integração:** componentes com dependências reais (limitado).
* **Segurança:** cenários adversariais.
* **Aceitação:** fluxos ponta a ponta.

## Cobertura mínima

| Camada | Cobertura alvo |
| --- | --- |
| Máquina de estados | 100% das transições. |
| Policy engine | 100% das regras. |
| Validação de paths | 100% dos casos. |
| Redaction | 100% dos filtros. |
| Idempotência | 100% dos fluxos. |
| Adapter OpenCode | 80%. |
| Bridge em geral | 70%. |

## Frameworks

* xUnit para testes em .NET.
* FluentAssertions para asserções legíveis.
* Testcontainers para integração com Postgres.
* WireMock / TestServer para MCP.
* OWASP ZAP para segurança (futuro).

## Fixtures

* Repositório Git de teste.
* Workspace temporário.
* OpenCode Server de teste.

## Estratégia por área

### Máquina de estados

* Tabela de transições.
* Transições inválidas.
* Idempotência.
* Cancelamento.
* Timeout.
* Reconciliação.

### Policy engine

* Comandos permitidos.
* Comandos bloqueados.
* Path traversal.
* Symlink escape.
* Limites de recursos.

### Workspace

* Criação e limpeza.
* Permissões.
* Validação de paths.
* Branch de execução.

### Validation pipeline

* Comando OK.
* Comando falho.
* Timeout.
* Bloqueio por política.

### Artifact management

* Checksum.
* Sanitização.
* Limite de tamanho.
* Retenção.

### MCP

* Schemas válidos.
* Idempotência.
* Rate limit.
* Erros padronizados.

### OpenCode adapter

* Mapeamento de eventos.
* Cancelamento.
* Timeout.

### Operações

* Backup.
* Restore.
* Limpeza.
* Health checks.

## Referências relacionadas

* [`test-pyramid.md`](test-pyramid.md)
* [`contract-tests.md`](contract-tests.md)
* [`integration-tests.md`](integration-tests.md)
* [`security-tests.md`](security-tests.md)
* [`acceptance-tests.md`](acceptance-tests.md)
