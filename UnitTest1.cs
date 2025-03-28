using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace npgsql_reproduction;

public class UnitTest1 : IAsyncLifetime
{
    private readonly AppDbContext _dbContext;
    public const String ConnectionString = "Host=localhost;Username=postgres;Password=postgres;Database=efcore_jsonb_test";

    public UnitTest1()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .EnableSensitiveDataLogging()
            .Options;

        _dbContext = new AppDbContext(options);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task Test1(Int32 userId)
    {
        var result = await _dbContext.Users.FirstOrDefaultAsync(m => m.Id == userId);

        Assert.NotNull(result);
    }

    public async ValueTask InitializeAsync()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.Database.EnsureCreatedAsync();

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using (var cmd = new NpgsqlCommand("INSERT INTO \"Users\" (\"Id\", \"MetaData\") VALUES (1, @data::jsonb)", connection))
        {
            cmd.Parameters.AddWithValue("data", "{\"Access\":[null]}");
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = new NpgsqlCommand("INSERT INTO \"Users\" (\"Id\", \"MetaData\") VALUES (2, @data::jsonb)", connection))
        {
            cmd.Parameters.AddWithValue("data", "{\"Access\":null}");
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = new NpgsqlCommand("INSERT INTO \"Users\" (\"Id\", \"MetaData\") VALUES (3, @data)",
                         connection))
        {
            cmd.Parameters.AddWithValue("data", DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = new NpgsqlCommand("INSERT INTO \"Users\" (\"Id\", \"MetaData\") VALUES (4, @data::jsonb)", connection))
        {
            cmd.Parameters.AddWithValue("data", "{\"Access\":[]}");
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = new NpgsqlCommand("INSERT INTO \"Users\" (\"Id\", \"MetaData\") VALUES (5, @data::jsonb)", connection))
        {
            cmd.Parameters.AddWithValue("data", "{\"Access\":[{\"Name\":\"Admin\",\"Level\":10}]}");
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = new NpgsqlCommand("INSERT INTO \"Users\" (\"Id\", \"MetaData\") VALUES (6, @data::jsonb)", connection))
        {
            cmd.Parameters.AddWithValue("data", "{}");
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
    }
}

public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.OwnsOne(m => m.MetaData, m =>
            {
                m.ToJson();
                m.OwnsMany(u => u.Access);
            });
        });
    }
}

public class User
{
    public Int32 Id { get; set; }
    public MetaData? MetaData { get; set; }
}

public class MetaData
{
    public List<AccessMetaData>? Access { get; set; }
}

public class AccessMetaData
{
    public String Name { get; set; }
    public Int32 Level { get; set; }
}