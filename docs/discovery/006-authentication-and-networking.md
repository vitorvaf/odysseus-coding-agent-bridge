# Discovery 006 — Authentication and Networking

> **Status:** Completed
> **Bloqueia:** Fase 1
> **Prioridade:** P0
> **Data:** 2026-07-27

## Objetivo

Validar a topologia de rede Docker e o esquema de autenticação interna entre Odysseus, Coding Agent Bridge e OpenCode Runner, fundamentando ADR-0013 (rede privada) e o requisito OQ-090 (autenticação MCP).

## Topologia proposta

```mermaid
flowchart LR
    O[Odysseus / Cliente MCP]
    B[ocab-bridge<br/>MCP Server<br/>:9010]
    OC[ocab-opencode-runner<br/>OpenCode Server<br/>:4096]
    PG[(ocab-postgres<br/>:5432)]
    EXT[Repositórios remotos]

    subgraph ocab-net[Rede Docker privada ocab-net]
        O <-->|MCP Streamable HTTP<br/>Bearer Token| B
        B -->|dispatch| OC
        B <--> PG
    end
    OC -.->|https<br/>allowlist| EXT
```

> Restrição obrigatória: **nenhuma porta publicada no host** exceto quando explicitamente autorizado por ADR específica. Toda comunicação interna usa DNS da rede privada.

## Validação de DNS interno (POC executada)

```bash
docker network create ocab-net
docker run -d --network ocab-net --name ocab-test-a alpine:3.20 sleep 600
sleep 2
docker run --rm --network ocab-net alpine:3.20 sh -c "nslookup ocab-test-a"
```

```text
Server:         127.0.0.11
Address:        127.0.0.11:53

Non-authoritative answer:

Name:   ocab-test-a
Address: 172.21.0.2
```

```bash
docker run --rm --network ocab-net alpine:3.20 sh -c "ping -c 1 ocab-test-a"
```

```text
PING ocab-test-a (172.21.0.2): 56 data bytes
64 bytes from 172.21.0.2: seq=0 ttl=64 time=0.095 ms
```

**Conclusão:**

* **DNS interno do Docker Compose funciona.** Containers se resolvem por nome dentro da rede.
* `127.0.0.11` é o resolvedor interno do Docker.
* Resolução por nome não requer configuração adicional além de `--network ocab-net`.

```bash
docker stop ocab-test-a
docker rm -f ocab-test-a
docker network rm ocab-net
```

## Endpoints e portas (revisão)

| Serviço | Porta interna | Publicação no host | ADR de suporte |
| --- | --- | --- | --- |
| `ocab-bridge` MCP | `9010` | `expose` (não `publish`); necessário para o cliente MCP no host | ADR-0013 |
| `ocab-postgres` | `5432` | apenas `expose` | ADR-0013 |
| `ocab-opencode-runner` | `4096` | apenas `expose` | ADR-0013, OQ-020 |
| `llama-server` (opcional) | `8080` | apenas `expose` | ADR-0013 (pós-MVP) |

> Regra geral: usar `expose:` para portas internas; usar `publish:` **somente** quando o componente precisar ser acessado de fora da rede Docker (ex.: `Odysseus` rodando no host, no MVP).

### Premissas de URL

```text
http://coding-agent-bridge:9010/mcp
http://opencode-runner:4096
http://llama-server:8080/v1
```

> O nome do serviço segue convenção do Docker Compose (`<service-name>`), não `host.docker.internal`.

## Autenticação interna

### Entre Odysseus e bridge (MCP)

| Aspecto | Decisão | Justificativa |
| --- | --- | --- |
| Tipo | Bearer token (estático para o MVP) | REST/HTTP nativo, sem dependência adicional |
| Header | `Authorization: Bearer <token>` | padrão OAuth 2.0 / RFC 6750 |
| Tamanho mínimo | 32 bytes (256 bits) aleatórios | entropia suficiente para MVP |
| Local de armazenamento | arquivo montado em `/run/secrets/ocab_mcp_token` ou `~/.config/ocab/token` no host, nunca em env no Compose | ADR-0007 |
| Rotação | manual no MVP (procedimento em `security/secrets-management.md`); rotação automática planejada para Fase 10 | OQ-030 |
| Validação | middleware ASP.NET Core antes do `MapMcp(...)` | rejeição com `401 unauthorized` |
| CORS | o MCP Streamable HTTP é consumido pelo Odysseus localmente; CORS pode ser dispensado no MVP | ADR-0013 |
| TLS | HTTP em ambiente local (loopback); TLS opcional em deploy remoto | fora de escopo do MVP |

