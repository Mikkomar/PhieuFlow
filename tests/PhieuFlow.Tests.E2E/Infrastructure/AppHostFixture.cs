using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Xunit;

namespace PhieuFlow.Tests.E2E.Infrastructure;

/// <summary>
/// The one expensive shared resource for the whole E2E assembly (ADR 0006: stand up the
/// application topology once per test-run category). Starts the full AppHost graph — SQL,
/// migrations, seed, hub, form-builder — via <see cref="DistributedApplicationTestingBuilder"/>,
/// then boots a single headless Chromium that every test opens its own context against.
/// </summary>
public sealed class AppHostFixture : IAsyncLifetime
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    private DistributedApplication _app = null!;
    private IPlaywright _playwright = null!;

    public IBrowser Browser { get; private set; } = null!;

    /// <summary>Base URL of the running form-builder UI (Blazor Server).</summary>
    public Uri FormBuilderBaseUrl { get; private set; } = null!;

    /// <summary>
    /// Base URL of the form-filler UI. The resource does not exist yet (ADR 0001/0006);
    /// the skipped submission specs read this and will resolve once it is added to AppHost.
    /// </summary>
    public Uri? FormFillerBaseUrl { get; private set; }

    /// <summary>HTTP client pointed at the hub REST API, for asserting persisted state.</summary>
    public HttpClient CreateHubClient() => _app.CreateHttpClient("hub");

    public async Task InitializeAsync()
    {
        await PlaywrightInstaller.EnsureInstalledAsync();

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.PhieuFlow_AppHost>();

        _app = await builder.BuildAsync();
        await _app.StartAsync();

        using var startupCts = new CancellationTokenSource(StartupTimeout);
        var notifications = _app.Services.GetRequiredService<ResourceNotificationService>();
        await notifications.WaitForResourceHealthyAsync("hub", startupCts.Token);
        await notifications.WaitForResourceHealthyAsync("formbuilder", startupCts.Token);

        FormBuilderBaseUrl = _app.GetEndpoint("formbuilder", "http");
        FormFillerBaseUrl = TryGetEndpoint("formfiller", "http");

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.DisposeAsync();
        }

        _playwright?.Dispose();

        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private Uri? TryGetEndpoint(string resourceName, string endpointName)
    {
        try
        {
            return _app.GetEndpoint(resourceName, endpointName);
        }
        catch (Exception)
        {
            // Resource not in the topology yet — expected until the form-filler ships.
            return null;
        }
    }
}
