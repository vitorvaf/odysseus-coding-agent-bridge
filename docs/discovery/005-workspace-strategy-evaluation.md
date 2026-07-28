# Discovery 005 — Workspace Strategy Evaluation

> **Status:** Completed
> **Bloqueia:** Fase 5
> **Prioridade:** P0
> **Data:** 2026-07-27

## Objetivo

Comparar as estratégias candidatas para criação de workspaces isolados, validar [`../specs/007-workspace-isolation/spec.md`](../specs/007-workspace-isolation/spec.md) e ADR-0005, e recomendar a abordagem para o MVP.

## Estratégias comparadas

| Estratégia | Comando base | Mecânica |
| --- | --- | --- |
| Clone local | `git clone <url>` | Cópia completa do repositório |
| Worktree | `git worktree add` | Branch dedicada em novo diretório |
| Cópia de diretório | `cp -a` | Cópia de filesystem |
| Clone com `--shared` | `git clone --shared <url>` | Reaproveita objetos `.git/objects` |
| Clone com `--reference` | `git clone --reference <local> <url> <dir>` | Usa um repositório local como referência |

## POC executada

A POC foi executada sobre a fixture local criada em [`004-repository-pilot-selection.md`](004-repository-pilot-selection.md), com medições de tempo de criação, espaço em disco e complexidade de limpeza. Todos os comandos foram isolados em `/tmp/opencode/discovery-005/`, sem afetar a origem.

### Resultados

```bash
cd /tmp/opencode/discovery-005
```

| Estratégia | Tempo (s) | Tamanho | Comando executado |
| --- | --- | --- | --- |
| Clone local | 0.011 | 252K | `git clone /tmp/opencode/discovery-005/pilot-repo /tmp/opencode/discovery-005/clone-test` |
| Worktree | 0.014 | 28K | `git -C pilot-repo worktree add /tmp/opencode/discovery-005/worktree-test -b feature-ocab` |
| `cp -a` | 0.006 | 232K | `cp -a /tmp/opencode/discovery-005/pilot-repo /tmp/opencode/discovery-005/copy-test` |
| Clone `--shared` | 0.011 | 200K | `git clone --shared /tmp/opencode/discovery-005/pilot-repo /tmp/opencode/discovery-005/shared-test` |
| Clone `--reference` | 0.010 | 256K | `git clone --reference /tmp/opencode/discovery-005/pilot-repo /tmp/opencode/discovery-005/pilot-repo /tmp/opencode/discovery-005/reference-test` |

> As medições referem-se à fixture OCAB-PILOT (poucos arquivos). Para repositórios reais, a diferença entre `clone` e `worktree` deve se ampliar significativamente em favor do `worktree`.

## Matriz comparativa

| Critério | Clone local | Worktree | cp -a | Clone --shared | Clone --reference |
| --- | --- | --- | --- | --- | --- |
| Isolamento | ✅ cópia total | ✅ mesmo `.git`, branch nova | ✅ cópia total | ✅ cópia total | ⚠ dependente do `reference` |
| Velocidade (POC) | 0.011s | 0.014s | 0.006s | 0.011s | 0.010s |
| Disco (POC) | 252K | 28K | 232K | 200K | 256K |
| Concorrência | alto (cópia total) | médio (1 `.git` por repo) | alto (cópia total) | alto (cópia total, `objects` compartilhados) | alto |
| Branch dedicada | ✅ necessário manual | ✅ automático | ⚠ precisa reconfigurar | ✅ manual | ✅ manual |
| Submodules | ✅ | ⚠ necessário `--submodules` ou setup próprio | ⚠ podem quebrar | ⚠ exige `--recurse-submodules` | ⚠ idem |
| Git LFS | ✅ | ⚠ necessário pull adicional | ⚠ exige re-checkout | ✅ | ✅ |
| Alterações locais | não afetam origem | não afetam origem | não afetam origem | não afetam origem | dependente de `reference` |
| Limpeza | trivial | trivial (remove + `git worktree prune`) | trivial | trivial | trivial |
| Restauração por `runId` | trivial | trivial | trivial | trivial | trivial |
| Compatibilidade com volumes Docker | ✅ | ✅ | ✅ | ✅ | ✅ |
| Permissões POSIX | exige `chmod` pós-clone | herdadas do `.git` pai | exigem `chmod` pós-cópia | idem clone | idem clone |
| Risco de corrupção | baixo | baixo (mesmo `.git`) | baixo | baixo | baixo se `reference` intacto |

## Análise qualitativa

### `git worktree add`

Prós:

