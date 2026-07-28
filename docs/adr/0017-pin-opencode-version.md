# ADR-0017 — Pin OpenCode Version and Generated HTTP Contract

## Status

Proposed

## Contexto

O `OpenCodeAdapter` (`src/OcabBridge.Api/Adapters/OpenCodeAdapter.cs`) e a `Spec 005` (`docs/specs/005-opencode-runner/spec.md`) assumem um contrato HTTP que não bate com o OpenCode Server real: rotas no plural (`/sessions`, `/sessions/{id}/prompt`, `/sessions/{id}/cancel`) que o servidor responde como SPA fallback em `text/html` em vez de `application/json`. Esse drift foi registrado em `OQ-201` e documentado em `docs/discovery/011-opencode-real-validation.md`.

O contrato real foi capturado em `docs/discovery/012-opencode-contract-spike.md` via `GET /doc` (OpenAPI 3.1.0 emitido em runtime pelas versões v1.17.20 e v1.18.8) e validado com curl + Basic Auth. A spike comparou as duas versões e concluiu que **v1.18.8** é a versão alvo:

* Mesma estrutura de paths (162 paths idênticos) e schemas (472 schemas idênticos) entre v1.17.20 e v1.18.8 — sem regressão crítica.
* `v1.18.8` é a release mais recente do upstream (`2026-07-28T06:07:54Z`).
* Asset musl (`opencode-linux-x64-musl.tar.gz`) tem SHA-256 publicado pelo GitHub release (`7e7a991aff33ae330308e88bfa8e6a5ea4125b468f4de6657b93d76200897a41`).
* `ghcr.io/anomalyco/opencode` retornou HTTP 401 neste ambiente (mesmo bloqueio do `ghcr.io/sst/opencode` registrado em `011-...`), então o `poc/opencode-container/Dockerfile` baixa o binário diretamente do GitHub release em vez de pull do registry.

Esta ADR fixa a versão alvo e o contrato utilizado; **não** substitui a `ADR-0009` (que decidiu que OpenCode é o primeiro runner).

## Decisão

1. **Versão fixada:** `v1.18.8` (tag do GitHub release `anomalyco/opencode`).
2. **Mecanismo de instalação:** download direto do asset musl do GitHub release, com verificação de SHA-256. Justificativa: `opencode.ai/install` sempre baixa a release mais recente (sem controle de versão) e o registry `ghcr.io/anomalyco/opencode` não está acessível neste ambiente. Manter o install dentro do `Dockerfile` (em vez de bind-mount do host) garante reprodutibilidade e fail-fast.
3. **Asset fixado (Alpine/musl):** `https://github.com/anomalyco/opencode/releases/download/v1.18.8/opencode-linux-x64-musl.tar.gz` com SHA-256 `7e7a991aff33ae330308e88bfa8e6a5ea4125b468f4de6657b93d76200897a41`.
4. **Contrato utilizado:** família legada `/session/*` (singular), conforme recomendação em `docs/discovery/012-...` e nos critérios de aceite da `SLICE-STAB-002`. Em particular:
   * `GET /global/health` — liveness (substitui `/health`, que cai em SPA fallback).
   * `POST /session` — cria sessão.
   * `POST /session/{sessionID}/prompt_async` — envia prompt assíncrono (compatível com a máquina de estados persistente do bridge).
   * `GET /event` — stream SSE global (compatível com adapter e bridge atuais; pode evoluir para `GET /api/session/{sessionID}/event` por sessão).
   * `POST /session/{sessionID}/abort` — cancelamento.
   * `GET /session/status` — status global.
5. **Geração do client:** **HttpClient manual** com `System.Net.Http.Json` (já presente no projeto) para os endpoints comprovados. **Code generation (NSwag / Kiota / openapi-generator)** fica como evolução futura se o contrato crescer; **sidecar TypeScript** está rejeitado por adicionar dependência runtime Node não justificada no MVP.
6. **Estratégia de compatibilidade:** o adapter é implementado estritamente contra o OpenAPI 3.1.0 fixado em `v1.18.8`. Qualquer rota nova ou mudança de schema exige nova spike + nova ADR substituta. O adapter nunca assume rota não documentada pelo OpenAPI fixado.
7. **Política de atualização:** bump de versão exige:
   * Re-rodar a spike comparativa (`docs/discovery/012-...`) entre a versão atualmente pinada e a candidata.
   * Nova ADR substituta referenciando `ADR-0017`.
   * Atualização do asset e do SHA-256 no `Dockerfile`.
   * Atualização da `Spec 005` se houver mudança de rotas ou schemas.
   * Smoke test ponta a ponta verde (cliente MCP → bridge → OpenCode → repositório piloto) **antes** do merge do bump.
