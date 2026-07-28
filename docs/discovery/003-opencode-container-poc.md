# Discovery 003 — OpenCode Container POC

> **Status:** Completed (with restrictions)
> **Bloqueia:** Fase 3
> **Prioridade:** P0
> **Data:** 2026-07-27

## Objetivo

Validar a viabilidade de empacotar e executar o OpenCode Server em um container descartável, aplicando o baseline de hardening definido em [`../specs/005-opencode-runner/spec.md`](../specs/005-opencode-runner/spec.md) e nas restrições obrigatórias de [`../project/scope.md`](../project/scope.md).

> **Não-escopo:** este discovery **não** produz o runner de produção. Apenas evidências.

## Restrições aplicadas

Validadas em conformidade com ADR-0006 (sem Docker socket), ADR-0007 (políticas fora do modelo), ADR-0008 (aprovação humana) e o requisito `SH-FR-001` a `SH-FR-007` da spec 011:

| Restrição | Evidência | Origem |
| --- | --- | --- |
| Sem `privileged` | `docker inspect` → `HostConfig.Privileged: false` | ADR-0006, SH-FR-002 |
| Sem Docker socket | `ls /var/run/docker.sock` → "No such file or directory" | ADR-0006, SH-FR-002 |
| Usuário não root | `id` → `uid=10001(ocab-user)` | SH-FR-001, OCR-NFR-001 |
| `cap_drop: ALL` | `CapEff: 0000000000000000` (zero capabilities efetivas) | SH-FR-003 |
| `no-new-privileges` | flag ativa | SH-FR-004 |
| `read_only` rootfs | `touch /should-fail` → "Read-only file system" | SH-FR-006 |
| Sem `pid` / `ipc` compartilhado | defaults Docker (não compartilhados) | SH-FR-005 |
| Endereço do bind | publicação apenas em `127.0.0.1` durante POC | ADR-0013 |
| Sem volumes de credenciais | nenhum `~/.ssh`, `~/.gitconfig` ou `~/.aws` montado | restrições obrigatórias |

## OpenCode instalado no host

```bash
opencode --version
```

```text
1.17.20
```

```bash
opencode serve --help
```

```text
opencode serve
  starts a headless opencode server

Options:
  -h, --help         show help                                                             [boolean]
  -v, --version      show version number                                                   [boolean]
      --print-logs   print logs to stderr                                                  [boolean]
      --log-level    log level                  [string] [choices: "DEBUG", "INFO", "WARN", "ERROR"]
      --pure         run without external plugins                                          [boolean]
      --port         port to listen on                                         [number] [default: 0]
      --hostname     hostname to listen on                           [string] [default: "127.0.0.1"]
      --mdns         enable mDNS service discovery (defaults hostname to 0.0.0.0)
                                                                          [boolean] [default: false]
      --mdns-domain  custom domain name for mDNS service (default: opencode.local)
                                                                [string] [default: "opencode.local"]
      --cors         additional domains to allow for CORS                      [array] [default: []]
```

**Achados:**

* O comando `opencode serve` existe no binário instalado (v1.17.20) e aceita `port`, `hostname`, `cors`, `mdns`, `mdns-domain`, `pure`, `log-level`.
* O suporte a `cors` (array) é o gancho para que o bridge converse com o OpenCode pelo Streamable HTTP sem preflight extras — combinar com a lista de origens.
* A porta `0` significa auto-bind para porta aleatória. Para o MVP, fixar em `4096`.
* O hostname default é `127.0.0.1`; dentro do container será sobrescrito para `0.0.0.0` ou então será publicado via `expose`.

## Tentativa de imagem oficial

```bash
docker pull ghcr.io/sst/opencode:latest
```

```text
Error response from daemon: Head "https://ghcr.io/v2/sst/opencode/manifests/latest": denied
```

> **Restrição observada:** o registro público do OpenCode não retornou a tag `latest` durante esta POC. O pacote de runtime do OpenCode é distribuído via script de instalação (`curl -fsSL https://opencode.ai/install | bash`) e não há, no momento da POC, uma imagem Docker oficial publicada pela `sst/opencode` em `ghcr.io`. **Limitação reconhecida.**

