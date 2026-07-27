# Deployment — Implantação

> **Status:** Proposed

Procedimento para implantar o OCAB em ambiente local.

## Pré-requisitos

* Linux (qualquer distribuição suportada pelo Docker Engine).
* Docker Engine 24+.
* Docker Compose v2.
* `curl` e `bash`.
* Sem dependência de WSL ou Docker Desktop.

## Variáveis de ambiente

Arquivo `.env` (não versionado):

```ini
OCAB_VERSION=1.0.0-MVP
POSTGRES_PASSWORD=__SET_ME__
BRIDGE_TOKEN=__SET_ME__
OPENCODE_TOKEN=__SET_ME__
OCAB_ARTIFACTS_DIR=/var/lib/ocab/artifacts
OCAB_PGDATA_DIR=/var/lib/ocab/pgdata
TZ=UTC
```

## Estrutura esperada

```text
ocab/
  deploy/
    compose.yaml
    bridge.env
  secrets/
    bridge.token
    opencode.token
  artifacts/   # montado do host
  pgdata/      # montado do host
```

## Subida

```bash
cd ocab
cp .env.example .env
# editar .env substituindo __SET_ME__
docker compose pull
docker compose up -d
docker compose ps
docker compose logs --tail=200 ocab-bridge
```

## Verificação

```bash
curl -fsS http://ocab-bridge:9010/health
curl -fsS http://ocab-bridge:9010/ready
curl -fsS http://ocab-bridge:9010/metrics | head
```

## Stop

```bash
docker compose stop
docker compose down
```

## Atualização

1. Backup do banco.
2. Backup de artefatos.
3. Atualizar `OCAB_VERSION`.
4. `docker compose pull`.
5. `docker compose up -d`.
6. Verificar `/ready` e `/health`.

## Rollback

1. Definir `OCAB_VERSION=<versão anterior>`.
2. `docker compose up -d`.

## Remoção total

```bash
docker compose down -v
```

> Atenção: volumes são removidos.

## Referências relacionadas

* [`configuration.md`](configuration.md)
* [`backup-and-restore.md`](backup-and-restore.md)
* [`health-checks.md`](health-checks.md)
* [`../architecture/deployment-architecture.md`](../architecture/deployment-architecture.md)
* [`../specs/014-operations/spec.md`](../specs/014-operations/spec.md)
