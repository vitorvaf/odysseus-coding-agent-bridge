# ADR-0004 — PostgreSQL como persistência e fila inicial

## Status

Accepted

## Contexto

O MVP precisa de persistência para estado de execução, eventos, fila e auditoria. As alternativas consideradas:

* PostgreSQL como fonte única, fila implementada com `SELECT ... FOR UPDATE SKIP LOCKED` ou tabela dedicada.
* PostgreSQL + Redis como fila.
* PostgreSQL + RabbitMQ.
* PostgreSQL + Kafka.

Introduzir um broker no MVP aumenta a complexidade operacional, exige nova peça de monitoramento e adiciona dependência sem evidência de demanda imediata. O PostgreSQL sozinho, com locks e tabela de fila, atende os requisitos de serialização por repositório e auditoria com baixo custo operacional.

## Decisão

O MVP usa PostgreSQL como única fonte de verdade. A fila de execução é implementada com tabela dedicada e uso de `SELECT ... FOR UPDATE SKIP LOCKED` para consumo concorrente. Eventos, auditoria e estado da execução também residem em PostgreSQL.

## Alternativas consideradas

* **Redis como fila:** rejeitada — adiciona dependência operacional; filas de execução têm baixo throughput no MVP.
* **RabbitMQ:** rejeitada — overhead de operação e infraestrutura desproporcional ao MVP.
* **Kafka:** rejeitada — complexidade excessiva para o cenário.

## Consequências positivas

* Redução do número de peças em operação.
* Consistência transacional entre estado, eventos e fila.
* Backups unificados.
* Operação bem conhecida em ambientes Linux/Docker.

## Consequências negativas

* Latência maior que um broker dedicado em cenários de altíssimo throughput.
* Necessidade de cuidado com locks para evitar contenção.

## Riscos

* Crescimento inesperado da fila pode exigir revisão.
* Long-running transactions podem segurar locks.

## Impacto operacional

* Necessidade de monitorar profundidade da fila, locks e duração de transações.
* Necessidade de rotina de vacuum e manutenção do PostgreSQL.

## Impacto de segurança

* Conexão ao banco apenas pela rede privada Docker.
* Credenciais em arquivo montado, nunca no código.
* Auditoria centralizada em um único componente.

## Critérios para revisitar

* Caso a fila atinja limites que comprometam latência.
* Caso surja requisito de multi-instância do bridge com alta concorrência.
* Caso requisitos de evento streaming se tornem reais.

## Referências relacionadas

* [`../architecture/data-architecture.md`](../architecture/data-architecture.md)
* [`../data/conceptual-model.md`](../data/conceptual-model.md)
* [`../data/relational-model.md`](../data/relational-model.md)
* [`../specs/003-run-lifecycle/spec.md`](../specs/003-run-lifecycle/spec.md)
* [`../operations/backup-and-restore.md`](../operations/backup-and-restore.md)
