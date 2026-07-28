# Discovery 011 — Validação ponta a ponta do OpenCode real

> **Status:** Blocked (contract mismatch entre `OpenCodeAdapter` e o binário `opencode serve` v1.17.20 observado)
> **Data:** 2026-07-28
> **Bloqueia:** Fechamento completo do stabilization gate da Fase 1 (Epic 1) → Epic 2

## Contexto

O stabilization gate do Epic 1 inclui validar o OpenCode real em container. O Discovery 003 registrou que `ghcr.io/sst/opencode:latest` retornou `denied` neste ambiente, e propôs duas estratégias alternativas:

1. Construir imagem local com instalador oficial (`curl -fsSL https://opencode.ai/install | sh`).
2. Bind-mount do binário `opencode` instalado no host em `/usr/local/bin/opencode` no container.

Ambas as estratégias foram tentadas nesta sessão. Os resultados estão abaixo.

## Ambiente

* Host: Ubuntu 22.04 (kernel conforme `uname -r`).
* Docker: 29.6.2.
* Docker Compose: v5.3.1 (plug-in `docker compose`).
* Binário `opencode` no host: `/home/vitor/.opencode/bin/opencode`, **versão 1.17.20**, 180.5 MB, permissões `755 vitor:vitor`.
* Acesso à internet: `https://opencode.ai/install` retornou HTTP 200 (após 1 redirect) e 13.4 KB de script de instalação; `ghcr.io/v2/sst/opencode/manifests/latest` retornou HTTP 401 (mesma limitação observada no Discovery 003).

## Estratégia aplicada

Foi tentada a **estratégia 1 (instalador oficial)** usando o Dockerfile existente em `poc/opencode-container/Dockerfile`. Em paralelo, foi feita a **estratégia 2 (bind-mount do binário do host)**. Nenhuma das duas levou o container a executar `opencode serve` com sucesso. Foi então executado o fallback definitivo — **`opencode serve` direto do host** — para validar o contrato HTTP.

## Construção da imagem

```bash
docker build -t ocab-opencode-runner:dev-s115 poc/opencode-container
```

**Resultado:** build terminou com sucesso (cache indicou que a layer de install já estava presente em build anterior). Imagem `ocab-opencode-runner:dev-s115` disponível em `docker images`.

**Falha silenciosa detectada (bug estrutural):** o step de install no Dockerfile termina com `... | tee /tmp/ocab-install.log || echo "WARN: ..."`. Como `tee` retorna 0 mesmo quando o pipe anterior falha, o fallback `WARN` nunca é emitido e o build "passa" mesmo quando o `sh /tmp/ocab-opencode-install.sh` retorna diferente de zero. A consequência é que `/usr/local/bin/opencode` **não existe** dentro da imagem, apesar do build concluir.

Evidência:

```bash
docker run --rm ocab-opencode-runner:dev-s115
# → exec /usr/local/bin/opencode: no such file or directory
```

**Recomendação:** corrigir o Dockerfile removendo o `tee` do pipeline (ou trocando o operador `||` por algo que dependa apenas do `sh`):

```dockerfile
RUN curl -fsSL https://opencode.ai/install -o /tmp/ocab-opencode-install.sh && \
    sh /tmp/ocab-opencode-install.sh > /tmp/ocab-install.log 2>&1 || \
    { echo "WARN: opencode.ai install failed; bind-mount OpenCode binary at runtime" >&2; exit 0; }
```

Esta correção está fora do escopo deste PR — registrada como follow-up.

## Execução do container

Com bind-mount do binário do host:

```bash
docker run -d --rm \
  --name ocab-opencode-validation \
  --user 10001:10001 --read-only \
  --tmpfs /tmp:size=128m,mode=1777 \
  --tmpfs /home/ocab/.cache:size=64m \
  --security-opt no-new-privileges --cap-drop ALL \
  -p 127.0.0.1:14096:4096 \
  --memory 4G --cpus 2.0 --pids-limit 512 \
  -v /home/vitor/.opencode/bin/opencode:/usr/local/bin/opencode:ro \
  ocab-opencode-runner:dev-s115
```

