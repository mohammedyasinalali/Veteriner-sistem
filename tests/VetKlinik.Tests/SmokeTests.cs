using Microsoft.Data.SqlClient;
using VetKlinik.Tests.Infrastructure;

namespace VetKlinik.Tests;

public class SmokeTests
{
    [Fact]
    public async Task Sql_server_is_reachable()
    {
        await using var connection = new SqlConnection(TestConnection.ForDatabase("master"));
        await connection.OpenAsync();

        await using var command = new SqlCommand("SELECT 1", connection);
        Assert.Equal(1, await command.ExecuteScalarAsync());
    }
}