### Entre bridge e OpenCode Runner

| Aspecto | Decisão | Justificativa |
| --- | --- | --- |
| Tipo | Bearer token interno (diferente do MCP) | defesa em profundidade |
| Endereço do OpenCode | `http://opencode-runner:4096` | DNS privado |
| Endereço do bridge para o OpenCode | `Authorization: Bearer <token>` | header HTTP |
| Token | gerado por script na inicialização do Compose; montado via `secrets:` do Docker | minimiza exposição |

### Entre bridge e PostgreSQL

| Aspecto | Decisão | Justificativa |
| --- | --- | --- |
| Tipo | usuário/senha internos | ADR-0004 |
| Endereço | `postgres://ocab:__SET_ME__@ocab-postgres:5432/ocab` | `__SET_ME__` placeholder |
| Persistência | `secrets:` no Compose | obrigatório |
| TLS | `sslmode=disable` no MVP (rede privada) | ADR-0013 |

## Restrições de saída para o runner

* **Default deny** na rede egress.
* Para o OpenCode acessar o repositório remoto, permitir os hosts cadastrados via `Repository.urlCanonical`.
* Implementação: usar o DNS interno do Docker para `*.git.internal` (e quaisquer hosts externos explicitamente permitidos); bloquear demais hosts via regras no iptables ou network policies.

> **Pendente para slice 1.1.1:** definir a estratégia de egress (iptables no bridge vs. proxy HTTP explícito vs. `network_mode` customizado). O risco é registrado em OQ-032.

## Ausência de Docker socket (validação adicional)

```bash
docker exec <container> sh -c "ls /var/run/docker.sock 2>&1"
```

Resultado esperado em produção OCAB:

```text
ls: /var/run/docker.sock: No such file or directory
```

Validado em [`003-opencode-container-poc.md`](003-opencode-container-poc.md).

## Trust boundaries

| Boundary | Direção | Mecanismo |
| --- | --- | --- |
| Odysseus → Bridge | inbound | Bearer token MCP, validação em middleware |
| Bridge → OpenCode | outbound | Bearer token interno, restrição por porta |
| Bridge → Postgres | outbound | usuário/senha + `secrets:` |
| Runner → Internet | outbound | allowlist explícita por repositório |
| Runner → Host | bloqueado | `--read-only` + `cap_drop: ALL` + sem `pid`/`ipc` |

## Decisões registradas

| Decisão | Origem |
| --- | --- |
| Bearer token para MCP | este discovery |
| Bearer token interno para runner | este discovery |
| DNS interno via `ocab-net` | ADR-0013 (validado POC) |
| Sem Docker socket | ADR-0006 (validado em 003) |
| Sem `network_mode: host` | ADR-0013 |
| Sem `host.docker.internal` | restrição obrigatória |

## Questões abertas afetadas

* **OQ-030** (rotação de credenciais): parcialmente fechada — procedimento manual definido; rotação automática fica para Fase 10.
* **OQ-032** (rede dos runners): estratégia `default-deny + allowlist` definida; implementação técnica fica para slice 1.1.1.
* **OQ-090** (auth MCP): fechada — Bearer token no MVP; rotação e mTLS são evolução.
* **OQ-091** (auth OpenCode): parcialmente fechada — definida a forma (`Authorization: Bearer`); validação no container fica para slice 1.1.3.

## Próximo passo

* Slice 1.1.1 deve implementar o middleware de validação do token MCP e o header de `Authorization` no adapter OpenCode.
* Adicionar entrada de secrets no `compose.yaml` para `ocab_mcp_token` e `ocab_runner_token`.
* Documentar rotação manual em `docs/security/secrets-management.md` se ainda não estiver registrado.
