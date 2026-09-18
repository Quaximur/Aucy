using Aucy.Data;
using Aucy.Workers;
using Aucy.Workers.Extensions;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting workers...");

    var builder = Host.CreateApplicationBuilder(args);

    DotNetEnv.Env.TraversePath().Load();

    builder.Services.AddSerilog((services, loggerConfig) => loggerConfig
       .ReadFrom.Configuration(builder.Configuration)
       .ReadFrom.Services(services));

    builder.AddDataServices();
    builder.AddWorkerStuff();

    var host = builder.Build();
    
    await WorkerBoot.InitItemsProcessing(host);

    host.Run();
}
catch (Exception e)
{
    Log.Fatal(e, "Workers terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}