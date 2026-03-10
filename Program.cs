using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

// Resolve and validate the database path before the host starts.
Program.DbPath = Path.Combine(AppContext.BaseDirectory, "Data", "drow_dictionary.db");
if (!File.Exists(Program.DbPath))
    throw new Exception($"Database file can't be found at {Program.DbPath}");

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Redirect GET / to the web UI. Comment out to disable.
        services.AddTransient<IStartupFilter, RootRedirectFilter>();

        // OpenAPI metadata displayed in the Swagger UI.
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

/// <summary>
/// ASP.NET Core startup filter that issues a 302 redirect from <c>GET /</c>
/// to <c>/api/Home</c>, so visiting the root URL opens the web UI directly.
/// </summary>
/// <remarks>
/// To disable this behaviour, remove the <c>AddTransient&lt;IStartupFilter, RootRedirectFilter&gt;</c>
/// registration in <c>Program.cs</c>.
/// <para>
/// When deployed to Azure, also set the <c>AzureWebJobsDisableHomepage</c> application
/// setting to <c>true</c> so the Azure Functions default homepage does not intercept <c>/</c>
/// before this filter can act on it.
/// </para>
/// </remarks>
class RootRedirectFilter : IStartupFilter
{
    /// <inheritdoc/>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseWhen(ctx => ctx.Request.Path == "/", branch =>
            branch.Run(ctx => { ctx.Response.Redirect("/api/Home"); return Task.CompletedTask; }));
        next(app);
    };
}
