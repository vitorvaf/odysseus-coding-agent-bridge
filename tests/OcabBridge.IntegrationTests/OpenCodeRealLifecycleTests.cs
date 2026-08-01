using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OcabBridge.TestSupport;
using Xunit;

namespace OcabBridge.IntegrationTests;

// SLICE-STAB-005A.2 — OpenCodeRealLifecycleTests.
//
// STAB-005A.2 reabilita o cenário Completion contra o OpenCode v1.18.8 real,
// instrumentado com DeterministicOpenCodeProvider (HTTP OpenAI-compatible).
// A fixture inicia o OpenCode real via ProcessStartInfo, escreve um
// opencode.jsonc que aponta para o provider in-process em uma porta
// livre, e expõe o provider no PilotRepoPath do worktree.
//
// Os cenários cancel/timeout/provider-error permanecem skipped — STAB-005A.2
// é escopado para Completion apenas.

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class OpenOpenCodeRealCollection : ICollectionFixture<OpenCodeRealFixture>
{
    public const string Name = "OpenCodeRealLifecycle";
}

public sealed class OpenCodeRealFixture : IAsyncLifetime
{
    private const string OpenCodeBinaryEnv = "OCAB_TEST_OPENCODE_BIN";
    private const string OpenCodeBinaryDefault = "/tmp/opencode-v1.18.8/opencode";
    private const string OpenCodePassword = "test123_reallifecycle";

    // Binary download support mirrors OpenCodeTestServer. When the
    // OpenCode v1.18.8 binary is not present at the default path (e.g.
    // in a fresh CI runner), the fixture downloads it from the official
    // GitHub release tarball before starting the runner. The
    // process-wide lock serialises concurrent fixture initialisations.
    private static readonly SemaphoreSlim _downloadLock = new(1, 1);
    private const string DefaultInstallRoot = "/tmp/opencode-v1.18.8";
    private const string DefaultVersionTag = "v1.18.8";
    private const string ExpectedVersionLiteral = "1.18.8";
    private const string DownloadUrl =
        $"https://github.com/anomalyco/opencode/releases/download/{DefaultVersionTag}/opencode-linux-x64.tar.gz";

    public int Port { get; private set; }
    public string BaseUrl => $"http://127.0.0.1:{Port}";
    public string ConfigRoot { get; private set; } = "";
    public DeterministicOpenCodeProvider Provider { get; private set; } = null!;
    public string PilotRepoPath { get; } = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../poc/fixtures/pilot-repo"));
    public const string ProviderConfigKey = "__SET_ME__";

    private Process? _process;
    private Task? _stdoutDrain;
    private Task? _stderrDrain;

