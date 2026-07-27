# ADR-0012 — Artefatos no filesystem

## Status

Accepted

## Contexto

Logs extensos, diffs, relatórios e resultados de validação podem ser volumosos. As alternativas:

* Persistir tudo em PostgreSQL.
* Persistir artefatos grandes no filesystem e manter apenas referência no banco.
* Persistir em object storage dedicado (S3 compatível).

Object storage adiciona dependência operacional. PostgreSQL não foi projetado para blobs grandes e sofre com backups e vacuum. O filesystem local é suficiente para o MVP e mantém a operação simples.

## Decisão

Artefatos grandes (logs, diffs, relatórios, arquivos de validação) são persistidos no filesystem, em volume dedicado `ocab-artifacts`. O banco mantém apenas referência (caminho relativo), checksum, tamanho, tipo e data. Detalhes em [`../data/conceptual-model.md`](../data/conceptual-model.md) e [`../specs/009-artifact-management/spec.md`](../specs/009-artifact-management/spec.md).

## Alternativas consideradas

* **Tudo em PostgreSQL:** rejeitada — degrada performance e backup.
* **Object storage:** rejeitada para o MVP — adiciona dependência.
* **Filesystem local:** aceita — simples e suficiente.

## Consequências positivas

* PostgreSQL permanece enxuto.
* Operações de leitura de artefatos são rápidas.
* Backup do banco é independente de artefatos.

## Consequências negativas

* Necessidade de rotina de retenção de artefatos.
* Necessidade de backup separado do filesystem.
* Acesso ao filesystem precisa ser controlado.

## Riscos

* Disco cheio pode comprometer operação.
* Permissões incorretas podem expor artefatos.

## Impacto operacional

* Necessidade de monitorar uso de disco.
* Necessidade de rotina de limpeza.

## Impacto de segurança

* Permissões POSIX no volume.
* Caminhos validados no acesso.
* Redaction aplicada antes da gravação.

## Critérios para revisitar

* Caso volume de artefatos ultrapasse o que o filesystem local comporta.
* Caso requisito de object storage surja (ex.: S3 compatível).

## Referências relacionadas

* [`../architecture/data-architecture.md`](../architecture/data-architecture.md)
* [`../specs/009-artifact-management/spec.md`](../specs/009-artifact-management/spec.md)
* [`../data/conceptual-model.md`](../data/conceptual-model.md)
* [`../operations/backup-and-restore.md`](../operations/backup-and-restore.md)
