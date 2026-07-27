# Spec 001 — Platform Foundation

## Status

Proposed

## Resumo

Estabelece a fundação técnica do OCAB: solução, serviços, configuração, Docker Compose, rede privada, PostgreSQL, health checks, observabilidade baseline e convenções modulares.

## Contexto

Antes de implementar qualquer fluxo de execução, é necessário ter:

* Solução .NET 8 com ASP.NET Core.
* Estrutura modular que acomode MCP, persistência, runners e relatórios.
* Compose capaz de subir todos os serviços.
* Rede Docker privada.
* PostgreSQL pronto.
* Configuração por ambiente.
* Health checks básicos.
* Baseline de observabilidade.

## Problema

Sem uma fundação consistente, qualquer feature do MVP ficaria comprometida pela falta de base. Começar por uma fundação bem desenhada reduz retrabalho.

## Objetivos

* Disponibilizar `ocab-bridge` e `ocab-postgres` funcionais.
* Definir estrutura modular para `bridge` (camadas Interface/Application/Domain/Infrastructure).
* Estabelecer rede `ocab-net`.
* Disponibilizar health checks e endpoints administrativos.
* Implementar logs estruturados e métricas mínimas.
* Documentar como configurar e iniciar a plataforma.

## Não objetivos

* Implementar runners além do OpenCode.
* Suportar multi-tenant.
* Publicar dashboards de observabilidade.

## Escopo funcional

* Solução .NET 8 com projetos separados por camada.
* Dockerfile do bridge multi-stage.
* Compose com bridge, postgres, opencode-runner e volume de artefatos.
* Configuração por ambiente via variáveis e arquivos `appsettings`.
* Health checks HTTP.
* Logs estruturados em JSON.
* Métricas Prometheus.
* Tracer OpenTelemetry.
* Endpoint `/health`, `/ready`, `/metrics`.
* Versionamento semântico do bridge (`1.0.0-MVP`).

## Requisitos funcionais

* **PF-FR-001** O Compose deve subir `ocab-bridge`, `ocab-postgres` e `ocab-opencode-runner` com `docker compose up`.
* **PF-FR-002** O bridge deve expor endpoint `/health` retornando `200 OK` quando saudável.
* **PF-FR-003** O bridge deve expor endpoint `/ready` retornando `200 OK` somente após conexão com PostgreSQL.
* **PF-FR-004** O bridge deve expor endpoint `/metrics` em formato Prometheus.
* **PF-FR-005** Logs estruturados devem incluir timestamp, level, service, environment, trace_id, run_id quando aplicável.
* **PF-FR-006** Toda configuração deve ser carregada de `appsettings.json` e sobrescrita por `appsettings.{Environment}.json` e variáveis de ambiente.
* **PF-FR-007** Placeholders `__SET_ME__` devem ser usados em arquivos versionados para valores sensíveis.
* **PF-FR-008** O bridge deve ter um endpoint administrativo para listar versão e build commit (quando aplicável).
* **PF-FR-009** O volume `ocab-artifacts` deve ser criado pelo Compose e referenciado por bridge e runner.
* **PF-FR-010** O volume `ocab-pgdata` deve ser criado pelo Compose e referenciado pelo postgres.

## Requisitos não funcionais

* **PF-NFR-001** O bridge deve iniciar em menos de 30 segundos em ambiente local.
* **PF-NFR-002** O bridge deve estar preparado para executar como usuário não root no container.
* **PF-NFR-003** Logs não devem conter segredos.
* **PF-NFR-004** Métricas devem ter cardinalidade limitada.
* **PF-NFR-005** Configurações devem ser validadas no startup.
* **PF-NFR-006** Health checks devem responder em menos de 1 segundo.

## Atores e componentes envolvidos

* Operador técnico.
* Bridge (servidor MCP).
* PostgreSQL.
* OpenCode Runner (preparado para uso).
* Artifact Storage (volume).

## Casos de uso

* Operador inicia a plataforma.
* Operador consulta saúde do bridge.
* Operador consulta métricas.
* Bridge registra evento de startup.

## Fluxos principais

* Inicialização: `docker compose up -d` → containers sobem → bridge valida config → conecta ao postgres → expõe endpoints.
* Verificação de saúde: operador faz `curl /health` → recebe 200 OK.

## Fluxos de erro

* Falha de configuração: bridge loga erro estruturado e sai com código não zero.
* Falha de conexão ao postgres: bridge tenta reconectar com backoff exponencial; readiness retorna `503` enquanto não conecta.

## Estados e transições

Não aplicável diretamente. Estados do `Run` são cobertos pela spec 003.

## Contratos

* [`../contracts/mcp-tools.md`](../contracts/mcp-tools.md) — ferramentas MCP.
* [`../contracts/errors.md`](../contracts/errors.md) — erros padronizados.

## Modelo de dados afetado

* `RunnerHealth` — entidade para registrar saúde observada dos serviços.
* Tabela de configuração (chave/valor) carregada no startup.

## Segurança

* Container do bridge preparado para executar como usuário não root.
* Senhas e tokens carregados via variável de ambiente ou arquivo montado.
* Logs passam por redaction antes de gravação.

## Observabilidade

* Logs JSON no stdout.
* Métricas Prometheus em `/metrics`.
* Tracer OTLP configurável.
* Eventos de startup e de falha.

## Estratégia de testes

* Unitários: configuração, validação de env, parsing de placeholders.
* Integração: subir postgres em container, validar conexão.
* Contrato: smoke test em `/health`, `/ready`, `/metrics`.
* Segurança: validar ausência de segredos em logs.

## Critérios de aceite

* **PF-AC-001** Dado um ambiente Linux com Docker e Docker Compose, quando o operador executar `docker compose up -d`, então `ocab-bridge`, `ocab-postgres` e `ocab-opencode-runner` iniciam sem erros.
* **PF-AC-002** Quando o operador consultar `/health`, recebe `200 OK` em menos de 1 segundo.
* **PF-AC-003** Quando o operador consultar `/metrics`, recebe saída Prometheus com métricas básicas.
* **PF-AC-004** Quando o bridge iniciar sem conexão com postgres, readiness retorna `503`.
* **PF-AC-005** Quando o bridge falhar ao validar configuração, registra log estruturado de erro e sai com código não zero.
* **PF-AC-006** Quando o operador consultar `/version`, recebe a versão e o commit.

## Dependências

* Docker Engine.
* Docker Compose.
* .NET 8 SDK.
* Imagem base Linux (ex.: Debian slim).

## Riscos

* Mudança breaking no Docker Compose v2 pode exigir ajustes.
* Versão .NET 8 pode ficar desatualizada antes do MVP; revisar periodicamente.

## Decisões relacionadas

* [ADR-0004](../adr/0004-postgresql-as-initial-queue.md)
* [ADR-0012](../adr/0012-filesystem-artifact-storage.md)
* [ADR-0013](../adr/0013-private-docker-network.md)

## Questões em aberto

* Versão exata da imagem base.
* Decisão entre OTLP e Prometheus puro para exportação.
* Estratégia de rotação de credenciais.

## Fora de escopo

* Dashboards.
* Alertas.
* Autenticação avançada.

## Estratégia de entrega incremental

1. Esqueleto da solução .NET.
2. Dockerfile multi-stage.
3. Compose mínimo.
4. Configuração e validação.
5. Health checks.
6. Logs estruturados.
7. Métricas.
8. Documentação de operação.
