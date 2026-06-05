using System.Reflection;

namespace Eu.EDelivery.AS4.WindowsService;

/// <summary>
/// The start point class for the Payload Service Web API.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="Startup" /> class.
/// </remarks>
/// <param name="configuration">The hosting environment configuration.</param>
public class Startup(IConfiguration configuration)
{
    /// <summary>
    /// Gets the <see cref="IConfiguration" /> implementation for the Payload Service Web API.
    /// </summary>
    public IConfiguration Configuration { get; } = configuration;

    /// <summary>
    /// This method gets called by the runtime. Use this method to add services to the container.
    /// </summary>
    /// <param name="services">The services.</param>
    public void ConfigureServices(IServiceCollection services)
    {
        var assemblyLocationFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (assemblyLocationFolder != null && string.Compare(Environment.CurrentDirectory, assemblyLocationFolder, StringComparison.OrdinalIgnoreCase) != 0)
        {
            Environment.CurrentDirectory = assemblyLocationFolder;
        }

        services
            .AddWindowsService(options =>
             {
                 options.ServiceName = "AS4.NET Service";
             })
            .AddAS4()
            .AddAS4Receivers()
            .AddAS4ReceiversHttp()
            .AddAS4Transformers()
            .AddAS4Steps()
            .AddAS4Agents(@"config\settings-service.xml");
    }
}
