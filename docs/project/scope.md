# Escopo do projeto

> **Status:** Proposed

## Resumo

Define o escopo do MVP do OCAB, os componentes cobertos, as restrições obrigatórias e o que está fora de escopo.

## Escopo do MVP

* Servidor MCP Streamable HTTP exposto pelo Coding Agent Bridge.
* Persistência e fila inicial em PostgreSQL.
* Registro de repositórios por slug com allowlist.
* Workspaces Git isolados por execução.
* OpenCode Runner como executor único do MVP.
* Execuções read-only e workspace-write controladas.
* Máquina de estados completa da execução.
* Cancelamento, timeout e retry idempotente.
* Pipeline de validação declarativo por repositório.
* Artefatos no filesystem com referência no banco.
* Logs estruturados, métricas e traces mínimos.
* Relatório final padronizado.
* Aprovação humana obrigatória antes de aplicar alterações fora do workspace.

## Fora de escopo do MVP

* Codex Runner (entra no Epic 4).
* Antigravity Runner (entra no Epic 5 após discovery).
* Push, merge, deploy e criação automática de pull request.
* Execução de qualquer container com Docker socket.
* Autenticação avançada multiusuário.
* Dashboards de observabilidade completos.
* Multi-tenant.
* Replicação geográfica do PostgreSQL.
* Execução paralela de múltiplos runners sobre o mesmo repositório de escrita simultânea.

## Restrições obrigatórias

Todas as restrições abaixo são premissas bloqueadas. Qualquer mudança exige nova ADR que referencie a anterior.

1. **Linux como host.** Ambiente suportado: distribuições Linux compatíveis com Docker Engine e Docker Compose.
2. **Containers separados.** Cada componente principal roda em container próprio.
3. **Rede Docker privada.** Comunicação entre serviços via DNS interno. Portas publicadas no host apenas quando estritamente necessário.
4. **Sem WSL ou Docker Desktop.** Nenhuma dependência dessas tecnologias.
5. **Sem Docker socket.** Nenhum container monta `/var/run/docker.sock`.
6. **Sem root em runners.** Todos os runners executam como usuário não privilégiado.
7. **Repositórios por slug.** Caminhos físicos arbitrários não são aceitos como entrada (ADR-0014).
8. **Sem push automático.** `git push`, criação de PR e merge nunca são automáticos (ADR-0008).
9. **Sem deploy automático.** Implantações em qualquer ambiente são manuais.
10. **Shell controlado.** Comandos fora do catálogo são bloqueados; comandos fora do workspace são rejeitados.
11. **Sem segredos em prompts.** Credenciais nunca são incorporadas ao prompt enviado ao agente.
12. **Sem segredos em logs.** Redaction obrigatório em logs e relatórios.
13. **Repositório original read-only.** Alterações sempre em workspace isolado (ADR-0005).
14. **Concorrência limitada.** No máximo uma execução de escrita por repositório simultaneamente.
15. **Cancelamento e timeout obrigatórios.** Toda execução deve poder ser cancelada e deve respeitar timeout configurado.
16. **Histórico persistente.** Estado da execução e eventos sobrevivem a restart do bridge.
17. **Auditoria de políticas.** Toda decisão de política é registrada com ator, motivo e timestamp.
18. **Relatório padronizado.** Toda execução concluída gera relatório no contrato de `run_report`.

## Componentes cobertos

| Componente | MVP | Pós-MVP |
| --- | --- | --- |
| Odysseus | Integração via MCP | — |
| Coding Agent Bridge | Sim | — |
| OpenCode Runner | Sim | — |
| Codex Runner | Não | Epic 4 |
| Antigravity Runner | Não | Epic 5 |
| PostgreSQL | Sim | — |
| Artifact Storage (filesystem) | Sim | — |
| ChromaDB | Não | Opcional |
| SearXNG | Não | Opcional |
| ntfy | Não | Opcional |
| llama-server | Não | Opcional |

## Limites de interface

* O bridge fala com o Odysseus exclusivamente via MCP Streamable HTTP.
* O bridge fala com runners exclusivamente via adapter interno, conforme [`docs/contracts/runner-adapter.md`](../contracts/runner-adapter.md).
* O bridge fala com o repositório original exclusivamente em modo read-only.
* O runner fala com a rede Docker interna apenas via DNS privado.

## Critérios de aceite do MVP (resumo)

Para a lista detalhada, ver [`docs/planning/backlog.md`](../planning/backlog.md#critérios-de-aceite-do-mvp) e a seção 22 do prompt.

1. Cliente MCP consegue listar repositórios e agentes.
2. Execução read-only pode ser criada e acompanhada.
3. Execução pode ser cancelada e sofre timeout.
4. Estado da execução sobrevive a restart.
5. Repositório original não é alterado.
6. Execução workspace-write ocorre em workspace isolado.
7. Diff pode ser consultado.
8. Validações declarativas são executadas.
9. Logs e artefatos são correlacionados ao `runId`.
10. Path traversal, symlink escape e caminhos arbitrários são rejeitados.
11. Repositório read-only rejeita escrita.
12. Agente não autorizado é rejeitado.
13. `git push` nunca é executável no runner.
14. Runner não executa como root.
15. Nenhum serviço possui Docker socket.
16. Segredos nunca aparecem em prompts ou logs.
17. Execuções concorrentes de escrita não compartilham workspace.
18. Falha de validação é distinta de falha de infraestrutura.
19. Relatório final segue contrato padronizado.

## Decisões que afetam o escopo

* ADR-0001 (Odysseus como Interaction Plane) — fixa a separação entre plano de interação e plano de controle.
* ADR-0002 (Bridge como MCP server) — fixa o contrato externo do bridge.
* ADR-0004 (PostgreSQL como fila) — evita引入 broker externo no MVP.
* ADR-0006 (sem Docker socket) — limita o bridge a gerenciar runners já iniciados, sem ciclo de vida de containers.
* ADR-0008 (aprovação humana) — limita o que pode ser automatizado.
* ADR-0009 (OpenCode como primeiro runner) — fixa o foco vertical do MVP.
* ADR-0014 (slug de repositório) — fixa a forma de identificar repositórios.

## Riscos que afetam o escopo

* Mudança de API MCP ou do OpenCode Server pode forçar revisão do contrato.
* Ausência de autenticação robusta no MCP pode exigir mudança de escopo antes do MVP público.
* Descoberta tardia de limitações de isolamento do runner pode exigir revisão de workspace.

## Questões em aberto relacionadas

Ver [`docs/open-questions.md`](../open-questions.md).