    public async Task InitializeAsync()
    {
        var binary = Environment.GetEnvironmentVariable(OpenCodeBinaryEnv) ?? OpenCodeBinaryDefault;
        if (!File.Exists(binary))
        {
            await EnsureBinaryDownloadedAsync(binary).ConfigureAwait(false);
        }
        if (!File.Exists(binary))
        {
            throw new FileNotFoundException(
                $"OpenCode binary still missing at {binary} after download attempt. " +
                "Set OCAB_TEST_OPENCODE_BIN or fix the network access to " +
                "github.com/anomalyco/opencode releases.");
        }

        // Per-test exclusive XDG_CONFIG_HOME so OpenCode cannot
        // mutate any other fixture's config. Each test gets its own
        // fresh tree at $XDG_CONFIG_HOME/opencode/.
        ConfigRoot = Path.Combine(
            Path.GetTempPath(),
            $"ocab-real-{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(ConfigRoot, "opencode"));

        // Provider host: deterministic HTTP provider on a free port.
        var providerPort = GetFreePort();
        Provider = new DeterministicOpenCodeProvider(providerPort);
        await Provider.StartAsync();

        // Build an opencode.jsonc that points the runner at the
        // deterministic provider, with `api` set so the SDK receives
        // the baseURL via `model.api.url` (bypasses the providerOptions
        // bug #5674 where per-call options are silently ignored for
        // @ai-sdk/openai-compatible).
        var openCodeConfigDir = Path.Combine(ConfigRoot, "opencode");
        var openCodeConfigPath = Path.Combine(openCodeConfigDir, "opencode.jsonc");
        var openCodeAuthPath = Path.Combine(openCodeConfigDir, "auth.json");
        var openCodeConfig = $$"""
        {
          "$schema": "https://opencode.ai/config.json",
          "model": "deterministic/deterministic-model",
          "provider": {
            "deterministic": {
              "npm": "@ai-sdk/openai-compatible",
              "name": "OCAB deterministic provider",
              "api": "http://127.0.0.1:{{providerPort}}/v1",
              "options": { "apiKey": "{{ProviderConfigKey}}" },
              "models": {
                "deterministic-model": {
                  "name": "OCAB deterministic model",
                  "limit": { "context": 32768, "output": 4096 }
                }
              }
            }
          }
        }
        """;
        await File.WriteAllTextAsync(openCodeConfigPath, openCodeConfig);
        var authJson = $$$"""{"deterministic":{"type":"api","key":"{{ProviderConfigKey}}"}}""";
        await File.WriteAllTextAsync(openCodeAuthPath, authJson);

        Port = GetFreePort();

        var psi = new ProcessStartInfo
        {
            FileName = binary,
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
        psi.EnvironmentVariables["XDG_CONFIG_HOME"] = ConfigRoot;
        psi.EnvironmentVariables["OPENCODE_SERVER_PASSWORD"] = OpenCodePassword;

        // Materialise the pilot-repo when absent (e.g. in CI checkouts
        // where poc/fixtures/ is gitignored). OpenCode requires its
        // working directory to exist before Process.Start succeeds;
        // creating a minimal git-backed directory here avoids a
        // Win32Exception at startup without depending on the fixture
        // being versioned.
        EnsurePilotRepoExists();

        psi.WorkingDirectory = PilotRepoPath;

        try
        {
            _process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start OpenCode");
        }
        catch
        {
            try { await Provider.DisposeAsync(); } catch { }
            Directory.Delete(ConfigRoot, recursive: true);
            throw;
        }

        var logPath = Path.Combine(ConfigRoot, "opencode.log");
        _stdoutDrain = DrainAsync(_process.StandardOutput, logPath + ".stdout");
        _stderrDrain = DrainAsync(_process.StandardError, logPath + ".stderr");

        await WaitForReadyAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
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
                killPsi.ArgumentList.Add($"kill -TERM {this._process.Id} 2>/dev/null; true");
                using var kill = Process.Start(killPsi);
                kill?.WaitForExit(2000);
            }
            catch { /* SIGTERM failed; rely on WaitForExit below */ }

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

        if (_stdoutDrain is not null)
        {
            try { await _stdoutDrain.ConfigureAwait(false); } catch { }
        }
        if (_stderrDrain is not null)
        {
            try { await _stderrDrain.ConfigureAwait(false); } catch { }
        }

        if (Provider is not null)
        {
            try { await Provider.DisposeAsync().ConfigureAwait(false); } catch { }
        }

        // Best-effort cleanup; do not fail the fixture if the directory
        // is still held by lingering handles.
        try { Directory.Delete(ConfigRoot, recursive: true); } catch { }
    }

    // Materialises a minimal git-backed pilot repository at PilotRepoPath
    // when it is missing from the checkout (e.g. CI runners where
    // poc/fixtures/ is gitignored). OpenCode requires its working
    // directory to exist before Process.Start succeeds; creating the
    // directory here avoids a Win32Exception at startup without
    // depending on the fixture being versioned. If the path already
    // exists and contains a .git directory, it is left untouched so
    // local development with a real fixture is not disturbed.
    private void EnsurePilotRepoExists()
    {
        if (Directory.Exists(PilotRepoPath)
            && Directory.Exists(Path.Combine(PilotRepoPath, ".git")))
        {
            return;
        }

        Directory.CreateDirectory(PilotRepoPath);
        File.WriteAllText(
            Path.Combine(PilotRepoPath, "README.md"),
            "# OCAB Pilot\n\nFixture materialised by OpenCodeRealFixture for CI.\n");

        void RunGit(params string[] args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = PilotRepoPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var arg in args) psi.ArgumentList.Add(arg);
            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException(
                    $"git failed to start: {string.Join(' ', args)}");
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            if (proc.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"git {string.Join(' ', args)} failed (exit={proc.ExitCode}). " +
                    $"stdout={stdout} stderr={stderr}");
            }
        }

