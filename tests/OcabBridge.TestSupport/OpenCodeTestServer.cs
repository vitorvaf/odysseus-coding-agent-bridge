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
    // Pinned upstream version validated by the spike in
    // docs/discovery/012-opencode-contract-spike.md and locked in by
    // ADR-0017. Exposed as instance property for assertions on
    // /global/health payload; the literal is duplicated as a const so
    // EnsureBinaryDownloadedAsync can use it without holding the
    // fixture instance.
    public string ExpectedVersion { get; } = "1.18.8";

    private string? _binaryPath;
    private string? _scriptPath;
    private string _pidFile = "";
    private string _logFile = "";
    private int _openCodePid;

    // Static lock to serialize downloads of the OpenCode binary when
    // multiple xUnit collections (each holding its own fixture
    // instance) start concurrently on a CI runner. The lock is
    // process-wide so a single download serves all collection
    // fixtures within the same test process.
    private static readonly SemaphoreSlim _downloadLock = new(1, 1);

    // Default location used when neither OCAB_TEST_OPENCODE_BIN is set
    // nor a fixture-managed download has been performed yet.
    private const string DefaultBinaryPath = "/tmp/opencode-v1.18.8/opencode";
    private const string DefaultInstallRoot = "/tmp/opencode-v1.18.8";
    private const string DefaultVersionTag = "v1.18.8";
    private const string ExpectedVersionLiteral = "1.18.8";
    // The release tarball for Linux x64 (glibc). OpenCode also ships musl
    // and arm64 variants; the GitHub Actions runner is ubuntu-latest
    // which is glibc x64, so this asset is the right one.
    private const string DownloadUrl =
        $"https://github.com/anomalyco/opencode/releases/download/{DefaultVersionTag}/opencode-linux-x64.tar.gz";

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

    // Downloads the pinned OpenCode binary into the default location if
    // it is not already present. Serialized across the process so
    // concurrent fixtures (Contract + Integration tests share the
    // binary path) do not race each other.
    private static async Task EnsureBinaryDownloadedAsync(string targetPath)
    {
        // Already downloaded by another fixture in this process?
        if (File.Exists(targetPath)) return;

        await _downloadLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Re-check after acquiring the lock (another fixture may
            // have finished the download while we were waiting).
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

            // Verify the binary is executable and reports the pinned version.
            try
            {
                System.IO.File.SetUnixFileMode(targetPath,
                    System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite |
                    System.IO.UnixFileMode.UserExecute | System.IO.UnixFileMode.GroupRead |
                    System.IO.UnixFileMode.GroupExecute | System.IO.UnixFileMode.OtherRead |
                    System.IO.UnixFileMode.OtherExecute);
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

            // Clean up the tarball; keep the install root tidy.
            try { File.Delete(tarballPath); } catch { /* best-effort */ }

            Console.Error.WriteLine($"[opencode-fixture] downloaded and verified {targetPath} ({versionOut})");
        }
        finally
        {
            _downloadLock.Release();
        }
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
