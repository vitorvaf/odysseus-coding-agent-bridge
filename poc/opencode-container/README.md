# OpenCode Runner — POC image (slice 1.1.3)

> **Status:** POC for `slice 1.1.3` (OpenCode Read-Only Vertical Slice).
> **Not** the production runner; the same hardening profile is mirrored
> in `/compose.yaml` at the repo root under `services.ocab-opencode-runner`.

## Objetivo

Publica uma imagem Docker do OpenCode Server endurida, validada
manualmente via `/health` e utilizável pelo `OpenCodeAdapter` da slice
1.1.3. A imagem é construída em cima do `node:20-alpine`, instala o
binário `opencode` via instalador oficial, e corre como UID 10001.

Restrições aplicadas (de `docs/discovery/009-security-discovery.md`):

* Sem `docker.sock` mount
* Sem `privileged: true`
* `user: "10001:10001"` no compose
* `cap_drop: ALL` no compose
* `read_only: true` no compose
* `security_opt: no-new-privileges` no compose
* `pids_limit`, `cpus`, `memory` definidos no compose
* Sem `host.docker.internal`

## Como usar

```bash
# Ad-hoc check (sem Compose):
docker build -t ocab-opencode-runner:dev-s113 .
docker run --rm -d \
  --name ocab-opencode-runner \
  --user 10001:10001 \
  --read-only \
  --tmpfs /tmp:size=64m,mode=1777 \
  --security-opt no-new-privileges \
  --cap-drop ALL \
  -p 127.0.0.1:14096:4096 \
  --network ocab-net \
  --memory 4G --cpus 2.0 --pids-limit 512 \
  ocab-opencode-runner:dev-s113

# Healthcheck
./healthcheck.sh
# or:
wget --no-verbose --tries=1 --spider http://127.0.0.1:4096/health
```

## Endpoints esperados do OpenCode Server (alvo upstream)

| Endpoint | Método | Status |
| --- | --- | --- |
| `/health` | GET | 200 quando saudável |
| `/sessions` | POST | cria sessão |
| `/sessions/{id}/prompt` | POST | envia prompt |
| `/sessions/{id}/events` | GET (SSE) | stream de eventos |
| `/sessions/{id}/cancel` | POST | cancela sessão |

> Os endpoints exatos dependem da versão do `opencode` instalada.
> O construto do Dockerfile tenta a versão oficial via `opencode.ai/install`.
> Em ambiente offline, monte o binário local em `/usr/local/bin/opencode`
> ao subir o container.

## Limitações conhecidas

* O instalador oficial baixa um binário Go. Em ambiente offline, o build
  emite `WARN: opencode.ai install failed`; nesse caso, faça bind-mount
  do binário do host.
* O runner não persiste sessões entre execuções (ciclo efêmero).

## Próximo passo

* Slice 1.1.4 (MCP Contract Completion) — adicionar validação ponta a
  ponta contra esta imagem via Odysseus real ou cliente MCP.
