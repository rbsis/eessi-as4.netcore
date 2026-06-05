namespace Eu.EDelivery.AS4.Fe.SubmitTool;

/// <summary>
/// Setup submit tool dependencies
/// </summary>
/// <seealso cref="Pmodes.IPmodeSetup" />
public class SubmitToolSetup : ISubmitToolSetup
{
    /// <summary>
    /// Runs the specified services.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="configuration">The configuration.</param>
    public void Run(IServiceCollection services, IConfiguration configuration) => services
        .Configure<SubmitToolOptions>(configuration.GetSection("SubmitTool"))
        .AddSingleton<ISubmitMessageCreator, SubmitMessageCreator>()
        .AddSingleton<IPayloadHandler, PayloadHttpServiceHandler>()
        .AddSingleton<IPayloadHandler, SimulatePayloadServiceHandler>()
        .AddSingleton<IPayloadHandler, FilePayloadHandler>()
        .AddSingleton<IMessageHandler, MshMessageHandler>()
        .AddSingleton<IMessageHandler, FileMessageHandler>();
}
