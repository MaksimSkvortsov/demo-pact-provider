using System.Net;
using DemoPactProvider.Api;
using Microsoft.AspNetCore.Builder;
using PactNet.Verifier;
using Xunit.Abstractions;

namespace DemoPactProvider.ContractTests;

public sealed class ProviderContractTests
{
    private const string ProviderName = "customer-provider";

    private readonly ITestOutputHelper output;

    public ProviderContractTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task Verify_TypeScript_Consumer()
    {
        await VerifyConsumerPactsAsync(
            environmentVariableName: "PACT_TS_PACT_DIR",
            defaultRelativePath: Path.Combine("contracts", "ts", "pacts"));
    }

    [Fact]
    public async Task Verify_DotNet_Consumer()
    {
        await VerifyConsumerPactsAsync(
            environmentVariableName: "PACT_DOTNET_PACT_DIR",
            defaultRelativePath: Path.Combine("contracts", "dotnet", "pacts"));
    }

    private async Task VerifyConsumerPactsAsync(string environmentVariableName, string defaultRelativePath)
    {
        var pactDirectory = ResolvePactDirectory(environmentVariableName, defaultRelativePath);
        var pactFiles = pactDirectory.Exists
            ? pactDirectory.GetFiles("*.json", SearchOption.TopDirectoryOnly)
            : [];

        if (pactFiles.Length == 0)
        {
            var message = $"No Pact JSON files found in '{pactDirectory.FullName}'. Set {environmentVariableName} to override this path.";

            if (IsCi())
            {
                throw new InvalidOperationException(message);
            }

            output.WriteLine(message);
            return;
        }

        await using var provider = await RunningProvider.StartAsync();

        output.WriteLine($"Provider listening at {provider.BaseUri}");
        output.WriteLine($"Verifying Pact files from {pactDirectory.FullName}");

        new PactVerifier(ProviderName)
            .WithHttpEndpoint(provider.BaseUri)
            .WithDirectorySource(pactDirectory, ["*.json"])
            .WithProviderStateUrl(new Uri(provider.BaseUri, "/provider-states"))
            .Verify();
    }

    private static DirectoryInfo ResolvePactDirectory(string environmentVariableName, string defaultRelativePath)
    {
        var configuredPath = Environment.GetEnvironmentVariable(environmentVariableName);

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return new DirectoryInfo(Path.GetFullPath(configuredPath));
        }

        return new DirectoryInfo(Path.GetFullPath(Path.Combine(GetRepositoryRoot(), defaultRelativePath)));
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DemoPactProvider.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root.");
    }

    private static bool IsCi()
    {
        return string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RunningProvider : IAsyncDisposable
    {
        private readonly WebApplication app;

        private RunningProvider(WebApplication app, Uri baseUri)
        {
            this.app = app;
            BaseUri = baseUri;
        }

        public Uri BaseUri { get; }

        public static async Task<RunningProvider> StartAsync()
        {
            var baseUri = new Uri($"http://127.0.0.1:{GetAvailablePort()}");
            var app = Program.BuildApp(["--urls", baseUri.ToString()]);
            await app.StartAsync();

            var provider = new RunningProvider(app, baseUri);
            await provider.WaitUntilReadyAsync();

            return provider;
        }

        public async ValueTask DisposeAsync()
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }

        private async Task WaitUntilReadyAsync()
        {
            using var client = new HttpClient();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            while (!timeout.IsCancellationRequested)
            {
                try
                {
                    using var response = await client.GetAsync(new Uri(BaseUri, "/customers/123"), timeout.Token);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return;
                    }
                }
                catch (HttpRequestException)
                {
                }

                await Task.Delay(100, timeout.Token);
            }

            throw new TimeoutException($"Provider did not start at {BaseUri}.");
        }

        private static int GetAvailablePort()
        {
            var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);

            try
            {
                listener.Start();
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
