# Discovery 004 — Repository Pilot Selection

> **Status:** Completed (fixture local)
> **Bloqueia:** Fase 3
> **Prioridade:** P0
> **Data:** 2026-07-27

## Objetivo

Selecionar e provisionar o repositório que será usado nas primeiras fatias verticais do MVP. Conforme ADR-0014 e o requisito "não usar projetos sensíveis como laboratório sem justificativa", o alvo é um **fixture Git local** em vez de um repositório remoto real.

## Critérios aplicados

| Critério | Requisito | Origem |
| --- | --- | --- |
| Pequeno | < 1 MB clonado | escopo da POC |
| Não sensível | sem código de produção | ADR-0008 + restrição obrigatória |
| Sem segredos | sem tokens, chaves ou senhas | SH-FR-008 |
| Git limpo | sem alterações locais no piloto | WI-FR-002 |
| Build simples | comando único | validação de Fase 3 |
| Testes rápidos | < 60 s | validação de Fase 3 |
| Sem serviços externos | sem dependência de banco ou API | isolamento |
| Sem Git LFS | apenas texto | OQ-013 |
| Sem submodules | estrutura plana | OQ-012 |
| Sem monorepo | escopo único | OQ-015 |
| Restaurável | criação via fixture | imutabilidade |

## Decisão

**Não criar repositório remoto durante a Fase 0.** Em vez disso, criar uma **fixture Git local** que satisfaça todos os critérios acima e que seja usada como `slug` registrado no contrato MCP durante a Fase 3.

A fixture é criada em `poc/fixtures/pilot-repo` e exposta no formato `file:///.../pilot-repo` para o `Repository Registry` (mais simples, sem rede externa). Quando o bridge estiver pronto, o slug `ocab-pilot` aponta para essa fixture.

## Fixture criada

Localização: `/tmp/opencode/discovery-005/pilot-repo` durante a POC. Para a POC, foi criada em `/tmp`; para uso permanente, mover para `poc/fixtures/pilot-repo` no repositório (a partir da Fase 3).

Estrutura:

```text
pilot-repo/
├── .git/
├── .gitignore
├── README.md
├── package.json
└── src/
    └── index.js
```

Conteúdo:

```text
# OCAB Pilot
```

```text
node_modules/
*.log
```

```json
{
  "name": "ocab-pilot",
  "version": "0.1.0",
  "scripts": {
    "start": "node src/index.js",
    "test": "node -e \"console.log('test ok')\""
  }
}
```

```javascript
console.log("hello world");
```

Inicialização (não-destructive, dentro de `poc/fixtures/pilot-repo`):

```bash
mkdir -p poc/fixtures
cd poc/fixtures
mkdir -p pilot-repo
cd pilot-repo
git init -q --initial-branch=main
git config user.email "pilot@example.invalid"
git config user.name "OCAB Pilot"
echo "# OCAB Pilot" > README.md
printf "node_modules/\n*.log\n" > .gitignore
mkdir -p src
echo 'console.log("hello world");' > src/index.js
echo '{"name":"ocab-pilot","version":"0.1.0","scripts":{"start":"node src/index.js","test":"node -e \"console.log(\\\"test ok\\\")\""}}' > package.json
git add .
git commit -q -m "initial commit"
```

> Todos os comandos acima são executados em ambiente local, isolado, sem acesso a credenciais reais ou projetos do host. Os endereços de e-mail usam o domínio `example.invalid` que, conforme RFC 6761, é reservado e jamais roteável.

## Validação smoke

```bash
cd poc/fixtures/pilot-repo
git log --oneline
```

```text
<hash> initial commit
```

```bash
node src/index.js
```

```text
hello world
```

## Tarefas piloto propostas para a Fase 3

| Tarefa | Tipo | Resultado esperado |
| --- | --- | --- |
| `list_files` | read-only | enumera `README.md`, `package.json`, `src/index.js` |
| `summarize_readme` | read-only | retorna o título e o primeiro parágrafo |
| `describe_structure` | read-only | retorna árvore de diretórios em JSON |
| `analyze_package` | read-only | retorna `name`, `version`, `scripts`, dependências |
| `run_test` | read-only (subprocesso restrito) | executa `node src/index.js` e captura stdout |

> A última tarefa, `run_test`, depende da política de comandos (spec 006). **Não** será incluída no vertical mínimo da Fase 3. Será introduzida em Fase 6 (Validation Pipeline).

## Registro proposto em `examples/repositories.example.yaml`

```yaml
repositories:
  - slug: ocab-pilot
    displayName: OCAB Pilot (fixture local)
    urlCanonical: file:///abs/path/poc/fixtures/pilot-repo.git
    defaultBranch: main
    writable: true
    allowedAgents:
      - opencode
    validations:
      - command: node --check src/index.js
        cwd: ./
        timeoutSeconds: 10
    policies:
      requireReview: false
      maxFilesChanged: 5
      readOnlyOnly: false
```

> O caminho `file:///abs/path/poc/fixtures/pilot-repo.git` será substituído pelo `git clone --bare` baseado em `git init --bare` durante a Fase 3; essa decisão é responsabilidade do slice 1.1.2 (Repository Registry).

## Critérios de sucesso da seleção

* [x] Fixture local criada e commit feito.
* [x] Estrutura mínima coerente com um projeto real (README, manifesto, código).
* [x] Smoke de execução passando (`node src/index.js`).
* [x] Sem segredos, sem dependências externas.
* [x] Slug `ocab-pilot` reservado para uso no `Repository Registry`.

## Limitações e riscos

| Limitação | Mitigação |
| --- | --- |
| Sem build complexo | adicionar mais um arquivo de teste no slice 1.1.2 se necessário |
| Sem Git LFS | registrar para resolução futura (OQ-013) |
| Sem submodules | registrar para resolução futura (OQ-012) |
| Fixture local não exercita credenciais remotas | fará parte da POC de autenticação se forçada |

## Questões abertas afetadas

* **OQ-010** (repositório piloto): `Resolved`. Slug `ocab-pilot` definido, fixture local criada.

## Próximo passo

* Mover a fixture de `/tmp/opencode/discovery-005/` para `poc/fixtures/pilot-repo/` quando o slice 1.1.2 estiver em execução.
* Adicionar entrada no `examples/repositories.example.yaml` durante o slice 1.1.2.
