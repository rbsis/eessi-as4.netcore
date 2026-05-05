using System.ComponentModel;
using System.Configuration;
using System.Xml;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Services.DynamicDiscovery;
using Eu.EDelivery.AS4.Validators;
using FluentValidation;
using Microsoft.Extensions.Logging;
using AS4Party = Eu.EDelivery.AS4.Model.Core.Party;
using InvalidOperationException = System.InvalidOperationException;
using PartyId = Eu.EDelivery.AS4.Model.Core.PartyId;
using PModeParty = Eu.EDelivery.AS4.Model.PMode.Party;
using SubmitParty = Eu.EDelivery.AS4.Model.Common.Party;
using static Eu.EDelivery.AS4.Constants.Namespaces;

namespace Eu.EDelivery.AS4.Steps.Submit;

/// <summary>
/// <see cref="IStep" /> implementation to dynamically complete the <see cref="SendingProcessingMode"/>.
/// </summary>    
[Info("Perform Dynamic Discovery if required")]
[Description(
    "Contacts an SMP server and executes the configured SMP Profile if dynamic discovery is enabled. \n\r" +
    "The information returned from the SMP server is used to complete the sending PMode.")]
public class DynamicDiscoveryStep : IStep
{
    private readonly ILogger<DynamicDiscoveryStep> _logger;
    private readonly IDynamicDiscoveryProfileResolver _dynamicDiscoveryProfileResolver;
    private readonly IValidator<SendingProcessingMode> _sendingProcessingModeValidator;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicDiscoveryStep"/> class.
    /// </summary>
    public DynamicDiscoveryStep(
        ILogger<DynamicDiscoveryStep> logger,
        IDynamicDiscoveryProfileResolver dynamicDiscoveryProfileResolver,
        IValidator<SendingProcessingMode> sendingProcessingModeValidator)
    {
        _logger = logger;
        _dynamicDiscoveryProfileResolver = dynamicDiscoveryProfileResolver;
        _sendingProcessingModeValidator = sendingProcessingModeValidator;
    }

    /// <summary>
    /// Execute the step for a given <paramref name="messagingContext"/>.
    /// </summary>
    /// <param name="messagingContext">Message used during the step execution.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(messagingContext);

        if (messagingContext.AS4Message == null && messagingContext.Mode == MessagingContextMode.Forward)
        {
            var message = $"{nameof(DynamicDiscoveryStep)} requires an AS4Message when used in a Forward Agent, "
                + "please make sure that the ReceivedMessage is deserialized before executing this step."
                + $"{Environment.NewLine} Possibly this failure happened because the Transformer of the Forward Agent is still using "
                + "the ForwardMessageTransformer instead of the AS4MessageTransformer";

            _logger.LogError(message);

            throw new InvalidOperationException(
                "Dynamic Discovery process cannot be used in a Forwarding scenario for messages that are not AS4Messages");
        }

        if (messagingContext.SendingPMode == null || !messagingContext.SendingPMode.DynamicDiscoverySpecified)
        {
            _logger.LogTrace("Skip Dynamic Discovery because SendingPMode {SendingPModeId} is not configured for Dynamic Discovery", messagingContext.SendingPMode?.Id);
            return await StepResult.SuccessAsync(messagingContext);
        }

        var clonedPMode = (SendingProcessingMode)messagingContext.SendingPMode.Clone();
        clonedPMode.Id = $"{clonedPMode.Id}_SMP";

        var smpProfile = messagingContext.SendingPMode.DynamicDiscovery?.SmpProfile
            ?? throw new InvalidOperationException("SmpProfile not found");

        var profile = _dynamicDiscoveryProfileResolver.Resolve(smpProfile);
        _logger.LogInformation("{LogTag} DynamicDiscovery is enabled in SendingPMode - using {Profile}", messagingContext.LogTag, profile.GetType().Name);

        var toParty = messagingContext.AS4Message != null && messagingContext.Mode == MessagingContextMode.Forward
            ? ResolveAS4ReceiverParty(messagingContext.AS4Message)
            : ResolveSubmitOrPModeReceiverParty(
                messagingContext.SubmitMessage?.PartyInfo?.ToParty,
                messagingContext.SendingPMode.MessagePackaging?.PartyInfo?.ToParty,
                messagingContext.SendingPMode.AllowOverride);

        var result = await DynamicDiscoverSendingPModeAsync(messagingContext.SendingPMode, profile, toParty, cancellation);
        _logger.LogDebug("SendingPMode {CompletedSendingPModeId} completed with SMP metadata", result.CompletedSendingPMode.Id);

        messagingContext.SendingPMode = result.CompletedSendingPMode;
        if (messagingContext.SubmitMessage != null && result.OverrideToParty)
        {
            if (messagingContext.SubmitMessage.PartyInfo != null)
            {
                messagingContext.SubmitMessage.PartyInfo.ToParty = null;
            }

            messagingContext.SubmitMessage.PMode = result.CompletedSendingPMode;
        }

