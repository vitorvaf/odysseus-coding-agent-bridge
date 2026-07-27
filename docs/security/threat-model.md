# Threat Model

> **Status:** Proposed

Modelagem de ameaças do OCAB usando abordagem estruturada (variante de STRIDE). Para cada ameaça são documentados: ativo, vetor, impacto, probabilidade, mitigação, detecção e risco residual.

## Ativos

* Repositórios Git cadastrados.
* Código-fonte em execução.
* Workspaces de execução.
* Artefatos (logs, diffs, relatórios).
* Banco PostgreSQL com estado, eventos e auditoria.
* Configuração do bridge.
* Credenciais e tokens internos.
* MCP server e contrato público.
* Ambiente do host.

## Ameaças

### T1 — Prompt injection via arquivos do repositório

* **Ativo:** workspace de execução.
* **Vetor:** conteúdo de arquivo (e.g., `README.md`) instrui o agente a executar comandos maliciosos.
* **Impacto:** execução de comandos fora da política.
* **Probabilidade:** Alta.
* **Mitigação:** policy engine fora do modelo, allowlist de comandos, ausência de credenciais, sem acesso à rede externa.
* **Detecção:** logs de `policy.decision` com `deny`.
* **Risco residual:** Médio — depende da qualidade do policy engine.

### T2 — Path traversal

* **Ativo:** workspace, artifact storage.
* **Vetor:** paths com `..` ou absolutos em argumentos de comando.
* **Impacto:** leitura/escrita fora do workspace.
* **Probabilidade:** Média.
* **Mitigação:** validação rigorosa de paths, normalização antes da operação.
* **Detecção:** eventos de path inválido.
* **Risco residual:** Baixo.

### T3 — Symlink escape

* **Ativo:** workspace.
* **Vetor:** symlink criado dentro do workspace apontando para fora.
* **Impacto:** acesso a arquivos fora do workspace.
* **Probabilidade:** Média.
* **Mitigação:** resolução de symlinks antes de cada operação, recusa de symlinks em escrita quando apropriado.
* **Detecção:** logs de recusa.
* **Risco residual:** Baixo.

### T4 — Execução destrutiva

* **Ativo:** workspace, repositório.
* **Vetor:** comandos como `git reset --hard`, `rm -rf`, `git clean -fdx`.
* **Impacto:** perda de trabalho.
* **Probabilidade:** Média.
* **Mitigação:** blocklist, ausência de credenciais de push, ambiente isolado.
* **Detecção:** métricas de política.
* **Risco residual:** Baixo.

### T5 — Exfiltração de segredo

* **Ativo:** credenciais internas.
* **Vetor:** saída de comando enviada para URL externa.
* **Impacto:** vazamento de credenciais.
* **Probabilidade:** Média.
* **Mitigação:** rede restrita ao Docker network privado, redaction em logs e artefatos, ausência de credenciais sensíveis no runner.
* **Detecção:** tentativas de conexão bloqueadas.
* **Risco residual:** Baixo.

### T6 — Vazamento de segredo em logs

* **Ativo:** logs, artefatos, eventos.
* **Vetor:** segredo presente em comando stdout/stderr.
* **Impacto:** exposição de credenciais.
* **Probabilidade:** Alta sem redaction.
* **Mitigação:** redaction obrigatória, filtros configuráveis.
* **Detecção:** testes de segurança contínuos.
* **Risco residual:** Baixo com redaction ativa.

### T7 — Download de código malicioso

* **Ativo:** workspace.
* **Vetor:** `curl`, `wget` para URLs externas.
* **Impacto:** introdução de código não confiável.
* **Probabilidade:** Média.
* **Mitigação:** blocklist de comandos de download, rede restrita.
* **Detecção:** logs.
* **Risco residual:** Baixo.

### T8 — Dependência comprometida

* **Ativo:** bibliotecas do bridge, runner, base images.
* **Vetor:** supply chain.
* **Impacto:** execução de código malicioso.
* **Probabilidade:** Média.
* **Mitigação:** SBOM, pinning de versões, scanning periódico.
* **Detecção:** scanners de vulnerabilidade.
* **Risco residual:** Médio.

### T9 — Artifact poisoning

* **Ativo:** artifact storage.
* **Vetor:** artefato com conteúdo malicioso injetado.
* **Impacto:** leitura comprometida de artefatos.
* **Probabilidade:** Baixa.
* **Mitigação:** checksum, validação de paths, permissões POSIX.
* **Detecção:** divergência de checksum.
* **Risco residual:** Baixo.

### T10 — Acesso de rede lateral entre containers