**Resultado:** container iniciou (ID retornado) mas morreu imediatamente. Reexecução em foreground:

```
exec /usr/local/bin/opencode: no such file or directory
```

A causa mais provável é que o `ro` flag combinado com permissões de arquivo impediu o bind-mount efetivo do binário dentro do container. O bug do Dockerfile acima também impacta: se `/usr/local/bin/opencode` não existia no layer anterior, o bind-mount não pôde substituir (em alguns runtimes, bind-mount sobre arquivo inexistente falha silenciosamente).

## Fallback aplicado: `opencode serve` no host

Para validar o contrato HTTP sem dependência do container, foi iniciado:

```bash
/home/vitor/.opencode/bin/opencode serve --hostname 127.0.0.1 --port 14097 --print-logs > /tmp/opencode-host-serve.log 2>&1 &
```

**Resultado:** processo ativo (PID capturado), escutando em `127.0.0.1:14097`. Log inicial:

```
Warning: OPENCODE_SERVER_PASSWORD is not set; server is unsecured.
timestamp=... level=INFO ... message=loading path=/home/vitor/.config/opencode/config.json
...
opencode server listening on http://127.0.0.1:14097
```

**Achado positivo:** a variável `OPENCODE_SERVER_PASSWORD` é a chave de auth (HTTP Basic quando definida; servidor unsecured quando ausente). Isso **valida a forma de auth documentada em OQ-091** (Basic) e fecha essa questão.

## Contrato HTTP — smoke test

Cada chamada abaixo foi feita contra `127.0.0.1:14097`.

| # | Verificação | Comando | Resultado | Conclusão |
| - | --- | --- | --- | --- |
| 1 | Container real do OpenCode iniciado (host fallback) | `opencode serve` rodando, porta 4096/14097 listening | PASS | binário executa; serve responde |
| 2 | Sessão real criada | `POST /sessions` com `{"title":"smoke-test"}` | HTTP 200 + **HTML SPA (2884 B)** | **FAIL**: adapter espera JSON `{"sessionId":"...","status":"ready"}` |
| 3 | Prompt enviado | `POST /sessions/{id}/prompt` | HTTP 200 + **HTML SPA** | **FAIL**: resposta não é JSON |
| 4 | Resultado recebido | `GET /sessions/{id}/events` | não exercitado (depende de sessão válida) | **NOT TESTED** |
| 5 | Cancelamento confirmado | `POST /sessions/{id}/cancel` | HTTP 200 + **HTML SPA** | **FAIL**: resposta não é confirmação JSON |
| 6 | Timeout confirmado | dependeria de sessão válida com prompt longo | não exercitado | **NOT TESTED** |
| 7 | Repositório original inalterado | `git rev-parse HEAD` antes/depois | HEAD inalterado durante o smoke test | PASS (trivial — nenhuma escrita feita) |
| 8 | Reinício do runner tratado | não exercitado neste lane | — | **NOT TESTED** |
| 9 | Erro de autenticação tratado | com `OPENCODE_SERVER_PASSWORD` definido, chamada sem Basic | não exercitado | **NOT TESTED** |
| 10 | Indisponibilidade do runner tratada | bridge deveria reportar erro com runner down | não exercitado neste lane | **NOT TESTED** |

## Comportamento de cancelamento e timeout

Não foi possível validar via adapter porque o contrato HTTP de cancelamento/timeout depende de sessão válida (passo 2 falhou).

## Repositório original intocado

Nenhuma escrita foi feita no repositório. `git rev-parse HEAD` permaneceu inalterado durante todo o smoke test. Isso é trivial porque o adapter não conseguiu sequer criar sessão.

## Reinício do runner, auth error, indisponibilidade

Não exercitados neste lane. Estão listados como **NOT TESTED** e devem ser cobertos por uma próxima iteração (slice de estabilização adicional ou pelo smoke test do PR do Slice 2.1.1).

