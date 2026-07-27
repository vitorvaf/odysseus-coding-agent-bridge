# Endurecimento de Containers

> **Status:** Proposed

Define endurecimento padrão dos containers do OCAB.

## Princípios

* Menor privilégio possível.
* Sem Docker socket.
* Usuário não root.
* `cap_drop: ALL` + adição mínima.
* `security_opt: no-new-privileges`.
* Sem compartilhamento de `pid` e `ipc`.
* `read_only` rootfs quando aplicável.
* Recursos limitados.

## Imagem base

* Imagem mínima Linux (e.g., `debian-slim`).
* Sem pacotes desnecessários.
* Usuário dedicado criado durante build.

## User namespace

* Usuário não root.
* UID dedicado (e.g., 10001).
* Diretórios com permissão `0750` quando necessário.

## Capabilities

* `cap_drop: ALL`.
* Adicionar apenas:
  * `CHOWN` (quando necessário).
  * `SETUID`/`SETGID` (quando necessário).
* Sem `NET_ADMIN`, `SYS_ADMIN`, `SYS_PTRACE`.

## Security options

* `security_opt: no-new-privileges`.
* `read_only: true` em rootfs (quando aplicável).
* Volumes `tmpfs` para `/tmp` com `noexec`.

## Limites

* `mem_limit` configurado.
* `cpus` configurado.
* `pids_limit` configurado.

## Volumes

* Apenas volumes necessários.
* Volumes sensíveis com permissões `0600`.
* Sem `/var/run/docker.sock`.

## Rede

* Apenas rede Docker privada.
* DNS interno.
* Sem `network_mode: host`.

## Variáveis de ambiente

* Sem segredos em env vars em produção.
* Apenas configurações não sensíveis.

## Logging

* Logs enviados para stdout/stderr.
* Coletados externamente.

## Exemplos

### Bridge

```yaml
ocab-bridge:
  image: ocab/bridge:1.0.0-MVP
  user: "10001:10001"
  read_only: true
  cap_drop: [ALL]
  security_opt: [no-new-privileges]
  mem_limit: 512m
  cpus: "1.0"
  pids_limit: 256
  tmpfs:
    - /tmp: size=64m,mode=1777,noexec
  volumes:
    - artifacts:/app/artifacts:rw
    - ./secrets/bridge:/app/secrets:ro
  networks:
    - ocab-net
```

### OpenCode Runner

```yaml
ocab-opencode-runner:
  image: ocab/opencode-runner:1.0.0-MVP
  user: "10002:10002"
  read_only: true
  cap_drop: [ALL]
  security_opt: [no-new-privileges]
  mem_limit: 1g
  cpus: "2.0"
  pids_limit: 256
  tmpfs:
    - /tmp: size=64m,mode=1777,noexec
  volumes:
    - workspaces:/workspace:rw
  networks:
    - ocab-net
```

## Verificações

* Sem root no container (`whoami` deve retornar UID).
* Sem Docker socket em `/var/run`.
* Capabilities mínimas verificadas com `capsh --print`.
* `read_only` validado por teste.

## Referências relacionadas

* [`threat-model.md`](threat-model.md)
* [`command-policy.md`](command-policy.md)
* [`../architecture/container-architecture.md`](../architecture/container-architecture.md)
* [ADR-0006](../adr/0006-no-docker-socket.md)
