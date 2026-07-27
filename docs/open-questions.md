# Questões em Aberto

> **Status:** Active

Lista questões que ainda precisam de validação prática. Não devem ser resolvidas silenciosamente.

## Plataforma

* Nome definitivo do projeto (atualmente "Odysseus Coding Agent Bridge").
* Repositório piloto definitivo para Fase 0.
* .NET 8 ou versão posterior (a confirmar após discovery).
* SDK MCP exato (versão e pacote).
* Mecanismo final de fila — manter PostgreSQL ou introduzir broker?
* Biblioteca de execução de processos (subprocess management).
* Biblioteca de geração de IDs (ULID vs UUID).
* Ferramenta de observabilidade (Prometheus + Grafana? OTLP-only?).

## Repositórios e Workspaces

* Clone, copy ou worktree como estratégia padrão.
* Tratamento de submodules.
* Tratamento de Git LFS.
* Tratamento de repositórios com alterações locais.
* Estratégia para monorepos.

## Runners

* OpenCode persistente ou iniciado sob demanda.
* Autenticação exata do OpenCode.
* Autenticação exata do Codex (quando entrar).
* Interface exata do Antigravity (em discovery).
* Política de retries em falhas transitórias do runner.

## Segurança

* Política de rotação de credenciais (frequência, procedimento).
* Limites de CPU/memória padrão por runner.
* Política de rede dos runners (default deny, allowlist).
* SBOM e scanning de dependências (ferramenta).
* Ferramenta de pentest.

## Operações

* Política de backup (frequência, retenção, off-host).
* Ferramenta de backup remoto (quando aplicável).
* Política de retenção padrão (30 dias é razoável?).
* Janela de manutenção.
* Ferramenta de visualização (Grafana ou similar).
* Ferramenta de alerta.

## Limites e tamanhos

* Tamanho máximo de diff retornado em `run_diff`.
* Tamanho máximo de prompt.
* Limite de eventos por execução.
* Tamanho máximo de artefato.

## Sanitização

* Sanitização de arquivos gerados.
* Tratamento de binários.

## Revisão humana

* Mecanismo de aplicação das alterações aprovadas (manual via Git).
* Assinatura Git para execuções aprovadas.

## Observabilidade

* Política de amostragem de traces no pós-MVP.

## MCP

* Suporte a SSE streaming para eventos em tempo real.
* Política de rate limit.

## Próximos passos

* Cada item aqui deve gerar uma entrada de discovery ou ADR substituta antes de implementação.
* Itens bloqueantes para Fase 0 devem ser respondidos antes de iniciar Fase 1.
