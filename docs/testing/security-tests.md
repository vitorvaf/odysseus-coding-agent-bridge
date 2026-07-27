# Security Tests

> **Status**: Proposed

Define testes de segurança do OCAB.

## Cenários

### Path traversal

* Slug com `..` é rejeitado.
* Argumentos com `../` são rejeitados.
* Paths absolutos são rejeitados.

### Symlink escape

* Symlink que aponta para fora é detectado.
* Operação é bloqueada.

### Comandos bloqueados

* `git push` bloqueado.
* `rm -rf /` bloqueado.
* `sudo` bloqueado.
* `curl` para host não permitido bloqueado.

### Acesso fora do workspace

* Comando com `cwd` fora do workspace é bloqueado.
* Comando com path absoluto fora do workspace é bloqueado.

### Segredos em logs

* Token em stdout é redacted.
* Senha em stderr é redacted.
* URL com credencial é mascarada.

### Runner root

* `whoami` retorna UID não-zero.
* Capabilities mínimas.

### Docker socket

* `/var/run/docker.sock` não está montado.

### Excesso de recursos

* Fork bomb é limitada por `pids_limit`.
* Loop de alocação é limitado por `mem_limit`.

### Request não autenticado

* Chamada MCP sem token retorna 401.
* Token expirado retorna 401.
* Token com escopo insuficiente retorna 403.

### Idempotência

* Mesma chave com payload divergente retorna 409.

### Concorrência

* Execuções de escrita no mesmo repositório não compartilham workspace.

## Ferramentas

* xUnit para testes.
* Docker exec para inspeção de containers.
* Scripts de inspeção POSIX.

## Frequência

* Em todo PR.
* Nightly ampliado.
* Pentest manual periódico.

## Referências relacionadas

* [`strategy.md`](strategy.md)
* [`../security/threat-model.md`](../security/threat-model.md)
* [`../security/command-policy.md`](../security/command-policy.md)
* [`../security/container-hardening.md`](../security/container-hardening.md)
