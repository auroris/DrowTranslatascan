using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

internal class Program
{
    public static string DbPath { get; set; } = "";

    private static void Main(string[] args)
    {
        DbPath = Path.Combine(AppContext.BaseDirectory, "Data", "drow_dictionary.db");
        if (!File.Exists(DbPath)) 
        {
            throw new Exception($"Database file can't be found at {DbPath}");
        }

        IHost host = new HostBuilder()
            .ConfigureFunctionsWebApplication()
            .ConfigureServices(services =>
        {
            services.AddApplicationInsightsTelemetryWorkerService();
            services.ConfigureFunctionsApplicationInsights();
        })
        .Build();

        host.Run();
    }
}