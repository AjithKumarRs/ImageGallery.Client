﻿using System;
using System.Collections.Generic;
using ImageGallery.Client.Configuration;
using ImageGallery.Client.Services;
using Loggly;
using Loggly.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SpaServices.Webpack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using StackExchange.Redis;
using Swashbuckle.AspNetCore.Swagger;

namespace ImageGallery.Client
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Log.Information("Startup:ImageGallery.Client");
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddSerilog();
            });

            Log.Information("ConfigureServices:ImageGallery.Client");

            services.AddMvc();

            services.AddOptions();
            services.Configure<ApplicationOptions>(Configuration);

            services.Configure<ApplicationOptions>(Configuration.GetSection("applicationSettings"));
            services.Configure<Dataprotection>(Configuration.GetSection("dataprotection"));
            services.Configure<OpenIdConnectConfiguration>(Configuration.GetSection("openIdConnectConfiguration"));
            services.Configure<LogglyClientConfiguration>(Configuration.GetSection("logglyClientConfiguration"));

            var config = Configuration.Get<ApplicationOptions>();

            Console.WriteLine($"Dataprotection Enabled: {config.Dataprotection.Enabled}");
            Console.WriteLine($"DataprotectionRedis: {config.Dataprotection.RedisConnection}");
            Console.WriteLine($"RedisKey: {config.Dataprotection.RedisKey}");

            Console.WriteLine($"Authority: {config.OpenIdConnectConfiguration.Authority}");
            Console.WriteLine($"ClientId: {config.OpenIdConnectConfiguration.ClientId}");
            Console.WriteLine($"ClientSecret: {config.OpenIdConnectConfiguration.ClientSecret}");
            Console.WriteLine($"LogglyKey: {config.LogglyClientConfiguration.LogglyKey}");

            if (config.Dataprotection.Enabled)
            {
                var redis = ConnectionMultiplexer.Connect(config.Dataprotection.RedisConnection);
                services.AddDataProtection().PersistKeysToRedis(redis, config.Dataprotection.RedisKey);
            }

            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new Info
                {
                    Title = "ImageGallery.Client",
                    Description = "ImageGallery.Client",
                    Version = "v1",
                });
            });

            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.Authority = config.OpenIdConnectConfiguration.Authority;
                    options.RequireHttpsMetadata = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateAudience = true,
                        ValidAudiences = new List<string>
                        {
                            $"{config.OpenIdConnectConfiguration.Authority}/resources",
                            config.OpenIdConnectConfiguration.ClientId,
                        },
                    };
                });

            services.AddAuthorization(authorizationOptions =>
            {
                authorizationOptions.AddPolicy(
                    "CanOrderFrame",
                    policyBuilder =>
                    {
                        policyBuilder.RequireAuthenticatedUser();
                        policyBuilder.RequireClaim("country", "be");
                        policyBuilder.RequireClaim("subscriptionlevel", "PayingUser");
                        policyBuilder.RequireRole("PayingUser");
                    });
            });

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddScoped<IImageGalleryHttpClient, ImageGalleryHttpClient>();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            StartLoggly();
            Console.WriteLine($"EnvironmentName: {env.EnvironmentName}");

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
                app.UseWebpackDevMiddleware(new WebpackDevMiddlewareOptions
                {
                    HotModuleReplacement = true,
                    HotModuleReplacementEndpoint = "/dist/__webpack_hmr",
                });
            }
            else
            {
                app.UseExceptionHandler("/Shared/Error");
            }

            var forwardOptions = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
                RequireHeaderSymmetry = false,
            };

            forwardOptions.KnownNetworks.Clear();
            forwardOptions.KnownProxies.Clear();

            app.UseForwardedHeaders(forwardOptions);

            var config = Configuration.Get<ApplicationOptions>();
            Console.WriteLine("Authority" + config.OpenIdConnectConfiguration.Authority);

            app.UseAuthentication();

            app.UseStaticFiles();

            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "ImageGallery.Client V1");
                options.ConfigureOAuth2("swaggerui", string.Empty, string.Empty, "Swagger UI");
            });

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Gallery}/{action=Index}/{id?}");

                routes.MapSpaFallbackRoute(
                    name: "spa-fallback",
                    defaults: new { controller = "Gallery", action = "Index" });
            });
        }

        private static void StartLoggly()
        {
            var logglyToken = Environment.GetEnvironmentVariable("LOGGLY_TOKEN");
            Console.WriteLine($"LogglyToken:{logglyToken}");

            var config = LogglyConfig.Instance;
            config.CustomerToken = "c3176aed-1b75-4315-9ee6-21cf1bd84dd8";
            config.ApplicationName = "ImageGallery.Client";

            config.Transport.EndpointHostname = "logs-01.loggly.com";
            config.Transport.EndpointPort = 443;
            config.Transport.LogTransport = LogTransport.Https;

            var ct = new ApplicationNameTag
            {
                Formatter = "application-{0}",
            };

            config.TagConfig.Tags.Add(ct);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Debug)
                .Enrich.FromLogContext()
                .WriteTo.Loggly()
                .CreateLogger();
            Log.Information("Loggly started");
        }
    }
}