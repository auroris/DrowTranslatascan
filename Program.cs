using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

Program.DbPath = Path.Combine(AppContext.BaseDirectory, "Data", "drow_dictionary.db");
if (!File.Exists(Program.DbPath))
    throw new Exception($"Database file can't be found at {Program.DbPath}");

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddTransient<IStartupFilter, RootRedirectFilter>(); // comment out to disable / redirect
        services.AddSingleton<IOpenApiConfigurationOptions>(_ => new OpenApiConfigurationOptions
        {
            Info = new OpenApiInfo
            {
                Title = "Drow Translatascan API",
                Description = "Translates text between English (Common) and Drow.",
                Version = "1.0.0",
            }
        });
    })
    .Build();

host.Run();

// Redirects GET / to /api/Home
class RootRedirectFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseWhen(ctx => ctx.Request.Path == "/", branch =>
            branch.Run(ctx => { ctx.Response.Redirect("/api/Home"); return Task.CompletedTask; }));
        next(app);
    };
}
