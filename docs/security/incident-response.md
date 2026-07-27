# Resposta a Incidentes

> **Status:** Proposed

Define o procedimento a ser seguido quando um incidente de segurança é detectado ou suspeito.

## Classificação de incidentes

| Severidade | Descrição | Resposta |
| --- | --- | --- |
| P1 | Exfiltração confirmada, execução root não autorizada, push automatizado. | Imediata. |
| P2 | Tentativa de escape, falha de redaction, dependência comprometida. | Em horas. |
| P3 | Anomalia menor, falso positivo de policy, lentidão. | Em dias úteis. |

## Papéis

* **Respondedor:** primeiro a identificar e conter.
* **Coordenador:** organiza investigação e comunicação.
* **Sponsor:** autoriza ações sensíveis (rotação, restore).

## Procedimento

### 1. Identificação

* Sinal: métrica, alerta, log, relato.
* Confirmar se é incidente real.

### 2. Contenção

* Ações possíveis:
  * Pausar runners.
  * Bloquear execução de novos runs.
  * Reverter mudanças externas.
  * Bloquear token afetado.
* Cada ação é registrada com timestamp e responsável.

### 3. Erradicação

* Identificar vetor.
* Aplicar mitigação:
  * Patch.
  * Atualização de policy.
  * Rotação de segredo.
* Validar que vetor está mitigado.

### 4. Recuperação

* Restaurar backups se necessário.
* Reestabelecer serviço.
* Validar saúde.

### 5. Lições aprendidas

* Documentar incidente.
* Atualizar ADRs, specs ou threat model.
* Comunicar partes interessadas.

## Comunicação

* Canal seguro (fora do OCAB).
* Atualizações periódicas.
* Documento final de lições aprendidas.

## Preservação de evidência

* Logs preservados.
* Artefatos preservados com `diagnostic=true`.
* Backup de banco preservado até conclusão.

## Checklist inicial

* [ ] Identificar severidade.
* [ ] Notificar coordenador.
* [ ] Iniciar contenção.
* [ ] Registrar timestamps.
* [ ] Preservar evidências.
* [ ] Abrir documento de incidente.

## Exemplos

### Exfiltração de segredo

1. Detectar em métrica/log.
2. Rotacionar segredo imediatamente.
3. Identificar execução afetada.
4. Preservar artefatos.
5. Documentar.

### Tentativa de push

1. Detectar em policy log.
2. Verificar se houve sucesso.
3. Bloquear agente se necessário.
4. Documentar.

### Dependência comprometida

1. Identificar versão afetada.
2. Atualizar para versão corrigida.
3. Validar ambiente.
4. Documentar.

## Referências relacionadas

* [`threat-model.md`](threat-model.md)
* [`secrets-management.md`](secrets-management.md)
* [`container-hardening.md`](container-hardening.md)
* [`../operations/troubleshooting.md`](../operations/troubleshooting.md)
