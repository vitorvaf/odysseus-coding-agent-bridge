namespace OcabBridge.Api.Application;

// Result type returned by IRunExecutionCoordinator.WaitForTerminalStateAsync.
// Captures the terminal status the dispatcher persisted and the raw
// `runs.result_json` payload (carrying the runner report) so callers
// (MCP run_get / run_report / HTTP /v1/runs/{id}) can render without
// hitting the database a second time.
public sealed record RunTerminalResult(
    string Status,
    string? ResultJson);
