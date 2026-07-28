# Discovery 001 — Environment Baseline

> **Status:** Completed
> **Bloqueia:** todas as fases
> **Prioridade:** P0
> **Data:** 2026-07-27

## Objetivo

Registrar o baseline do ambiente onde o OCAB executará. Sem este baseline, nenhuma decisão de runtime, biblioteca ou container é segura.

## Ambiente coletado

### Sistema operacional e kernel

```bash
uname -a
```

```text
Linux vitorvaf 6.8.0-136-generic #136~22.04.1-Ubuntu SMP PREEMPT_DYNAMIC Fri Jul  3 16:29:11 UTC  x86_64 x86_64 x86_64 GNU/Linux
```

```bash
cat /etc/os-release | head -8
```

```text
PRETTY_NAME="Ubuntu 22.04.5 LTS"
NAME="Ubuntu"
VERSION_ID="22.04"
VERSION="22.04.5 LTS (Jammy Jellyfish)"
VERSION_CODENAME=jammy
```

* **Impacto arquitetural:** distribuição suportada, kernel recente. Suficiente para userns, cgroup v2, overlay2 e capacidades Linux.
* **Risco:** distribuição LTS, atualizações de segurança disponíveis.
* **Decisão:** manter Ubuntu 22.04 LTS como baseline.

### Arquitetura e recursos

| Item | Valor |
| --- | --- |
| Arquitetura | `x86_64` |
| CPUs | `8` |
| Memória total | `31Gi` |
| Memória disponível | `~17Gi` |
| Swap | `2,0Gi` |

* **Impacto:** capacidade confortável para MVP. Limites do runner devem ser conservadores.
* **Risco:** swap pequeno pode pressionar sistema durante picos.
* **Decisão:** registrar limites de runner conforme [`008-resource-limits-baseline.md`](008-resource-limits-baseline.md).

### Filesystem

| Ponto de montagem | Tamanho | Disponível | Uso |
| --- | --- | --- | --- |
| `/mnt/hd2` | 916G | 775G | 11% |
| `/` | 219G | 4,1G | 99% |
| `/tmp` | 219G | 4,1G | 99% |

* **Impacto:** artefatos e workspaces devem residir em `/mnt/hd2`, não em `/` ou `/tmp`.
* **Risco:** `/` e `/tmp` saturados são vetor de indisponibilidade.
* **Decisão:** forçar volumes Docker a usar `/mnt/hd2/ocab/{artifacts,pgdata}`.

### Docker Engine e Compose

| Item | Valor |
| --- | --- |
| Docker Engine | `29.6.2` |
| Docker Compose | `v5.3.1` |
| Storage Driver | `overlay2` |
| Cgroup Driver | `systemd` |
| Kernel | `6.8.0-136-generic` |

* **Impacto:** compatibilidade total com recursos modernos (userns, cgroup v2, buildkit).
* **Risco:** nenhuma restrição observada.
* **Decisão:** manter `overlay2` e `systemd` como padrão.

### Capacidades do kernel relevantes

| Item | Valor |
| --- | --- |
| `user.max_user_namespaces` | `126914` |
| `kernel.unprivileged_userns_clone` | `1` |

* **Impacto:** isolamento de UID/GID via namespaces de usuário é viável.
* **Decisão:** reforçar runners com `read_only`, `cap_drop: ALL` e `no-new-privileges`.

### Runtimes e SDKs

| Ferramenta | Versão | Caminho/Notas |
| --- | --- | --- |
| Git | `2.34.1` | — |
| **.NET SDK** | `10.0.110` | único SDK instalado |
| .NET runtime 8 | `8.0.29` | `Microsoft.NETCore.App 8.0.29` |
| .NET runtime 10 | `10.0.10` | `Microsoft.NETCore.App 10.0.10` |
| ASP.NET Core 8 | `8.0.29` | instalado |
| ASP.NET Core 10 | `10.0.10` | instalado |
| Node.js | `v22.14.0` | via nvm |
| npm | `11.1.0` | — |
| OpenCode (host) | `1.17.20` | `/home/vitor/.opencode/bin/opencode` |
| psql (client) | `14.23` | — |
| jq | `1.6` | — |
| curl | `7.81.0` | OpenSSL/3.0.2 |

