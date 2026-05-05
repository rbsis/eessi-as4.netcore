using System.ComponentModel;
using Eu.EDelivery.AS4.Common;
using Eu.EDelivery.AS4.Extensions;
using Eu.EDelivery.AS4.Mappings.PMode;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Steps.Submit;

/// <summary>
/// <see cref="IStep" /> implementation
/// to create a default configured <see cref="AS4Message" />
/// </summary>
[NotConfigurable]
public class CreateDefaultAS4MessageStep : IConfigStep
{
    private readonly ILogger<CreateDefaultAS4MessageStep> _logger;
    private readonly IConfig _config;
    private readonly ISendingPModeMap _sendingPModeMap;

    private IDictionary<string, string> _properties;

    [Info("Default pmode", type: "pmode")]
    [Description("The default pmode to be used to create a message.")]
    private string DefaultPmode => _properties.ReadOptionalProperty("default-pmode");

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateDefaultAS4MessageStep" /> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="config">The configuration.</param>
    /// <param name="sendingPModeMap"></param>
    public CreateDefaultAS4MessageStep(ILogger<CreateDefaultAS4MessageStep> logger, IConfig config, ISendingPModeMap sendingPModeMap)
    {
        _config = config;
        _logger = logger;
        _sendingPModeMap = sendingPModeMap;
        _properties = new Dictionary<string, string>();
    }

    /// <summary>
    /// Configure the step with a given Property Dictionary
    /// </summary>
    /// <param name="properties"></param>
    public void Configure(IDictionary<string, string> properties)
    {
        _properties = properties;
    }

    /// <summary>
    /// Start creating a <see cref="AS4Message" />
    /// </summary>
    /// <param name="messagingContext"></param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(messagingContext);

        if (messagingContext.AS4Message == null)
        {
            throw new ArgumentException($"{nameof(CreateDefaultAS4MessageStep)} requires an AS4Message to assign the default UserMessage to but no AS4Message is present in the MessagingContext");
        }

        var pmode = _config.GetSendingPMode(DefaultPmode)
            ?? throw new InvalidOperationException($"SendingPMode {DefaultPmode} was not found");

        var parts = messagingContext.AS4Message.Attachments.Select(PartInfo.CreateFor);

        var userMessage = _sendingPModeMap.CreateUserMessage(pmode, [.. parts]);

        messagingContext.AS4Message.AddMessageUnit(userMessage);
        messagingContext.SendingPMode = pmode;

        _logger.LogInformation("{LogTag} Default AS4Message is created using SendingPMode {PModeId}",
            messagingContext.LogTag,
            pmode.Id);
        return await StepResult.SuccessAsync(messagingContext);
    }
}
