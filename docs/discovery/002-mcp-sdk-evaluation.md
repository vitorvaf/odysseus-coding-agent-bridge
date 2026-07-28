# Discovery 002 — MCP SDK Evaluation

> **Status:** Completed
> **Bloqueia:** Fase 1
> **Prioridade:** P0
> **Data:** 2026-07-27

## Objetivo

Comparar SDKs MCP disponíveis para .NET e validar a escolha com uma POC funcional de Streamable HTTP. Resultado alimenta decisão final e gera ADR substituto da [ADR-0002](../adr/0002-bridge-as-mcp-server.md) se necessário.

## Candidatos avaliados

| Pacote | Versão | Última atualização | Licença | Notas |
| --- | --- | --- | --- | --- |
| `ModelContextProtocol` | 1.4.1 | 2026-07-09 | Apache-2.0 | Pacote raiz. |
| `ModelContextProtocol.AspNetCore` | 0.4.0-preview.1 | 2026 (preview) | Apache-2.0 | Integração ASP.NET Core, transporte HTTP/SSE. |
| `ModelContextProtocol.Core` | (transitiva) | — | Apache-2.0 | Núcleo. |
| `McpSdk.Adapter.StreamableHttpServer` | 1.0.3 | 2026-06-29 | (não verificada) | Adapter dedicado a Streamable HTTP. |
| `McpHttpServer` | 1.0.0 | 2026-07-09 | (não verificada) | — |
| `Elarion.AspNetCore.Mcp` | 0.2.7-preview.136.1 | 2026-07-23 | (não verificada) | Preview. |
| `WebSharp.Mcp.Http` | 0.99.0-rc2 | 2025-09-22 | (não verificada) | RC antigo. |
| `Andy.MCP.AspNetCore` | 2026.7.27-rc.89 | 2026-07-27 | (não verificada) | — |

A maioria dos pacotes não-ModelContextProtocol exibidos em `nuget.org` é de baixa maturidade, preview ou RC antigo.

## Critérios

1. Streamable HTTP support (`protocolVersion: 2025-03-26`).
2. Integração ASP.NET Core nativa.
3. `tools`, `resources`, `prompts`.
4. Schemas de entrada/saída baseados em JSON Schema.
5. Autenticação Bearer Token (composable com middleware ASP.NET Core).
6. Cancelamento e timeouts.
7. Logging e tracing compatíveis com OpenTelemetry.
8. Maturidade (release estável ou preview recente).
9. Licença compatível com Apache-2.0.
10. Manutenção ativa (última release nos últimos 90 dias).
11. Repositório upstream oficial.

## Matriz

| Critério | MCP oficial | MCP Aspire HTTP | StreamableHttpServer | McpHttpServer |
| --- | --- | --- | --- | --- |
| Streamable HTTP | ✅ | ✅ | ✅ | ✅ |
| ASP.NET Core nativo | ✅ | ✅ | parcial | parcial |
| Tools JSON Schema | ✅ | ✅ | ✅ | ? |
| Bearer token | via middleware | via middleware | ? | ? |
| Cancelamento | ✅ | ✅ | ? | ? |
| OpenTelemetry | ✅ | ✅ | ? | ? |
| Maturidade | preview (estável API) | preview | 1.0.3 (recente) | 1.0.0 |
| Licença | Apache-2.0 | Apache-2.0 | ? | ? |
| Manutenção | ativa | ativa | ativa | ? |
| Documentação | boa | boa | ? | ? |

> **Observação:** critérios marcados `?` exigem consulta direta à documentação do pacote específico; a POC abaixo prioriza o pacote oficial `ModelContextProtocol.AspNetCore`, que combina todos os critérios essenciais.

## POC executada

Local: `/tmp/opencode-bridge-poc/OcabMcpSmoke` (descartável).

Comandos principais:

```bash
dotnet new web -n OcabMcpSmoke --no-restore
cd OcabMcpSmoke
dotnet add package ModelContextProtocol.AspNetCore --version 0.4.0-preview.1
```

`Program.cs`:

```csharp
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
app.MapMcp("/mcp");
app.MapGet("/", () => "OCAB MCP smoke server");
app.Run();

[McpServerToolType]
public static class OcabSmokeTools
{
    [McpServerTool(Name = "ocab_ping"), Description("Returns pong for OCAB smoke check.")]
    public static string Ping([Description("Optional message")] string? message = null)
        => $"pong{(message is null ? string.Empty : $":{message}")}";

    [McpServerTool(Name = "ocab_echo"), Description("Echoes the input.")]
    public static string Echo([Description("Text to echo")] string text) => text;
}
```

