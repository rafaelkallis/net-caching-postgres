using Npgsql;

namespace RafaelKallis.Extensions.Caching.Postgres.Tests;

[Collection(PostgresFixture.CollectionName)]
public sealed class PostgresCacheDatabaseMigratorIntegrationTest(
    ITestOutputHelper output,
    PostgresFixture postgresFixture) : IntegrationTest(output, postgresFixture)
{
    [Fact]
    public async Task TableShouldExist()
    {
        await using NpgsqlConnection connection = await PostgresFixture.OpenConnection();
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $@"
            SELECT 1 FROM information_schema.tables 
            WHERE  tables.table_schema = '{PostgresCacheConstants.DefaultSchemaName}'
            AND    tables.table_name   = '{PostgresCacheConstants.DefaultTableName}'";
        await using NpgsqlDataReader dataReader = await command.ExecuteReaderAsync();
        bool readResult = await dataReader.ReadAsync();
        readResult.Should().BeTrue("the table should exist");
    }
}