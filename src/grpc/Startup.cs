using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;
using Serilog;
using TracingDemo.GrpcService.Providers;
using TracingDemo.GrpcService.Services;

namespace TracingDemo.GrpcService
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<ForwardedHeadersOptions>(
                options => options.ForwardedHeaders = ForwardedHeaders.All
            );

            services.AddGrpc();

            services.AddScoped<IGreetingRepository, SqlGreetingRepository>();

            // we need to create the singleton cache provider here so we can use the Connection in tracing (below)
            var redisProtoCache = new RedisProtoCache(Configuration.GetConnectionString("Redis"));
            services.AddSingleton<IProtoCache>(redisProtoCache);

            // create and register an activity source
            var activitySource = new ActivitySource(Program.Name);
            services.AddSingleton(activitySource);

            // Configure OpenTelemetry
            services.AddOpenTelemetry(builder =>
            {
                // register the activity source
                builder.AddActivitySource(activitySource.Name);

                // TextFormat Defaults to W3C - Enable B3 via configuration
                string tracingFormat = Configuration["Tracing:Format"]?.ToLowerInvariant();

                // Add ASP.Net Core Request Handling
                builder.AddAspNetCoreInstrumentation(options =>
                {
                    if (tracingFormat == "b3m") // B3 (multi) headers come from Nginx
                        options.TextFormat = new B3Format(singleHeader: false);
                    else if (tracingFormat == "b3s")
                        options.TextFormat = new B3Format(singleHeader: true);
                });

                // add automatic instrumentation for Sql Server
                builder.AddSqlClientDependencyInstrumentation(options =>
                {
                    options.SetTextCommandContent = true;   // probably not in production?
                    options.EnableConnectionLevelAttributes = true;
                });

                // add automatic instrumentation for Redis (needs ConnectionMultiplexer)
                builder.AddRedisInstrumentation(redisProtoCache.Connection);

                // export to Zipkin receiver
                string zipkinUrl = Configuration.GetConnectionString("Telemetry");
                if (!string.IsNullOrEmpty(zipkinUrl))
                {
                    builder.UseZipkinExporter(options =>
                    {
                        options.ServiceName = Program.Name;
                        options.Endpoint = new Uri(zipkinUrl);
                    });
                }

                // enable console output by configuration
                if (Configuration.GetValue<bool>("Tracing:Console", false))
                    builder.UseConsoleExporter();
            });

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseForwardedHeaders();

            app.UseRouting();
            app.UseSerilogRequestLogging();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<HealthCheckService>();
                endpoints.MapGrpcService<GreeterService>();

                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
                });
            });
        }
    }
}
