using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;
using Serilog;
using TracingDemo.Protobuf;

namespace TracingDemo.WebApi
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<ForwardedHeadersOptions>(
                options => options.ForwardedHeaders = ForwardedHeaders.All
            );

            services.AddHealthChecks();
            services.AddControllers();

            // This switch must be set before creating the GrpcChannel/HttpClient.
            // This allows the app to call gRPC services it thinks are not secure, mTLS is provided by Linkerd.
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            services.AddHttpClient();

            // add Typed Grpc Client
            services.AddGrpcClient<Greeter.GreeterClient>(options =>
            {
                options.Address = new Uri(Configuration["Services:Greeter"]);
            });

            // Register the Swagger generator, defining 1 or more Swagger documents
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = Program.Name, Version = "1.0" });
            });

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

                // Add HttpClient & gRPC Propagation (order matters for gRPC to support B3 format)
                builder.AddHttpClientInstrumentation(options =>
                {
                    if (tracingFormat == "b3m") // Send B3 headers downstream (for Linkerd)
                        options.TextFormat = new B3Format(singleHeader: false);
                    else if (tracingFormat == "b3s")
                        options.TextFormat = new B3Format(singleHeader: true);
                })
                .AddGrpcClientInstrumentation();

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

            app.UseRouting();
            app.UseSerilogRequestLogging();

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", Program.Name);
                c.RoutePrefix = "api/swagger";
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHealthChecks("/healthz");

                endpoints.MapControllers();

                endpoints.MapGet("/", async context =>
                {
                    context.Response.Redirect("/api/swagger/index.html");
                    await context.Response.CompleteAsync();
                });
            });
        }
    }
}
