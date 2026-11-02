using EnglishSteamTrainer.Admin;
using EnglishSteamTrainer.Admin.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient());
builder.Services.AddScoped<GitHubApi>();
builder.Services.AddScoped<AdminSettings>();
builder.Services.AddScoped<ContentState>();

await builder.Build().RunAsync();
