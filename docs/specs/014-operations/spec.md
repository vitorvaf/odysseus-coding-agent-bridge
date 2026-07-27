# Spec 014 — Operations

## Status

Proposed

## Resumo

Define procedimentos de instalação, atualização, backup, restore, retenção, limpeza, troubleshooting, rotação de segredos e disaster recovery.

## Contexto

A plataforma precisa operar de forma confiável em ambiente local. Procedimentos explícitos reduzem risco operacional.

## Problema

Como garantir operação consistente, recuperação de falhas e manutenção segura?

## Objetivos

* Padronizar instalação e atualização.
* Garantir backup e restore.
* Aplicar retenção e limpeza.
* Fornecer troubleshooting.
* Tratar rotação de segredos.
* Tratar disaster recovery.

## Não objetivos

* Implementar UI de operação.
* Substituir práticas de DevOps do host.

## Escopo funcional

* Procedimentos de instalação.
* Procedimentos de atualização.
* Backup e restore de PostgreSQL.
* Backup e restore de artefatos.
* Retenção configurável.
* Limpeza agendada.
* Troubleshooting.
* Rotação de segredos.
* Disaster recovery.
* Capacidade.

## Requisitos funcionais

* **OP-FR-001** Instalação deve usar Docker Compose com placeholders documentados.
* **OP-FR-002** Atualização deve seguir fluxo versionado sem perda de estado.
* **OP-FR-003** Backup do PostgreSQL deve ser agendado (default diário).
* **OP-FR-004** Backup de artefatos deve ser agendado (default diário).
* **OP-FR-005** Restore deve suportar ponto no tempo aproximado.
* **OP-FR-006** Retenção padrão: 30 dias para execuções, configurável.
* **OP-FR-007** Limpeza deve usar lock por `runId`.
* **OP-FR-008** Rotação de segredos deve ser documentada e exigir restart.
* **OP-FR-009** Disaster recovery deve incluir procedimentos para falha total do host.
* **OP-FR-010** Monitoramento de capacidade (disco, memória, PIDs) deve ser contínuo.

## Requisitos não funcionais

* **OP-NFR-001** Procedimentos devem ser executáveis manualmente sem dependência de UI.
* **OP-NFR-002** Backups devem ser verificados periodicamente.
* **OP-NFR-003** Restauração deve ser testada em ambiente de staging.

## Atores e componentes envolvidos

* Operador técnico.
* Engenheiro de operações.
* Bridge.
* PostgreSQL.

## Casos de uso

* Instalar plataforma do zero.
* Atualizar bridge.
* Recuperar banco de falha.
* Recuperar artefatos.
* Diagnosticar execução.
* Liberar espaço em disco.
* Rodar segredos.

## Instalação

* Requisitos: Linux, Docker, Docker Compose.
* Etapas:
  1. Clonar repositório (read-only).
  2. Copiar `.env.example` para `.env`.
  3. Substituir placeholders `__SET_ME__`.
  4. `docker compose pull` (ou build local).
  5. `docker compose up -d`.
  6. Validar `/health` e `/ready`.

## Atualização

* Verificar release notes.
* Backup antes de atualizar.
* `docker compose pull`.
* `docker compose up -d`.
* Validar saúde.
* Reverter em caso de falha usando imagem anterior.

## Backup

* `pg_dump` agendado em volume externo.
* `tar` agendado para `ocab-artifacts`.
* Retenção de backup configurável (default 14 dias para backup local).
* Backup remoto opcional.

## Restore

* `pg_restore` para banco.
* Extração de `tar` para artefatos.
* Validação de saúde pós-restore.

## Retenção e limpeza

* Cron interna remove artefatos expirados.
* Lock por `runId` evita corrida.
* Auditoria de limpeza.

## Troubleshooting

Ver [`../operations/troubleshooting.md`](../operations/troubleshooting.md).

## Rotação de segredos

* Procedimento manual documentado.
* Restart do bridge após rotação.
* Verificação de saúde.

## Disaster recovery

* Plano documentado.
* RPO/RTO definidos.
* Backup off-host.

## Capacidade

* Limites de disco, memória e PIDs documentados.
* Alertas de saturação.

## Manutenção de runners

* Reinício programado.
* Atualização de imagem.
* Verificação de saúde.

## Contratos

* Sem contrato MCP dedicado. Operações usam CLI e endpoints administrativos.

## Modelo de dados afetado

* `Run` é a base para auditoria.
* Configurações de retenção em arquivo de configuração.

## Segurança

* Procedimentos manuais com auditoria.
* Segredos em arquivos com permissões restritas.

## Observabilidade

* Métricas de saúde.
* Logs de manutenção.

## Estratégia de testes

* DRP testado em staging.
* Backup/restore testado periodicamente.
* Procedimentos revisados.

## Critérios de aceite

* **OP-AC-001** Operador consegue instalar a plataforma do zero seguindo a documentação.
* **OP-AC-002** Backup do PostgreSQL é executado e validado.
* **OP-AC-003** Restore do PostgreSQL restaura o estado em staging.
* **OP-AC-004** Limpeza remove artefatos expirados sem afetar execuções ativas.
* **OP-AC-005** Rotação de segredos é documentada e testada.

## Dependências

* Spec 001 (Platform Foundation).
* Spec 009 (Artifact Management).

## Riscos

* Falha de backup.
* Falha de restore não detectada.
* Sobrecarga de disco.

## Decisões relacionadas

* [ADR-0012](../adr/0012-filesystem-artifact-storage.md)
* [ADR-0013](../adr/0013-private-docker-network.md)

## Questões em aberto

* Janela de manutenção.
* Ferramenta de backup remoto.
* Política de capacidade.

## Fora de escopo

* UI de operação.
* Multi-cloud.

## Estratégia de entrega incremental

1. Procedimentos de instalação.
2. Backup automatizado.
3. Restore testado.
4. Retenção e limpeza.
5. Troubleshooting.
6. Rotação de segredos.
7. Disaster recovery.
