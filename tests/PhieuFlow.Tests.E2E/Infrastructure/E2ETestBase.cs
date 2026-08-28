using System.Net.Http.Json;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using PhieuFlow.Hub.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace PhieuFlow.Tests.E2E.Infrastructure;

/// <summary>
/// Per-test Playwright setup: a fresh browser context + page, a trace recorded for the
/// run (ADR 0006 calls out the trace viewer as the reason Playwright was chosen), and
/// helpers for the flows every builder/versioning test repeats.
/// </summary>
[Collection(E2ECollection.Name)]
public abstract class E2ETestBase : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;

    protected E2ETestBase(AppHostFixture fixture, ITestOutputHelper output)
    {
        Fixture = fixture;
        _output = output;
    }

    protected AppHostFixture Fixture { get; }

    protected IBrowserContext Context { get; private set; } = null!;

    protected IPage Page { get; private set; } = null!;

    /// <summary>Autosave debounce in <c>FormBuilder.razor</c> is 800 ms; wait past it.</summary>
    protected static readonly TimeSpan AutosaveSettle = TimeSpan.FromMilliseconds(1500);

    public async Task InitializeAsync()
    {
        Context = await Fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
        });
        await Context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true,
        });
        Page = await Context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "traces");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{Sanitize(CurrentTestName())}.zip");
        await Context.Tracing.StopAsync(new TracingStopOptions { Path = path });
        await Context.DisposeAsync();
    }

    // ---- shared flows -------------------------------------------------------

    protected string FormBuilderUrl(string path) => new Uri(Fixture.FormBuilderBaseUrl, path).ToString();

    protected Task GotoFormBuilderAsync(string path = "/") => NavigateAsync(Page, FormBuilderUrl(path));

    /// <summary>
    /// Navigates and waits for the Blazor Server interactive circuit to come up, so the
    /// first <c>@oninput</c>/<c>@onclick</c> is not lost against the pre-rendered DOM.
    /// </summary>
    protected static async Task NavigateAsync(IPage page, string url)
    {
        IWebSocket? circuit = null;
        void OnWebSocket(object? _, IWebSocket ws) => circuit ??= ws;
        page.WebSocket += OnWebSocket;
        try
        {
            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (circuit is null && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100);
            }
        }
        finally
        {
            page.WebSocket -= OnWebSocket;
        }

        // Let the SignalR handshake finish and the first interactive render settle.
        await page.WaitForTimeoutAsync(500);
    }

    /// <summary>
    /// Header save indicator settled to a "saved …" state — matches "saved just now",
    /// "saved N min ago", "saved at HH:mm"; deliberately not "unsaved changes".
    /// </summary>
    private static readonly Regex SavedIndicator = new(@"saved (just now|\d+ min ago|at \d)", RegexOptions.IgnoreCase);

    protected async Task WaitForSavedAsync()
    {
        // Callers reach here right after an edit. Wait past the 800 ms autosave debounce
        // so a lingering "saved just now" from the previous save can't satisfy the wait
        // before the new save has even started.
        await Page.WaitForTimeoutAsync(1000);
        try
        {
            await Page.GetByText(SavedIndicator).First
                .WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        }
        catch (TimeoutException)
        {
            var header = await Page.Locator("header").First.InnerTextAsync();
            throw new TimeoutException(
                $"Autosave indicator never reached a 'saved' state. Header text was:{Environment.NewLine}{header}");
        }
    }

    protected async Task ClickPublishAsync()
    {
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Publish" }).ClickAsync();
        await Page.GetByText("This version is published").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
    }

    // ---- hub assertions ---------------------------------------------------

    protected async Task<FormDto> GetFormAsync(Guid id)
    {
        using var client = Fixture.CreateHubClient();
        var dto = await client.GetFromJsonAsync<FormDto>($"/forms/{id}");
        return dto ?? throw new InvalidOperationException($"Hub returned no form for {id}.");
    }

    protected async Task<FormListItemDto?> GetFormListItemAsync(Guid id)
    {
        using var client = Fixture.CreateHubClient();
        var batch = await client.GetFromJsonAsync<FormBatchResponse>("/forms?take=100");
        return batch?.Items.FirstOrDefault(i => i.Id == id);
    }

    protected async Task<Guid> GetFormIdByTitleAsync(string title)
    {
        using var client = Fixture.CreateHubClient();
        var batch = await client.GetFromJsonAsync<FormBatchResponse>("/forms?take=100");
        var match = batch?.Items.FirstOrDefault(i => i.Title == title)
            ?? throw new InvalidOperationException($"No hub form titled '{title}' after autosave.");
        return match.Id;
    }

    // ---- misc -----------------------------------------------------------

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name.Length > 120 ? name[..120] : name;
    }

    private string CurrentTestName()
    {
        // xUnit v2 exposes the running test only via the output helper's private field.
        var field = _output.GetType().GetField("test", BindingFlags.Instance | BindingFlags.NonPublic);
        return (field?.GetValue(_output) as ITest)?.DisplayName ?? $"e2e-{Guid.NewGuid():N}";
    }
}
