# ADR-0005 — Workspaces isolados

## Status

Accepted

## Contexto

Agentes de desenvolvimento frequentemente alteram o repositório diretamente, sem isolamento. As alternativas:

* Permitir edição direta no repositório original.
* Criar workspaces isolados por execução, com branch dedicada.

Editar diretamente viola o princípio de origem preservada, torna auditoria mais difícil e aumenta risco de danos. Workspaces isolados por execução com branch dedicada permitem revisão humana antes de aplicar alterações.

## Decisão

Toda execução de escrita ocorre em um workspace Git isolado por execução, com branch dedicada baseada na referência configurada. O repositório original é montado em modo read-only. Após validações e relatório, a aplicação ao repositório original é uma decisão humana manual (ADR-0008).

## Alternativas consideradas

* **Edição direta no repositório:** rejeitada — risco de danos, baixa auditabilidade.
* **Patch aplicado automaticamente:** rejeitada — viola ADR-0008.
* **Worktree por execução:** aceita como mecanismo; ADR não fixa método, apenas invariantes.

## Consequências positivas

* Repositório original preservado.
* Auditoria clara de quais alterações foram feitas em qual workspace.
* Possibilidade de revisão humana.
* Paralelismo entre execuções desde que limitadas por repositório.

## Consequências negativas

* Necessidade de gestão de workspaces (criação, limpeza, retenção).
* Necessidade de lidar com casos de erro durante criação ou limpeza.
* Overhead de disco.

## Riscos

* Disco cheio pode impedir criação de workspace.
* Limpeza incompleta pode gerar workspaces órfãos.

## Impacto operacional

* Necessidade de rotina de limpeza com política de retenção.
* Necessidade de monitorar espaço em disco.

## Impacto de segurança

* Caminho do workspace é validado pelo `Workspace Manager`.
* Permissões de filesystem impedem leitura fora do workspace.
* Repositório original em modo read-only.

## Critérios para revisitar

* Caso o overhead de workspace se torne inviável para o uso real.
* Caso o método de isolamento precise mudar por requisitos do runner.
* Caso o tempo de preparação de workspace comprometa a experiência.

## Referências relacionadas

* [`../specs/007-workspace-isolation/spec.md`](../specs/007-workspace-isolation/spec.md)
* [`../architecture/security-architecture.md`](../architecture/security-architecture.md)
* [`../security/command-policy.md`](../security/command-policy.md)
* [ADR-0008](0008-human-approval-required.md)
