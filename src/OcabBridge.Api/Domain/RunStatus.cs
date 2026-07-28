namespace OcabBridge.Api.Domain;

// Single source of truth for Run state values. Backlog slice 1.1.3
// covers the read-only path: Pending -> Running -> {Completed, Failed,
// Cancelled}. Full state machine (Cancelling, ValidatingResult,
// ReviewRequired, CompletedWithValidationErrors, etc.) is deferred to
// Phase 3 + workspace-write slices.

public static class RunStatus
{
    public const string Pending = "Pending";
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Cancelling = "Cancelling";
    public const string Cancelled = "Cancelled";
    public const string Failed = "Failed";
    public const string TimedOut = "TimedOut";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Pending, Running, Completed, Cancelling, Cancelled, Failed, TimedOut
    };

    public static readonly IReadOnlySet<string> Terminal = new HashSet<string>
    {
        Completed, Cancelled, Failed, TimedOut
    };

    public static bool IsTerminal(string status) => Terminal.Contains(status);
}
