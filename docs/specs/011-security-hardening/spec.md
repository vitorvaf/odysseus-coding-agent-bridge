# Spec 011 — Security Hardening

## Status

Proposed

## Resumo

Define ameaças, mitigações, controles e validações de segurança ao longo do OCAB.

## Contexto

A plataforma lida com execução de código, acesso a repositórios e credenciais. Sem endurecimento, ataques de prompt injection, escape de workspace ou abuso de recursos podem comprometer o ambiente.

## Problema

Como mitigar ameaças concretas sem comprometer a operação?

## Objetivos

* Tratar ameaças prioritárias.
* Aplicar defesas em camadas.
* Auditar decisões sensíveis.
* Fornecer resposta a incidentes.

## Não objetivos

* Substituir práticas de segurança do host.
* Fornecer serviço de segurança dedicado.

## Escopo funcional

* Endurecimento de containers.
* Política de comandos.
* Restrições de rede.
* Auditoria.
* Resposta a incidentes.

## Requisitos funcionais

* **SH-FR-001** Containers devem executar como usuário não root.
* **SH-FR-002** Containers não devem montar Docker socket.
* **SH-FR-003** Containers devem ter `cap_drop: ALL` e capacidades mínimas.
* **SH-FR-004** Containers devem ter `security_opt: no-new-privileges`.
* **SH-FR-005** Containers não devem compartilhar `pid` ou `ipc`.
* **SH-FR-006** Runners devem ter `read_only` rootfs sempre que possível.
* **SH-FR-007** Runner deve ter rede restrita à rede privada Docker.
* **SH-FR-008** Credenciais devem ser carregadas via arquivos montados e nunca via env var em produção.
* **SH-FR-009** Logs devem passar por redaction.
* **SH-FR-010** Decisões sensíveis devem ser auditadas.
* **SH-FR-011** Toda suspeita de incidente deve acionar playbook.

## Requisitos não funcionais

* **SH-NFR-001** Imagens devem ser mínimas e atualizadas periodicamente.
* **SH-NFR-002** Vulnerabilidades em dependências devem ser monitoradas.
* **SH-NFR-003** Logs de auditoria devem ser imutáveis após gravação.

## Atores e componentes envolvidos

* Bridge.
* Runners.
* Operador técnico.
* Equipe de segurança.

## Casos de uso

* Detectar prompt injection.
* Detectar tentativa de escape.
* Detectar uso excessivo de recursos.
* Detectar dependência comprometida.

## Ameaças principais

Ver [`../security/threat-model.md`](../security/threat-model.md).

## Mitigações

| Ameaça | Mitigação primária |
| --- | --- |
| Prompt injection | Policy engine + revisão humana |
| Escape de workspace | Path validation + permissões POSIX |
| Path traversal | Validação rigorosa |
| Symlink escape | Resolução de symlinks antes de operação |
| Execução destrutiva | Lista de comandos + sem credenciais |
| Exfiltração | Rede restrita + redaction |
| Fork bomb | Limites de PIDs |
| Disco cheio | Limites de uso + monitoramento |
| Dependência comprometida | SBOM + scanning |
| Artefato malicioso | Sanitização + redaction |
| Falsificação de evento | Assinatura ou lock pessimista |
| Reuso de idempotency key | Validação de payload |

## Detecção

* Métricas de uso de recursos.
* Alertas de uso anormal.
* Logs de decisão.
* Análise periódica de eventos.

## Resposta a incidentes

Ver [`../security/incident-response.md`](../security/incident-response.md).

## Endurecimento de containers

Ver [`../security/container-hardening.md`](../security/container-hardening.md).

## Política de comandos

Ver [`../security/command-policy.md`](../security/command-policy.md).

## Fronteiras de confiança

Ver [`../security/trust-boundaries.md`](../security/trust-boundaries.md).

## Modelo de dados afetado

* `PolicyDecision` (auditoria).
* `RunEvent` (trilha).

## Segurança

* Cobertura ampla por camadas.
* Auditoria imutável.

## Observabilidade

* Métricas de segurança.
* Logs de alerta.

## Estratégia de testes

* Testes de segurança (ver [`../testing/security-tests.md`](../testing/security-tests.md)).
* Pentests manuais periódicos.

## Critérios de aceite

* **SH-AC-001** Nenhum container monta Docker socket.
* **SH-AC-002** Nenhum runner executa como root.
* **SH-AC-003** Tentativas de escape de workspace falham.
* **SH-AC-004** Tentativas de path traversal falham.
* **SH-AC-005** Segredos não aparecem em logs.
* **SH-AC-006** Decisões sensíveis são registradas em `PolicyDecision`.

## Dependências

* Spec 006 (Policy Engine).
* Spec 007 (Workspace Isolation).
* Spec 010 (Observability).

## Riscos

* Ameaça nova não coberta pelas mitigações atuais.
* Regressão por mudança de configuração.

## Decisões relacionadas

* [ADR-0005](../adr/0005-isolated-workspaces.md)
* [ADR-0006](../adr/0006-no-docker-socket.md)
* [ADR-0007](../adr/0007-policy-enforcement-outside-model.md)
* [ADR-0008](../adr/0008-human-approval-required.md)

## Questões em aberto

* SBOM e scanning de dependências.
* Ferramenta de pentest.
* Política de rotação de credenciais.

## Fora de escopo

* WAF.
* IDS/IPS.
* Antivírus.

## Estratégia de entrega incremental

1. Endurecimento básico.
2. Política de comandos.
3. Auditoria.
4. Limites de recursos.
5. Rede restrita.
6. SBOM.
7. Pentests.