* **Ativo:** rede Docker privada.
* **Vetor:** container comprometido acessando outro.
* **Impacto:** movimentação lateral.
* **Probabilidade:** Média.
* **Mitigação:** containers sem acesso desnecessário, política de rede, sem Docker socket.
* **Detecção:** logs de firewall do Docker.
* **Risco residual:** Baixo.

### T11 — Fork bomb

* **Ativo:** runner.
* **Vetor:** comando que gera processos recursivos.
* **Impacto:** exaustão de PIDs.
* **Probabilidade:** Baixa.
* **Mitigação:** `pids_limit` no Compose.
* **Detecção:** métricas de PIDs.
* **Risco residual:** Baixo.

### T12 — Preenchimento de disco

* **Ativo:** artifact storage, postgres.
* **Vetor:** geração excessiva de artefatos.
* **Impacto:** indisponibilidade.
* **Probabilidade:** Média.
* **Mitigação:** limites de tamanho por execução, retenção, monitoramento de disco.
* **Detecção:** métricas de disco.
* **Risco residual:** Baixo.

### T13 — Uso excessivo de CPU/memória

* **Ativo:** runner.
* **Vetor:** loop infinito, alocação excessiva.
* **Impacto:** indisponibilidade do runner.
* **Probabilidade:** Média.
* **Mitigação:** limites de CPU e memória, timeout.
* **Detecção:** métricas de recursos.
* **Risco residual:** Baixo.

### T14 — Falsificação de evento

* **Ativo:** `RunEvent`, `PolicyDecision`.
* **Vetor:** inserção direta no banco.
* **Impacto:** trilha de auditoria comprometida.
* **Probabilidade:** Baixa.
* **Mitigação:** inserção exclusiva pela aplicação, permissões do banco.
* **Detecção:** logs do banco.
* **Risco residual:** Baixo.

### T15 — Reuso indevido de idempotency key

* **Ativo:** `Run.idempotencyKey`.
* **Vetor:** cliente usa mesma chave com payload divergente.
* **Impacto:** confusão entre execuções.
* **Probabilidade:** Média.
* **Mitigação:** validação de payload + chave.
* **Detecção:** resposta `idempotency_conflict`.
* **Risco residual:** Baixo.

### T16 — Cancelamento incompleto

* **Ativo:** runner.
* **Vetor:** runner não responde ao cancel.
* **Impacto:** processo órfão.
* **Probabilidade:** Baixa.
* **Mitigação:** timeout de cancel, kill signal, reconciliação.
* **Detecção:** health check do runner.
* **Risco residual:** Baixo.

### T17 — Acesso não autorizado ao MCP

* **Ativo:** MCP server.
* **Vetor:** ausência de token, token fraco.
* **Impacto:** criação de execuções arbitrárias.
* **Probabilidade:** Média.
* **Mitigação:** token Bearer obrigatório, rotação, rate limit.
* **Detecção:** logs de autenticação.
* **Risco residual:** Baixo.

### T18 — Execução concorrente conflitante

* **Ativo:** workspace de escrita.
* **Vetor:** duas execuções no mesmo repositório.
* **Impacto:** corrupção de workspace.
* **Probabilidade:** Média.
* **Mitigação:** lock por `repositoryId` para execuções de escrita.
* **Detecção:** logs.
* **Risco residual:** Baixo.

### T19 — Manipulação de relatório

* **Ativo:** relatório final.
* **Vetor:** adulteração após geração.
* **Impacto:** perda de confiança no relatório.
* **Probabilidade:** Baixa.
* **Mitigação:** imutabilidade após gravação, checksum, separação de permissões.
* **Detecção:** checksum.
* **Risco residual:** Baixo.

### T20 — Confusão de papéis (validação vs infraestrutura)

* **Ativo:** status da execução.
* **Vetor:** falha de validação classificada como erro de runner.
* **Impacto:** relatórios enganosos.
* **Probabilidade:** Média.
* **Mitigação:** máquina de estados com `CompletedWithValidationErrors`.
* **Detecção:** inspeção do relatório.
* **Risco residual:** Baixo.

## Resumo de risco

| ID | Risco residual |
| --- | --- |
| T1 | Médio |
| T8 | Médio |
| Demais | Baixo |

## Mitigações prioritárias

* Implementar policy engine rigoroso (T1).
* Aplicar redaction universal (T6).
* Implementar lock de execução por repositório (T18).
* Manter SBOM e scanning (T8).

## Referências relacionadas

* [`trust-boundaries.md`](trust-boundaries.md)
* [`command-policy.md`](command-policy.md)
* [`container-hardening.md`](container-hardening.md)
* [`secrets-management.md`](secrets-management.md)
* [`incident-response.md`](incident-response.md)
* [`../specs/011-security-hardening/spec.md`](../specs/011-security-hardening/spec.md)
