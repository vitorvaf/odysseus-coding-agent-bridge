# Spec 002 — Repository Registry

## Status

Proposed

## Resumo

Define como repositórios são cadastrados, identificados, validados e protegidos dentro do OCAB.

## Contexto

O OCAB precisa acessar repositórios de forma controlada. Sem uma allowlist e validação adequada, fica exposto a:

* Acesso a repositórios não autorizados.
* Path traversal.
* Configuração inconsistente entre execuções.

## Problema

Como cadastrar, identificar e proteger repositórios de forma determinística e auditável?

## Objetivos

* Identificar repositórios exclusivamente por slug.
* Manter allowlist de repositórios.
* Validar entrada de slug e recusar caminhos arbitrários.
* Aplicar configurações por repositório (branch, permissões, validações, políticas).
* Rejeitar repositórios indisponíveis.

## Não objetivos

* Provisionar infraestrutura de Git.
* Substituir sistemas de controle de versão.

## Escopo funcional

* Cadastro e consulta de repositórios por slug.
* Mapeamento slug → URL canônica, branch padrão, modos de acesso permitidos, agentes permitidos, comandos de validação e políticas.
* Validação de slug (formato, unicidade).
* Rejeição de caminhos físicos arbitrários.
* Validação de repositório indisponível.

## Requisitos funcionais

* **RR-FR-001** O `Repository.slug` deve ser único, em kebab-case, com no máximo 64 caracteres.
* **RR-FR-002** O `Repository` deve mapear para uma URL canônica (https ou ssh) usada em modo read-only.
* **RR-FR-003** O `Repository` deve ter `defaultBranch` configurável.
* **RR-FR-004** O `Repository` deve ter flag `writable` que indica se aceita execuções `WorkspaceWrite`.
* **RR-FR-005** O `Repository` deve ter `allowedAgents` — lista de IDs de agentes autorizados.
* **RR-FR-006** O `Repository` deve ter `validations` — lista de comandos declarativos (ver spec 008).
* **RR-FR-007** O `Repository` deve ter `policies` — referência para o `Policy Engine`.
* **RR-FR-008** Slug com caracteres inválidos deve ser rejeitado.
* **RR-FR-009** Slug duplicado deve ser rejeitado.
* **RR-FR-010** Slug inexistente em `run_create` deve rejeitar a solicitação sem criar workspace.
* **RR-FR-011** Repositório indisponível (clone falha) deve marcar a execução como `Failed` com mensagem específica.
* **RR-FR-012** Path traversal no slug deve ser rejeitado.

## Requisitos não funcionais

* **RR-NFR-001** A consulta de repositório deve responder em menos de 50 ms (cache local).
* **RR-NFR-002** Mudanças no cadastro devem ser versionadas (auditáveis).
* **RR-NFR-003** A allowlist deve ser carregada no startup e recarregável sem reiniciar o bridge (em janelas de manutenção).

## Atores e componentes envolvidos

* Operador técnico.
* Mantenedor do Odysseus.
* Bridge.
* Workspace Manager.

## Casos de uso

* Operador cadastra novo repositório.
* Operador consulta repositório por slug.
* Bridge valida repositório em `run_create`.
* Bridge tenta acessar repositório indisponível.

## Fluxos principais

* Cadastro: operador submete configuração → bridge valida → persiste → publica evento.
* `run_create`: bridge consulta repositório por slug → valida `allowedAgents` → segue para `ValidatingRequest`.
* Indisponibilidade: clone falha → execução marcada como `Failed` com código específico.

## Fluxos de erro

* Slug inválido → rejeição com `400 invalid_slug`.
* Slug inexistente → rejeição com `404 repository_not_found`.
* Repositório indisponível → execução `Failed` com `repository_unavailable`.

## Estados e transições

Não aplicável diretamente. Estados da execução são cobertos pela spec 003.

## Contratos

* [`../contracts/mcp-tools.md`](../contracts/mcp-tools.md) — `repositories_list`.
* [`../examples/repositories.example.yaml`](../examples/repositories.example.yaml).

## Modelo de dados afetado

* `Repository` — slug, displayName, defaultBranch, writable, allowedAgents, validations, policies, createdAt, updatedAt, archivedAt.

## Segurança

* Slug validado por regex estrita.
* URL canônica validada (sem placeholders não resolvidos).
* Path traversal bloqueado estruturalmente (ADR-0014).
* Read-only na origem.

## Observabilidade

* Eventos de cadastro/atualização/remoção.
* Métricas de `repository_runs_total`.
* Logs estruturados de validação.

## Estratégia de testes

* Unitários: validação de slug, unicidade, formato.
* Integração: ciclo de cadastro, alteração e remoção.
* Segurança: tentar path traversal deve falhar.
* Contrato: `repositories_list` retorna shape esperado.

## Critérios de aceite

* **RR-AC-001** Dado um slug em formato válido, quando `repositories_list` for chamado, então o repositório aparece na lista.
* **RR-AC-002** Dado um slug com `..` ou `/`, quando `run_create` for chamado, então a solicitação é rejeitada com `400 invalid_slug`.
* **RR-AC-003** Dado um slug inexistente, quando `run_create` for chamado, então a solicitação é rejeitada sem criar workspace.
* **RR-AC-004** Dado um repositório cadastrado como read-only, quando `run_create` for chamado com `accessMode=WorkspaceWrite`, então a solicitação é rejeitada com `409 repository_not_writable`.
* **RR-AC-005** Dado um repositório indisponível, quando o bridge tentar clonar, então a execução vai para `Failed` com `repository_unavailable`.
* **RR-AC-006** Dado um agente não listado em `allowedAgents`, quando `run_create` for chamado, então a solicitação é rejeitada com `403 agent_not_allowed`.

## Dependências

* Spec 001 (Platform Foundation).
* Spec 006 (Policy Engine).
* ADR-0014.

## Riscos

* Configuração incorreta do cadastro pode bloquear execuções.
* URL canônica maliciosa pode exfiltrar dados (mitigada por read-only).

## Decisões relacionadas

* [ADR-0014](../adr/0014-repository-slug-registry.md)
* [ADR-0005](../adr/0005-isolated-workspaces.md)

## Questões em aberto

* Como versionar a configuração de repositórios.
* Mecanismo de reload sem restart.
* Fonte da verdade: arquivo YAML, banco, ou ambos.

## Fora de escopo

* Auto-discovery de repositórios.
* Hooks externos para atualização da allowlist.

## Estratégia de entrega incremental

1. Tabela `repositories`.
2. Seed inicial.
3. `repositories_list` MCP.
4. Validação em `run_create`.
5. Bloqueio por agente não permitido.
6. Tratamento de indisponibilidade.
