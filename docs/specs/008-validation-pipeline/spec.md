# Spec 008 — Validation Pipeline

## Status

Proposed

## Resumo

Define como o bridge executa validações declarativas após o término da execução do runner, captura resultados e os associa à execução.

## Contexto

Após o runner terminar, é necessário validar as alterações (quando houver) com comandos configurados por repositório. Validações podem falhar parcial ou totalmente.

## Problema

Como executar comandos de validação de forma controlada, capturar resultados estruturados e distinguir falha de validação de falha de infraestrutura?

## Objetivos

* Executar validações declarativas.
* Limitar tempo de execução por comando.
* Capturar stdout, stderr e arquivos de resultado.
* Distinguir falha de validação de falha de infraestrutura.
* Permitir comandos condicionais.

## Não objetivos

* Implementar validação visual ou interativa.
* Substituir CI completo.

## Escopo funcional

* Lista de comandos por repositório.
* Execução sequencial.
* Timeout por comando.
* Captura de saída.
* Arquivos de resultado declarados.
* Status final agregado.

## Requisitos funcionais

* **VP-FR-001** Validações devem ser configuradas por repositório.
* **VP-FR-002** Cada comando deve ter timeout próprio (default 300 s).
* **VP-FR-003** Cada comando deve ter diretório de trabalho (`cwd`) dentro do workspace.
* **VP-FR-004** Cada comando pode declarar arquivos de resultado (ex.: `test-results.xml`).
* **VP-FR-005** Cada comando pode declarar variáveis de ambiente restritas.
* **VP-FR-006** Status final agregado deve indicar sucesso, falha ou erro de infraestrutura.
* **VP-FR-007** Saída deve ser capturada e persistida como artefato.
* **VP-FR-008** Comando deve ser executado pelo runner, sob política de comandos.
* **VP-FR-009** Comandos fora do catálogo devem ser recusados.

## Requisitos não funcionais

* **VP-NFR-001** Execução sequencial.
* **VP-NFR-002** Logs de validação devem ser separados de logs do runner.
* **VP-NFR-003** Comandos devem respeitar redaction.

## Atores e componentes envolvidos

* Bridge.
* Validation Pipeline.
* Runner.
* Policy Engine.

## Casos de uso

* Validar execução workspace-write.
* Coletar resultados de teste.
* Coletar cobertura.
* Falha de comando.

## Fluxos principais

* Após `Running` concluído, bridge consulta `Repository.validations`.
* Para cada comando, runner executa com `cwd` no workspace.
* Saída é capturada e persistida.
* Status agregado é gerado.

## Fluxos de erro

* Comando bloqueado pela política → execução `Failed` com `validation_blocked`.
* Timeout de comando → `ValidationResult.timeout=true`.
* Comando retorna código não zero → `ValidationResult.failed=true`.

## Comandos permitidos

Ver [`../security/command-policy.md`](../security/command-policy.md).

## Modelo de dados afetado

* `ValidationResult` — id, runId, command, exitCode, stdoutRef, stderrRef, timeout, startedAt, finishedAt, durationMs, status, artifacts.

## Segurança

* Comando validado pela política.
* Ambiente limpo (sem credenciais).
* Redaction de saída.

## Observabilidade

* Spans por validação.
* Métrica `agent_validation_duration_seconds`.
* Logs estruturados por comando.

## Estratégia de testes

* Unitários: agregação de status, parsing de resultados.
* Integração: comando real em workspace de teste.
* Segurança: comando bloqueado falha conforme política.

## Critérios de aceite

* **VP-AC-001** Dado um repositório com `validations`, quando a execução termina, então os comandos são executados em sequência.
* **VP-AC-002** Dado um comando que excede o timeout, o resultado marca `timeout=true` e a execução não fica pendurada.
* **VP-AC-003** Dado um comando bloqueado pela política, a execução vai para `Failed` com `validation_blocked`.
* **VP-AC-004** Dado uma falha de validação, o estado final é `CompletedWithValidationErrors` e não `Failed`.
* **VP-AC-005** Stdout, stderr e arquivos de resultado são persistidos como artefatos.

## Dependências

* Spec 001 (Platform Foundation).
* Spec 005 (OpenCode Runner).
* Spec 006 (Policy Engine).
* Spec 007 (Workspace Isolation).

## Riscos

* Timeout inadequado pode comprometer用户体验.
* Comando mal configurado pode falhar silenciosamente.

## Decisões relacionadas

* [ADR-0007](../adr/0007-policy-enforcement-outside-model.md)

## Questões em aberto

* Variáveis de ambiente permitidas.
* Ordem dos comandos quando há dependência.

## Fora de escopo

* Execução paralela de validações.
* Cache de dependências.

## Estratégia de entrega incremental

1. Lista de comandos por repositório.
2. Execução sequencial.
3. Timeout.
4. Captura de saída.
5. Arquivos de resultado.
6. Status agregado.
