using Npgsql;

namespace RafaelKallis.Extensions.Caching.Postgres.Tests;

[Collection(PostgresFixture.CollectionName)]
public class PostgresCacheDatabaseMigratorIntegrationTest : IntegrationTest
{
    private readonly PostgresFixture _postgresFixture;

    public PostgresCacheDatabaseMigratorIntegrationTest(ITestOutputHelper output, PostgresFixture postgresFixture) : base(output)
    {
        _postgresFixture = postgresFixture;
    }

    protected override void ConfigureOptions(PostgresCacheOptions options)
    {
        options.ConnectionString = _postgresFixture.ConnectionString;
    }

    [Fact]
    public async Task TableShouldExist()
    {
        await using NpgsqlConnection connection = await _postgresFixture.OpenConnection();
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $@"
            SELECT 1 FROM information_schema.tables 
            WHERE  tables.table_schema = '{PostgresCacheConstants.DefaultSchema}'
            AND    tables.table_name   = '{PostgresCacheConstants.DefaultTableName}'";
        await using NpgsqlDataReader dataReader = await command.ExecuteReaderAsync();
        bool readResult = await dataReader.ReadAsync();
        readResult.Should().BeTrue("because the table should exist");
    }
}