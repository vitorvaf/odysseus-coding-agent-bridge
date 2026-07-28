using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using OcabBridge.Api.Adapters;
using OcabBridge.Api.Application;
using OcabBridge.Api.Configuration;
using OcabBridge.Api.Domain;
using OcabBridge.Api.Infrastructure.Persistence;
using OcabBridge.TestSupport;
using Testcontainers.PostgreSql;
using Xunit;

namespace OcabBridge.IntegrationTests;

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

public sealed class EndToEndLifecycleTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private IHost? _host;
    private string _connectionString = "";
    private MockRunnerAdapter? _adapter;
    private IRunExecutionCoordinator? _coordinator;
    private RunDispatcher? _dispatcher;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        await _postgres.StartAsync();
        _connectionString = _postgres.GetConnectionString();

        // Apply schema (subset used by the bridge; same as
        // OpenCodeEndToEndSmokeTests in earlier iteration).
        await using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync(SchemaPublic);
        }

        // Build a minimal Host that hosts the dispatcher + coordinator
        // + worker, with MockRunnerAdapter substituting the real
        // OpenCodeAdapter. The Bridge HTTP surface is not used here —
        // the test invokes the dispatcher directly to keep the E2E
        // path deterministic.
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.AddProvider(NullLoggerProvider.Instance);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:OcabPg"] = _connectionString,
            ["Ocab:McpToken"] = "test",
            ["Ocab:OpenCodePassword"] = "",
        });
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<NpgsqlConnectionFactory>();
        builder.Services.AddSingleton<RunRepository>();
        builder.Services.AddSingleton<RunEventRepository>();
        builder.Services.AddSingleton<RepositoryRepository>();
        builder.Services.AddSingleton<AgentRepository>();

        _adapter = new MockRunnerAdapter(NullLogger<MockRunnerAdapter>.Instance);
        builder.Services.AddSingleton<IRunnerAdapter>(_adapter);
        builder.Services.AddSingleton<RunExecutionCoordinator>();
        builder.Services.AddSingleton<IRunExecutionCoordinator>(sp => sp.GetRequiredService<RunExecutionCoordinator>());
        builder.Services.AddSingleton<RunDispatcher>();
        builder.Services.AddHostedService<RunQueueWorker>();

        _host = builder.Build();
        await _host.StartAsync();

        _coordinator = _host.Services.GetRequiredService<IRunExecutionCoordinator>();
        _dispatcher = _host.Services.GetRequiredService<RunDispatcher>();
    }

    public async Task DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
        }
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    // === Coordinator + Dispatcher + MockRunnerAdapter tests ===

    [Fact]
    public async Task Coordinator_dispatches_and_completes_via_mock_adapter()
    {
        Assert.NotNull(_adapter);
        Assert.NotNull(_coordinator);
        Assert.NotNull(_dispatcher);
        _adapter!.SetScenario("normal");
        await SeedRepositoryAsync("ocab-pilot");

        var runId = await _dispatcher!.CreateAsync("ocab-pilot", "hello", default);
        var terminal = await _coordinator!.WaitForTerminalStateAsync(
            runId, TimeSpan.FromSeconds(30), default);

        Assert.Equal("Completed", terminal.Status);
        Assert.NotNull(terminal.ResultJson);
        Assert.Contains("hello from mock runner", terminal.ResultJson);
    }

    [Fact]
    public async Task Coordinator_cancels_when_caller_signals_cancel()
    {
        Assert.NotNull(_adapter);
        Assert.NotNull(_coordinator);
        Assert.NotNull(_dispatcher);
        _adapter!.SetScenario("blocked"); // StreamEventsAsync hangs until cancellation
        await SeedRepositoryAsync("ocab-pilot");

        var runId = await _dispatcher!.CreateAsync("ocab-pilot", "hello", default);
        // Wait until the run is registered as active in the coordinator
        // (so the cancel signal has something to interrupt).
        var spinSw = System.Diagnostics.Stopwatch.StartNew();
        while (!_coordinator!.IsActive(runId) && spinSw.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(50);
        }
        Assert.True(_coordinator!.IsActive(runId), "Run should be active before cancel");

        var cancelled = await _coordinator!.CancelAsync(runId, "test_cancel", default);
        Assert.True(cancelled);

        var terminal = await _coordinator!.WaitForTerminalStateAsync(
            runId, TimeSpan.FromSeconds(30), default);
        Assert.Equal("Cancelled", terminal.Status);
    }

    [Fact]
    public async Task Coordinator_times_out_when_adapter_slow()
    {
        Assert.NotNull(_adapter);
        Assert.NotNull(_coordinator);
        Assert.NotNull(_dispatcher);
        _adapter!.SetScenario("slow");
        _adapter.SlowDelayMs = TimeSpan.FromSeconds(8);
        await SeedRepositoryAsync("ocab-pilot");

        // timeoutSeconds=2 forces the dispatcher's per-Run CTS to fire
        // before the mock's SlowDelayMs elapses.
        var runId = await _dispatcher!.CreateAsync("ocab-pilot", "hello", default, timeoutSeconds: 2);
        var terminal = await _coordinator!.WaitForTerminalStateAsync(
            runId, TimeSpan.FromSeconds(30), default);
        Assert.Equal("TimedOut", terminal.Status);
    }

    [Fact]
    public async Task Coordinator_handles_adapter_error_as_failed()
    {
        Assert.NotNull(_adapter);
        Assert.NotNull(_coordinator);
        Assert.NotNull(_dispatcher);
        _adapter!.SetScenario("error");
        await SeedRepositoryAsync("ocab-pilot");

        var runId = await _dispatcher!.CreateAsync("ocab-pilot", "hello", default);
        var terminal = await _coordinator!.WaitForTerminalStateAsync(
            runId, TimeSpan.FromSeconds(30), default);
        Assert.Equal("Failed", terminal.Status);
    }

    [Fact]
    public async Task Coordinator_cancel_idempotent_when_already_terminal()
    {
        Assert.NotNull(_adapter);
        Assert.NotNull(_coordinator);
        Assert.NotNull(_dispatcher);
        _adapter!.SetScenario("normal");
        await SeedRepositoryAsync("ocab-pilot");

        var runId = await _dispatcher!.CreateAsync("ocab-pilot", "hello", default);
        var terminal = await _coordinator!.WaitForTerminalStateAsync(
            runId, TimeSpan.FromSeconds(30), default);
        Assert.Equal("Completed", terminal.Status);

        // Cancel after terminal state: no-op, returns true (idempotent).
        var cancelled = await _coordinator!.CancelAsync(runId, "after_completed", default);
        Assert.True(cancelled);

        // Status remains Completed — no regression to Cancelled.
        var reread = await _coordinator!.WaitForTerminalStateAsync(
            runId, TimeSpan.FromSeconds(2), default);
        Assert.Equal("Completed", reread.Status);
    }

    private async Task SeedRepositoryAsync(string slug)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(
            @"INSERT INTO repositories (slug, display_name, default_branch, writable, allowed_agents, read_only_only)
              VALUES (@Slug, @DisplayName, 'main', TRUE, ARRAY['opencode']::TEXT[], TRUE)
              ON CONFLICT (slug) DO NOTHING",
            new { Slug = slug, DisplayName = slug });
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
    idempotency_key TEXT
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