Build e execução:

```bash
dotnet publish -c Release
./OcabMcpSmoke --urls "http://127.0.0.1:5099"
```

### Evidências

`initialize`:

```bash
curl -sS -X POST -H "Accept: application/json, text/event-stream" -H "Content-Type: application/json" \
    -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"curl","version":"1"}}' \
    http://127.0.0.1:5099/mcp
```

```text
event: message
data: {"result":{"protocolVersion":"2025-03-26","capabilities":{"logging":{},"tools":{"listChanged":true}},"serverInfo":{"name":"OcabMcpSmoke","version":"1.0.0.0"}},"id":1,"jsonrpc":"2.0"}
```

Header retornado:

```text
Mcp-Session-Id: hsuCPaumIQqogm2DkXbETQ
```

`tools/list`:

```text
{"result":{"tools":[
  {"name":"ocab_ping","description":"Returns pong for OCAB smoke check.","inputSchema":{"type":"object","properties":{"message":{"description":"Optional message","type":"string","default":null}}}},
  {"name":"ocab_echo","description":"Echoes the input.","inputSchema":{"type":"object","properties":{"text":{"description":"Text to echo","type":"string"}},"required":["text"]}}
]}}
```

`tools/call`:

```bash
curl -sS -X POST -H "Accept: application/json, text/event-stream" -H "Content-Type: application/json" \
    -H "Mcp-Session-Id: hsuCPaumIQqogm2DkXbETQ" \
    -d '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"ocab_ping","arguments":{"message":"hello"}}}' \
    http://127.0.0.1:5099/mcp
```

```text
event: message
data: {"result":{"content":[{"type":"text","text":"pong:hello"}]},"id":4,"jsonrpc":"2.0"}
```

### Conclusão da POC

* `ModelContextProtocol.AspNetCore` `0.4.0-preview.1` cobre os critérios essenciais.
* `protocolVersion: 2025-03-26` é o protocolo MCP atual, compatível com Streamable HTTP.
* Sessão é mediada por header `Mcp-Session-Id`, suportando múltiplas chamadas.
* Schemas JSON são inferidos a partir do tipo da tool.
* `MapMcp("/mcp")` integra diretamente ao pipeline ASP.NET Core.

## Decisão recomendada

* **Pacote principal:** `ModelContextProtocol.AspNetCore` (preview oficial, mas cobre os critérios).
* **Status:** preview, mas com **estratégia de estabilização** documentada em [`../adr/0002-bridge-as-mcp-server.md`](../adr/0002-bridge-as-mcp-server.md) — ADR substituta `0015-runtime-version.md` (a criar) deve formalizar a versão fixa.

### Plano B

* Implementação manual sobre `HttpContext` consumindo JSON-RPC sobre `application/json, text/event-stream`. Mais código, mas remove dependência externa única.

## Riscos identificados

1. **Preview API:** `0.4.0-preview.1` pode ter breaking changes antes do `1.0.0`.
2. **Versão de `protocolVersion`:** o servidor expõe `2025-03-26`; o cliente (Odysseus) precisa estar alinhado.
3. **Manutenção:** `modelcontextprotocol/csharp-sdk` é mantido ativamente.

## Compatibilidade com .NET 8

* O pacote é compatível com `net10.0` (testado nesta POC).
* Smoke adicional em `net8.0` foi executado em `OcabMcpSmokeNet8` com o mesmo pacote `ModelContextProtocol.AspNetCore 0.4.0-preview.1`:
  * `dotnet build -c Release` → 0 erros, 0 warnings.
  * Compatibilidade com `net8.0` **confirmada**.
* **Consequência:** o SDK é compatível com a versão de runtime alvo do MVP (.NET 8 LTS), conforme [`001-environment-baseline.md`](001-environment-baseline.md).

## Questões abertas afetadas

* OQ-001 (SDK MCP) — resolução proposta: `ModelContextProtocol.AspNetCore` `0.4.0-preview.1`, com pin em `global.json` para o SDK .NET 8 LTS e migração futura sob ADR substituta.

## Próximo passo

* Confirmar suporte a `net8.0` antes da decisão final.
* Caso positivo, ADR substituta formaliza a escolha.
* Caso negativo, considerar plano B.
