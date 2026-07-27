# Spec 006 — Policy Engine

## Status

Proposed

## Resumo

Define o motor de políticas determinístico que avalia permissões e regras antes, durante e depois de cada execução, auditando cada decisão.

## Contexto

Políticas críticas não podem depender apenas de prompts. O motor precisa ser determinístico, auditável e independente do modelo.

## Problema

Como garantir que comandos sensíveis, permissões de repositório e limites sejam aplicados de forma consistente?

## Objetivos

* Implementar decisões determinísticas.
* Manter allowlist de comandos.
* Aplicar modos `ReadOnly` e `WorkspaceWrite`.
* Bloquear comandos sensíveis.
* Auditar cada decisão.
* Limitar recursos por execução.

## Não objetivos

* Treinar ou ajustar prompts.
* Implementar aprendizado de máquina.

## Escopo funcional

* Avaliação de política em três fases:
  * `pre-execution` (antes do workspace).
  * `execution` (antes de cada comando).
  * `post-execution` (sobre resultados).
* Decisões registradas em `PolicyDecision`.
* Allowlist e blocklist de comandos.
* Limites de tempo, memória e PIDs.

## Requisitos funcionais

* **PE-FR-001** Toda decisão deve ser registrada com `actor`, `reason`, `timestamp`, `policyVersion`.
* **PE-FR-002** Decisões `pre-execution` avaliam: slug válido, agente permitido, modo de acesso compatível com `writable`, políticas específicas do repositório.
* **PE-FR-003** Decisões `execution` avaliam cada comando ou operação conforme [`../security/command-policy.md`](../security/command-policy.md).
* **PE-FR-004** Decisões `post-execution` avaliam validações, diff e findings.
* **PE-FR-005** Política deve recusar comandos fora do workspace.
* **PE-FR-006** Política deve recusar symlink escape.
* **PE-FR-007** Política deve recusar path traversal.
* **PE-FR-008** Política deve recusar `git push` e similares.
* **PE-FR-009** Política deve impor limites de CPU, memória e PIDs ao runner.
* **PE-FR-010** Política deve permitir `sudo` apenas em lista explícita (vazia no MVP).
* **PE-FR-011** Política deve validar tempo máximo de execução.
* **PE-FR-012** Política deve recusar download de binários fora de fontes permitidas.

## Requisitos não funcionais

* **PE-NFR-001** Avaliação de política deve ocorrer em menos de 10 ms.
* **PE-NFR-002** Decisões devem ser imutáveis após gravação.
* **PE-NFR-003** Política deve ser versionada por arquivo de configuração.

## Atores e componentes envolvidos

* Bridge.
* Workspace Manager.
* Runner.
* Auditor.

## Casos de uso

* Validar slug em `run_create`.
* Avaliar comando antes de executar.
* Aplicar limite de recursos ao runner.
* Recusar push em modo read-only.
* Auditar decisão.

## Fluxos principais

* `pre-execution`: bridge consulta `Repository`, `Agent`, modo de acesso, executa `Policy.evaluate` → resultado `allow` ou `deny`.
* `execution`: runner pede autorização antes de comando → bridge consulta policy → permite ou bloqueia.
* `post-execution`: bridge consulta findings → política pode exigir revisão.

## Fluxos de erro

* `deny` em qualquer fase registra `PolicyDecision` e falha a execução conforme estado atual.

## Decisões suportadas

* `allow`
* `deny` com motivo
* `require_review`
* `require_approval`

## Concorrência

* Avaliação é local e não bloqueia banco.
* Apenas gravação em `PolicyDecision` usa lock implícito por transação.

## Contratos

* [`../contracts/events.md`](../contracts/events.md) — evento `policy.decision`.
* [`../security/command-policy.md`](../security/command-policy.md) — comandos.

## Modelo de dados afetado

* `PolicyDecision` — id, runId, phase, actor, policyVersion, decision, reason, timestamp, metadata.

## Segurança

* Política aplicada fora do modelo (ADR-0007).
* Decisões imutáveis.
* Recusa de comandos sensíveis reforçada por múltiplas camadas (filesystem, ausência de credenciais, etc.).

## Observabilidade

* Métrica `agent_policy_denials_total` por motivo.
* Logs estruturados com decisão e motivo.

## Estratégia de testes

* Unitários: tabela de decisão, paths inválidos, comandos bloqueados.
* Integração: execução simulada com políticas ativas.
* Segurança: tentativas de bypass devem falhar.

## Critérios de aceite

* **PE-AC-001** Dado um comando fora da allowlist, quando o runner pedir autorização, então a execução é bloqueada com `deny` registrado.
* **PE-AC-002** Dado um path traversal, a política recusa e registra decisão.
* **PE-AC-003** Dado um `git push`, a política recusa mesmo em modo `WorkspaceWrite`.
* **PE-AC-004** Dada uma execução com `timeoutSeconds=60`, ao exceder, a execução transita para `TimedOut`.
* **PE-AC-005** Toda decisão é registrada em `PolicyDecision` com `actor`, `reason`, `timestamp`.

## Dependências

* Spec 001 (Platform Foundation).
* Spec 002 (Repository Registry).
* Spec 003 (Run Lifecycle).
* ADR-0007.

## Riscos

* Listas desatualizadas podem bloquear operações legítimas.
* Falsos negativos em decisão `allow`.

## Decisões relacionadas

* [ADR-0007](../adr/0007-policy-enforcement-outside-model.md)

## Questões em aberto

* Fonte da verdade das listas (arquivo versionado, banco).
* Versionamento de políticas.
* Política por perfil de execução (`profile`).

## Fora de escopo

* Aprendizado dinâmico de políticas.
* UI de administração.

## Estratégia de entrega incremental

1. Tabela `PolicyDecision`.
2. Avaliação `pre-execution`.
3. Avaliação `execution` para comandos.
4. Avaliação `post-execution` básica.
5. Limites de recursos.
6. Política por repositório.
7. Auditoria.
