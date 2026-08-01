# ADR-0016 — Licença Apache-2.0 para o repositório OCAB

## Status

Accepted

## Contexto

O arquivo `LICENSE` na raiz do repositório contém o texto integral da Apache License 2.0 e foi criado durante o Milestone 0 (Discovery). A documentação ao longo do projeto (`README.md`, ADRs anteriores, `project/scope.md`, `operations/configuration.md`) passou a assumir Apache-2.0 como a licença do OCAB sem que houvesse aprovação explícita do sponsor. A questão ficou registrada como `OQ-100 — Licença do repositório` em `../open-questions.md`, com status `Open` e prioridade P1.

A indefinição bloqueia:

* Aceitação de contribuições externas com cessão clara de direitos.
* Publicação do projeto em registries públicos.
* Compatibilidade declarada com dependências Apache-2.0 já em uso no stack (`Npgsql`, várias `Microsoft.Extensions.*`).
* Compatibilidade com `coverlet.collector`, `xunit`, `Testcontainers`, `gitleaks`, que em sua maioria são permissivos (MIT/Apache-2.0).

A aprovação foi registrada na sessão do stabilization gate em 2026-07-28, autorizando formalmente a licença Apache-2.0 que a documentação já assumia.

## Decisão

O repositório **OCAB é distribuído sob Apache License, versão 2.0** (`SPDX: Apache-2.0`). O arquivo `LICENSE` na raiz permanece como fonte canônica do texto da licença. Toda contribuição é cedida sob os mesmos termos, conforme Cláusula 5 da licença.

## Alternativas consideradas

* **MIT.** Mais permissiva e simples, mas sem cessão explícita de patentes (Cláusula 3 da Apache-2.0). Rejeitada por dar menos proteção ao ecossistema em caso de litígio de patentes, e por divergir do que o restante da documentação já havia assumido.
* **Adiar formalmente a decisão.** Mantém `OQ-100` aberta e atrasa o stabilization gate. Rejeitada porque o stabilization gate exige fechamento das pendências do Epic 1 antes de iniciar escrita em workspaces, e a indefinição de licença é uma dessas pendências.
* **Dual licensing (ex.: Apache-2.0 + comercial).** Adiciona complexidade operacional não justificada pelo estágio atual do projeto (MVP fechado, sem produto comercializável). Rejeitada.
* **GPLv3.** Forte copyleft e proteção contra tivoção, mas incompatível com algumas dependências e com a postura permissiva já presente no ecossistema de ferramentas MCP. Rejeitada.

## Consequências positivas

* Compatibilidade declarada com dependências que já são Apache-2.0 (`Npgsql`, `Microsoft.Extensions.*`).
* Cessão de patentes explícita (Cláusula 3) protege contribuidores e utilizadores.
* Clareza para contribuidores externos antes do projeto abrir para contribuição pública.
* Resolve `OQ-100` em direção a `Resolved`.
* Compatível com `MIT` em双向 (projetos MIT podem consumir código Apache-2.0 desde que preservem os avisos), o que simplifica reuso em ecossistemas adjacentes.

## Consequências negativas

* Compatibilidade com GPLv2 é explícita (Apache-2.0 é GPLv2-compatible); com GPLv3 há atrito (Apache-2.0 não é GPLv3-compatible) — não usar dependências GPLv3-only sem análise.
* A cláusula de patentes (Cláusula 3) pode exigir renúncia caso o patrocinador detenha patentes relevantes; avaliar caso a caso em contribuição substancial.
* Caso o sponsor queira licenciar sob termos diferentes no futuro, será necessário nova ADR substituta referenciando esta (cláusula de [`../../AGENTS.md`](../../AGENTS.md) sobre premissas bloqueadas).

## Riscos

* **Mudança de licença no futuro:** se a estratégia de distribuição mudar, será necessário relicenciar todo o histórico de commits com consentimento dos contribuidores. Mitigação: manter lista atualizada de contribuidores em `CONTRIBUTING.md`.
* **Incompatibilidade com dependência futura GPLv3-only:** cada nova dependência precisa checar licença. Mitigação: gate no CI com `gitleaks`/`license-checker` adicionado em PR futuro.

## Impacto operacional

* `LICENSE` permanece na raiz como texto canônico.
* `README.md` deve referenciar a licença no bloco inicial de cabeçalho (esta ADR registra a obrigação; a atualização editorial do cabeçalho faz parte do stabilization gate).
* `CONTRIBUTING.md` deve referenciar a licença Apache-2.0 no bloco de contribuição (verificar consistência — fora de escopo desta ADR).
* Cabeçalhos SPDX nos arquivos fonte individuais não são obrigatórios sob Apache-2.0, mas podem ser adicionados opcionalmente para clareza.

## Impacto de segurança

* Nenhum direto. A cessão de patentes (Cláusula 3) e o termo de "AS IS" (Cláusula 7) seguem o padrão Apache-2.0 amplamente aceito.

## Critérios para revisitar

* Caso o sponsor queira licenciar sob termos diferentes.
* Caso surja dependência GPLv3-only cuja substituição não seja viável.
* Caso a estratégia de distribuição mude (ex.: componente comercial embutido).

## Referências relacionadas

* [`../open-questions.md`](../open-questions.md) — `OQ-100`.
* [`../../LICENSE`](../../LICENSE) — texto canônico da licença.
* [Apache License, Version 2.0 — texto oficial](https://www.apache.org/licenses/LICENSE-2.0).
* [`../../CONTRIBUTING.md`](../../CONTRIBUTING.md) — política de contribuição (a verificar).
* [`0009-opencode-first-runner.md`](0009-opencode-first-runner.md) — ADR que já menciona dependências Apache-2.0.
* [`0014-repository-slug-registry.md`](0014-repository-slug-registry.md) — ADR que pressupõe licença permissiva.