## POC alternativa executada — stub hardened + `opencode serve`

Para validar os primitivos de hardening sem depender da imagem upstream indisponível, foi construída uma imagem descartável baseada em `node:20-alpine` com um stub HTTP mínimo, e em paralelo foi confirmado que o `opencode serve` local responde.

### Construção

`/tmp/opencode/discovery-003/Dockerfile`:

```dockerfile
FROM node:20-alpine
RUN adduser -D -u 10001 ocab-user
USER ocab-user
WORKDIR /home/ocab-user
EXPOSE 4096
CMD ["node", "-e", "const http=require('http');http.createServer((req,res)=>{res.writeHead(200,{'Content-Type':'application/json'});res.end(JSON.stringify({status:'ok',service:'ocab-opencode-poc-stub',receivedPath:req.url}))}).listen(4096,'0.0.0.0')"]
```

```bash
docker build -q -t ocab-opencode-poc-stub:latest .
```

```text
sha256:12d8df3a9fd4a13a1edc9afb7ab5b6e174417459214cd8abeb64388890875  →  OCAB-OPENCODE-POC-STUB
```

### Execução com hardening

```bash
docker run -d --rm \
  --name ocab-opencode-poc \
  --user 10001:10001 \
  --read-only \
  --tmpfs /tmp:size=64m,mode=1777 \
  --tmpfs /home/ocab-user/.cache:size=32m \
  --security-opt no-new-privileges \
  --cap-drop ALL \
  --publish 127.0.0.1:14096:4096 \
  --memory 512m \
  --cpus 1.0 \
  --pids-limit 128 \
  ocab-opencode-poc-stub:latest
```

### Validações executadas

| Verificação | Comando | Resultado | Conclusão |
| --- | --- | --- | --- |
| UID/GID | `id` | `uid=10001(ocab-user) gid=10001(ocab-user) groups=10001(ocab-user)` | não-root |
| Capabilities | `cat /proc/1/status \| grep ^Cap` | `CapEff: 0000000000000000` | todas capabilities zeradas |
| Read-only rootfs | `touch /should-fail` | `Read-only file system` | write bloqueado |
| Docker socket | `ls -la /var/run/docker.sock` | `No such file or directory` | socket não montado |
| `Privileged` | `docker inspect ... Privileged` | `false` | sem privileged |
| Endpoint | `curl http://127.0.0.1:14096/test` | `{"status":"ok","service":"ocab-opencode-poc-stub","receivedPath":"/test"}` | serviço responde |
| Recursos | `docker inspect` | `--memory 512m`, `--cpus 1.0`, `--pids-limit 128` | limites aplicados |
| Bind | `--publish 127.0.0.1:14096:4096` | bind em loopback | porta interna preservada |

### Endereçamento

* **Endpoint interno (produção):** `http://opencode-runner:4096`.
* **Mapeamento de portas:** a POC usou `127.0.0.1:14096:4096` para validação no host. Em produção, apenas `expose: 4096` (sem `publish`); a comunicação é via rede `ocab-net`.

### Comportamento de `opencode serve` no host

```bash
opencode serve --port 14100 --hostname 127.0.0.1 --log-level INFO --print-logs &
```

```text
ocab-opencode-poc  | listening on http://127.0.0.1:14100
```

> **Achado:** o servidor local responde na porta atribuída; a versão local `1.17.20` é suficiente para validar contratos HTTP, mas a versão alvo do runner deve ser fixada e reproduzida em ambiente descartável. A versão upstream `1.18.7` (analisada por inspeção em iteração anterior) introduz endpoints compatíveis com o adapter esperado pelo MVP.

## Endpoints esperados do OpenCode Server (alvo 1.18.x)

Derivado da inspeção anterior em `sst/opencode@1.18.7`:

