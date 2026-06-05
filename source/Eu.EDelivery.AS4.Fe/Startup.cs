using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Eu.EDelivery.AS4.Fe.Authentication;
using Eu.EDelivery.AS4.Fe.Controllers;
using Eu.EDelivery.AS4.Fe.Exceptions;
using Eu.EDelivery.AS4.Fe.Modules;
using Eu.EDelivery.AS4.Fe.Monitor;
using Eu.EDelivery.AS4.Fe.Runtime;
using Eu.EDelivery.AS4.Fe.Settings;
using Eu.EDelivery.AS4.Fe.Swagger;
using Eu.EDelivery.AS4.Model.PMode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Newtonsoft.Json;

namespace Eu.EDelivery.AS4.Fe;

/// <summary>
/// The start point class for the Payload Service Web API.
/// </summary>
public class Startup
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Startup" /> class.
    /// </summary>
    /// <param name="configuration">The hosting environment configuration.</param>
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    private string AssemblyVersion => GetType().GetTypeInfo().Assembly.GetName().Version?.ToString() ?? string.Empty;

    /// <summary>
    /// Gets the <see cref="IConfiguration" /> implementation for the Payload Service Web API.
    /// </summary>
    public IConfiguration Configuration { get; }

    /// <summary>
    /// This method gets called by the runtime. Use this method to add services to the container.
    /// </summary>
    /// <param name="services">The services.</param>
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddOptions();
        services.Configure<FormOptions>(x =>
        {
            x.ValueLengthLimit = int.MaxValue;
            x.ValueCountLimit = int.MaxValue;
            x.MultipartBodyLengthLimit = int.MaxValue;
            x.MemoryBufferThreshold = int.MaxValue;
        });
        services.Configure<ApplicationSettings>(Configuration.GetSection("Settings"));
        services.Configure<PortalSettings>(Configuration.Bind);
        services.Configure<JsonOptions>(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });
        //services.AddAS4()
        services.AddMappers();
        services.AddModules();

        ConfigureModularServices<IRunAtServicesStartup>(services, service => service.Run(services, Configuration));

        services
            .AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy = null;
            });

        services
            .AddMvc(options =>
            {
                options.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            });

        services.AddScoped<ISettingsSource, FileSettingsSource>();
        services.AddScoped<IPortalSettingsService, PortalSettingsService>();
        services.AddScoped<ITokenService, TokenService>();

        services.AddSingleton<IRuntimeLoader, RuntimeLoader>(sp => RuntimeLoader.Initialize(
            sp.GetRequiredService<ILogger<RuntimeLoader>>(),
            sp.GetRequiredService<IOptions<ApplicationSettings>>()));

        // Add framework services.
        services.AddControllers();

        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(
            options =>
            {
                options.SwaggerDoc(
                    "v1",
                    new OpenApiInfo
                    {
                        Title = "AS4.NET Frontend Api",
                        Version = $"v{AssemblyVersion}",
                        Description = "A Web API to support the AS4.NET Frontend.",
                        TermsOfService = new Uri("https://ec.europa.eu/digital-building-blocks/code/projects/EDELIVERY/repos/eessi-as4.net/browse"),
                        Contact = new OpenApiContact { Name = "DG EMPL" },
                        License = new OpenApiLicense
                        {
                            Name = "EUPL License v1.1.",
                            Url = new Uri("https://joinup.ec.europa.eu/community/eupl/og_page/european-union-public-licence-eupl-v11")
                        }
                    });

                options.OperationFilter<FileUploadOperation>();
                options.IncludeXmlComments(GetXmlCommentsPath());
            });

        services.AddApplicationInsightsTelemetry();
    }

    public void Configure(WebApplication app)
    {
        ConfigureModularApplication<IRunAtAppConfiguration>(app, service => service.Run(app));

        app.UseDefaultFiles();
#if DEBUG
        app.UseDeveloperExceptionPage();
#endif
        app.UseStaticFiles();
        app.UseAuthentication();

        ConfigureExceptionHandlers(app);

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapHub<SubmitToolMessageHub>("/SubmitToolMessageHub");

        app.MapControllers();

        //app.Use(async (context, next) =>
        //{
        //    await next();

        //    if (context.Request.Path.StartsWithSegments("/api")) return;
        //    if (context.Response.StatusCode != 200 && context.Request.Path.Value?.IndexOf(".") == -1)
        //    {
        //        context.Request.Path = "/index.html";
        //        await next();
        //    }
        //});
        app.MapFallbackToFile("/index.html");
    }

    private static void ConfigureModularServices<T>(IServiceCollection services, Action<T> caller)
    {
        foreach (var runat in services.BuildServiceProvider().GetServices<T>())
        {
            caller(runat);
        }
    }

    private static void ConfigureModularApplication<T>(IApplicationBuilder builder, Action<T> caller)
    {
        foreach (var runat in builder.ApplicationServices.GetServices<T>())
        {
            caller(runat);
        }
    }

    private void ConfigureExceptionHandlers(WebApplication app)
    {
        var settings = Configuration.GetRequiredSection("Settings").Get<ApplicationSettings>()
           ?? throw new InvalidOperationException("Application settings not found.");

        app.UseExceptionHandler(options =>
        {
            options.Run(async context =>
            {
                context.Response.ContentType = "application/json";
                var ex = context.Features.Get<IExceptionHandlerFeature>();
                if (ex != null)
                {
                    var response = new ErrorResponse
                    {
                        Exception = !settings.ShowStackTraceInExceptions ? null : ex.Error.StackTrace,
                        Message = ex.Error.Message
                    };

                    if (ex.Error is AlreadyExistsException alreadyExists)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                        response.Type = "businessexception";
                        response.ExceptionType = typeof(AlreadyExistsException).Name;
                    }
                    else if (ex.Error is NotFoundException notFound)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        response.Type = "businessexception";
                        response.ExceptionType = typeof(NotFoundException).Name;
                    }
                    else if (ex.Error is BusinessException businessEx)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.ExpectationFailed;
                        response.Type = "businessexception";
                        response.ExceptionType = typeof(BusinessException).Name;
                    }
                    else if (ex.Error is InvalidPModeException invalidPmodeEx)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.ExpectationFailed;
                        response.Type = "businessexception";
                        response.ExceptionType = typeof(BusinessException).Name;
                    }
                    else
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        response.Type = "businessexception";
                        response.ExceptionType = typeof(BusinessException).Name;
                    }

                    await context.Response.WriteAsync(JsonConvert.SerializeObject(response));

                    var logger = app.Services.GetRequiredService<ILogger>();
                    logger.LogError(ex.Error, response.Message);
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                }
            });
        });
    }

    private static string GetXmlCommentsPath()
    {
        const string Xml = "Eu.EDelivery.AS4.FE.xml";
        var binPath = Path.Combine(AppContext.BaseDirectory, "bin", Xml);
        return File.Exists(binPath) ? binPath : Path.Combine(AppContext.BaseDirectory, Xml);
    }
}
