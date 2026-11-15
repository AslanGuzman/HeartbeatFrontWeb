using System.Globalization;
using Heartbeat.Portal.Components.Shared;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Heartbeat.Portal.Tests;

[TestClass]
public sealed class LanguageSelectorTests
{
    [TestMethod]
    [DataRow("es", "Español", "English")]
    [DataRow("en", "English", "Español")]
    public async Task ShowsCurrentLanguageAsPressedButton(string culture, string active, string inactive)
    {
        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            using var services = new ServiceCollection()
                .AddLogging()
                .AddLocalization()
                .AddSingleton<IJSRuntime, UnusedJsRuntime>()
                .BuildServiceProvider();
            await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
            var html = await renderer.Dispatcher.InvokeAsync(async () =>
            {
                var component = await renderer.RenderComponentAsync<LanguageSelector>();
                return System.Net.WebUtility.HtmlDecode(component.ToHtmlString());
            });

            StringAssert.Contains(html, $"aria-label=\"{active}\" aria-pressed=\"true\"");
            StringAssert.Contains(html, $"aria-label=\"{inactive}\" aria-pressed=\"false\"");
            Assert.IsFalse(html.Contains("<select", StringComparison.Ordinal));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    private sealed class UnusedJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new InvalidOperationException("Rendering must not change the browser language.");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }
}
