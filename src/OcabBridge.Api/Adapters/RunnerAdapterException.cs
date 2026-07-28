using System.Net;

namespace OcabBridge.Api.Adapters;

// Thrown by IRunnerAdapter implementations to surface a normalized
// error to the dispatcher. The dispatcher maps Code to a Run terminal
// state and Detail to a diagnostic RunEvent.
//
// Code values are stable contract identifiers — see ADR-0017 § Decisão
// for the full list (runner_auth_failed, session_not_found,
// session_conflict, runner_rate_limited, runner_unavailable,
// runner_error).
public class RunnerAdapterException : Exception
{
    public string Code { get; }
    public string Operation { get; }
    public HttpStatusCode HttpStatus { get; }
    public string Detail { get; }

    public RunnerAdapterException(
        string code,
        string operation,
        HttpStatusCode status,
        string detail)
        : base($"runner_adapter[{code}] op={operation} status={(int)status}: {detail}")
    {
        Code = code;
        Operation = operation;
        HttpStatus = status;
        Detail = detail;
    }
}

// Subtype for UpstreamContractMismatch: the runner returned a response
// with a Content-Type other than application/json (typically text/html
// from the SPA fallback) where the adapter expected a JSON contract.
// ADR-0017 § Tratamento de contract drift; Spec 005 § OCR-AC-008.
public sealed class RunnerContractMismatchException : RunnerAdapterException
{
    public string ActualContentType { get; }

    public RunnerContractMismatchException(
        string operation,
        HttpStatusCode status,
        string actualContentType,
        string detail)
        : base("upstream_contract_mismatch", operation, status, detail)
    {
        ActualContentType = actualContentType;
    }
}
