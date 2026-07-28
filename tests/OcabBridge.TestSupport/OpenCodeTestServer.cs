using System.Diagnostics;
using Xunit;

namespace OcabBridge.TestSupport;

// xUnit collection fixture that starts a real OpenCode v1.18.8 server
// on a dedicated test port via tests/scripts/start-opencode.sh and
// tears it down after the assembly's contract + lifecycle tests have
// run. The script uses setsid + </dev/null + log-file redirection to
// detach OpenCode from the test runner's process group; this is
// required because .NET Process.Start pipe redirection caused OpenCode
// to log "listening on …" but never actually accept TCP connections
// in this environment.
//
// The fixture type lives in this TestSupport assembly. Each test
// assembly that wants to consume the shared instance must declare a
// matching [CollectionDefinition] in its own assembly (xUnit requires
// the definition to live in the same assembly as the [Collection]
// consumer), referencing this type as the fixture.
public sealed class OpenCodeTestServer : IAsyncLifetime
{
    public const string CollectionName = "OpenCodeContractServer";

    public int Port { get; } = 14301;
    public string BaseUrl => $"http://127.0.0.1:{Port}";
    public string Password { get; } = "test123_contract";
    public string ExpectedVersion { get; } = "1.18.8";

    private string? _binaryPath;
    private string? _scriptPath;
    private string _pidFile = "";
    private string _logFile = "";
    private int _openCodePid;

    public async Task InitializeAsync()
    {
        _binaryPath = Environment.GetEnvironmentVariable("OCAB_TEST_OPENCODE_BIN")
            ?? "/tmp/opencode-v1.18.8/opencode";
        if (!File.Exists(_binaryPath))
        {
            throw new FileNotFoundException(
                $"OpenCode binary not found at {_binaryPath}. " +
                "Set OCAB_TEST_OPENCODE_BIN or download v1.18.8 from " +
                "https://github.com/anomalyco/opencode/releases/download/v1.18.8/.");
        }

        _scriptPath = Environment.GetEnvironmentVariable("OCAB_TEST_OPENCODE_SCRIPT")
            ?? Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "scripts", "start-opencode.sh"));
        if (!File.Exists(_scriptPath))
        {
            throw new FileNotFoundException(
                $"OpenCode start script not found at {_scriptPath}. " +
                "Set OCAB_TEST_OPENCODE_SCRIPT or restore tests/scripts/start-opencode.sh.");
        }

        _pidFile = Path.Combine(Path.GetTempPath(), $"ocab-opencode-{Port}-{Environment.ProcessId}.pid");
        _logFile = Path.Combine(Path.GetTempPath(), $"ocab-opencode-{Port}-{Environment.ProcessId}.log");

        // Invoke the script via bash. The script detaches OpenCode via
        // setsid, writes the PID to _pidFile and returns immediately.
        var psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add(_scriptPath);
        psi.ArgumentList.Add(Port.ToString());
        psi.ArgumentList.Add(_pidFile);
        psi.ArgumentList.Add(_logFile);
        psi.ArgumentList.Add(_binaryPath);
        psi.ArgumentList.Add(Password);

        using var launcher = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to launch start-opencode.sh");
        await launcher.WaitForExitAsync();
        if (launcher.ExitCode != 0)
        {
            string log = "<log not written>";
            try { if (File.Exists(_logFile)) log = await File.ReadAllTextAsync(_logFile); } catch { }
            throw new InvalidOperationException(
                $"start-opencode.sh exited with code {launcher.ExitCode}.\n--- log ---\n{log}");
        }

        // Read the PID the script wrote.
        var pidText = await File.ReadAllTextAsync(_pidFile);
        if (!int.TryParse(pidText.Trim(), out _openCodePid))
        {
            throw new InvalidOperationException(
                $"start-opencode.sh wrote an invalid PID file '{_pidFile}': '{pidText}'");
        }

        await WaitForReadyAsync(TimeSpan.FromSeconds(30));
    }

    public async Task DisposeAsync()
    {
        if (_openCodePid > 0)
        {
            try
            {
                var proc = Process.GetProcessById(_openCodePid);
                if (!proc.HasExited)
                {
                    proc.Kill(entireProcessTree: true);
                    await proc.WaitForExitAsync();
                }
                proc.Dispose();
            }
            catch
            {
                // Process may already be gone — that's fine.
            }
        }

        // Best-effort cleanup of temp files; ignore failures.
        try { if (File.Exists(_pidFile)) File.Delete(_pidFile); } catch { }
        try { if (File.Exists(_logFile)) File.Delete(_logFile); } catch { }
    }

    private async Task WaitForReadyAsync(TimeSpan timeout)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        // The fixture starts the runner with OPENCODE_SERVER_PASSWORD set;
        // send Basic Auth on the liveness probe so the runner accepts it.
        var authBytes = System.Text.Encoding.UTF8.GetBytes($"opencode:{Password}");
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(authBytes));

        var sw = Stopwatch.StartNew();
        Exception? last = null;
        var attempts = 0;
        while (sw.Elapsed < timeout)
        {
            attempts++;
            try
            {
                using var resp = await http.GetAsync($"{BaseUrl}/global/health");
                if (resp.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"[opencode-fixture] ready after {attempts} attempt(s) in {sw.Elapsed.TotalSeconds:F1}s");
                    return;
                }
                last = new InvalidOperationException($"status {(int)resp.StatusCode}");
            }
            catch (Exception ex) { last = ex; }
            await Task.Delay(500);
        }
        string tail = "<log not written>";
        try
        {
            if (File.Exists(_logFile))
            {
                var lines = await File.ReadAllLinesAsync(_logFile);
                tail = string.Join(Environment.NewLine, lines);
            }
        }
        catch { /* ignore */ }
        throw new InvalidOperationException(
            $"OpenCode server did not become ready within {timeout} after {attempts} attempts. Last error: {last?.Message}\n--- log ({_logFile}) ---\n{tail}",
            last);
    }
}
