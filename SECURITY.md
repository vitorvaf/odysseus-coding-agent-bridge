# Security

> **Status:** Proposed

Política de segurança do OCAB.

## Reportando vulnerabilidades

* Reportar via canal interno seguro.
* Não publicar publicamente até coordenação.
* Incluir passos de reprodução e impacto.

## Princípios

* Política fora do modelo (ADR-0007).
* Repositórios read-only na origem (ADR-0005).
* Sem Docker socket (ADR-0006).
* Aprovação humana (ADR-0008).
* Repositórios por slug (ADR-0014).

## Threat model

Ver [`docs/security/threat-model.md`](docs/security/threat-model.md).

## Resposta a incidentes

Ver [`docs/security/incident-response.md`](docs/security/incident-response.md).

## Gestão de segredos

Ver [`docs/security/secrets-management.md`](docs/security/secrets-management.md).

## Política de comandos

Ver [`docs/security/command-policy.md`](docs/security/command-policy.md).

## Endurecimento de containers

Ver [`docs/security/container-hardening.md`](docs/security/container-hardening.md).

## Fronteiras de confiança

Ver [`docs/security/trust-boundaries.md`](docs/security/trust-boundaries.md).

## Atualizações

* Dependências atualizadas periodicamente.
* SBOM e scanning de vulnerabilidades.
* Avisos de segurança publicados após correção.
