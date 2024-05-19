using Npgsql;

namespace RafaelKallis.Extensions.Caching.Postgres.Tests;

public sealed class PostgresCacheMigratorIntegrationTest(
    ITestOutputHelper output,
    PostgresFixture postgresFixture) : IntegrationTest(output, postgresFixture)
{
    protected override void ConfigureOptions(PostgresCacheOptions options)
    {
        base.ConfigureOptions(options);
        options.MigrateOnStart = false;
    }

    [Theory]
    [CombinatorialData]
    public async Task WhenMigrate_TablesShouldExist(bool pgBouncer)
    {
        if (pgBouncer)
        {
            PostgresCacheOptions.ConnectionString = PostgresFixture.ConnectionStringPgBouncer;
        }

        string schemaName = PostgresCacheConstants.DefaultSchemaName;
        string tableName = PostgresCacheConstants.DefaultTableName;
        string mirationsHistoryTableName = PostgresCacheConstants.DefaultMigrationsHistoryTableName;

        PostgresCacheOptions.SchemaName = schemaName;
        PostgresCacheOptions.TableName = tableName;
        PostgresCacheOptions.MigrationHistoryTableName = tableName;

        await PostgresCache.MigrateAsync(CancellationToken.None);

        await using NpgsqlConnection connection = await PostgresFixture.OpenConnectionAsync();

        await using (NpgsqlCommand command = connection.CreateCommand())
        {
            command.CommandText = $@"
                SELECT 1 FROM information_schema.tables 
                WHERE  tables.table_schema = '{schemaName}'
                AND    tables.table_name   = '{mirationsHistoryTableName}';";
            await using NpgsqlDataReader dataReader = await command.ExecuteReaderAsync();
            bool readResult = await dataReader.ReadAsync();
            readResult.Should().BeTrue("the table should exist");
        }

        await using (NpgsqlCommand command = connection.CreateCommand())
        {
            command.CommandText = $@"
                SELECT 1 FROM information_schema.tables 
                WHERE  tables.table_schema = '{schemaName}'
                AND    tables.table_name   = '{tableName}';";
            await using NpgsqlDataReader dataReader = await command.ExecuteReaderAsync();
            bool readResult = await dataReader.ReadAsync();
            readResult.Should().BeTrue("the table should exist");
        }
    }
}