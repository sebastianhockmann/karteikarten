using Microsoft.JSInterop;

namespace EnglishSteamTrainer.Admin.Services;

/// <summary>Zugangsdaten, gespeichert im Browser (localStorage) dieses Geräts.</summary>
public sealed class AdminSettings(IJSRuntime js)
{
    public const string DefaultRepository = "sebastianhockmann/karteikarten";
    public const string ContentBranch = "main";
    public const string ReportBranch = "berichte";

    private const string Prefix = "karteikarten.";

    public string Repository { get; set; } = DefaultRepository;
    public string Token { get; set; } = "";

    public async Task LoadAsync()
    {
        Repository = await GetAsync("repository") is { Length: > 0 } repository ? repository : DefaultRepository;
        Token = await GetAsync("token") ?? "";
    }

    public async Task SaveAsync()
    {
        await js.InvokeVoidAsync("localStorage.setItem", Prefix + "repository", Repository.Trim());
        await js.InvokeVoidAsync("localStorage.setItem", Prefix + "token", Token.Trim());
    }

    public async Task ForgetTokenAsync()
    {
        Token = "";
        await js.InvokeVoidAsync("localStorage.removeItem", Prefix + "token");
    }

    private ValueTask<string?> GetAsync(string key) => js.InvokeAsync<string?>("localStorage.getItem", Prefix + key);
}
