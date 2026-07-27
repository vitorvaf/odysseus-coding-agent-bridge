# Premissas e Hipóteses

> **Status:** Proposed

Lista premissas e hipóteses que fundamentam decisões do OCAB.

## Premissas

* O ambiente operacional é Linux + Docker Engine + Docker Compose.
* O Odysseus é a interface primária.
* Repositórios são acessados em modo read-only na origem.
* OpenCode Server pode ser executado em container.
* PostgreSQL 14+ é suficiente para o MVP.
* A rede Docker privada oferece DNS interno suficiente.
* Operadores seguem procedimentos documentados.

## Hipóteses

* Throughput inicial é baixo, sem necessidade de broker dedicado.
* Latência de MCP Streamable HTTP é aceitável para o caso de uso.
* Limites padrão de CPU/memória são suficientes para o MVP.
* OpenCode Server será mantido pela comunidade durante a fase do MVP.
* Codex CLI será mantido e estável durante a Fase 8.
* A interface do Antigravity será documentada antes da Fase 9.

## Premissas descartadas

* Windows/WSL como ambiente suportado — descartado por restrição.
* Docker socket em qualquer container — descartado por restrição.
* UI própria — descartada por ADR-0001.
* Multi-tenant — fora do escopo do MVP.
* Object storage dedicado — descartado por ADR-0012.

## Pontos a validar

* Versão do OpenCode Server compatível.
* Compatibilidade de PostgreSQL com EF Core.
* Latência real de MCP em ambiente local.
* Throughput real para dimensionar fila.

## Referências relacionadas

* [`glossary.md`](glossary.md)
* [`open-questions.md`](open-questions.md)
* [`../planning/risk-register.md`](../planning/risk-register.md)
