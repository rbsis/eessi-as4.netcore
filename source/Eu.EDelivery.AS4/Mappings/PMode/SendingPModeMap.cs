using Eu.EDelivery.AS4.Factories;
using Eu.EDelivery.AS4.Model.Core;
using static Eu.EDelivery.AS4.Constants.Namespaces;
using AgreementReference = Eu.EDelivery.AS4.Model.Core.AgreementReference;
using Service = Eu.EDelivery.AS4.Model.Core.Service;

namespace Eu.EDelivery.AS4.Mappings.PMode;

/// <summary>
/// Collection of mapping functions from ebMS models to models in the <see cref="PMode"/> namespace.
/// </summary>
public class SendingPModeMap : ISendingPModeMap
{
    private readonly IIdentifierFactory _identifierFactory;

    public SendingPModeMap(IIdentifierFactory identifierFactory)
    {
        _identifierFactory = identifierFactory;
    }

    /// <summary>
    /// Creates an <see cref="UserMessage"/> entirely from the given <paramref name="sendingPMode"/> information.
    /// </summary>
    /// <param name="sendingPMode">
    ///     The pmode from which the values in the <see cref="Model.PMode.SendingProcessingMode.MessagePackaging"/> will be used to create an <see cref="UserMessage"/>.
    /// </param>
    /// <param name="parts">The optional list of part references for attachments in the <see cref="AS4Message"/>.</param>
    public UserMessage CreateUserMessage(Model.PMode.SendingProcessingMode sendingPMode, params PartInfo[] parts)
    {
        ArgumentNullException.ThrowIfNull(sendingPMode);

        var properties =
            sendingPMode.MessagePackaging
                        ?.MessageProperties
                        ?.Where(p => p != null)
                        .Select(p => new MessageProperty(p.Name, p.Value, p.Type))
                        .ToArray() ?? Enumerable.Empty<MessageProperty>();

        return new UserMessage(
            _identifierFactory.Create(),
            sendingPMode.MessagePackaging?.Mpc,
            new CollaborationInfo(
                ResolveAgreementReference(sendingPMode),
                ResolveService(sendingPMode),
                ResolveAction(sendingPMode),
                CollaborationInfo.DefaultConversationId),
            ResolveSender(sendingPMode.MessagePackaging?.PartyInfo?.FromParty),
            ResolveReceiver(sendingPMode.MessagePackaging?.PartyInfo?.ToParty),
            parts ?? Enumerable.Empty<PartInfo>(),
            properties);
    }

    /// <summary>
    /// Resolves the <see cref="AgreementReference"/> from the <see cref="Model.PMode.SendingProcessingMode.MessagePackaging"/> element.
    /// </summary>
    /// <param name="pmode">The pmode to retrieve the agreement reference from.</param>
    internal static Maybe<AgreementReference> ResolveAgreementReference(Model.PMode.SendingProcessingMode? pmode)
    {
        if (pmode == null || pmode.MessagePackaging == null)
        {
            return Maybe<AgreementReference>.Nothing;
        }
        var pmodeAgreement = pmode.MessagePackaging.CollaborationInfo?.AgreementReference;
        if (pmodeAgreement?.Value == null)
        {
            return Maybe<AgreementReference>.Nothing;
        }

        var type = (pmodeAgreement.Type != null).ThenMaybe(pmodeAgreement.Type!);

        var pmodeId = (pmodeAgreement.PModeId != null)
            .ThenMaybe(pmodeAgreement.PModeId!)
            .Where(_ => pmode.MessagePackaging.IncludePModeId);

        return Maybe.Just(new AgreementReference(pmodeAgreement.Value, type, pmodeId));
    }

    /// <summary>
    /// Resolves the <see cref="Service"/> from the <see cref="Model.PMode.SendingProcessingMode.MessagePackaging"/> element.
    /// </summary>
    /// <param name="pmode">The pmode to retrieve the service from.</param>
    public Service ResolveService(Model.PMode.SendingProcessingMode? pmode)
    {
        if (pmode?.MessagePackaging?.CollaborationInfo?.Service != null)
        {
            var pmodeService = pmode.MessagePackaging.CollaborationInfo.Service;
            if (string.IsNullOrEmpty(pmodeService.Value))
            {
                return Service.TestService;
            }

            if (pmodeService.Type == null)
            {
                return new Service(pmodeService.Value);
            }

            return new Service(pmodeService.Value, pmodeService.Type);
        }

        return Service.TestService;
    }

    /// <summary>
    /// Resolves the Action from the  <see cref="Model.PMode.SendingProcessingMode.MessagePackaging"/> element. 
    /// </summary>
    /// <param name="pmode">The pmode to retrieve the action from.</param>
    public string ResolveAction(Model.PMode.SendingProcessingMode? pmode)
    {
        var pmodeCollaboration = pmode?.MessagePackaging?.CollaborationInfo;

        if (string.IsNullOrEmpty(pmodeCollaboration?.Action))
        {
            return Constants.Namespaces.TestAction;
        }

        return pmodeCollaboration.Action;
    }

    /// <summary>
    /// Resolves the sender party or FromParty from the <see cref="Model.PMode.Party"/> element.
    /// </summary>
    /// <param name="party">The party of the pmode to map to an ebMS party.</param>
    internal static Party ResolveSender(Model.PMode.Party? party) =>
        party != null ? CreatePartyModel(party) : Party.DefaultFrom;

    /// <summary>
    /// Resolves the receiver party or ToParty from the <see cref="Model.PMode.Party"/> element.
    /// </summary>
    /// <param name="party">The party of the pmode to map to an ebMS party.</param>
    internal static Party ResolveReceiver(Model.PMode.Party? party) =>
        party != null ? CreatePartyModel(party) : Party.DefaultTo;

    private static Party CreatePartyModel(Model.PMode.Party p)
    {
        var ids = p.PartyIds == null
            ? []
            : p.PartyIds
                .Where(p => !string.IsNullOrEmpty(p.Id))
                .Select(id => string.IsNullOrEmpty(id.Type)
                    ? new PartyId(id.Id!)
                    : new PartyId(id.Id!, id.Type))
                .ToArray();

        return new Party(p.Role ?? EbmsDefaultRole, ids);
    }
}
