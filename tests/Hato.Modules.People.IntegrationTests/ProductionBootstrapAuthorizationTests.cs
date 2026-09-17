using Hato.Api.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Hato.Modules.People.IntegrationTests;

public sealed class ProductionBootstrapAuthorizationTests
{
    [Fact]
    public void InitialProductionAdmin_RequiresTheConfiguredHeaderToken()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bootstrap:InitialAdminToken"] = "one-time-bootstrap-token"
            })
            .Build();
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Production };
        var request = new DefaultHttpContext().Request;

        Assert.False(PeopleEndpoints.IsInitialProductionAdminAuthorized(environment, configuration, request));

        request.Headers["X-Hato-Bootstrap-Token"] = "wrong-token";
        Assert.False(PeopleEndpoints.IsInitialProductionAdminAuthorized(environment, configuration, request));

        request.Headers["X-Hato-Bootstrap-Token"] = "one-time-bootstrap-token";
        Assert.True(PeopleEndpoints.IsInitialProductionAdminAuthorized(environment, configuration, request));
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = string.Empty;
        public string ApplicationName { get; set; } = "Hato.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
