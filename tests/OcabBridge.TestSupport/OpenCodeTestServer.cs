using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Xunit;

namespace OcabBridge.TestSupport;

// xUnit collection fixture that starts a real OpenCode v1.18.8 server
// on a dedicated test port via a plain Process (no setsid) and tears it
// down via SIGTERM → grace period → SIGKILL. The previous implementation
// used `setsid + </dev/null + log file redirect via bash`, which
// crashed the OpenCode runner in CI ("ServeError" right after loading
// config) while the same command worked when run manually. The new
// path keeps the lifecycle fully under the fixture's control: the
// process is a ProcessStartInfo child with redirected stdio drained by
// background Tasks, cancellation propagates via Kill(entireProcessTree),
// and the per-test XDG_CONFIG_HOME is wiped in DisposeAsync.
//
// Auth: OPENCODE_SERVER_PASSWORD is set via env on the Process, so the
// fixture sends Basic Auth on /global/health and the runner refuses
// unauthenticated requests (mirrors the production posture).
//
// The fixture type lives in this TestSupport assembly. Each test
// assembly that wants to consume the shared instance must declare a
// matching [CollectionDefinition] in its own assembly (xUnit requires
// the definition to live in the same assembly as the [Collection]
// consumer), referencing this type as the fixture.
public sealed class OpenCodeTestServer : IAsyncLifetime
{
    public const string CollectionName = "OpenCodeContractServer";

    // Dynamic free port per fixture instance. ContractTests and
    // IntegrationTests each declare their own ICollectionFixture<
    // OpenCodeTestServer> and xUnit runs the two assemblies in parallel,
    // so a shared fixed port (previously 14301) collided intermittently
    // and flaked CI. Reserving an OS-assigned ephemeral port per
    // instance removes the collision regardless of how many collections
    // start concurrently. Mirrors OpenCodeRealFixture.GetFreePort.
    public int Port { get; }
    public string BaseUrl => $"http://127.0.0.1:{Port}";

