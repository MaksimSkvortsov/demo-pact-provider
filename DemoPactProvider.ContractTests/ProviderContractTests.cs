using System.Net;
using DemoPactProvider.Api;
using Microsoft.AspNetCore.Builder;
using PactNet;
using PactNet.Infrastructure.Outputters;
using PactNet.Verifier;
using Xunit.Abstractions;

namespace DemoPactProvider.ContractTests;

// Pact verifier tests start real HTTP servers, so keep them serial for clearer logs
// and to avoid multiple verifier runs competing for local resources in the demo.
[Collection("Provider verification")]
public sealed class ProviderContractTests
{
    // This must match the provider name written into the consumer Pact files.
    private const string ProviderName = "customer-provider";

    private readonly ITestOutputHelper output;

    public ProviderContractTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task Verify_TypeScript_Consumer()
    {
        // Keep each consumer in its own test so the test output identifies the
        // affected consumer when a provider change breaks a contract.
        await VerifyConsumerPactsAsync(
            environmentVariableName: "PACT_TS_PACT_DIR",
            defaultRelativePath: Path.Combine("contracts", "ts", "pacts"));
    }

    [Fact]
    public async Task Verify_DotNet_Consumer()
    {
        // The .NET consumer contract is verified separately from the TypeScript
        // contract because the two consumers depend on different response fields.
        await VerifyConsumerPactsAsync(
            environmentVariableName: "PACT_DOTNET_PACT_DIR",
            defaultRelativePath: Path.Combine("contracts", "dotnet", "pacts"));
    }

    private async Task VerifyConsumerPactsAsync(string environmentVariableName, string defaultRelativePath)
    {
        // CI checks out consumer repositories into contracts/*, but the env var
        // override makes local verification easy against any generated Pact folder.
        var pactDirectory = ResolvePactDirectory(environmentVariableName, defaultRelativePath);
        var pactFiles = pactDirectory.Exists
            ? pactDirectory.GetFiles("*.json", SearchOption.TopDirectoryOnly)
            : [];

        if (pactFiles.Length == 0)
        {
            var message = $"No Pact JSON files found in '{pactDirectory.FullName}'. Set {environmentVariableName} to override this path.";

            // Locally, missing Pact files are a convenience skip. In GitHub Actions,
            // missing Pact files should fail because the provider was not verified.
            if (IsCi())
            {
                throw new InvalidOperationException(message);
            }

            output.WriteLine(message);
            return;
        }

        var config = new PactVerifierConfig
        {
            LogLevel = PactLogLevel.Information,
            // Send Pact verifier output into xUnit so Actions shows the exact
            // mismatch, for example "Actual map is missing ... name".
            Outputters = [new XunitOutput(output)]
        };

        foreach (var pactFile in pactFiles.OrderBy(file => file.Name))
        {
            // Start the real ASP.NET Core provider. Pact then exercises the same
            // controller endpoint an external consumer would call over HTTP.
            await using var provider = await RunningProvider.StartAsync();

            output.WriteLine($"Provider listening at {provider.BaseUri}");
            output.WriteLine($"Verifying Pact file {pactFile.FullName}");

            new PactVerifier(ProviderName, config)
                .WithHttpEndpoint(provider.BaseUri)
                .WithFileSource(pactFile)
                // If a consumer contract includes provider states, Pact will call
                // this endpoint before verifying the interaction.
                .WithProviderStateUrl(new Uri(provider.BaseUri, "/provider-states"))
                .Verify();
        }
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
            var app = Program.BuildApp([]);

            // Bind explicitly to the selected local port instead of relying on
            // launchSettings.json or the default development ports.
            app.Urls.Add(baseUri.ToString());
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
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(1)
            };

            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);

            while (DateTimeOffset.UtcNow < deadline)
            {
                try
                {
                    // Probe the real contract endpoint before handing it to Pact.
                    // This avoids reporting a contract failure when the server is
                    // simply not ready yet.
                    using var response = await client.GetAsync(new Uri(BaseUri, "/customers/123"));

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return;
                    }
                }
                catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
                {
                }

                await Task.Delay(100);
            }

            throw new TimeoutException($"Provider did not start at {BaseUri}.");
        }

        private static int GetAvailablePort()
        {
            // Ask the OS for a free port, then reuse that port for the short-lived
            // provider instance started by this test.
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

    private sealed class XunitOutput : IOutput
    {
        private readonly ITestOutputHelper output;

        public XunitOutput(ITestOutputHelper output)
        {
            this.output = output;
        }

        public void WriteLine(string line)
        {
            output.WriteLine(line);
        }
    }
}

[CollectionDefinition("Provider verification", DisableParallelization = true)]
public sealed class ProviderVerificationCollection;
