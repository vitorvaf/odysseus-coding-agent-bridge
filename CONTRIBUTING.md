# Contributing

> **Status:** Proposed

Como contribuir com o OCAB.

## Princípios

1. Respeitar as ADRs aceitas.
2. Atualizar a documentação junto com o código.
3. Adicionar testes proporcionais ao risco.
4. Não introduzir credenciais reais.

## Fluxo de contribuição

1. Ler [`AGENTS.md`](AGENTS.md).
2. Identificar spec e ADRs relacionados.
3. Propor plano em [`docs/open-questions.md`](docs/open-questions.md) se houver decisões abertas.
4. Implementar conforme spec.
5. Atualizar documentação afetada.
6. Submeter revisão.

## Padrões de documentação

* PT-BR.
* Links relativos.
* Marcadores de status explícitos.
* Diagramas em Mermaid (sem sintaxe experimental).

## Padrões de código

* .NET 8 + ASP.NET Core.
* EF Core para persistência.
* OpenTelemetry para observabilidade.
* xUnit para testes.

## Não escopo

* Push automático.
* Merge automático.
* Deploy automático.
* Caminhos arbitrários.
* Docker socket.
* Root em runners.

## Revisão

Toda PR deve:

* Passar em testes automatizados.
* Ter sign-off de pelo menos um revisor.
* Ter documentação atualizada.

## Questões

Em caso de dúvida, abrir issue ou entrada em [`docs/open-questions.md`](docs/open-questions.md).
