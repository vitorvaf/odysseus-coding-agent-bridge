namespace OcabBridge.Api.Mcp;

// DTOs for the read-only report returned by run_report. The shape
// matches the contract under docs/contracts/run-report.md (mirrored
// from docs/specs/004-mcp-contract/spec.md). Slice 1.1.3 ships the
// minimal read-only rendering: `filesChanged` is always empty since
// read-only runs do not produce diffs.

public sealed record RunReportDto(
    string RunId,
    string Status,
    string RepositorySlug,
    string? Summary,
    string[] Scope,
    ChangedFileDto[] FilesChanged,
    FindingDto[] Findings,
    string[] Artifacts);

public sealed record ChangedFileDto(
    string Path,
    int Additions,
    int Deletions,
    string? PatchRef = null);

public sealed record FindingDto(
    string Severity,
    string Title,
    string? Detail = null);
