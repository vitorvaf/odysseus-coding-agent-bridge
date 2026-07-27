# Spec 009 — Artifact Management

## Status

Proposed

## Resumo

Define tipos, estrutura, ciclo de vida e governança dos artefatos gerados pelo OCAB.

## Contexto

Execuções produzem logs, diffs, relatórios, eventos, resultados de validação e snapshots. Esses artefatos precisam ser persistidos de forma controlada e referenciados.

## Problema

Como persistir artefatos grandes sem sobrecarregar o banco, garantindo integridade, retenção e acesso seguro?

## Objetivos

* Padronizar tipos e estrutura.
* Garantir integridade via checksum.
* Limitar tamanho e aplicar redaction.
* Permitir download via referência.
* Aplicar retenção configurável.

## Não objetivos

* Implementar object storage dedicado.
* Fornecer UI de navegação de artefatos.

## Escopo funcional

* Tipos:
  * `events.jsonl`
  * `diff.patch`
  * `stdout.log`
  * `stderr.log`
  * `final-report.md`
  * `test-results.xml`
  * `coverage.xml`
  * `validation/*.log`
  * `workspace-snapshot.tar.gz` (opcional).
* Estrutura sob `runs/<runId>/`.
* Checksum SHA-256.
* Limite de tamanho configurável (default 50 MB por arquivo, 1 GB total).
* Sanitização de paths.
* Download autenticado via MCP.

## Requisitos funcionais

* **AM-FR-001** Todo artefato deve ter `path`, `checksum`, `size`, `type`, `createdAt`.
* **AM-FR-002** Checksum deve ser SHA-256 calculado no momento da gravação.
* **AM-FR-003** Artefato deve residir sob `ocab-artifacts/runs/<runId>/`.
* **AM-FR-004** Path do artefato deve ser validado para impedir traversal.
* **AM-FR-005** Tamanho máximo por arquivo deve ser configurável.
* **AM-FR-006** Tamanho total por execução deve ser configurável.
* **AM-FR-007** Excesso de limite deve gerar artefato truncado com flag `truncated=true` ou erro.
* **AM-FR-008** Acesso a artefatos deve ser via referência retornada por MCP.
* **AM-FR-009** Retenção padrão deve ser 30 dias, configurável.
* **AM-FR-010** Limpeza deve usar lock por `runId`.
* **AM-FR-011** Download deve passar por redaction.
* **AM-FR-012** Nome de arquivo deve ser sanitizado.

## Requisitos não funcionais

* **AM-NFR-001** Checksum calculado em streaming para arquivos grandes.
* **AM-NFR-002** Listagem de artefatos por `runId` deve responder em menos de 100 ms.
* **AM-NFR-003** Permissões POSIX devem impedir leitura por outros containers não autorizados.

## Atores e componentes envolvidos

* Bridge.
* Workspace Manager.
* Runner.
* Operador técnico.

## Casos de uso

* Persistir diff após execução workspace-write.
* Persistir logs de validação.
* Persistir relatório final.
* Consultar artefato por referência.
* Limpar artefatos expirados.

## Fluxos principais

* Gravação: bridge abre stream → escreve → calcula checksum → grava `Artifact`.
* Consulta: MCP retorna referência → operador baixa via endpoint autenticado.
* Limpeza: rotina agendada identifica expirados → remove arquivo e marca `Artifact.deletedAt`.

## Fluxos de erro

* Disco cheio → erro `disk_full` e execução em estado apropriado.
* Permissão negada → erro `permission_denied`.
* Path inválido → erro `invalid_artifact_path`.

## Modelo de dados afetado

* `Artifact` — id, runId, type, path, checksum, size, mimeType, createdAt, deletedAt, truncated, metadata.

## Segurança

* Path traversal bloqueado.
* Permissões POSIX.
* Redaction antes de gravação e antes de download.
* Tamanho máximo.

## Observabilidade

* Métrica `agent_artifact_size_bytes`.
* Eventos de criação, exclusão, truncamento.

## Estratégia de testes

* Unitários: cálculo de checksum, sanitização, validação de path.
* Integração: gravação e leitura em filesystem temporário.
* Segurança: tentativas de traversal devem falhar.

## Critérios de aceite

* **AM-AC-001** Dado uma execução, os artefatos são gravados sob `runs/<runId>/` com checksum válido.
* **AM-AC-002** Path inválido é rejeitado.
* **AM-AC-003** Arquivo maior que o limite é truncado com `truncated=true` ou erro, conforme config.
* **AM-AC-004** Artefatos com retenção expirada são removidos pela rotina de limpeza.
* **AM-AC-005** Download passa por redaction antes de retornar.
* **AM-AC-006** Nome de arquivo é sanitizado.

## Dependências

* Spec 001 (Platform Foundation).
* Spec 003 (Run Lifecycle).
* Spec 007 (Workspace Isolation).
* ADR-0012.

## Riscos

* Disco cheio.
* Crescimento descontrolado de artefatos.

## Decisões relacionadas

* [ADR-0012](../adr/0012-filesystem-artifact-storage.md)

## Questões em aberto

* Tamanho máximo padrão.
* Compressão automática.
* Política para `workspace-snapshot.tar.gz`.

## Fora de escopo

* UI de navegação.
* Object storage.

## Estratégia de entrega incremental

1. Estrutura de diretórios.
2. Gravação com checksum.
3. Listagem por runId.
4. Sanitização.
5. Limite de tamanho.
6. Rotina de limpeza.
7. Download autenticado.
