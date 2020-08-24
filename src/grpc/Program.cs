using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace TracingDemo.GrpcService
{
    static class Program
    {
        internal const string Name = "TracingDemo.GrpcService";

        private static IConfiguration Configuration;

        public static void Main(string[] args)
        {
            try
            {
                // start with default Serilog logger to console (replace below)
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .WriteTo.Console()
                    .CreateLogger();

                // Configure Global Tracing Options
                Activity.DefaultIdFormat = ActivityIdFormat.W3C;
                Activity.ForceDefaultIdFormat = true;

                // Build Host
                IHost host = Host.CreateDefaultBuilder(args)
                    .ConfigureAppConfiguration((hostingContext, config) =>
                    {
                        // set up configuration
                        string envName = hostingContext.HostingEnvironment.EnvironmentName.ToLowerInvariant();
                        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                              .AddJsonFile($"appsettings.{envName}.json", optional: true, reloadOnChange: true)
                              .AddEnvironmentVariables()
                              .AddCommandLine(args);

                        Configuration = config.Build();
                    })
                    .ConfigureWebHostDefaults(webBuilder =>
                    {
                        // setup unencrypted gRPC over HTTP, mTLS provided by Linkerd
                        webBuilder.ConfigureKestrel(kestrel =>
                        {
                            int port = Configuration.GetValue<int>("GrpcPort", 80);
                            kestrel.ListenAnyIP(port, o => o.Protocols = HttpProtocols.Http2);
                        });

                        webBuilder.UseStartup<Startup>();
                        webBuilder.UseSerilog();
                    }).Build();

                // build proper logger from loaded configuration
                Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(Configuration)
                    .CreateLogger();

                // Let's do this!
                host.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Main() Failed");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
