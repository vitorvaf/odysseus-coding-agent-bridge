# Estratégia de Migrations

> **Status:** Proposed

Define como o schema do PostgreSQL é versionado e aplicado.

## Princípios

* Migrations versionadas e auditáveis.
* Aplicação explícita, não automática no startup da aplicação.
* Reversíveis quando possível.
* Sem migração silenciosa em produção.

## Ferramenta

* Entity Framework Core Migrations é a ferramenta padrão.
* Pasta: `db/migrations/`.
* Convenção: `YYYYMMDDHHMMSS_<Nome>.cs`.

## Aplicação

* Job específico executa migrations.
* Comando: `dotnet ef database update`.
* Aplicação em staging antes de produção.

## Reversibilidade

* Cada migration inclui `Up` e `Down`.
* Em produção, `Down` é manual e exige aprovação.

## Compatibilidade

* Mudanças compatíveis (nova coluna, novo índice) são aplicadas online.
* Mudanças incompatíveis exigem janela de manutenção.
* Tabela `schema_migrations` mantém histórico.

## Versionamento

* Cada migration é identificada por `MigrationId` único.
* Aplicação registrada com `appliedAt`.

## Auditoria

* Logs estruturados de cada migration.
* Eventos em `RunEvent` quando relevantes.

## Estratégia de rollout

```mermaid
flowchart LR
    Dev[Desenvolvimento] --> Stg[Staging]
    Stg --> Test[Testes de carga]
    Test --> Prod[Produção]
```

## Política de mudanças

| Mudança | Janela |
| --- | --- |
| Adição de coluna opcional | Online |
| Remoção de coluna | Janela de manutenção |
| Mudança de tipo | Janela de manutenção |
| Renomeação | Migração multi-etapa |

## Backup pré-migration

* Backup lógico antes de qualquer migration em produção.
* Validação de restore em staging.

## Testes

* Testes de migration em CI.
* Verificação de idempotência.

## Referências relacionadas

* [`conceptual-model.md`](conceptual-model.md)
* [`relational-model.md`](relational-model.md)
* [`../operations/backup-and-restore.md`](../operations/backup-and-restore.md)
