# Princípios de engenharia

> **Status:** Accepted

## Princípios

### 1. Controle antes de conveniência

Toda decisão sensível é mediada pelo bridge antes de chegar ao runner. Policiais e regras ficam fora do prompt.

### 2. Política fora do modelo

A política é determinística, registrada e auditável. Não dependemos exclusivamente do modelo para bloquear comandos, caminhos ou segredos.

### 3. Isolamento por padrão

Workspaces são isolados por execução. Containers são isolados por papel. Recursos são limitados por configuração.

### 4. Origem preservada

O repositório original é lido em modo read-only. Alterações só ocorrem em workspace dedicado.

### 5. Falhas explícitas

Cada estado de erro é distinto. Falha de validação não é confundida com falha de infraestrutura.

### 6. Auditoria completa

Cada decisão relevante — política, transição, validação — é registrada com ator, motivo e timestamp.

### 7. Aprovação humana obrigatória

Push, merge, deploy e criação de pull request são manuais. O sistema nunca realiza essas operações sozinho.

### 8. Reversibilidade operacional

Workspaces e artefatos são preservados conforme política de retenção para diagnóstico e auditoria.

### 9. Contratos antes de código

Contratos MCP, eventos e relatórios são definidos e versionados antes de qualquer implementação.

### 10. Documentação como código

Toda decisão é registrada como ADR ou como entrada em `docs/open-questions.md`. Toda spec é revisável.

### 11. Testes proporcionais ao risco

Cobertura alta em máquina de estados, policy engine, validação de caminhos, redaction e idempotência. Cobertura padrão em outras camadas.

### 12. Observabilidade por padrão

Logs estruturados, métricas e traces são emitidos desde a primeira execução. Correlação por `runId` é obrigatória.

### 13. Compatibilidade com o ecossistema

Sempre que possível, o bridge adota contratos abertos (MCP, OpenTelemetry) e formatos padronizados.

### 14. Minimalismo responsável

YAGNI se aplica, mas não como desculpa para cortar segurança, auditoria ou validação. Decisões de redução de escopo passam por ADR.

## Anti-padrões

* Tratar prompt como mecanismo principal de segurança.
* Confundir "funcionou na minha máquina" com validação.
* Adicionar dependências externas sem justificativa.
* Misturar logs de aplicação com logs de auditoria.
* Usar caminhos físicos arbitrários.
* Esconder decisões em comentários de código.

## Como aplicar

* Antes de adicionar uma nova biblioteca ou serviço, abrir ADR ou entrada em `docs/open-questions.md`.
* Antes de introduzir um novo estado na máquina de execuções, atualizar a spec 003.
* Antes de adicionar um campo em evento, atualizar `contracts/events.md`.
* Antes de introduzir um comando na allowlist, atualizar `security/command-policy.md`.