    public OpenCodeTestServer()
    {
        Port = GetFreePort();
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
    public string Password { get; } = "test123_contract";
    // Pinned upstream version validated by the spike in
    // docs/discovery/012-opencode-contract-spike.md and locked in by
    // ADR-0017.
    public string ExpectedVersion { get; } = "1.18.8";

    // Process-wide lock to serialize downloads of the OpenCode binary
    // when multiple xUnit collections start concurrently on a CI runner.
    private static readonly SemaphoreSlim _downloadLock = new(1, 1);

    // Default location used when neither OCAB_TEST_OPENCODE_BIN is set
    // nor a fixture-managed download has been performed yet.
    private const string DefaultBinaryPath = "/tmp/opencode-v1.18.8/opencode";
    private const string DefaultInstallRoot = "/tmp/opencode-v1.18.8";
    private const string DefaultVersionTag = "v1.18.8";
    private const string ExpectedVersionLiteral = "1.18.8";
    // Release tarball for Linux x64 (glibc). ubuntu-latest CI runners
    // are glibc x64 so this asset matches the runner; arm64 / musl
    // variants exist on the release page for other targets.
    private const string DownloadUrl =
        $"https://github.com/anomalyco/opencode/releases/download/{DefaultVersionTag}/opencode-linux-x64.tar.gz";

    private const string StartupDeadline = "60s";

    private string? _binaryPath;
    private string? _configRoot;
    private string? _logFile;
    private Process? _process;
    private Task? _stdoutDrain;
    private Task? _stderrDrain;

    public async Task InitializeAsync()
    {
        _binaryPath = Environment.GetEnvironmentVariable("OCAB_TEST_OPENCODE_BIN")
            ?? DefaultBinaryPath;
        if (!File.Exists(_binaryPath))
        {
            await EnsureBinaryDownloadedAsync(_binaryPath).ConfigureAwait(false);
        }
        if (!File.Exists(_binaryPath))
        {
            throw new FileNotFoundException(
                $"OpenCode binary still missing at {_binaryPath} after download attempt. " +
                "Set OCAB_TEST_OPENCODE_BIN or fix the network access to " +
                "github.com/anomalyco/opencode releases.");
        }

        // Per-test exclusive XDG_CONFIG_HOME so OpenCode does not read
        // or mutate any other fixture's config/auth.json/cache. Each
        // test gets its own fresh $XDG_CONFIG_HOME/opencode/ tree.
        _configRoot = Path.Combine(
            Path.GetTempPath(),
            $"ocab-config-{Port}-{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_configRoot, "opencode"));

        _logFile = Path.Combine(_configRoot, "opencode.log");

        var psi = new ProcessStartInfo
        {
            FileName = _binaryPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("serve");
        psi.ArgumentList.Add("--hostname");
        psi.ArgumentList.Add("127.0.0.1");
        psi.ArgumentList.Add("--port");
        psi.ArgumentList.Add(Port.ToString());
        psi.ArgumentList.Add("--print-logs");

        // Per-test XDG_CONFIG_HOME applies to the child process only.
        // We do not mutate the parent's XDG_CONFIG_HOME because that
        // would leak across tests.
        psi.EnvironmentVariables["XDG_CONFIG_HOME"] = _configRoot;
        // Basic Auth on the runner side.
        psi.EnvironmentVariables["OPENCODE_SERVER_PASSWORD"] = Password;

        try
        {
            _process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start OpenCode");
        }
        catch
        {
            Directory.Delete(_configRoot, recursive: true);
            throw;
        }

        // Drain stdout/stderr continuously so the pipe buffer does not
        // fill and deadlock the child process (OpenCode writes structured
        // logs to stderr). Drain tasks exit when the process closes its
        // handles (DisposeAsync / process exit).
        _stdoutDrain = DrainAsync(_process.StandardOutput, _logFile + ".stdout");
        _stderrDrain = DrainAsync(_process.StandardError, _logFile + ".stderr");

        await WaitForReadyAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        // Try to stop OpenCode gracefully via SIGTERM; fall back to
        // SIGKILL (via Process.Kill) if the runner does not exit within
        // the grace period. The kill is best-effort: if the process has
        // already exited, both paths are no-ops.
        if (_process is { HasExited: false })
        {
        try
        {
            var killPsi = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            killPsi.ArgumentList.Add("-c");
            killPsi.ArgumentList.Add($"kill -TERM {_process.Id} 2>/dev/null; true");
            using var kill = Process.Start(killPsi);
            kill?.WaitForExit(2000);
        }
        catch { /* SIGTERM delivery failed; rely on WaitForExit timeout below */ }

            try
            {
                if (!_process.WaitForExit(TimeSpan.FromSeconds(5)))
                {
                    _process.Kill(entireProcessTree: true);
                    _process.WaitForExit(2000);
                }
            }
            catch { /* best effort */ }
        }
        _process?.Dispose();

        // Wait for the drain tasks to finish (process handles closed).
        if (_stdoutDrain is not null)
        {
            try { await _stdoutDrain.ConfigureAwait(false); } catch { /* ignore */ }
        }
        if (_stderrDrain is not null)
        {
            try { await _stderrDrain.ConfigureAwait(false); } catch { /* ignore */ }
        }

        // Validate that the port is actually free; fail the fixture if
        // a leftover process is still listening. This is the second
        // gate that backs the CI's contract-tests step (the first is
        // the /global/health probe; this is the post-mortem).
        if (IsPortOpen("127.0.0.1", Port, TimeSpan.FromSeconds(2)))
        {
            throw new InvalidOperationException(
                $"Port {Port} still in use after OpenCode shutdown; " +
                "a leftover process is likely bound to the test port");
        }

        // Best-effort cleanup of the per-test config root. Failure
        // here does not fail the fixture; the runner has already exited.
        try { Directory.Delete(_configRoot!, recursive: true); } catch { /* ignore */ }
    }

    private static async Task DrainAsync(StreamReader reader, string logPath)
    {
        await using var writer = new StreamWriter(logPath, append: true);
        try
        {
            string? line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
            {
                await writer.WriteLineAsync(line).ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
            }
        }
        catch { /* best effort */ }
    }

    private static bool IsPortOpen(string host, int port, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(host, port);
                if (connectTask.Wait(TimeSpan.FromMilliseconds(200)) && client.Connected)
                {
                    return true;
                }
            }
            catch { /* connection refused / timeout */ }
        }
        return false;
    }

    private async Task WaitForReadyAsync(TimeSpan timeout)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var auth = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"opencode:{Password}"));
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", auth);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Exception? last = null;
        var attempts = 0;
        while (sw.Elapsed < timeout)
        {
            attempts++;
            try
            {
                using var resp = await http.GetAsync($"{BaseUrl}/global/health").ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (body.Contains($"\"version\":\"{ExpectedVersionLiteral}\""))
                    {
                        Console.Error.WriteLine(
                            $"[opencode-fixture] ready after {attempts} attempt(s) in {sw.Elapsed.TotalSeconds:F1}s");
                        return;
                    }
                    last = new InvalidOperationException(
                        $"OpenCode reports wrong version. body='{body}' expected version='{ExpectedVersionLiteral}'");
                }
                else
                {
                    last = new InvalidOperationException($"status {(int)resp.StatusCode}");
                }
            }
            catch (Exception ex) { last = ex; }
            await Task.Delay(500).ConfigureAwait(false);
        }
        string logTail = "<log not written>";
        try
        {
            if (_logFile is not null && File.Exists(_logFile + ".stderr"))
            {
                var lines = await File.ReadAllLinesAsync(_logFile + ".stderr").ConfigureAwait(false);
                logTail = string.Join(Environment.NewLine, lines.TakeLast(50));
            }
        }
        catch { /* ignore */ }
        throw new InvalidOperationException(
            $"OpenCode server did not become ready within {timeout} after {attempts} attempts. " +
            $"Last error: {last?.Message}\n--- last stderr lines ---\n{logTail}",
            last);
    }

    // Downloads the pinned OpenCode binary into the default location if
    // it is not already present. Serialized across the process so
    // concurrent fixtures (Contract + Integration share the binary
    // path) do not race each other.
    private static async Task EnsureBinaryDownloadedAsync(string targetPath)
    {
        if (File.Exists(targetPath)) return;

        await _downloadLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (File.Exists(targetPath)) return;

            if (Environment.OSVersion.Platform != PlatformID.Unix)
            {
                throw new PlatformNotSupportedException(
                    $"OpenCode binary auto-download is only supported on Unix. " +
                    $"On this platform ({Environment.OSVersion.Platform}), set OCAB_TEST_OPENCODE_BIN manually.");
            }

            var installRoot = Environment.GetEnvironmentVariable("OCAB_TEST_OPENCODE_DIR")
                ?? DefaultInstallRoot;
            Directory.CreateDirectory(installRoot);

            var tarballPath = Path.Combine(installRoot, "opencode-linux-x64.tar.gz");

            Console.Error.WriteLine($"[opencode-fixture] downloading {DownloadUrl} → {tarballPath}");
            var curlPsi = new ProcessStartInfo
            {
                FileName = "curl",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            curlPsi.ArgumentList.Add("--fail");
            curlPsi.ArgumentList.Add("--silent");
            curlPsi.ArgumentList.Add("--show-error");
            curlPsi.ArgumentList.Add("--location");
            curlPsi.ArgumentList.Add("--max-time");
            curlPsi.ArgumentList.Add("120");
            curlPsi.ArgumentList.Add("--output");
            curlPsi.ArgumentList.Add(tarballPath);
            curlPsi.ArgumentList.Add(DownloadUrl);
            using (var curl = Process.Start(curlPsi)
                ?? throw new InvalidOperationException("Failed to start curl"))
            {
                await curl.WaitForExitAsync().ConfigureAwait(false);
                if (curl.ExitCode != 0)
                {
                    var stderr = await curl.StandardError.ReadToEndAsync().ConfigureAwait(false);
                    throw new InvalidOperationException(
                        $"curl failed with exit {curl.ExitCode} downloading {DownloadUrl}: {stderr}");
                }
            }

            // Extract into the install root. The tarball ships a single
            // executable named `opencode` at the archive root.
            Console.Error.WriteLine($"[opencode-fixture] extracting {tarballPath} → {installRoot}");
            var tarPsi = new ProcessStartInfo
            {
                FileName = "tar",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = installRoot,
            };
            tarPsi.ArgumentList.Add("--extract");
            tarPsi.ArgumentList.Add("--file");
            tarPsi.ArgumentList.Add(tarballPath);
            tarPsi.ArgumentList.Add("--no-same-owner");
            using (var tar = Process.Start(tarPsi)
                ?? throw new InvalidOperationException("Failed to start tar"))
            {
                await tar.WaitForExitAsync().ConfigureAwait(false);
                if (tar.ExitCode != 0)
                {
                    var stderr = await tar.StandardError.ReadToEndAsync().ConfigureAwait(false);
                    throw new InvalidOperationException(
                        $"tar failed with exit {tar.ExitCode} extracting {tarballPath}: {stderr}");
                }
            }

            // chmod +x via PlatformNotSupportedException guard so the
            // CA1416 analyzer warning stays suppressed on Windows.
            try
            {
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(targetPath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }
            }
            catch (PlatformNotSupportedException) { /* not on Unix */ }

            var versionPsi = new ProcessStartInfo
            {
                FileName = targetPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                ArgumentList = { "--version" },
            };
            using var versionProc = Process.Start(versionPsi)
                ?? throw new InvalidOperationException("Failed to start OpenCode for version check");
            await versionProc.WaitForExitAsync().ConfigureAwait(false);
            var versionOut = (await versionProc.StandardOutput.ReadToEndAsync().ConfigureAwait(false)).Trim();
            if (versionProc.ExitCode != 0 || !versionOut.Contains(ExpectedVersionLiteral))
            {
                var stderr = await versionProc.StandardError.ReadToEndAsync().ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"OpenCode binary at {targetPath} reports unexpected version. " +
                    $"stdout='{versionOut}' stderr='{stderr}' expected='{ExpectedVersionLiteral}'");
            }

            try { File.Delete(tarballPath); } catch { /* best-effort */ }

            Console.Error.WriteLine($"[opencode-fixture] downloaded and verified {targetPath} ({versionOut})");
        }
        finally
        {
            _downloadLock.Release();
        }
    }
}
