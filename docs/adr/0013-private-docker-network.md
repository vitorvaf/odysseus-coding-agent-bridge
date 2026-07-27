# ADR-0013 — Rede Docker privada

## Status

Accepted

## Contexto

Comunicação entre serviços pode ocorrer em rede compartilhada com o host ou em rede Docker privada. As alternativas:

* Publicar todas as portas no host.
* Usar rede Docker privada e publicar apenas o estritamente necessário.

Publicar portas amplia superfície de ataque e expõe serviços internos sem necessidade. A rede privada usa DNS interno e reduz acoplamento ao host.

## Decisão

A comunicação entre os componentes do OCAB ocorre em rede Docker privada `ocab-net`. Nenhuma porta é publicada no host, exceto quando explicitamente autorizado por ADR específica. Resolução de nomes via DNS interno (e.g., `http://ocab-bridge:9010/mcp`).

## Alternativas consideradas

* **Todas as portas publicadas no host:** rejeitada — exposição desnecessária.
* **Host networking:** rejeitada — quebra isolamento.
* **Rede privada com DNS:** aceita — equilíbrio entre isolamento e operação.

## Consequências positivas

* Superfície de ataque reduzida.
* Configuração previsível via DNS.
* Independência em relação ao host.

## Consequências negativas

* Necessidade de planejamento de portas publicadas.
* Debug local exige conhecer o DNS interno.

## Riscos

* DNS interno do Docker pode falhar em cenários específicos.
* Mudança na topologia exige revisão de configuração.

## Impacto operacional

* Necessidade de documentar portas publicadas.
* Necessidade de monitorar conectividade entre containers.

## Impacto de segurança

* Reduz superfície de exposição.
* Compatível com hardening de containers.

## Critérios para revisitar

* Caso requisitos de acesso externo mudem.
* Caso a topologia precise evoluir (ex.: múltiplas redes).

## Referências relacionadas

* [`../architecture/container-architecture.md`](../architecture/container-architecture.md)
* [`../architecture/deployment-architecture.md`](../architecture/deployment-architecture.md)
* [`../security/container-hardening.md`](../security/container-hardening.md)
