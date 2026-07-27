# Stakeholders e personas

> **Status:** Proposed

Identifica as personas e os agentes internos e externos ao projeto.

## Personas humanas

### Operador técnico

* Opera a plataforma localmente.
* Configura repositórios, agentes e políticas.
* Acompanha execuções.
* Aplica aprovações quando uma execução altera código.

### Mantenedor do Odysseus

* Integra o bridge ao Odysseus via MCP.
* Garante que a interface conversacional use os contratos corretos.
* Reporta incompatibilidades e sugere melhorias de contrato.

### Engenheiro de segurança

* Revisa threat model, política de comandos e endurecimento.
* Define requisitos de auditoria e retenção.
* Acompanha incidentes.

### Engenheiro de operações

* Implanta e mantém a plataforma.
* Monitora saúde, métricas e logs.
* Executa backup, restore e limpeza.

### Desenvolvedor usuário final

* Interage via Odysseus.
* Solicita análises e alterações.
* Consome relatórios.

## Agentes internos

### Coding Agent Bridge

* Servidor MCP.
* Plano de controle da execução.
* Aplicação de políticas.
* Geração de relatório.

### Runners

* OpenCode Runner (MVP).
* Codex Runner (pós-MVP).
* Antigravity Runner (pós-MVP).

Cada runner é um agente de execução especializado em um sistema de IA para desenvolvimento.

### PostgreSQL

* Persistência.
* Fila de execução.
* Eventos.
* Auditoria.

### Artifact Storage

* Filesystem dedicado a artefatos.
* Referenciado pelo banco.

## Agentes externos

* **Odysseus** — interação conversacional.
* **OpenCode Server** — execução do agente OpenCode.
* **Codex CLI** — execução do agente Codex (quando integrado).
* **Antigravity** — interface do agente Antigravity (sujeita a discovery).
* **Git remoto** — alvo da operação, sempre em modo read-only para o bridge.

## Matriz de responsabilidade

| Persona / agente | Repositórios | Bridge | Runners | Relatórios | Auditoria |
| --- | --- | --- | --- | --- | --- |
| Operador técnico | R/W (configuração) | R | R | R | R |
| Mantenedor do Odysseus | — | R | R | R | — |
| Engenheiro de segurança | R | R | — | R | R/W |
| Engenheiro de operações | R | R | R | R | R |
| Desenvolvedor usuário final | — | R/W | — | R | — |

R = leitura, R/W = leitura e escrita (configuração, auditoria ou estado).

## Interesses e preocupações

| Persona | Interesses | Preocupações |
| --- | --- | --- |
| Operador técnico | Baixa fricção, boa observabilidade | Falhas silenciosas, perda de histórico |
| Mantenedor do Odysseus | Contrato estável | Mudanças breaking no MCP |
| Engenheiro de segurança | Isolamento forte, auditabilidade | Exfiltração, prompt injection, escape |
| Engenheiro de operações | Saúde, alertas úteis | Falta de métricas, logs ruidosos |
| Desenvolvedor usuário final | Resposta rápida, relatório útil | Execuções demoradas, relatórios vagos |

## Comunicação entre personas

* Issues e PRs no repositório.
* Atas de decisão em ADRs.
* Relatórios de execução gerados pelo bridge.
* Alertas e dashboards na camada de observabilidade.
