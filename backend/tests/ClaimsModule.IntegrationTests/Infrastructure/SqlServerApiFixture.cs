using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace ClaimsModule.IntegrationTests.Infrastructure;

public sealed class SqlServerApiFixture : IAsyncLifetime
{
    public static readonly Guid OrganisationId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid HandlerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid SupervisorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private WebApplicationFactory<Program> _factory = null!;

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _sqlServer.StartAsync();

        ConnectionString = new SqlConnectionStringBuilder(_sqlServer.GetConnectionString()) { InitialCatalog = "ClaimsModuleIntegration" }.ConnectionString;

        await using (var context = CreateDbContext(OrganisationId))
        {
            await context.Database.MigrateAsync();
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        });
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _sqlServer.DisposeAsync();
    }

    public HttpClient CreateClient(Guid userId, string role)
    {
        var client = _factory.CreateClient();
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { userId, role })));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public HttpClient CreateHandlerClient() => CreateClient(HandlerId, "handler");

    public HttpClient CreateSupervisorClient() => CreateClient(SupervisorId, "supervisor");

    public ClaimsDbContext CreateDbContext(Guid organisationId)
    {
        var options = new DbContextOptionsBuilder<ClaimsDbContext>().UseSqlServer(ConnectionString).Options;
        return new ClaimsDbContext(options, new FixedCurrentUser(organisationId), TimeProvider.System);
    }

    private sealed class FixedCurrentUser(Guid organisationId) : ICurrentUserService
    {
        public Guid? UserId => null;
        public string? Role => null;
        public Guid OrganisationId => organisationId;
    }
}

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<SqlServerApiFixture>
{
    public const string Name = "SQL Server integration";
}
