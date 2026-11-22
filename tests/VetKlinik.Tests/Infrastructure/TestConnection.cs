using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace VetKlinik.Tests.Infrastructure;

public static class TestConnection
{
    public static string ForDatabase(string database)
    {
        var connectionString = Environment.GetEnvironmentVariable("VETKLINIK_TEST_CONN")
            ?? new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .Build()
                .GetConnectionString("Default")
            ?? throw new InvalidOperationException("Bağlantı dizesi tanımlı değil (README: Kurulum).");

        return new SqlConnectionStringBuilder(connectionString) { InitialCatalog = database }.ConnectionString;
    }
}