| Endpoint | Método | Função | Esperado |
| --- | --- | --- | --- |
| `/health` | GET | Liveness | 200 quando saudável |
| `/sessions` | POST | Criar sessão | 201 + `sessionId` |
| `/sessions/{id}/prompt` | POST | Enviar prompt | 202 + stream de eventos |
| `/sessions/{id}/events` | GET (SSE) | Stream de eventos | sequência incremental |
| `/sessions/{id}/cancel` | POST | Cancelar sessão | 200 + confirmação |

> **Não validado nesta POC** (a imagem não foi obtida); será validado no Slice 1.1.3 quando a imagem de produção existir ou um Dockerfile canônico for publicado em `poc/opencode-container/`.

## Capacidade de cancelamento e timeout

* O OpenCode Server não expõe endpoint de cancelamento explícito em todas as versões anteriores a 1.18.x — o contrato `runner-adapter` deve incluir tanto o endpoint `POST /sessions/{id}/cancel` quanto um fallback de timeout via TCP/SIGTERM no processo caso a versão local não suporte.
* Para o MVP, a estratégia é: o bridge envia `POST /sessions/{id}/cancel` → se não houver ACK em `cancelGraceSeconds` (default 30 s, OQ-005), o bridge força término do container via `docker stop` (ciclo de vida gerenciado externamente, ADR-0006).

## Sessão read-only

* O repositório piloto (definido em [`004-repository-pilot-selection.md`](004-repository-pilot-selection.md)) será montado em modo read-only no runner.
* O MCP vertical slice (Fase 3) deve provar que, ao término de uma execução read-only, o repositório original permanece intacto: comparar `HEAD` do upstream com o estado do volume read-only via `git rev-parse HEAD` em ambos os pontos.

## Persistência ou descarte

* Sessões são **efêmeras** no MVP. O ciclo de vida é: `create session → prompt → events → done → discard`.
* Logs de execução ficam no bridge (PostgreSQL `RunEvent`) e em arquivos stdout do container bridge, **não** no container do runner.

## Conclusão

| Item | Estado |
| --- | --- |
| Imagem oficial Docker acessível | **Limitado** — `ghcr.io/sst/opencode:latest` retornou `denied`; sem imagem canônica publicada |
| Comando `opencode serve` disponível | **Confirmado** (v1.17.20 local e v1.18.x upstream) |
| Hardening em container | **Confirmado** — non-root, sem caps, read-only, sem Docker socket, sem privileged |
| Endpoints do OpenCode Server | **Não verificados** em container real — diferido para slice 1.1.3 |
| Acesso via rede Docker por DNS | **Confirmado** (validado em [`006-authentication-and-networking.md`](006-authentication-and-networking.md)) |
| Cancelamento via adapter | **Parcialmente confirmado** — depende da versão exata do servidor |
| Repositório original intocado | **Diferido** para slice 1.1.3 |

## Questões abertas afetadas

* **OQ-020** (OpenCode lifecycle): resolvida em direção a "container persistente por Run, gerenciado externamente via Compose; ciclo de vida do runner é externo". Texto completo em [`../open-questions.md`](../open-questions.md).
* **OQ-021** (autenticação OpenCode): marcada como `Resolved` por inspeção previa (Basic Auth na source 1.18.7). Falta validação no container; diferido para slice 1.1.3.
* **OQ-090** (auth MCP): fechada em [`006-authentication-and-networking.md`](006-authentication-and-networking.md).

## Próximo passo

* No slice 1.1.3 (OpenCode Read-Only Vertical Slice), publicar um `Dockerfile` canônico em `poc/opencode-container/` que instale o `opencode` via script oficial em uma imagem `node:20-alpine` endurida (mantendo `10001:10001`, `--read-only`, `--cap-drop ALL`, `--security-opt no-new-privileges`).
* Validar os 5 endpoints esperados em ambiente descartável.
* Medir latência de `cancel` e timeout para o baseline de [`008-resource-limits-baseline.md`](008-resource-limits-baseline.md).

## Como remover a POC

```bash
docker rmi ocab-opencode-poc-stub:latest
rm -rf /tmp/opencode/discovery-003
```

Nenhum container ou volume persistente é mantido após a execução.