8. **Tratamento de contract drift:**
   * O adapter registra em cada execução: `UpstreamVersion` (vinda de `GET /global/health`) e `ContractChecksum` (SHA-256 do JSON OpenAPI capturado em build/start).
   * Responses com `Content-Type: text/html` são rejeitados como `UpstreamContractMismatch` (código interno da bridge) e a execução transita para `Failed` com `runner_contract_mismatch`.
   * 401/404/409/429/5xx são normalizados em códigos padronizados (ver `Spec 005` § Erros).
   * HTML inesperado é a única classe de drift atualmente modelada; outros casos (mudança de schema em JSON válido) serão cobertos por nova spike se surgirem.
9. **Auth:** Basic Auth com usuário fixo `opencode` e senha vinda de `OPENCODE_SERVER_PASSWORD`. Senha fora do repo (env file ou secret manager); o `compose.yaml` referencia via `${OCAB_OPENCODE_SERVER_PASSWORD:-__SET_ME__}`.

`Status: Proposed` será promovido para `Accepted` somente após o smoke test ponta a ponta (cliente MCP → `run_create` → bridge → OpenCode `v1.18.8` fixado → sessão real → prompt read-only → eventos → relatório) ser executado e a `OQ-200` + `OQ-201` forem fechadas com referência a esse smoke test.

## Alternativas consideradas

* **Permanecer em `v1.17.20`.** Rejeitada — a release mais recente (`v1.18.8`) tem o mesmo contrato HTTP comprovado e a spike não identificou regressão, então não há motivo para segurar a versão antiga. Fixar `v1.17.20` arriscaria acumular débito de upgrade.
* **Pin via digest do `ghcr.io/anomalyco/opencode`.** Rejeitada para o MVP — o registry está inacessível neste ambiente (HTTP 401). Quando o registry voltar a estar acessível, uma ADR substituta pode mover a fixação para o digest do `ghcr.io`.
* **Pin via `opencode.ai/install` (sempre latest).** Rejeitada — `opencode.ai/install` baixa a release mais recente sem mecanismo de pin; introduz drift silencioso e quebra reprodutibilidade.
* **Sidecar TypeScript usando o SDK oficial.** Rejeitada — adiciona dependência runtime Node não justificada pelo MVP; aumenta superfície de ataque e exige container extra para o adapter.
* **Code generation C# a partir do OpenAPI (NSwag / Kiota / openapi-generator).** Adiada — o OpenAPI é grande (478 KB, 162 paths, 472 schemas) e gera código volumoso para endpoints que não usamos. HttpClient manual é suficiente para os ~6 endpoints que o bridge consome. Pode ser revisitada se o contrato crescer e o número de endpoints usados aumentar.
* **Adotar a família `/api/session/*` em vez de `/session/*`.** Rejeitada para a SLICE-STAB-002 — a `/api/session/*` é mais nova e mais granular, mas a família legada `/session/*` é a recomendada pelo usuário e suficiente para o read-only do MVP. Pode ser adotada em revisão futura da `Spec 005`.

## Consequências positivas

* Adapter deixa de assumir contrato errado e passa a conversar com o OpenAPI real — `OQ-201` fica endereçada.
* Dockerfile fail-fast aborta builds se o install falhar, eliminando o bug `... | tee log || echo WARN` documentado em `011-...`.
* Pin por tag + SHA-256 permite reprodutibilidade bit-a-bit do container.
* `ContractChecksum` registrado em cada execução fornece trilha de auditoria para detectar drift futuro.
* Política de upgrade exige ADR + smoke test, evitando bumps silenciosos.

## Consequências negativas

* Versão pinada significa que benefícios da `v1.18.9+` (quando existir) só são absorvidos após spike + ADR + smoke test. Custo operacional conhecido.
* Pin por download direto do GitHub release introduz dependência do GitHub como fonte de binário. Mitigação: mirror interno se o ambiente corporativo exigir; ADR substituta para mover para o `ghcr.io` quando o registry voltar a estar acessível.
* Client manual exige manutenção explícita quando o contrato mudar; sem gerador, há risco de divergência se a revisão for descuidada. Mitigação: política de upgrade via ADR + smoke test; contract tests automatizados (Commit 4 da `SLICE-STAB-002`) detectam drift em CI.

## Riscos

* **Drift silencioso entre a fixação e o que o runner realmente roda.** Mitigação: `command -v opencode` + `opencode --version` no `Dockerfile` (fail-fast); `UpstreamVersion` e `ContractChecksum` registrados em cada execução; contract tests em CI que comparam contra o OpenAPI capturado no build.
* **Mudança de contrato em versão futura sem ADR.** Mitigação: smoke test ponta a ponta verde é pré-condição do bump; contract tests detectam mudança de schema antes do bump chegar a produção.
* **Falha de acesso ao GitHub release no build.** Mitigação: ADR substituta para mover para mirror ou para o `ghcr.io` quando disponível; cache local de artefatos na CI (a ser avaliado).
* **False positive em `UpstreamContractMismatch` quando o servidor retorna JSON válido mas com schema novo.** Mitigação: a política de upgrade via ADR + smoke test é a rede de segurança primária; se ocorrer em produção, é classificado como `runner_contract_mismatch` e a execução falha com Run terminal, forçando revisão.

