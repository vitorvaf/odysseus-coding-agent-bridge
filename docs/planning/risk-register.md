# Risk Register

> **Status:** Proposed

Lista de riscos identificados com probabilidade, impacto, mitigação e proprietário.

## Riscos

### R1 — Mudança breaking no MCP

* **Probabilidade:** Média.
* **Impacto:** Alto.
* **Descrição:** mudanças incompatíveis no protocolo MCP podem exigir revisão do bridge.
* **Mitigação:** versionamento de contrato, testes de contrato.
* **Detecção:** monitoramento de versão.
* **Proprietário:** arquiteto.

### R2 — Instabilidade do OpenCode Server

* **Probabilidade:** Média.
* **Impacto:** Alto.
* **Descrição:** o OpenCode Server pode ter bugs ou mudanças que afetem o adapter.
* **Mitigação:** pinning de versão, contrato de adapter estável, plano de contingência.
* **Detecção:** health checks, métricas.
* **Proprietário:** equipe de adapters.

### R3 — Falha de isolamento do runner

* **Probabilidade:** Baixa.
* **Impacto:** Alto.
* **Descrição:** runner pode escapar do isolamento via Docker socket, root ou permissões.
* **Mitigação:** hardening, ausência de Docker socket, rootfs read-only, capabilities mínimas.
* **Detecção:** testes de segurança, scans.
* **Proprietário:** segurança.

### R4 — Vazamento de segredo

* **Probabilidade:** Média.
* **Impacto:** Alto.
* **Descrição:** segredo pode aparecer em logs, artefatos ou eventos.
* **Mitigação:** reapplication de redaction, testes automatizados.
* **Detecção:** varredura periódica de logs.
* **Proprietário:** segurança.

### R5 — Crescimento descontrolado de artefatos

* **Probabilidade:** Média.
* **Impacto:** Médio.
* **Descrição:** execuções podem gerar artefatos grandes que ocupam disco.
* **Mitigação:** limites por execução, retenção, limpeza.
* **Detecção:** métricas de disco.
* **Proprietário:** operações.

### R6 — Concorrência excessiva

* **Probabilidade:** Baixa.
* **Impacto:** Médio.
* **Descrição:** múltiplas execuções podem concorrer por recursos.
* **Mitigação:** limites, lock por repositório, fila.
* **Detecção:** métricas de fila.
* **Proprietário:** operações.

### R7 — Mudança em API de agente externo (Codex/Antigravity)

* **Probabilidade:** Média.
* **Impacto:** Médio.
* **Descrição:** APIs podem mudar após o MVP.
* **Mitigação:** adapter encapsulado, testes de contrato.
* **Detecção:** nightly tests.
* **Proprietário:** equipe de adapters.

### R8 — Perda de backup

* **Probabilidade:** Baixa.
* **Impacto:** Alto.
* **Descrição:** backups podem estar corrompidos.
* **Mitigação:** verificação periódica, testes de restore em staging.
* **Detecção:** testes agendados.
* **Proprietário:** operações.

### R9 — Carga inesperada de execução

* **Probabilidade:** Baixa.
* **Impacto:** Médio.
* **Descrição:** aumento repentino de uso pode saturar recursos.
* **Mitigação:** limites, rate limit, fila.
* **Detecção:** métricas.
* **Proprietário:** operações.

### R10 — Prompt injection sofisticado

* **Probabilidade:** Média.
* **Impacto:** Alto.
* **Descrição:** vetores novos podem surgir e contornar policy engine.
* **Mitigação:** revisão contínua do threat model, política atualizada.
* **Detecção:** logs, métricas.
* **Proprietário:** segurança.

### R11 — Lock contention no banco

* **Probabilidade:** Baixa.
* **Impacto:** Médio.
* **Descrição:** locks pessimistas podem causar contenção.
* **Mitigação:** transações curtas, monitorar métricas.
* **Detecção:** métricas do banco.
* **Proprietário:** bridge.

### R12 — Erro de configuração

* **Probabilidade:** Média.
* **Impacto:** Médio.
* **Descrição:** configuração incorreta pode bloquear execuções.
* **Mitigação:** validação no startup, exemplos versionados.
* **Detecção:** logs de startup.
* **Proprietário:** operador.

### R13 — Descoberta de dependência vulnerável

* **Probabilidade:** Média.
* **Impacto:** Médio.
* **Descrição:** dependências podem ter vulnerabilidades.
* **Mitigação:** SBOM, scanning, atualizações.
* **Detecção:** scanners.
* **Proprietário:** segurança.

### R14 — Mudança de requisito regulatório

* **Probabilidade:** Baixa.
* **Impacto:** Médio.
* **Descrição:** novos requisitos legais podem exigir mudanças.
* **Mitigação:** revisão periódica, flexibilidade de retenção.
* **Detecção:** não aplicável.
* **Proprietário:** patrocinador.

## Matriz de risco

| ID | Probabilidade | Impacto | Risco |
| --- | --- | --- | --- |
| R1 | Média | Alto | Alto |
| R2 | Média | Alto | Alto |
| R3 | Baixa | Alto | Médio |
| R4 | Média | Alto | Alto |
| R5 | Média | Médio | Médio |
| R6 | Baixa | Médio | Baixo |
| R7 | Média | Médio | Médio |
| R8 | Baixa | Alto | Médio |
| R9 | Baixa | Médio | Baixo |
| R10 | Média | Alto | Alto |
| R11 | Baixa | Médio | Baixo |
| R12 | Média | Médio | Médio |
| R13 | Média | Médio | Médio |
| R14 | Baixa | Médio | Baixo |

## Revisão

* Risk register revisado a cada milestone.
* Novos riscos adicionados conforme surgem.

## Referências relacionadas

* [`../security/threat-model.md`](../security/threat-model.md)
* [`roadmap.md`](roadmap.md)
* [`definition-of-done.md`](definition-of-done.md)
