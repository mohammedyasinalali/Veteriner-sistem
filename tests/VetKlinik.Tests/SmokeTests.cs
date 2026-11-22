using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace VetKlinik.Tests;

public class SmokeTests
{
    [Fact]
    public async Task Sql_server_is_reachable()
    {
        var connectionString = Environment.GetEnvironmentVariable("VETKLINIK_TEST_CONN")
            ?? new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .Build()
                .GetConnectionString("Default");

        Assert.False(string.IsNullOrEmpty(connectionString), "Bağlantı dizesi tanımlı değil (README: Kurulum).");

        // Uygulama veritabanı henüz olmayabilir; sunucuya master üzerinden bağlanılır.
        var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("SELECT 1", connection);
        Assert.Equal(1, await command.ExecuteScalarAsync());
    }
}