### Conflito detectado: .NET 8 vs .NET 10

* O ambiente instala simultaneamente:
  * SDK 10.0.110 (default).
  * Runtime 8 e 10.
* O **prompt original** e o **README** indicam baseline `.NET 8`.
* O `dotnet build` padrão usa o SDK `10.0.110`.
* **Conflito potencial:** sem `global.json` fixando SDK, builds podem flutuar.

> **Questão aberta:** OQ-002 (`.NET 8 vs .NET 10`). Resolvida pela ADR proposta `0015-runtime-version.md` — **adotar .NET 8 LTS** como runtime alvo do MVP, com SDK 8 fixado via `global.json`. Runtime 10 fica disponível no host mas não é alvo do MVP.

### OpenCode

| Item | Valor |
| --- | --- |
| Versão CLI instalada | `1.17.20` |
| Repositório upstream | `https://github.com/sst/opencode` (sst/opencode) |
| Versão no repositório (main) | `1.18.7` |

> **A versão CLI instalada (1.17.20) é anterior à versão do `main` (1.18.7).** Os endpoints analisados neste discovery referem-se à versão **1.18.7** clonada para inspeção, não à CLI local.

### PostgreSQL

| Item | Valor |
| --- | --- |
| Cliente `psql` | `14.23` (instalado) |
| Servidor | não detectado nesta máquina — será necessário subir em container (ADR-0004) |

* **Decisão:** usar PostgreSQL 14+ em container via Compose. Sem instância local no host.

### Conectividade

* `docker run --rm hello-world` executa com sucesso. Conectividade de rede Docker para registro público OK.
* Containers já em execução no host (`odysseus-odysseus`, `searxng`, `ntfy`, `chromadb`) confirmam uso ativo de Docker.

## Comandos utilizados

```bash
uname -a
cat /etc/os-release
uname -m
nproc
free -h
df -h /mnt/hd2 /tmp /var
docker version
docker compose version
docker info
docker run --rm hello-world
cat /proc/sys/user/max_user_namespaces
cat /proc/sys/kernel/unprivileged_userns_clone
capsh --print
git --version
dotnet --list-sdks
dotnet --list-runtimes
node --version
npm --version
/home/vitor/.opencode/bin/opencode --version
psql --version
jq --version
curl --version | head -1
```

## Tabela de impacto

| Item | Valor | Impacto arquitetural | Risco | Decisão/OQ |
| --- | --- | --- | --- | --- |
| OS | Ubuntu 22.04 LTS | OK | — | manter |
| CPU | x86_64, 8 | OK | — | base para limites |
| Memória | 31Gi | OK | — | base para limites |
| `/` | 99% cheio | forçar uso de `/mnt/hd2` | saturação | mover volumes para `/mnt/hd2/ocab` |
| Docker | 29.6.2 / Compose v5.3.1 | OK | — | manter |
| userns | OK (126914) | OK | — | habilita isolamento |
| cgroup driver | systemd | OK | — | manter |
| .NET SDK | `10.0.110` (default) | conflito com baseline `.NET 8` | flutuação de SDK | **OQ-002 → ADR `0015`** |
| .NET runtime 8 | `8.0.29` | suficiente | — | alvo do MVP |
| .NET runtime 10 | `10.0.10` | disponível | — | fora do MVP |
| Node.js | 22 LTS | suficiente | — | OK para PoCs OpenCode |
| OpenCode CLI local | `1.17.20` | defasada | usou source `1.18.7` para inspeção | **OQ-021** |
| PostgreSQL server | ausente | precisa container | — | ADR-0004 mantém |
| jq | 1.6 | OK | — | útil para POCs |
| curl | 7.81 | OK | — | útil para health checks |
| `capsh` | root | ambiente host é root | não influencia containers | manter |

## Questões abertas afetadas

* OQ-001 (SDK MCP) — ainda precisa seleção.
* OQ-002 (.NET versão) — fechar com ADR `0015-runtime-version.md`.
* OQ-031 (limites CPU/memória) — base definida em [`008-resource-limits-baseline.md`](008-resource-limits-baseline.md).

## Próximo passo

* Preencher [`002-mcp-sdk-evaluation.md`](002-mcp-sdk-evaluation.md).
* Consolidar decisão de runtime na ADR `0015-runtime-version.md`.
