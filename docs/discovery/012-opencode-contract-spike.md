# Discovery 012 — OpenCode Contract Spike (v1.17.20 vs v1.18.8)

> **Status:** Completed (evidência capturada)
> **Data:** 2026-07-28
> **Bloqueia:** fechamento de `OQ-200` e `OQ-201`
> **Slice relacionado:** `SLICE-STAB-002`

## Objetivo

Validar o contrato HTTP do OpenCode Server em duas versões candidatas (v1.17.20 e v1.18.8), comparar o OpenAPI publicado em `/doc`, capturar comportamento de autenticação, identificar a família de rotas correta (singular `/session/*` vs plural `/sessions/*`) e embasar a decisão de versão alvo registrada na `ADR-0017`.

A fonte de verdade é o OpenAPI emitido em runtime pelo binário real (`GET /doc`), não a documentação web.

## Ambiente

| Item | Valor |
| --- | --- |
| Host | Ubuntu 22.04 (kernel conforme `uname -r`) |
| `opencode` v1.17.20 | `/home/vitor/.opencode/bin/opencode` (180.5 MB, 1.17.20, instalado pelo instalador oficial) |
| `opencode` v1.18.8 | `/tmp/opencode-v1.18.8/opencode` (179 MB, extraído de `opencode-linux-x64.tar.gz` baixado de `github.com/anomalyco/opencode/releases/download/v1.18.8/opencode-linux-x64.tar.gz`) |
| Acesso GitHub release `anomalyco/opencode` | HTTP 200 (asset disponível) |
| Acesso `ghcr.io/anomalyco/opencode` | HTTP 401 (mesmo bloqueio de registry do `sst/opencode` registrado em `docs/discovery/011-...`) |
| Portas usadas para spike | 14101 (v1.17.20) e 14102 (v1.18.8) |

## Procedimento

1. Subir `opencode serve` em portas distintas para cada versão (sem `OPENCODE_SERVER_PASSWORD` na primeira fase).
2. Capturar `/global/health`, `/doc`, `/event` e `POST /session`, `POST /session/{id}/{prompt_async,abort,message}` com curl, observando método, código HTTP, `Content-Type` e primeiros 200 bytes do body.
3. Reiniciar ambos com `OPENCODE_SERVER_PASSWORD` distintos e validar comportamento de auth (sem header → 401; com Basic Auth → 200).
4. Capturar OpenAPI completo de cada versão e comparar paths, operações e schemas.
5. Comparar o tamanho binário do spec para detectar drift estrutural.

## Resultados

### 1. Endpoints HTTP (mesmo conjunto de curl em ambas as versões)

| Método | Path | v1.17.20 | v1.18.8 |
| --- | --- | --- | --- |
| `GET` | `/global/health` | HTTP 200 `application/json` → `{"healthy":true,"version":"1.17.20"}` | HTTP 200 `application/json` → `{"healthy":true,"version":"1.18.8"}` |
| `GET` | `/health` | HTTP 200 `text/html` (SPA fallback, 2884 B) | HTTP 200 `text/html` (SPA fallback, 2884 B) |
| `GET` | `/doc` | HTTP 200 `application/json` (OpenAPI 3.1.0, 478 565 B) | HTTP 200 `application/json` (OpenAPI 3.1.0, 478 637 B) |
| `GET` | `/event` | HTTP 200 `text/event-stream` (SSE; emite `server.connected`) | HTTP 200 `text/event-stream` (SSE; emite `server.connected`) |
| `GET` | `/session/status` | HTTP 200 `application/json` → `{}` | HTTP 200 `application/json` → `{}` |
| `POST` | `/session` (body `{}`) | HTTP 200 `application/json` → `{"id":"ses_…","slug":"…","projectID":"…","directory":"…",…}` | HTTP 200 `application/json` → mesmo shape |
| `POST` | `/sessions` (plural, errado) | HTTP 200 `text/html` (SPA fallback) | HTTP 200 `text/html` (SPA fallback) |
| `POST` | `/session/test/prompt_async` | HTTP 500 `application/json` → `{"name":"UnknownError",…}` | HTTP 500 `application/json` → mesmo erro |
| `POST` | `/session/test/abort` | HTTP 500 `application/json` → `{"name":"UnknownError",…}` | HTTP 500 `application/json` → mesmo erro |
| `POST` | `/session/test/message` | HTTP 500 `application/json` → `{"name":"UnknownError",…}` | HTTP 500 `application/json` → mesmo erro |

**Achados:**

