using Npgsql;

namespace OcabBridge.Api.Infrastructure.Persistence;

// Wraps Npgsql connection creation. Centralizes the connection-string
// resolution so the bridge never embeds a literal connection string.
public sealed class NpgsqlConnectionFactory
{
    private readonly string _connectionString;

    public NpgsqlConnectionFactory(IConfiguration cfg)
    {
        _connectionString = cfg.GetConnectionString("OcabPg")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:OcabPg not configured. Set ConnectionStrings__OcabPg in env.");
    }

    public NpgsqlConnection Create()
    {
        var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        return conn;
    }
}