## Impacto operacional

* `poc/opencode-container/Dockerfile` passa a baixar o binário musl do GitHub release com SHA-256 fixado em `ARG`, falha alto se o install falhar, expõe `/global/health` no `HEALTHCHECK`, roda como `ocab` (uid 10001), sem Docker socket, sem bind-mount do host.
* `compose.yaml` adiciona `OPENCODE_SERVER_PASSWORD: ${OCAB_OPENCODE_SERVER_PASSWORD:-__SET_ME__}` no serviço `ocab-opencode-runner` e documenta em `.env.example`.
* `src/OcabBridge.Api/Adapters/OpenCodeAdapter.cs` é reescrito para usar exclusivamente a família `/session/*` (singular), enviar Basic Auth, validar `Content-Type`, rejeitar `text/html` como `UpstreamContractMismatch`, suportar cancelamento via `/abort`, aplicar timeout via `HttpClient.Timeout`, correlacionar sessão e `runId` e registrar `UpstreamVersion` + `ContractChecksum`.
* `tests/OcabBridge.ContractTests/` ganha testes reais contra o OpenCode `v1.18.8` (rotas, Basic Auth, desserialização, rejeição de HTML, normalização de 401/404/409/429/5xx, timeout, cancelamento).
* `tests/OcabBridge.IntegrationTests/` ganha testes ponta a ponta (Compose sobe OpenCode real, bridge cria sessão via `run_create`, prompt read-only, eventos persistidos, cancelamento, timeout, restart do runner, repositório piloto inalterado).
* CI em `.github/workflows/ci.yml` passa a falhar se qualquer contract test quebrar.

## Impacto de segurança

* Credencial Basic Auth lida de variável de ambiente (não commitada); `compose.yaml` usa placeholder `__SET_ME__`.
* Sem bind-mount do binário do host (que poderia vazar credenciais do host para o container).
* Runner continua não root, sem Docker socket, `cap_drop: ALL`, `no-new-privileges` — invariantes mantidas.
* `UpstreamContractMismatch` evita que resposta HTML seja processada como JSON válido, eliminando vetor de injeção de markup / XSS via resposta do runner.

## Critérios para revisitar

* Publicação de `v1.18.9` ou superior — exige nova spike + ADR substituta.
* Adoção da família `/api/session/*` no lugar de `/session/*` — exige spike + revisão da `Spec 005`.
* Adoção de code generation (NSwag / Kiota / openapi-generator) — exige POC + ADR substituta.
* Mudança da fonte do binário (mirror interno, `ghcr.io` quando acessível, etc.) — exige ADR substituta.

## Referências relacionadas

* [`../discovery/012-opencode-contract-spike.md`](../discovery/012-opencode-contract-spike.md) — spike comparativa v1.17.20 × v1.18.8.
* [`../discovery/011-opencode-real-validation.md`](../discovery/011-opencode-real-validation.md) — tentativa anterior que expôs o contract drift.
* [`../../specs/005-opencode-runner/spec.md`](../../specs/005-opencode-runner/spec.md) — spec atualizada para refletir o contrato fixado.
* [`../../../poc/opencode-container/Dockerfile`](../../../poc/opencode-container/Dockerfile) — runner image com install fail-fast e pin.
* [`../../../src/OcabBridge.Api/Adapters/OpenCodeAdapter.cs`](../../../src/OcabBridge.Api/Adapters/OpenCodeAdapter.cs) — adapter alinhado.
* [`../../../compose.yaml`](../../../compose.yaml) — runner com `OPENCODE_SERVER_PASSWORD`.
* [`../open-questions.md`](../open-questions.md) — `OQ-200` (Estabilização do Epic 1) e `OQ-201` (contract drift).
* [`../planning/backlog.md`](../planning/backlog.md) — `SLICE-STAB-002`.
* [`0009-opencode-first-runner.md`](0009-opencode-first-runner.md) — ADR que decide OpenCode como primeiro runner (não substituída por esta).
* [`0015-runtime-version.md`](0015-runtime-version.md) — ADR que fixa .NET 8 LTS como runtime alvo (referência por transversalidade).
* [OpenCode release `v1.18.8`](https://github.com/anomalyco/opencode/releases/tag/v1.18.8) — fonte canônica da versão pinada.
