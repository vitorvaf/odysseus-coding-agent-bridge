# Visão do projeto

> **Status:** Proposed

## Motivação

Agentes de desenvolvimento autônomos são cada vez mais capazes, mas seu uso em produção é prejudicado por:

* Falta de isolamento entre a operação do agente e o ambiente do desenvolvedor.
* Ausência de um plano de controle que aplique políticas fora do prompt.
* Dificuldade em auditar o que foi feito, em qual workspace e com quais decisões.
* Inexistência de relatórios estruturados para revisão humana.
* Falta de barreiras explícitas entre agentes e operações sensíveis (push, merge, deploy).

O OCAB surge para preencher esse espaço sem substituir o agente: ele adiciona uma camada de controle, observabilidade e governança entre o usuário (via Odysseus) e os agentes de código.

## Princípios orientadores

1. **Controle antes de conveniência.** Toda decisão sensível é mediada pelo bridge.
2. **Política fora do prompt.** Permitir ou bloquear não depende do modelo.
3. **Origem preservada.** O repositório original é preferencialmente read-only.
4. **Execução isolada.** Toda alteração ocorre em workspace dedicado com branch próprio.
5. **Auditoria completa.** Cada decisão relevante é registrada com ator, motivo e timestamp.
6. **Falhas explícitas.** Estados de erro são distinguíveis e não se confundem com sucesso.
7. **Aprovação humana.** Push, merge, deploy e pull request são sempre manuais.
8. **Reversibilidade.** Workspaces e artefatos ficam disponíveis para diagnóstico até a retenção configurada.

## Objetivos estratégicos

* Permitir que um usuário, via Odysseus, consiga criar execuções read-only e workspace-write de forma confiável.
* Fornecer um relatório padronizado por execução, com diff, validações e findings.
* Sustentar múltiplos agentes (OpenCode, Codex, Antigravity) sob contratos comuns.
* Operar de forma local, sem dependências de Windows, WSL ou Docker Desktop.
* Manter a barreira entre o plano de controle (bridge) e o plano de execução (runners).

## Não objetivos

* Substituir o Odysseus como plano de interação.
* Hospedar agentes de LLM.
* Servir como plataforma multi-tenant.
* Realizar push, merge ou deploy automáticos.
* Fornecer UI própria.

## Público

* Engenheiros que operam pipelines locais assistidos por IA.
* Equipes de segurança e conformidade que precisam de auditoria.
* Mantenedores do Odysseus e dos agentes compatíveis.

## Métricas de sucesso

* Taxa de execuções concluídas sem intervenção manual inesperada.
* Tempo médio entre `run_create` e `run_report`.
* Cobertura de validações automáticas em execuções workspace-write.
* Ausência de alterações no repositório original fora do workspace.
* Ausência de segredos em logs e artefatos expostos.
* Conformidade com a política de comandos em 100% das execuções.

## Restrições globais

* Ambiente: Linux + Docker + Docker Compose.
* Sem `host.docker.internal`, WSL ou dependências do Windows.
* Sem Docker socket em nenhum container.
* Usuário não root em todos os runners.
* Repositórios identificados por slug (ADR-0014).
* Sem push, merge ou deploy automáticos (ADR-0008).
* Sem segredos reais em arquivos versionados (placeholders `__SET_ME__`).

## Próximo passo recomendado

* Concluir o ciclo de revisão da documentação atual.
* Validar a Fase 0 do roadmap (Discovery) antes de iniciar implementação.
* Aguardar autorização explícita para iniciar o slice vertical mínimo (read-only com OpenCode).
