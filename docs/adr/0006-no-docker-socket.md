# ADR-0006 — Sem Docker socket

## Status

Accepted

## Contexto

Containers que controlam outros containers normalmente requerem acesso ao Docker socket do host. Isso dá ao container privilégios equivalentes ao host e viola o princípio de menor privilégio. As alternativas:

* Montar `/var/run/docker.sock` no bridge.
* Permitir que runners controlem containers.
* Não permitir que nenhum container do OCAB monte o socket Docker; gerenciar ciclo de vida de runners externamente.

A montagem do socket é vetor conhecido de escape de privilégio. Para o OCAB, gerenciar runners externamente (iniciar/parar pelo Compose) é suficiente.

## Decisão

Nenhum container da solução OCAB monta `/var/run/docker.sock`. O ciclo de vida dos runners é gerenciado externamente (Docker Compose). O bridge orquestra execução via adapter, mas não cria ou destrói containers.

## Alternativas consideradas

* **Montagem do Docker socket:** rejeitada — risco crítico de segurança.
* **Uso de Podman com socket compatível:** rejeitada — adiciona dependência sem ganho no MVP.
* **Execução bare-metal:** rejeitada — quebra o isolamento e a portabilidade.

## Consequências positivas

* Redução da superfície de ataque.
* Isolamento forte entre bridge e runners.
* Padronização do ciclo de vida via Compose.

## Consequências negativas

* Bridge não pode auto-ciclar runners.
* Necessidade de gestão manual de runners em algumas operações.

## Riscos

* Caso o runner trave, a intervenção precisa ser feita externamente.
* Operador pode ser tentado a montar o socket para debugar.

## Impacto operacional

* Necessidade de playbook para reinício de runners.
* Necessidade de monitorar saúde do runner externamente.

## Impacto de segurança

* Eliminação de vetor de escape por Docker socket.
* Compatibilidade com hardening de containers (ver [`../security/container-hardening.md`](../security/container-hardening.md)).

## Critérios para revisitar

* Caso futuro exija ciclo de vida gerenciado pelo bridge.
* Caso exista alternativa comprovadamente segura (e.g., API rootless) que justifique revisão.

## Referências relacionadas

* [`../security/container-hardening.md`](../security/container-hardening.md)
* [`../architecture/container-architecture.md`](../architecture/container-architecture.md)
* [`../security/threat-model.md`](../security/threat-model.md)
