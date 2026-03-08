using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Program.DbPath = Path.Combine(AppContext.BaseDirectory, "Data", "drow_dictionary.db");
if (!File.Exists(Program.DbPath))
    throw new Exception($"Database file can't be found at {Program.DbPath}");

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
    })
    .Build();

host.Run();