- A família `/session/*` (singular) é a **API real** e devolve `application/json` em todas as rotas.
- A família `/sessions/*` (plural, usada pelo `OpenCodeAdapter` atual) cai no **SPA fallback** do servidor web embutido e devolve `text/html` — esse é o contract drift que `OQ-201` captura.
- `GET /health` também cai no SPA fallback; o endpoint de health real é **`/global/health`**.
- `POST /session/{id}/{prompt_async,abort,message}` retornam `500 application/json` quando o `{id}` não existe — não é 404, é `UnknownError` com payload estruturado. O adapter precisa mapear `500 UnknownError` em códigos normalizados (ver `ADR-0017` § Decisão).
- O OpenAPI 3.1.0 está em `/doc` com 478 KB em ambas as versões.

### 2. Autenticação (com `OPENCODE_SERVER_PASSWORD`)

Restart de ambos os servidores com `OPENCODE_SERVER_PASSWORD` definido (`test123_aaa` em v1.17.20 e `test123_bbb` em v1.18.8):

| Cenário | v1.17.20 | v1.18.8 |
| --- | --- | --- |
| `GET /global/health` sem header | HTTP 401, sem `Content-Type`, sem body | HTTP 401, sem `Content-Type`, sem body |
| `GET /global/health` com `Authorization: Basic opencode:test123_aaa` | HTTP 200 `application/json` → `{"healthy":true,"version":"1.17.20"}` | HTTP 200 `application/json` → `{"healthy":true,"version":"1.18.8"}` |
| `POST /session` (body `{}`) sem header | HTTP 401 | HTTP 401 |
| `POST /session` (body `{}`) com Basic Auth | HTTP 200 `application/json` → sessão criada | HTTP 200 `application/json` → sessão criada |

**Achados:**

- A autenticação do OpenCode Server é **HTTP Basic** com o usuário fixo `opencode` e a senha vinda de `OPENCODE_SERVER_PASSWORD`.
- O `components.securitySchemes` no OpenAPI está **vazio** — a auth Basic é implementada fora da especificação OpenAPI (middleware nativo do servidor). Isso é importante: o adapter precisa conhecer o contrato de auth por documentação adicional (esta spike), não pelo OpenAPI.
- O comportamento é **idêntico em ambas as versões**.

### 3. Diff estrutural do OpenAPI

| Métrica | v1.17.20 | v1.18.8 |
| --- | --- | --- |
| Versão OpenAPI | `3.1.0` | `3.1.0` |
| `info.title` | `opencode` | `opencode` |
| `info.version` | `1.0.0` | `1.0.0` |
| Total de paths | 162 | 162 (idênticos) |
| Paths exclusivos | 0 | 0 |
| Total de schemas | 472 | 472 (idênticos) |
| Schemas exclusivos | 0 | 0 |
| `components.securitySchemes` | `{}` (vazio) | `{}` (vazio) |
| Tamanho do JSON serializado | 478 565 B | 478 637 B (+72 B) |

**Famílias de paths detectadas em ambas as versões:**

| Família | Estilo | Cancel | Prompt | SSE | Observação |
| --- | --- | --- | --- | --- | --- |
| **Legada `/session/*`** | singular | `POST /session/{id}/abort` | `POST /session/{id}/message` (sync) ou `/prompt_async` (async) | `GET /event` (global) ou `GET /api/session/{id}/event` (per-session) | Família alvo da SLICE-STAB-002 |
| **Moderna `/api/session/*`** | namespaced | `POST /api/session/{id}/interrupt` | `POST /api/session/{id}/prompt` | `GET /api/session/{id}/event` | Mais granular; pode ser adotada em revisão futura |
| **Experimental `/experimental/*`** | — | — | — | — | Não usar; marcado `experimental` |
| **TUI `/tui/*`** | — | — | — | — | Controles do terminal UI; não relevantes para o adapter |

**Achados:**

- **As duas versões têm o mesmo conjunto de paths e operações** — o diff binário de 72 B é compatível com melhorias incrementais em descrições, exemplos ou metadados, sem mudança estrutural.
- A família **legada `/session/*` é exatamente o que o usuário recomendou** (`POST /session`, `/session/{id}/message`, `/session/{id}/prompt_async`, `/session/{id}/abort`, `GET /event`, `GET /global/health`).
- A família `/api/session/*` é mais nova e mais granular; pode ser adotada em revisão futura, mas a SLICE-STAB-002 mantém `/session/*` por consistência com a recomendação.

### 4. Versão alvo — decisão

