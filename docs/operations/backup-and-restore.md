# Backup and Restore

> **Status:** Proposed

Define procedimentos de backup e restore.

## Escopo

* PostgreSQL.
* Artifact storage (`ocab-artifacts`).
* Configuração versionada.
* Segredos (cuidado especial).

## PostgreSQL

### Backup

* Ferramenta: `pg_dump`.
* Formato: custom (`-Fc`).
* Destino: arquivo em volume externo.
* Agendamento: diário às 02:00.

```bash
docker exec ocab-postgres pg_dump -U ocab -d ocab -Fc > backup_$(date +%Y%m%d).dump
```

### Restore

```bash
docker exec -i ocab-postgres pg_restore -U ocab -d ocab --clean --if-exists < backup.dump
```

### Retenção

* Local: 14 dias.
* Remoto: 30 dias (quando aplicável).

## Artifact storage

### Backup

* `tar` incremental do diretório.
* Destino externo.

```bash
tar czf artifacts_$(date +%Y%m%d).tar.gz /var/lib/ocab/artifacts
```

### Restore

```bash
tar xzf artifacts_YYYYMMDD.tar.gz -C /
```

### Retenção

* Local: 14 dias.
* Remoto: 30 dias.

## Configuração

* Backup do diretório `deploy/` em git (já versionado).
* Backup de `secrets/` em cofre externo.

## Validação

* Restore testado em staging mensalmente.
* Checksum de backups verificado.

## RPO/RTO

* RPO: até 24 horas (backup diário).
* RTO: até 4 horas (restore em staging).

## Disaster recovery

* Procedimento para falha total do host.
* Reconstrução via backups remotos.
* Reaplicação de migrations via scripts versionados.

## Referências relacionadas

* [`deployment.md`](deployment.md)
* [`configuration.md`](configuration.md)
* [`../security/secrets-management.md`](../security/secrets-management.md)
* [`../specs/014-operations/spec.md`](../specs/014-operations/spec.md)
