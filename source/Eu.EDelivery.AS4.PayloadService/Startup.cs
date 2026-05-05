using System.Reflection;
using Eu.EDelivery.AS4.PayloadService.Persistance;
using Eu.EDelivery.AS4.PayloadService.Services;
using Microsoft.OpenApi;

namespace Eu.EDelivery.AS4.PayloadService;

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
        services.AddSingleton<IPayloadPersister, FilePayloadPersister>();
        services.AddHostedService<CleanUpService>();

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
                        Title = "AS4.NET Payload Service",
                        Version = $"v{AssemblyVersion}",
                        Description = "A Web API to upload and download payloads in a persistent manner.",
                        TermsOfService = new Uri("https://ec.europa.eu/digital-building-blocks/code/projects/EDELIVERY/repos/eessi-as4.net/browse"),
                        Contact = new OpenApiContact { Name = "DG EMPL" },
                        License =
                            new OpenApiLicense
                            {
                                Name = "EUPL License v1.1.",
                                Url = new Uri("https://joinup.ec.europa.eu/community/eupl/og_page/european-union-public-licence-eupl-v11")
                            }
                    });

                //Obsolete: options.OperationFilter<FileUploadOperation>()
                options.IncludeXmlComments(GetXmlCommentsPath());
            });

        services.AddApplicationInsightsTelemetry();
    }

    private static string GetXmlCommentsPath()
    {
        const string Xml = "Eu.EDelivery.AS4.PayloadService.xml";
        var binPath = Path.Combine(AppContext.BaseDirectory, "bin", Xml);
        return File.Exists(binPath) ? binPath : Path.Combine(AppContext.BaseDirectory, Xml);
    }
}
