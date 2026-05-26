using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aucy.Data;

public static class DI
{
    public static void AddDataServices(this IHostApplicationBuilder builder)
    {
        var connectionString =
           Environment.GetEnvironmentVariable("ConnectionStrings__AucyDbContext")
           ?? builder.Configuration.GetConnectionString("AucyDbContext");

        builder.Services.AddDbContext<AucyDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });
    }
}
