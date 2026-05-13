using Eu.EDelivery.AS4.Factories;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.Validators;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Transformers;

/// <summary>
/// <see cref="ITransformer" /> implementation that's responsible for transformation PMode models to Pull Messages
/// instances.
/// </summary>
public class PModeToPullRequestTransformer : ITransformer
{
    private readonly ILogger<PModeToPullRequestTransformer> _logger;
    private readonly IValidator<SendingProcessingMode> _sendingProcessingModeValidator;
    private readonly IIdentifierFactory _identifierFactory;

    public PModeToPullRequestTransformer(
        ILogger<PModeToPullRequestTransformer> logger,
        IValidator<SendingProcessingMode> sendingProcessingModeValidator,
        IIdentifierFactory identifierFactory)
    {
        _logger = logger;
        _sendingProcessingModeValidator = sendingProcessingModeValidator;
        _identifierFactory = identifierFactory;
    }

    /// <summary>
    /// Configures the <see cref="ITransformer"/> implementation with specific user-defined properties.
    /// </summary>
    /// <param name="properties">The properties.</param>
    public void Configure(IDictionary<string, string> properties) { }

    /// <summary>
    /// Transform a given <see cref="ReceivedMessage" /> to a Canonical <see cref="MessagingContext" /> instance.
    /// </summary>
    /// <param name="message">Given message to transform.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public Task<MessagingContext> TransformAsync(ReceivedMessage message, CancellationToken cancellation)
    {
        if (message.UnderlyingStream == null)
        {
            throw new InvalidDataException($"Invalid incoming request stream received from {message.Origin}");
        }

        return CreatePullRequestAsync(message, cancellation);
    }

    private async Task<MessagingContext> CreatePullRequestAsync(ReceivedMessage receivedMessage, CancellationToken cancellation)
    {
        var pmode = await DeserializeValidPModeAsync(receivedMessage, cancellation);

        _logger.LogInformation("Prepare sending PullRequest with MPC=\"{Mpc}\"", pmode.MessagePackaging?.Mpc);
        var pullRequestMessage = AS4Message.Create(new PullRequest(_identifierFactory.Create(), pmode.MessagePackaging?.Mpc), pmode);

        return new MessagingContext(pullRequestMessage, MessagingContextMode.PullReceive) { SendingPMode = pmode };
    }

    private async Task<SendingProcessingMode> DeserializeValidPModeAsync(ReceivedMessage receivedMessage, CancellationToken cancellation)
    {
        var pmode = await AS4XmlSerializer.FromStreamAsync<SendingProcessingMode>(receivedMessage.UnderlyingStream, cancellation)
            ?? throw new InvalidPModeException("Deserialize PMode failed");

        var result = _sendingProcessingModeValidator.Validate(pmode);
        if (result.IsValid)
        {
            return pmode;
        }

        throw CreateInvalidPModeException(pmode, result);
    }

    private InvalidDataException CreateInvalidPModeException(IPMode pmode, ValidationResult result)
    {
        var errorMessage = result.AppendValidationErrorsToErrorMessage($"Receiving PMode {pmode.Id} is not valid");

        _logger.LogError(errorMessage);

        return new InvalidDataException(errorMessage);
    }
}