| Critério | v1.17.20 | v1.18.8 |
| --- | --- | --- |
| Contrato HTTP comprovado | ✓ | ✓ |
| Lifecycle assíncrono utilizável (`/prompt_async`) | ✓ | ✓ |
| SSE ou mecanismo equivalente | ✓ (`/event`) | ✓ (`/event`) |
| Cancelamento funcional (`/abort`) | ✓ | ✓ |
| Autenticação funcional (Basic via `OPENCODE_SERVER_PASSWORD`) | ✓ | ✓ |
| Compatibilidade com o modelo/provider usado | ✓ | ✓ |
| Ausência de regressão crítica identificada | ✓ (sem diffs estruturais) | ✓ (sem diffs estruturais) |
| Possibilidade de fixar versão ou digest | ✓ (já temos o binário) | ✓ (`v1.18.8` com tag + SHA-256 do asset) |
| Release mais recente | ✗ (2025-…) | ✓ (`2026-07-28T06:07:54Z`) |

**Decisão:** **`v1.18.8`** é a versão alvo. Mesma compatibilidade de contrato que v1.17.20, release oficial de 2026-07-28, tag `v1.18.8` no GitHub, asset musl (`opencode-linux-x64-musl.tar.gz`) com SHA-256 `7e7a991aff33ae330308e88bfa8e6a5ea4125b468f4de6657b93d76200897a41` para uso no Dockerfile Alpine.

**Fallback:** v1.17.20 permanece disponível como fallback documentado na `ADR-0017`. Reativação exige nova spike + nova ADR.

## Como reproduzir

```bash
# v1.17.20 (host binary)
/home/vitor/.opencode/bin/opencode serve --hostname 127.0.0.1 --port 14101 --print-logs &

# v1.18.8 (baixado do GitHub release)
curl -fsSL https://github.com/anomalyco/opencode/releases/download/v1.18.8/opencode-linux-x64.tar.gz \
  -o /tmp/opencode-v1.18.8.tar.gz
mkdir -p /tmp/opencode-v1.18.8 && tar -xzf /tmp/opencode-v1.18.8.tar.gz -C /tmp/opencode-v1.18.8
/tmp/opencode-v1.18.8/opencode serve --hostname 127.0.0.1 --port 14102 --print-logs &

# Capturar OpenAPI e validar contrato
curl -sS http://127.0.0.1:14101/doc -o /tmp/openapi-v1.17.20.json
curl -sS http://127.0.0.1:14102/doc -o /tmp/openapi-v1.18.8.json
curl -sS http://127.0.0.1:14101/global/health
curl -sS -X POST -H 'Content-Type: application/json' -d '{}' http://127.0.0.1:14101/session

# Validar auth (com OPENCODE_SERVER_PASSWORD=test123_* no env)
curl -sS -o /dev/null -w "no auth: HTTP %{http_code}\n" http://127.0.0.1:14101/global/health
curl -sS -u "opencode:test123_aaa" -o /dev/null -w "with auth: HTTP %{http_code}\n" http://127.0.0.1:14101/global/health

# Limpeza
kill %1 %2 2>/dev/null
```

## Consequências para a SLICE-STAB-002

* **`ADR-0017`** registra o pin de versão `v1.18.8` (musl) e o digest `7e7a991aff33ae330308e88bfa8e6a5ea4125b468f4de6657b93d76200897a41`.
* **`Spec 005`** é atualizada para refletir a família `/session/*` (singular), `prompt_async` para lifecycle assíncrono, `/global/health` para health, `/event` para SSE, `/abort` para cancelamento, Basic Auth via `OPENCODE_SERVER_PASSWORD` (não via OpenAPI `securitySchemes`).
* **`OpenCodeAdapter`** deve usar exclusivamente as rotas comprovadas, enviar Basic Auth via header `Authorization`, validar `Content-Type: application/json`, rejeitar `text/html` como `UpstreamContractMismatch` e registrar versão upstream + checksum do contrato em cada execução.
* **`poc/opencode-container/Dockerfile`** baixa o asset musl do GitHub release com SHA-256 fixo, fail-fast (`SHELL pipefail`, `set -eux`, `command -v opencode`, `opencode --version`), roda como `ocab` (uid 10001), expõe `/global/health` no healthcheck e não depende de bind-mount do binário do host.

## Referências relacionadas

* `docs/adr/0017-pin-opencode-version.md` — pin de versão (a ser criada).
* `docs/specs/005-opencode-runner/spec.md` — atualização do contrato HTTP.
* `docs/discovery/011-opencode-real-validation.md` — primeira tentativa de validação ponta a ponta que expôs o contract drift.
* `docs/open-questions.md` — `OQ-200` (Estabilização do Epic 1) e `OQ-201` (contract drift).
* `poc/opencode-container/Dockerfile` — runner image (a ser corrigido).
* `src/OcabBridge.Api/Adapters/OpenCodeAdapter.cs` — adapter (a ser alinhado).
