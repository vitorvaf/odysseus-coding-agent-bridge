// SLICE-STAB-003 / ADR-0018 end-to-end lifecycle tests.
//
// Two flavours:
//   1. With MockRunnerAdapter (substitui só o adapter, NÃO o OpenCode
//      real — vide SLICE-STAB-003 escopo): valida coordinator + dispatcher
//      + adapter chain ponta a ponta, incluindo completion, cancel, timeout
//      e provider error. Determinístico, sem dependência de credencial LLM.
//   2. With OpenCode real (OpenCodeTestServer): valida cancelamento
//      (POST /session/{id}/abort) e provider error (sem credencial).
//
// Substituir OpenCode por mock é explicitamente proibido pelo escopo
// da slice; o que está mockado aqui é apenas o adapter, mantendo o
// OpenCode real no caminho sempre que possível.
//
// Disable test parallelization within this assembly: the OpenCode
// fixture is shared across multiple test classes in this assembly and
// in the ContractTests assembly; concurrent tests against the same
// fixture intermittently see "connection refused" because the previous
// test's host shutdown races with the next test's startup.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
