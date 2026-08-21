using Npgsql;
using PRN222.RagAssistant.Infrastructure;

namespace PRN222.RagAssistant.Tests;

public sealed class PostgresConnectionStringResolverTests
{
    [Fact]
    public void Normalize_NpgsqlKeyValueConnectionString_ReturnsOriginalValue()
    {
        const string connectionString =
            "Host=localhost;Port=5432;Database=prn222_rag;Username=postgres;Password=postgres";

        var normalized = PostgresConnectionStringResolver.Normalize(connectionString);

        Assert.Equal(connectionString, normalized);
    }

    [Fact]
    public void Normalize_RenderPostgresUrl_ConvertsToNpgsqlConnectionString()
    {
        const string renderUrl =
            "postgresql://prn222_rag:p%40ss%3Aword@dpg-example-a.singapore-postgres.render.com:5432/prn222_rag";

        var normalized = PostgresConnectionStringResolver.Normalize(renderUrl);
        var builder = new NpgsqlConnectionStringBuilder(normalized);

        Assert.Equal("dpg-example-a.singapore-postgres.render.com", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("prn222_rag", builder.Database);
        Assert.Equal("prn222_rag", builder.Username);
        Assert.Equal("p@ss:word", builder.Password);
    }

    [Fact]
    public void Normalize_ExternalPostgresUrl_PreservesSupportedSslMode()
    {
        const string externalUrl =
            "postgres://user:secret@example.render.com/database?sslmode=require";

        var normalized = PostgresConnectionStringResolver.Normalize(externalUrl);
        var builder = new NpgsqlConnectionStringBuilder(normalized);

        Assert.Equal(SslMode.Require, builder.SslMode);
    }
}
