# ADR-0014 — Repositórios identificados por slug

## Status

Accepted

## Contexto

Repositórios podem ser identificados por caminho físico arbitrário, URL ou slug. As alternativas:

* Aceitar caminho físico arbitrário.
* Aceitar URL.
* Aceitar apenas slug registrado em allowlist.

Caminho físico arbitrário introduz risco de path traversal e acoplamento ao filesystem do host. URL depende de parsing e tratamento de credenciais. Slug é determinístico, validável e seguro.

## Decisão

Repositórios são identificados exclusivamente por slug. O bridge mantém uma allowlist (`Repository.slug`) com mapeamento para URL canônica e configurações. Nenhuma entrada aceita caminho físico arbitrário.

## Alternativas consideradas

* **Caminho físico arbitrário:** rejeitada — vetor de path traversal.
* **URL direta:** rejeitada — parsing complexo, gestão de credenciais.
* **Slug em allowlist:** aceita — seguro e determinístico.

## Consequências positivas

* Eliminação de path traversal como vetor.
* Configuração previsível por repositório.
* Permit/blocklist centralizada.

## Consequências negativas

* Necessidade de manter a allowlist atualizada.
* Operador precisa conhecer o slug antes de invocar.

## Riscos

* Slug duplicado ou inválido pode causar ambiguidade.
* Mudança de slug pode quebrar integrações.

## Impacto operacional

* Necessidade de processo de cadastro/remoção de repositórios.
* Necessidade de validação de unicidade do slug.

## Impacto de segurança

* Bloqueio estrutural de path traversal.
* Repositórios desconhecidos não podem ser acessados.

## Critérios para revisitar

* Caso o modelo de slug mostre limitações operacionais graves.
* Caso surja requisito de multi-instância ou multi-tenant que exija IDs adicionais.

## Referências relacionadas

* [`../specs/002-repository-registry/spec.md`](../specs/002-repository-registry/spec.md)
* [`../security/threat-model.md`](../security/threat-model.md)
* [`../examples/repositories.example.yaml`](../examples/repositories.example.yaml)
* [`../data/conceptual-model.md`](../data/conceptual-model.md)
