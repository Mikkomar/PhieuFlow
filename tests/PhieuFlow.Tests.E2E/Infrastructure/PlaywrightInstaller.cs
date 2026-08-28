namespace PhieuFlow.Tests.E2E.Infrastructure;

/// <summary>
/// Installs the Chromium build Playwright drives. ADR 0006 lists the one-time
/// <c>playwright install</c> step as a dev/CI setup cost; running it in-process keeps the
/// suite self-contained (there is no <c>pwsh</c> on PATH in every environment).
/// </summary>
internal static class PlaywrightInstaller
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _installed;

    public static async Task EnsureInstalledAsync()
    {
        if (_installed)
        {
            return;
        }

        await Gate.WaitAsync();
        try
        {
            if (_installed)
            {
                return;
            }

            // Microsoft.Playwright ships its CLI as an entry point on the test assembly.
            // (No "--with-deps": that shells out to apt and needs root; on CI install the
            // OS libraries separately.)
            var originalOut = Console.Out;
            var captured = new StringWriter();
            int exitCode;
            try
            {
                Console.SetOut(captured);
                exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            if (exitCode != 0)
            {
                throw new InvalidOperationException(
                    $"'playwright install chromium' exited with code {exitCode}.{Environment.NewLine}{captured}" +
                    "Install it manually and retry (see tests/PhieuFlow.Tests.E2E/README.md).");
            }

            _installed = true;
        }
        finally
        {
            Gate.Release();
        }
    }
}