        return await StepResult.SuccessAsync(messagingContext);
    }

    private async Task<DynamicDiscoveryResult> DynamicDiscoverSendingPModeAsync(
        SendingProcessingMode sendingPMode,
        IDynamicDiscoveryProfile profile,
        AS4Party toParty,
        CancellationToken cancellation)
    {
        try
        {
            var clonedPMode = (SendingProcessingMode)sendingPMode.Clone();
            clonedPMode.Id = $"{clonedPMode.Id}_SMP";

            if (clonedPMode.DynamicDiscovery == null)
            {
                throw new ConfigurationErrorsException(@"Cannot retrieve SMP metadata: SendingPMode requires a <DynamicDiscovery/> element");
            }

            var smpMetaData = await RetrieveSmpMetaDataAsync(profile, clonedPMode.DynamicDiscovery, toParty, cancellation);
            if (smpMetaData == null)
            {
                _logger.LogError("No SMP meta-data document was retrieved by the Dynamic Discovery profile: {Profile}", profile.GetType().Name);
                throw new InvalidDataException(
                    "No SMP meta-data document was retrieved during the Dynamic Discovery process");
            }

            var result = profile.DecoratePModeWithSmpMetaData(clonedPMode, smpMetaData);
            if (result == null)
            {
                _logger.LogError(@"No decorated SendingPMode was returned by the Dynamic Discovery profile: {Profile}", profile.GetType().Name);
                throw new InvalidDataException(
                    "No decorated SendingPMode was returned during the Dynamic Discovery");
            }

            ValidatePMode(result.CompletedSendingPMode);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "An exception occured during the Dynamic Discovery process of the profile: {ProfileName} "
                + "with the message having ToParty={ToParty} for SendingPMode {SendingPModeId}",
                profile.GetType().Name,
                toParty,
                sendingPMode.Id);

            throw new DynamicDiscoveryException(
                "An exception occured during the Dynamic Discovery process", ex);
        }
    }

    private static async Task<XmlDocument> RetrieveSmpMetaDataAsync(
        IDynamicDiscoveryProfile profile,
        DynamicDiscoveryConfiguration dynamicDiscovery,
        AS4Party toParty,
        CancellationToken cancellation)
    {
        if (dynamicDiscovery == null)
        {
            throw new ConfigurationErrorsException(
                @"Cannot retrieve SMP metadata: SendingPMode requires a <DynamicDiscovery/> element");
        }

        var customProperties = (dynamicDiscovery.Settings ?? [])
            .Where(s => s.Key != null && s.Value != null)
            .ToDictionary(s => s.Key!, s => s.Value!, StringComparer.OrdinalIgnoreCase);

        return await profile.RetrieveSmpMetaDataAsync(toParty, customProperties, cancellation);
    }

    private void ValidatePMode(SendingProcessingMode pmode)
    {
        _sendingProcessingModeValidator
            .Validate(pmode)
            .Result(
                onValidationSuccess: result => _logger.LogDebug("Dynamically completed PMode {PModeId} is valid", pmode.Id),
                onValidationFailed: result =>
                {
                    var errorMessage = result.AppendValidationErrorsToErrorMessage(
                        $"(Submit) Dynamically completed PMode {pmode.Id} was invalid:");

                    _logger.LogError(errorMessage);

                    throw new ConfigurationErrorsException(errorMessage);
                });
    }

    private AS4Party ResolveAS4ReceiverParty(AS4Message msg)
    {
        if (msg?.PrimaryMessageUnit is UserMessage m)
        {
            _logger.LogDebug("Resolve ToParty in a Forwarding scenario from AS4Message's primary messsage unit {MessageId} {{MessageType=UserMessage}}",
                m.MessageId);

            return m.Receiver;
        }

        throw new InvalidOperationException("Only AS4Message with an UserMessage as primary message unit can be used dynamically discover the SendingPMode");
    }

    private AS4Party ResolveSubmitOrPModeReceiverParty(
        SubmitParty? submitParty,
        PModeParty? pmodeParty,
        bool allowOverride)
    {
        if (!allowOverride
            && submitParty != null && pmodeParty != null
            && !submitParty.Equals(pmodeParty))
        {
            throw new NotSupportedException("SubmitMessage is not allowed by the SendingPMode to override ToParty");
        }

        if (submitParty == null && pmodeParty == null)
        {
            throw new InvalidOperationException(
                "Either the SubmitMessage or the SendingPMode is required to have a " +
                "ToParty configured for dynamic discovery in a non-Forwarding scenario");
        }

        if (submitParty != null)
        {
            _logger.LogDebug("Resolve ToParty in non-Forwarding scenario from SubmitMessage because SendingPMode allows overriding (AllowOverride = true)");

            return CreateToPartyFrom(
                "SubmitMessage",
                submitParty.Role ?? EbmsDefaultRole,
                (submitParty.PartyIds ?? [])
                    .Select(p => new PartyId(p.Id, p.Type.AsMaybe())));
        }

        if (pmodeParty!.PartyIds == null || !pmodeParty.PartyIds.Any())
        {
            _logger.LogError(
                "Cannot retrieve SMP metadata because SendingPMode must contain at lease one "
                + "<ToPartyId/> element in the MessagePackaging.PartyInfo.ToParty element");

            throw new ConfigurationErrorsException(
                "Cannot retrieve SMP metadata because the message is referencing an incomplete SendingPMode");
        }

        _logger.LogDebug("Resolve ToParty in non-Forwarding scenario from SendingPMode because SubmitMessage has none");
        return CreateToPartyFrom(
            "SendingPMode",
            pmodeParty.Role ?? EbmsDefaultRole,
            (pmodeParty.PartyIds ?? [])
                .Where(p => p.Id != null)
                .Select(p => new PartyId(p.Id!, p.Type.AsMaybe())));
    }

    private static AS4Party CreateToPartyFrom(string log, string role, IEnumerable<PartyId> ids)
    {
        try
        {
            return new AS4Party(role, ids);
        }
        catch (ArgumentNullException ex)
        {
            throw new InvalidDataException($"{log} has an incomplete ToParty: {ex.Message}");
        }
    }
}

/// <summary>
/// Represents a exception that occurs during the dynamic discovery process.
/// </summary>
[Serializable]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3925:\"ISerializable\" should be implemented correctly", Justification = "<Pending>")]
public class DynamicDiscoveryException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicDiscoveryException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the exception.</param>
    public DynamicDiscoveryException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicDiscoveryException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public DynamicDiscoveryException(string message, Exception innerException) : base(message, innerException) { }
}