        RunGit("init", "--initial-branch=main");
        RunGit("config", "user.name", "OCAB Tests");
        RunGit("config", "user.email", "ocab-tests@example.invalid");
        RunGit("add", "README.md");
        RunGit("commit", "-m", "init pilot repo");
    }

    // Downloads the OpenCode v1.18.8 binary from the official release
    // tarball when it is not present at the expected path. Mirrors the
    // download logic in OpenCodeTestServer. Serialised by a process-wide
    // lock so multiple fixtures initialising concurrently do not race.
    // Throws on non-Unix platforms (matches OpenCodeTestServer).
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

            Console.Error.WriteLine($"[opencode-real-fixture] downloading {DownloadUrl} → {tarballPath}");
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

            Console.Error.WriteLine($"[opencode-real-fixture] extracting {tarballPath} → {installRoot}");
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

            // Verify the binary reports the expected version before
            // allowing the fixture to proceed.
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
        }
        finally
        {
            _downloadLock.Release();
        }
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private async Task DrainAsync(StreamReader reader, string logPath)
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

    private async Task WaitForReadyAsync(TimeSpan timeout)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var auth = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"opencode:{OpenCodePassword}"));
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try
            {
                using var resp = await http.GetAsync($"{BaseUrl}/doc").ConfigureAwait(false);
                if (resp.IsSuccessStatusCode) return;
            }
            catch { /* still starting */ }
            await Task.Delay(500).ConfigureAwait(false);
        }
        throw new InvalidOperationException(
            $"Real OpenCode did not become ready on {BaseUrl} within {timeout}");
    }
}

[Collection(OpenOpenCodeRealCollection.Name)]
[Trait("Category", "RealOpenCodePoc")]
public sealed class OpenCodeRealLifecycleTests : IClassFixture<OpenCodeRealFixture>, IAsyncLifetime
{
    private readonly OpenCodeRealFixture _openCode;
    private IHost? _host;
    private string _connectionString = "";

    public OpenCodeRealLifecycleTests(OpenCodeRealFixture openCode) => _openCode = openCode;

