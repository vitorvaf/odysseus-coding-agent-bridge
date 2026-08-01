# Project Charter

> **Status:** Proposed

## Propósito

Formalizar a autorização, o mandato e a governança do projeto OCAB durante a fase de especificação e implementação do MVP.

## Mandato

* Especificar e, quando autorizado, implementar o MVP do Coding Agent Bridge.
* Manter a documentação como fonte de verdade até que código de produção exista.
* Garantir que decisões sigam o processo de ADR descrito em [`docs/adr/README.md`](../adr/README.md).
* Coordenar com os mantenedores do Odysseus e dos agentes compatíveis sempre que o contrato MCP ou a interface de runner for alterada.

## Patrocinador e partes interessadas

| Papel | Responsabilidade |
| --- | --- |
| Patrocinador técnico | Autoriza mudanças de premissa bloqueada e release do MVP. |
| Arquiteto de software | Mantém coerência das decisões e revisa ADRs. |
| Equipe de segurança | Revisa threat model, política de comandos e endurecimento. |
| Equipe de operações | Revisa deployment, backup, restore e observabilidade. |
| Mantenedores do Odysseus | Garantem compatibilidade do contrato MCP. |
| Mantenedores do OpenCode/Codex/Antigravity | Garantem compatibilidade dos adapters. |

## Autoridade e limites

* Mudanças de premissa bloqueada exigem ADR substituta aprovada pelo patrocinador técnico.
* Mudanças de escopo exigem atualização deste charter e da [`docs/project/scope.md`](scope.md).
* Mudanças de contrato MCP exigem versão semântica do contrato (ver [`docs/contracts/mcp-tools.md`](../contracts/mcp-tools.md)).
* Releases do MVP exigem sign-off do patrocinador técnico e da equipe de segurança.

## Premissas

* O ambiente operacional é Linux + Docker + Docker Compose.
* O Odysseus será a interface primária.
* OpenCode é o primeiro runner vertical.
* PostgreSQL é a persistência e fila do MVP.
* Não há dependência de Windows, WSL ou Docker Desktop.

## Entregas da fase atual

* Especificação completa (este repositório).
* 14 ADRs aceitos como premissas bloqueadas.
* 14 specs publicadas.
* Backlog hierárquico priorizado.
* Roadmap por fases.
* Plano de testes.

## Entregas futuras (pós-autorização)

* Código do Coding Agent Bridge (.NET 8 + ASP.NET Core).
* Adapter do OpenCode Runner.
* Compose de desenvolvimento e produção.
* Pipeline de validação configurável.
* Suíte de testes automatizados.

## Marcos preliminares

| Marco | Descrição |
| --- | --- |
| M0 | Especificação completa aceita (este documento). |
| M1 | Foundation da plataforma implementada (Fases 0 e 1 do roadmap). |
| M2 | Vertical slice read-only com OpenCode (Fases 2 a 4). |
| M3 | Execução workspace-write com validação (Fases 5 a 7). |
| M4 | Operação assistida (Epic 3). |
| M5 | Codex Runner (Epic 4). |
| M6 | Antigravity Runner (Epic 5). |

## Governança documental

* Todo documento deve usar os marcadores de status definidos em `AGENTS.md`.
* Toda decisão nova deve ser registrada como ADR ou em `docs/open-questions.md`.
* Toda referência cruzada deve usar links relativos.
* Diagramas em Mermaid, sem sintaxe experimental.

## Como propor mudanças

1. Abrir issue ou entrada em [`docs/open-questions.md`](../open-questions.md).
2. Avaliar impacto nas specs e ADRs.
3. Propor ADR substituta, se for o caso.
4. Atualizar roadmap e backlog.
5. Notificar partes interessadas afetadas.

## Riscos de charter

* Mudança prematura da interface MCP antes do MVP.
* Decisões tomadas sem ADR formal.
* Sobreposição com projetos paralelos que tratem do mesmo problema.
