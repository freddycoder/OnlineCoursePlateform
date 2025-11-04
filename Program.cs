using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using OnlineCoursePlateform.Components;
using Toolbelt.Blazor.Extensions.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Add services to the container.
builder.RootComponents.Add<App>("#app");
builder.Services.AddSpeechSynthesis();

var app = builder.Build();

await app.RunAsync();
