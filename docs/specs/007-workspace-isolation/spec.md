# Spec 007 — Workspace Isolation

## Status

Proposed

## Resumo

Define como workspaces são criados, isolados, limpos e protegidos contra escape.

## Contexto

Agentes não devem editar diretamente o repositório original. Workspaces isolados são obrigatórios para preservar a origem e permitir revisão.

## Problema

Como garantir isolamento forte entre execuções, integridade do repositório original e limpeza adequada após a execução?

## Objetivos

* Criar workspaces isolados por execução.
* Garantir origem read-only.
* Permitir revisão e auditoria.
* Limpar workspaces após retenção.
* Impedir escape (path traversal, symlink).

## Não objetivos

* Versionar snapshots do workspace.
* Suportar workspaces compartilhados entre execuções.

## Escopo funcional

* Criação de workspace via clone, worktree ou cópia.
* Branch de execução dedicada.
* Permissões POSIX rígidas.
* Validação de caminhos.
* Limpeza automática após retenção.
* Preservação para diagnóstico.

## Requisitos funcionais

* **WI-FR-001** O Workspace Manager deve criar um workspace por execução.
* **WI-FR-002** O repositório original deve ser acessado em modo read-only.
* **WI-FR-003** O workspace deve ter uma branch baseada em `baseReference`.
* **WI-FR-004** O workspace deve ser montado como volume dedicado à execução.
* **WI-FR-005** Caminhos fora do workspace devem ser rejeitados (path traversal).
* **WI-FR-006** Symlinks que apontem para fora do workspace devem ser rejeitados.
* **WI-FR-007** Workspace deve ter permissões POSIX que impeçam leitura por outros containers.
* **WI-FR-008** Cleanup deve ocorrer após `run_finished + retentionPeriod`.
* **WI-FR-009** Em caso de erro de preparação, workspace deve ser descartado imediatamente.
* **WI-FR-010** Workspace pode ser preservado em modo `diagnostic` por tempo adicional.

## Requisitos não funcionais

* **WI-NFR-001** Criação de workspace deve ocorrer em menos de 60 segundos para repositórios de até 1 GB.
* **WI-NFR-002** Caminhos validados em menos de 5 ms.
* **WI-NFR-003** Limpeza deve usar lock por `runId` para evitar corrida.

## Atores e componentes envolvidos

* Bridge.
* Workspace Manager.
* Runner.
* Operador técnico.

## Casos de uso

* Criar workspace para execução.
* Limpar workspace após execução.
* Diagnosticar execução preservada.
* Falha de preparação.

## Fluxos principais

* Criação: bridge prepara workspace → monta como volume → runner usa.
* Limpeza: rotina agendada identifica workspaces expirados → remove volume → registra evento.

## Fluxos de erro

* Falha de clone → execução `Failed` com `workspace_prepare_failed`.
* Symlink detectado → execução `Rejected`.
* Path traversal → execução `Rejected`.

## Estratégia de workspace

A escolha entre `clone`, `worktree` e `copy` depende de fatores como tamanho do repositório, presença de submodules e suporte do runner. A spec não fixa o método, apenas as invariantes:

* Workspace é exclusivo da execução.
* Origem read-only.
* Permissões restritivas.

## Integridade

* Hash do conteúdo é calculado ao final para o relatório.
* Snapshots parciais podem ser gerados para diagnóstico.

## Isolamento concorrente

* Workspaces diferentes para execuções diferentes.
* Execuções de escrita no mesmo repositório não compartilham workspace.

## Limpeza

* Padrão: remover volume e referências após retenção.
* Diagnóstico: preservar volume até liberação manual.

## Retenção

* Configurável por ambiente (default 30 dias).
* Configurável por execução.

## Proteção contra escape

* Validação de caminhos normalizados.
* Resolução de symlinks antes de cada operação.
* Bloqueio de `..` e caminhos absolutos.

## Preservação para diagnóstico

* Em caso de falha, workspace pode ser preservado.
* Operador pode inspecionar artefatos e logs.

## Contratos

* [`../contracts/artifacts.md`](../contracts/artifacts.md)
* [`../security/command-policy.md`](../security/command-policy.md)

## Modelo de dados afetado

* `Run.workspaceId` — referência ao volume criado.
* `Artifact` — referências aos artefatos dentro do volume.

## Segurança

* Read-only na origem.
* Permissões POSIX.
* Validação de caminhos.
* Sem Docker socket.

## Observabilidade

* Métricas `agent_workspace_size_bytes`.
* Eventos `workspace.prepare`, `workspace.cleanup`.

## Estratégia de testes

* Unitários: validação de paths, symlinks, traversal.
* Integração: criação e limpeza, contenção.
* Segurança: tentativas de escape devem falhar.
* Concorrência: workspaces isolados.

## Critérios de aceite

* **WI-AC-001** Dada uma execução, o workspace é criado em volume dedicado.
* **WI-AC-002** O repositório original é montado em modo read-only.
* **WI-AC-003** Path traversal é rejeitado com erro.
* **WI-AC-004** Symlink que aponta para fora do workspace é rejeitado.
* **WI-AC-005** Após retenção, o volume é removido e o evento registrado.
* **WI-AC-006** Em modo `diagnostic`, o volume é preservado além da retenção padrão.

## Dependências

* Spec 001 (Platform Foundation).
* Spec 003 (Run Lifecycle).
* Spec 006 (Policy Engine).
* ADR-0005.

## Riscos

* Disco cheio pode impedir criação.
* Limpeza incompleta pode gerar órfãos.

## Decisões relacionadas

* [ADR-0005](../adr/0005-isolated-workspaces.md)
* [ADR-0014](../adr/0014-repository-slug-registry.md)

## Questões em aberto

* Estratégia padrão de clone vs worktree — resolvida em [`../discovery/005-workspace-strategy-evaluation.md`](../discovery/005-workspace-strategy-evaluation.md): primária `git worktree add`; sem workspace para read-only.
* Política para submodules (OQ-012).
* Política para Git LFS (OQ-013).
* Política para repositórios com alterações locais (OQ-014).

## Fora de escopo

* Versionamento de snapshots.
* Compartilhamento entre execuções.

## Estratégia de entrega incremental

1. Criação básica de volume.
2. Montagem read-only.
3. Validação de caminhos.
4. Branch de execução.
5. Cleanup agendado.
6. Modo diagnóstico.