    public async Task InitializeAsync()
    {
        var postgres = new Testcontainers.PostgreSql.PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        await postgres.StartAsync();
        _connectionString = postgres.GetConnectionString();

        await using (var conn = new Npgsql.NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync(SchemaPublic);
        }

        // The OpenCodeAdapter reads OCAB_OPENCODE_WORKSPACE_DIR from the
        // environment to build /event?directory=... in the OpenCode
        // v1.18.8 SSE contract. The pilot repo path is the workspace.
        Environment.SetEnvironmentVariable("OCAB_OPENCODE_WORKSPACE_DIR", _openCode.PilotRepoPath);

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.AddProvider(NullLoggerProvider.Instance);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:OcabPg"] = _connectionString,
            ["Ocab:McpToken"] = "test123_smoke_real",
            ["Ocab:OpenCodeUrl"] = _openCode.BaseUrl,
            ["Ocab:OpenCodePassword"] = "test123_reallifecycle",
        });
        builder.Services.Configure<OcabBridge.Api.Configuration.OcabOptions>(
            builder.Configuration.GetSection("Ocab"));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<OcabBridge.Api.Infrastructure.Persistence.NpgsqlConnectionFactory>();
        builder.Services.AddSingleton<OcabBridge.Api.Infrastructure.Persistence.RunRepository>();
        builder.Services.AddSingleton<OcabBridge.Api.Infrastructure.Persistence.RunEventRepository>();
        builder.Services.AddSingleton<OcabBridge.Api.Infrastructure.Persistence.RepositoryRepository>();
        builder.Services.AddSingleton<OcabBridge.Api.Infrastructure.Persistence.AgentRepository>();
        builder.Services.AddTransient<OcabBridge.Api.Adapters.OpenCodeAuthHandler>();
        builder.Services.AddHttpClient<OcabBridge.Api.Adapters.OpenCodeAdapter>(c =>
        {
            c.BaseAddress = new Uri(_openCode.BaseUrl);
            c.Timeout = TimeSpan.FromSeconds(15);
        }).AddHttpMessageHandler<OcabBridge.Api.Adapters.OpenCodeAuthHandler>();
        builder.Services.AddSingleton<OcabBridge.Api.Adapters.IRunnerAdapter>(sp => sp.GetRequiredService<OcabBridge.Api.Adapters.OpenCodeAdapter>());
        builder.Services.AddSingleton<OcabBridge.Api.Application.RunExecutionCoordinator>();
        builder.Services.AddSingleton<OcabBridge.Api.Application.IRunExecutionCoordinator>(sp => sp.GetRequiredService<OcabBridge.Api.Application.RunExecutionCoordinator>());
        builder.Services.AddSingleton<OcabBridge.Api.Application.RunDispatcher>();
        builder.Services.AddHostedService<OcabBridge.Api.Application.RunQueueWorker>();

        _host = builder.Build();
        await _host.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_host is not null) await _host.StopAsync();
    }

    private async Task SeedRepositoryAsync(string slug)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(
            @"INSERT INTO repositories (slug, display_name, default_branch, writable, allowed_agents, read_only_only)
              VALUES (@Slug, @DisplayName, 'main', TRUE, ARRAY['opencode']::TEXT[], TRUE)
              ON CONFLICT (slug) DO NOTHING",
            new { Slug = slug, DisplayName = slug });
    }

    [Fact]
    [Trait("Category", "RealOpenCodePoc")]
    public async Task RealOpenCode_completion_returns_known_response()
    {
        _openCode.Provider.SetScenario("normal");
        var before = PilotGitState();
        var runId = await RunScenarioAndAssertTerminal(
            "ocab-pilot",
            "Completed",
            prompt: "Responda somente: OCAB_PROVIDER_OK");
        Assert.Equal(before, PilotGitState());
        var result = await ReadRunResultAsync(runId);
        Assert.Contains("OCAB_PROVIDER_OK", result);
        Assert.True(await CountRunEventsAsync(runId) >= 1,
            "expected at least one RunEvent for the completion");
    }

    [Fact(Skip = "Provider error path is out of scope for STAB-005A.2 (Completion only).")]
    [Trait("Category", "RealOpenCodePoc")]
    public async Task RealOpenCode_provider_error_fails_run()
    {
        _openCode.Provider.SetScenario("error");
        var before = PilotGitState();
        var runId = await RunScenarioAndAssertTerminal("ocab-pilot", "Failed", prompt: "Leia o README.md.");
        Assert.Equal("Failed", (await ReadRunAsync(runId)).Status);
        Assert.Equal(before, PilotGitState());
    }

    [Fact(Skip = "Cancel/timeout paths are out of scope for STAB-005A.2 (Completion only).")]
    [Trait("Category", "RealOpenCodePoc")]
    public async Task RealOpenCode_timeout_aborts_slow_prompt()
    {
        _openCode.Provider.SetScenario("slow");
        var before = PilotGitState();
        var runId = await RunScenarioAndAssertTerminal("ocab-pilot", "TimedOut", 2, "Leia o README.md e aguarde.");
        Assert.Equal("TimedOut", (await ReadRunAsync(runId)).Status);
        Assert.Equal(before, PilotGitState());
    }

    [Fact(Skip = "Cancel/timeout paths are out of scope for STAB-005A.2 (Completion only).")]
    [Trait("Category", "RealOpenCodePoc")]
    public async Task RealOpenCode_cancel_aborts_blocked_prompt()
    {
        _openCode.Provider.SetScenario("blocked");
        var before = PilotGitState();
        await SeedRepositoryAsync("ocab-pilot");
        var coordinator = _host!.Services.GetRequiredService<OcabBridge.Api.Application.IRunExecutionCoordinator>();
        var dispatcher = _host.Services.GetRequiredService<OcabBridge.Api.Application.RunDispatcher>();
        var runId = await dispatcher.CreateAsync("ocab-pilot", "Leia o README.md e aguarde.", default, 30);
        var sw = Stopwatch.StartNew();
        while (!coordinator.IsActive(runId) && sw.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(50);
        Assert.True(coordinator.IsActive(runId));
        Assert.True(await coordinator.CancelAsync(runId, "poc_cancel", default));
        var terminal = await coordinator.WaitForTerminalStateAsync(runId, TimeSpan.FromSeconds(30), default);
        Assert.Equal("Cancelled", terminal.Status);
        Assert.Equal(before, PilotGitState());
    }

    private async Task<Guid> RunScenarioAndAssertTerminal(string slug, string expected, int timeoutSeconds = 300, string prompt = "hello world")
    {
        Assert.NotNull(_host);
        await SeedRepositoryAsync(slug);
        var coordinator = _host!.Services.GetRequiredService<OcabBridge.Api.Application.IRunExecutionCoordinator>();
        var dispatcher = _host.Services.GetRequiredService<OcabBridge.Api.Application.RunDispatcher>();
        var runId = await dispatcher.CreateAsync(slug, prompt, default, timeoutSeconds);
        var terminal = await coordinator.WaitForTerminalStateAsync(runId, TimeSpan.FromSeconds(30), default);
        Assert.True(terminal.Status == expected,
            $"expected={expected}, actual={terminal.Status}, providerRequests={_openCode.Provider.AllRequests}");
        return runId;
    }

    private async Task<OcabBridge.Api.Domain.Run> ReadRunAsync(Guid runId)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        return await conn.QuerySingleAsync<OcabBridge.Api.Domain.Run>(
            "SELECT run_id AS RunId, status AS Status, result::text AS ResultJson FROM runs WHERE run_id=@runId", new { runId });
    }

    private async Task<string> ReadRunResultAsync(Guid runId) => (await ReadRunAsync(runId)).ResultJson ?? "";

    private async Task<int> CountRunEventsAsync(Guid runId)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM run_events WHERE run_id=@runId", new { runId });
    }

    private string PilotGitState()
    {
        using var status = Process.Start(new ProcessStartInfo("git", "status --short") { WorkingDirectory = _openCode.PilotRepoPath, RedirectStandardOutput = true, UseShellExecute = false });
        using var head = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD") { WorkingDirectory = _openCode.PilotRepoPath, RedirectStandardOutput = true, UseShellExecute = false });
        return $"{status!.StandardOutput.ReadToEnd()}|{head!.StandardOutput.ReadToEnd().Trim()}";
    }

    internal const string SchemaPublic = @"
CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS repositories (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    slug TEXT UNIQUE NOT NULL,
    display_name TEXT NOT NULL,
    default_branch TEXT NOT NULL DEFAULT 'main',
    writable BOOLEAN NOT NULL DEFAULT TRUE,
    allowed_agents TEXT[] NOT NULL DEFAULT ARRAY['opencode']::TEXT[],
    read_only_only BOOLEAN NOT NULL DEFAULT FALSE,
    validations JSONB NOT NULL DEFAULT '[]'::jsonb,
    policies JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS agents (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT UNIQUE NOT NULL,
    display_name TEXT NOT NULL,
    version TEXT NOT NULL DEFAULT '0.0.0',
    enabled BOOLEAN NOT NULL DEFAULT TRUE,
    capabilities TEXT[] NOT NULL DEFAULT ARRAY[]::TEXT[],
    endpoint TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS runs (
    run_id UUID PRIMARY KEY,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at TIMESTAMPTZ,
    finished_at TIMESTAMPTZ,
    status TEXT NOT NULL,
    repository_slug TEXT,
    prompt TEXT,
    result JSONB,
    timeout_seconds INT NOT NULL DEFAULT 300,
    idempotency_key TEXT,
    worker_id TEXT,
    lease_expires_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS run_events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    run_id UUID NOT NULL REFERENCES runs(run_id) ON DELETE CASCADE,
    sequence BIGSERIAL NOT NULL,
    from_state TEXT,
    to_state TEXT NOT NULL,
    actor TEXT NOT NULL,
    reason TEXT,
    metadata JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS idempotency_keys (
    key TEXT PRIMARY KEY,
    request_hash TEXT NOT NULL,
    run_id UUID NOT NULL REFERENCES runs(run_id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMPTZ NOT NULL DEFAULT (NOW() + INTERVAL '24 hours')
);
";
}
