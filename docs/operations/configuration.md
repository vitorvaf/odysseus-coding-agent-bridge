# Configuration — Configuração por ambiente

> **Status:** Proposed

Define como o OCAB é configurado em diferentes ambientes.

## Princípios

* Configuração externalizada.
* Placeholders `__SET_ME__` em arquivos versionados.
* Sem segredos em repositório.
* Validação no startup.

## Ambientes

| Ambiente | Uso |
| --- | --- |
| `local-dev` | Desenvolvimento |
| `ci` | Testes automatizados |
| `staging` | Validação |
| `prod` | Operação |

## Fontes

* `appsettings.json` (base).
* `appsettings.{Environment}.json` (override).
* Variáveis de ambiente (override final).
* Arquivos montados (segredos).

## Estrutura (exemplo)

```json
{
  "ocab": {
    "service": "bridge",
    "version": "1.0.0-MVP",
    "environment": "prod",
    "mcp": {
      "endpoint": "/mcp",
      "port": 9010
    },
    "postgres": {
      "host": "ocab-postgres",
      "port": 5432,
      "database": "ocab",
      "username": "ocab",
      "passwordFile": "/run/secrets/ocab/postgres-password"
    },
    "artifacts": {
      "root": "/app/artifacts",
      "maxFileBytes": 52428800,
      "maxRunBytes": 1073741824
    },
    "retention": {
      "defaultDays": 30,
      "backupDays": 14,
      "healthDays": 7
    },
    "run": {
      "defaultTimeoutSeconds": 1800,
      "maxTimeoutSeconds": 86400,
      "minTimeoutSeconds": 60
    },
    "policy": {
      "version": "v1",
      "configFile": "/app/config/policy.yaml"
    },
    "runner": {
      "opencode": {
        "baseUrl": "http://ocab-opencode-runner:4096",
        "tokenFile": "/run/secrets/ocab/opencode-token"
      }
    }
  }
}
```

## Validação no startup

* Bridge valida schema.
* Falha de validação é erro fatal.
* Log de erro é estruturado.

## Variáveis de ambiente (override)

```text
OCAB__POSTGRES__PASSWORD=__SET_ME__
OCAB__RUNNER__OPENCODE__TOKEN=__SET_ME__
```

## Segredos

* Montados via Docker secret ou volume dedicado.
* Permissões `0600`.
* Nunca em env vars em produção.

## Templates versionados

* `appsettings.json` versionado.
* `appsettings.{Environment}.json` versionado.
* `.env.example` versionado.
* `.env` **não** versionado.

## Referências relacionadas

* [`deployment.md`](deployment.md)
* [`../security/secrets-management.md`](../security/secrets-management.md)
