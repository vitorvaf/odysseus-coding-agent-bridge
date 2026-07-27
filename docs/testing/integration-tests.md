# Integration Tests

> **Status:** Proposed

Define testes de integração com dependências reais (limitadas).

## PostgreSQL

* Testcontainers.
* Schema aplicado via migrations.
* Cada teste usa schema isolado ou transações revertidas.

## Filesystem

* Diretório temporário por teste.
* Permissões POSIX testadas.

## Repositório Git

* Repositório de teste com fixture.
* Commits controlados.

## OpenCode Server

* Container dedicado em CI.
* Sessões descartáveis.
* Eventos capturados.

## Cancelamento

* Execuções longas canceladas.
* Verificação de estado terminal.

## Timeout

* Execuções com timeout curto.
* Verificação de `TimedOut`.

## Validação

* Comandos com sucesso.
* Comandos com falha.
* Comandos bloqueados.

## Concorrência

* Múltiplas execuções no mesmo repositório.
* Lock aplicado.

## Backup/Restore

* Backup de banco real.
* Restore em banco limpo.

## Cleanup

* Limpeza remove artefatos esperados.
* Lock por `runId`.

## Ferramentas

* Testcontainers.
* Docker CLI em CI.
* Bash scripts para setup.

## Referências relacionadas

* [`strategy.md`](strategy.md)
* [`../specs/014-operations/spec.md`](../specs/014-operations/spec.md)
