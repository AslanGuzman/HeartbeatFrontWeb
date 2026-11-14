using Heartbeat.Portal.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Heartbeat.Portal.Tests;

[TestClass]
public sealed class MissionControlSessionStoreTests
{
    [TestMethod]
    public async Task SaveAndRestoreKeepsTokensProtectedInBrowserStorage()
    {
        using var provider = new ServiceCollection()
            .AddDataProtection()
            .UseEphemeralDataProtectionProvider()
            .Services
            .BuildServiceProvider();
        var jsRuntime = new MemoryJsRuntime();
        var protection = provider.GetRequiredService<IDataProtectionProvider>();
        var session = new MissionControlSession();
        session.SetTokens(
            "raw-access-token",
            DateTimeOffset.UtcNow.AddHours(1),
            "raw-refresh-token",
            DateTimeOffset.UtcNow.AddHours(8));
        var store = new MissionControlSessionStore(jsRuntime, protection, session);

        await store.SaveAsync();

        var protectedValue = jsRuntime.Storage["heartbeat.mc.session"];
        Assert.IsFalse(protectedValue.Contains("raw-access-token", StringComparison.Ordinal));
        Assert.IsFalse(protectedValue.Contains("raw-refresh-token", StringComparison.Ordinal));

        var restoredSession = new MissionControlSession();
        var restoredStore = new MissionControlSessionStore(jsRuntime, protection, restoredSession);
        await restoredStore.RestoreAsync();

        Assert.AreEqual("raw-access-token", restoredSession.AccessToken);
        Assert.AreEqual("raw-refresh-token", restoredSession.RefreshToken);
        await restoredStore.ClearAsync();
        Assert.IsFalse(jsRuntime.Storage.ContainsKey("heartbeat.mc.session"));
    }

    private sealed class MemoryJsRuntime : IJSRuntime
    {
        private readonly MemoryJsModule _module = new();

        public Dictionary<string, string> Storage => _module.Storage;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            if (identifier == "import")
            {
                return ValueTask.FromResult((TValue)(object)_module);
            }

            throw new InvalidOperationException($"Unexpected JavaScript call '{identifier}'.");
        }
    }

    private sealed class MemoryJsModule : IJSObjectReference
    {
        public Dictionary<string, string> Storage { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            var key = (string)args![0]!;
            object? result = null;
            switch (identifier)
            {
                case "read":
                    Storage.TryGetValue(key, out var value);
                    result = value;
                    break;
                case "write":
                    Storage[key] = (string)args[1]!;
                    break;
                case "remove":
                    Storage.Remove(key);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected module call '{identifier}'.");
            }

            return ValueTask.FromResult(result is null ? default! : (TValue)result);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
