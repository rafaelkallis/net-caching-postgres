using Npgsql;

namespace RafaelKallis.Extensions.Caching.Postgres.Tests;

[Collection(PostgresFixture.CollectionName)]
public sealed class PostgresCacheMigratorIntegrationTest(
    ITestOutputHelper output,
    PostgresFixture postgresFixture) : IntegrationTest(output, postgresFixture)
{
    protected override void ConfigureOptions(PostgresCacheOptions options)
    {
        base.ConfigureOptions(options);
        options.MigrateOnStart = false;
    }

    [Fact]
    public async Task WhenMigrate_TableShouldExist()
    {
        string schemaName = PostgresCacheConstants.DefaultSchemaName;
        string tableName = PostgresCacheConstants.DefaultTableName;

        PostgresCacheOptions.SchemaName = schemaName;
        PostgresCacheOptions.TableName = tableName;

        await PostgresCache.MigrateAsync(CancellationToken.None);

        await using NpgsqlConnection connection = await PostgresFixture.OpenConnection();
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $@"
            SELECT 1 FROM information_schema.tables 
            WHERE  tables.table_schema = '{schemaName}'
            AND    tables.table_name   = '{tableName}';";
        await using NpgsqlDataReader dataReader = await command.ExecuteReaderAsync();
        bool readResult = await dataReader.ReadAsync();
        readResult.Should().BeTrue("the table should exist");
    }
}