## Conclusão

| Item | Estado |
| --- | --- |
| Imagem Docker do OpenCode Runner construída | PASS (com bug documentado) |
| Container executa `opencode serve` | **FAIL** (binário ausente na imagem; bind-mount falhou) |
| `opencode serve` executa do host | PASS |
| Endpoints JSON esperados pelo adapter | **FAIL** — v1.17.20 retorna HTML SPA |
| Auth Basic via `OPENCODE_SERVER_PASSWORD` | PASS (validado por observação) |
| Demais critérios (cancel, timeout, restart, repo intocado) | NOT TESTED ou PASS trivial |

### Decisão sobre o stabilization gate

O gate **não fecha** com este PR. A causa raiz é **contract drift** entre:

* `src/OcabBridge.Api/Adapters/OpenCodeAdapter.cs` (assume JSON em `/sessions`, `/sessions/{id}/prompt`, `/sessions/{id}/cancel`).
* `docs/specs/005-opencode-runner/spec.md` (documenta os mesmos endpoints JSON).
* OpenCode Server real v1.17.20 (serve HTML SPA nessas rotas — a API programática provavelmente está em outro conjunto de endpoints não documentado na spec atual, ou requer uma versão mais recente do upstream).

### Recomendações

1. **Bloqueador:** criar OQ nova (registrada como `OQ-201` em `docs/open-questions.md`) descrevendo o contract drift. Decidir entre (a) atualizar o adapter e a spec para casar com o upstream real v1.17.20+ ou (b) fixar uma versão upstream conhecida (ex.: v1.18.x ou superior) e atualizar a imagem/Dockerfile para instalá-la corretamente.
2. **Bug do Dockerfile:** corrigir a lógica de install para falhar alto (ou registrar warning de forma confiável) em vez de swallow com `tee`.
3. **Bind-mount:** investigar a falha específica do bind-mount `-v host:container:ro` neste ambiente antes de re-tentar.
4. **Próximo slice recomendado:** o gate de estabilização continua pendente até `OQ-201` ser fechada. Após, abrir novo PR (provavelmente `chore(stabilization): close Phase 1 runtime and quality gates — follow-up`) cobrindo:
   * Correção do Dockerfile.
   * Bind-mount funcional ou estratégia de install alternativa.
   * Re-validação do contrato com o adapter ajustado.
   * Cobertura dos critérios 4, 6, 8, 9, 10 da tabela acima.

## Questões abertas afetadas

* **OQ-021** (OpenCode lifecycle): já `Resolved` em iteração anterior (decisão sobre ciclo de vida do container). Esta descoberta não a afeta.
* **OQ-091** (Auth OpenCode): a observação de que `OPENCODE_SERVER_PASSWORD` habilita Basic auth confirma a forma Basic documentada em iteração anterior. **Esta lane marca OQ-091 como `Resolved`.** A validação de ponta a ponta do adapter contra o server real permanece em aberto via `OQ-201`.
* **OQ-201** (nova): contract drift entre `OpenCodeAdapter` e OpenCode Server real — registrada separadamente.

## Como reproduzir

```bash
# 1. Confirmar binário do host
/home/vitor/.opencode/bin/opencode --version  # esperado: 1.17.20

# 2. Subir serve do host
/home/vitor/.opencode/bin/opencode serve --hostname 127.0.0.1 --port 14097 --print-logs &
sleep 5

# 3. Health check
curl -sS -o /tmp/h.html -w "HTTP %{http_code}\n" http://127.0.0.1:14097/health
# Esperado (observado): HTTP 200 + HTML (2884 B) — não JSON

# 4. Sessão
curl -sS -o /tmp/s.html -w "HTTP %{http_code}\n" -X POST -H 'Content-Type: application/json' \
  -d '{"title":"smoke-test"}' http://127.0.0.1:14097/sessions
# Esperado: HTTP 200 + HTML — adapter espera JSON

# 5. Limpeza
kill %1
```