* **Menor uso de disco** quando há sobreposição entre execuções no mesmo repositório (uma única `.git` por repositório cadastrado).
* **Mais rápido** em escala (criar branch + checkout em vez de clonar tudo).
* **Branching automático** com base em `baseReference` definido pela spec 007.
* **Limpeza trivial** com `git worktree remove <path>` e `git worktree prune`.

Contras:

* Requer um repositório bare ou working tree já presente no host, o que **amplia a superfície** caso a origem seja contaminada.
* Submodules exigem inicialização no diretório de trabalho.
* Para isolamento forte, o `Workspace Manager` precisa montar o diretório do worktree no container como volume **read-write**, mas a origem como **read-only**.

### `git clone <url>`

Prós:

* Cópia autossuficiente, **origem completamente não tocada**.
* Comportamento previsível mesmo com fixtures locais ou remotas.
* Submodules fáceis de inicializar (`--recurse-submodules`).

Contras:

* **Maior uso de disco** em escala (cada execução clona tudo).
* Mais lento em repositórios grandes.
* Possível duplicação de objetos quando muitos workspaces do mesmo repositório coexistem.

### `cp -a`

Prós:

* **Mais rápido em POC pequena**.
* Não depende de Git; funciona até em diretórios sem `.git`.

Contras:

* Perde a relação com a origem (refspec, remote, reflog, etc.).
* Não cria branch automaticamente; precisa re-inicializar.
* Histórico de `.git` mantido, mas branches remotas podem quebrar.

### `git clone --shared` e `--reference`

Prós:

* Reaproveitam objetos Git, **economizando espaço** quando várias execuções partilham repositório.

Contras:

* `--shared` exige que o original tenha sido clonado com `--shared`.
* `--reference` exige um repositório local "canônico" no host.
* Em ambientes descartáveis, manter essa estrutura adiciona operacional e risco.

## Recomendação para o MVP

**Estratégia primária: `git worktree add` para workspaces de escrita.**

Justificativas:

1. Spec 007 exige **origem read-only** e **branch de execução** — `git worktree` satisfaz ambas naturalmente.
2. Repositórios de produção têm > 100 MB de `.git/objects`. O `worktree` reaproveita esses objetos entre execuções, atendendo o requisito "limite de disco" (R5 do risk register).
3. Limpeza é determinística: `git worktree remove` + `git worktree prune` + `rm -rf`.
4. A spec 007 não fixa o método, apenas as invariantes — `worktree` as cumpre.

**Estratégia secundária (read-only): uso direto da origem read-only sem cópia adicional.**

Para execuções read-only, não há necessidade de clonar: o runner lê direto do volume read-only. Isso simplifica o ciclo e elimina custo de criação de workspace para a Fase 3.

**Estratégia de contingência (fallback): `git clone` para ambientes onde worktree não for viável.**

Exemplos:

* Repositório bare corrompido.
* Worktree não suportado pela versão de Git do runner.
* Ambiente onde o host proíbe múltiplos worktrees do mesmo `.git`.

A escolha entre primária, secundária e contingência deve ser feita pelo `Workspace Manager` com base em:

* Tamanho do repositório.
* `runType` (`ReadOnly` → sem clone; `WorkspaceWrite` → worktree).
* Configuração por `Repository.writable`.

## Riscos identificados

| Risco | Mitigação |
| --- | --- |
| Disco cheio durante `worktree add` | limite por repositório e por execução; OQ-031 |
| `worktree` órfão se bridge crashar antes de `cleanup` | `WorktreeCleanup` agendado com base em retenção |
| Submodules no repositório | policy engine deve rejeitar execuções workspace-write em repositório com submódulos no MVP; OQ-012 |
| Git LFS no repositório | similar; OQ-013 |
| Alterações locais em fixture ou branch suja | OQ-014, tratado em fixture local |

## Questões abertas afetadas

* **OQ-011** (estratégia de workspace): `Resolved`. Primária = `git worktree add`. Em read-only, sem workspace adicional.
* **OQ-012** (submodules): `Open`. Tratar no slice 2.1.1 com limitação explícita.
* **OQ-013** (Git LFS): `Open`. Tratar no slice 2.1.1.
* **OQ-014** (alterações locais): `Open`. Detector e bloqueio no policy engine.
* **OQ-015** (monorepos): `Open`. Fora do MVP.

## Próximo passo

* Criar a seção "Estratégia de workspace" na spec 007 com a recomendação acima.
* Definir protocolo de erro no `Workspace Manager` para submodules/LFS no MVP.
* Persistir a fixture local em `poc/fixtures/pilot-repo/` como repositório bare (`--bare`) para suportar `worktree add`.
