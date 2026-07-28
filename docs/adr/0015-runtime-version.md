# ADR-0015 — Versão runtime do Coding Agent Bridge

## Status

Accepted

## Contexto

O ambiente de desenvolvimento validado em [`001-environment-baseline.md`](../discovery/001-environment-baseline.md) apresenta simultaneamente:

* SDK `10.0.110` (default).
* Runtime `Microsoft.NETCore.App` `8.0.29` e `10.0.10`.

A documentação (`README.md`, `specs/001-platform-foundation/spec.md`, `backlog.md`) refere-se a ".NET 8" como baseline. O ambiente, todavia, expõe dois runtimes e o `dotnet build` usa o SDK mais recente por padrão. Sem um pin de versão, builds podem flutuar entre `net8.0` e `net10.0`, prejudicando reprodutibilidade.

## Decisão

* **Runtime alvo:** .NET 8 LTS (`Microsoft.NETCore.App 8.0.10` ou superior da família 8.x).
* **SDK alvo:** família 8.x — em ambiente onde o SDK default é 10.x, a solução do bridge traz um `global.json` fixando `8.x` (e registrando o feature band esperado, por exemplo `8.0.0` com `rollForward: latestFeature`).
* **Compatibilidade com .NET 10:** o SDK `ModelContextProtocol.AspNetCore 0.4.0-preview.1` foi validado em ambas as versões (`net8.0` e `net10.0`) em [`002-mcp-sdk-evaluation.md`](../discovery/002-mcp-sdk-evaluation.md), portanto a escolha de runtime **não** é determinada pela disponibilidade do SDK MCP.
* **Evolução:** qualquer migração para runtime superior (ex.: `net10.0`) exige nova ADR substituta que referencie esta, conforme cláusula de [`../../AGENTS.md`](../../AGENTS.md) sobre premissas bloqueadas.

## Alternativas consideradas

* **Manter .NET 10 como alvo:** rejeitada — versões LTS oferecem suporte prolongado; .NET 8 está em suporte mainstream até novembro de 2026 e em suporte estendido até novembro de 2028 (referência: [_Microsoft .NET release schedule_](https://dotnet.microsoft.com/en-us/platform/support/policy)). A escolha por .NET 10 agora trocaria esta ADR por uma de risco operacional mais alto no curto prazo.
* **Multi-target (`net8.0;net10.0`):** rejeitada — aumenta superfície de teste sem ganho para o MVP; pode ser reavaliada em fase pós-MVP.
* **Sem pin de SDK (aceitar o default 10.x):** rejeitada — sem pin, o `dotnet build`/`publish` em uma máquina nova pode usar SDK 10.0.110 enquanto o `runtimeconfig.json` do artefato publicado referencia `net8.0` — funciona, mas mistura famílias. Pin é obrigatório por reprodutibilidade.

## Consequências positivas

* Reprodutibilidade de builds independentemente da máquina de desenvolvimento.
* Aproveitamento do suporte LTS.
* Compatibilidade validada com o SDK MCP em ambas as direções (`net8.0` POC e `net10.0` POC).
* Redução de risco de breaking change durante o MVP.

## Consequências negativas

* Necessidade de manter `global.json` atualizado sempre que o feature band LTS avançar.
* Possível atrito se o time de desenvolvimento trabalhar majoritariamente em .NET 10.
* Em ambiente Docker, a imagem base precisa ser `mcr.microsoft.com/dotnet/aspnet:8.0` (não `:latest`).

## Riscos

* **Fim de suporte .NET 8 LTS:** novembro de 2026 (mainstream) e novembro de 2028 (extended). Rever antes do MVP público.
* **SDK MCP em preview (`0.4.0-preview.1`):** pode ter breaking change até `1.0.0`. Monitorar releases (especificado em [`002-mcp-sdk-evaluation.md`](../discovery/002-mcp-sdk-evaluation.md)).

## Impacto operacional

* `compose.yaml` deve referenciar a imagem `mcr.microsoft.com/dotnet/aspnet:8.0` (ou tag equivalente).
* `Dockerfile` deve usar o stage `mcr.microsoft.com/dotnet/sdk:8.0` no build.
* `global.json` presente na raiz da solução bridge.
* `.csproj` com `<TargetFramework>net8.0</TargetFramework>`.

## Impacto de segurança

* .NET 8 LTS recebe patches de segurança; ainda assim, exige rotina de atualização (R13 do risk register).

## Critérios para revisitar

* Caso o suporte LTS indique necessidade de migrar para versão superior.
* Caso o SDK MCP exija versão de runtime específica incompatível com .NET 8.
* Caso o ecossistema de agentes conectados exija .NET 10+.

## Referências relacionadas

* [`../discovery/001-environment-baseline.md`](../discovery/001-environment-baseline.md)
* [`../discovery/002-mcp-sdk-evaluation.md`](../discovery/002-mcp-sdk-evaluation.md)
* [`../specs/001-platform-foundation/spec.md`](../specs/001-platform-foundation/spec.md)
* [ADR-0002](0002-bridge-as-mcp-server.md)
* [`../open-questions.md`](../open-questions.md) — OQ-002
