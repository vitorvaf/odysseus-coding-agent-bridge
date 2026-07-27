# Gestão de Segredos

> **Status:** Proposed

Define como segredos são criados, distribuídos, usados, rotacionados e descartados no OCAB.

## Princípios

* Segredos nunca são versionados em texto puro.
* Placeholders `__SET_ME__` em arquivos versionados.
* Carregamento via arquivo montado em runtime.
* Reapplication de redaction antes de qualquer exposição.

## Tipos de segredo

* Token MCP (cliente → bridge).
* Token interno OpenCode (bridge → runner).
* Token interno Codex (quando aplicável).
* Credenciais de PostgreSQL.
* Credenciais de Vault/secret manager (quando aplicável).

## Armazenamento

* Arquivos em volume dedicado com permissões `0600`.
* Diretório de segredos montado apenas em containers autorizados.
* Nenhum segredo em variável de ambiente em produção.

## Carregamento

* No startup, bridge valida presença e formato.
* Falha de carregamento é erro fatal.
* Logs de startup registram apenas presença, não valor.

## Uso

* Token MCP é validado em cada chamada.
* Token interno do OpenCode é enviado em header HTTP específico.
* Credenciais do PostgreSQL são lidas do arquivo.

## Rotação

* Procedimento manual documentado.
* Restart do bridge.
* Verificação de saúde pós-rotação.

## Descarte

* Arquivos antigos sobrescritos antes da remoção.
* Backups antigos expiram conforme política.

## Auditoria

* Toda leitura de segredo é registrada (sem expor valor).
* Acessos a segredos são parte da trilha de auditoria.

## Reapplication em logs

* Filtro obrigatório para:
  * Cabeçalhos HTTP sensíveis.
  * URLs com credenciais.
  * Tokens, API keys, senhas.
  * Conteúdo marcado como secreto.

## Limites

* Sem segredo em commit.
* Sem segredo em prompt.
* Sem segredo em artefato sem redaction.

## Procedimento de resposta a vazamento

1. Acionar [`incident-response.md`](incident-response.md).
2. Rotacionar segredo afetado.
3. Identificar vetor.
4. Documentar lições.

## Referências relacionadas

* [`threat-model.md`](threat-model.md)
* [`trust-boundaries.md`](trust-boundaries.md)
* [`container-hardening.md`](container-hardening.md)
* [`../operations/configuration.md`](../operations/configuration.md)
