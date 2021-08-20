using KubeOps.Operator;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using HostedDatabaseOperator;

static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); });

await CreateHostBuilder(args).Build().RunOperatorAsync(args);
