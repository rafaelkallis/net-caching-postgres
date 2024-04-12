namespace RafaelKallis.Extensions.Caching.Postgres;

public class PostgresCacheOptions
{
    /// <summary>
    /// The connection string to the Postgres database.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// The schema of the table.
    /// </summary>
    public string Schema { get; set; } = PostgresCacheConstants.DefaultSchema;

    public string TableName { get; set; } = PostgresCacheConstants.DefaultTableName;

    public string Owner { get; set; } = PostgresCacheConstants.DefaultOwner;

    public int KeyMaxLength { get; set; } = PostgresCacheConstants.DefaultKeyMaxLength;
}