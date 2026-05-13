using Eu.EDelivery.AS4.Factories;
using Eu.EDelivery.AS4.Mappings.PMode;
using Eu.EDelivery.AS4.Model.Common;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Model.Submit;
using AgreementReference = Eu.EDelivery.AS4.Model.Core.AgreementReference;
using CollaborationInfo = Eu.EDelivery.AS4.Model.Core.CollaborationInfo;
using MessageProperty = Eu.EDelivery.AS4.Model.Core.MessageProperty;
using Party = Eu.EDelivery.AS4.Model.Core.Party;
using PartyId = Eu.EDelivery.AS4.Model.Core.PartyId;
using Service = Eu.EDelivery.AS4.Model.Core.Service;
using static Eu.EDelivery.AS4.Constants.Namespaces;

namespace Eu.EDelivery.AS4.Mappings.Submit;

/// <summary>
/// Collection of mapping functions to create ebMS models from Submit models,
/// optionally forwarding calls to mapping from <see cref="Model.PMode.SendingProcessingMode"/> models.
/// </summary>
public class SubmitMessageMap : ISubmitMessageMap
{
    private readonly IIdentifierFactory _identifierFactory;
    private readonly ISendingPModeMap _sendingPModeMap;

    public SubmitMessageMap(IIdentifierFactory identifierFactory, ISendingPModeMap sendingPModeMap)
    {
        _identifierFactory = identifierFactory;
        _sendingPModeMap = sendingPModeMap;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="submit"></param>
    /// <param name="sendingPMode"></param>
    /// <returns></returns>
    public UserMessage CreateUserMessage(SubmitMessage submit, SendingProcessingMode? sendingPMode)
    {
        var collaboration = new CollaborationInfo(
            ResolveAgreement(submit, sendingPMode),
            ResolveService(submit, sendingPMode),
            ResolveAction(submit, sendingPMode),
            ResolveConversationId(submit));

        return new UserMessage(
            messageId: submit.MessageInfo.MessageId ?? _identifierFactory.Create(),
            refToMessageId: submit.MessageInfo.RefToMessageId,
            timestamp: DateTimeOffset.Now,
            mpc: ResolveMpc(submit, sendingPMode),
            collaboration: collaboration,
            sender: ResolveSenderParty(submit, sendingPMode),
            receiver: ResolveReceiverParty(submit, sendingPMode),
            partInfos: ResolvePartInfos(submit, sendingPMode).ToArray(),
            messageProperties: [.. ResolveMessageProperties(submit, sendingPMode)]);
    }

    private string ResolveAction(SubmitMessage submit, SendingProcessingMode? sendingPMode)
    {
        var submitAction = submit.Collaboration.Action;
        var pmodeAction = sendingPMode?.MessagePackaging?.CollaborationInfo?.Action;

        if (sendingPMode?.AllowOverride == false
            && !string.IsNullOrEmpty(submitAction)
            && !string.IsNullOrEmpty(pmodeAction)
            && !StringComparer.OrdinalIgnoreCase.Equals(submitAction, pmodeAction))
        {
            throw new NotSupportedException(
                $"SubmitMessage is not allowed by SendingPMode {sendingPMode.Id} to override Action");
        }

        if (!string.IsNullOrEmpty(submitAction))
        {
            return submitAction;
        }

        return _sendingPModeMap.ResolveAction(sendingPMode);
    }

    private static string ResolveConversationId(SubmitMessage submit)
    {
        var submitConversationId = submit?.Collaboration?.ConversationId;
        return string.IsNullOrEmpty(submitConversationId)
            ? CollaborationInfo.DefaultConversationId
            : submitConversationId;
    }

    private static Maybe<AgreementReference> ResolveAgreement(SubmitMessage submit, SendingProcessingMode? sendingPMode)
    {
        var pmodeAgreement = sendingPMode?.MessagePackaging?.CollaborationInfo?.AgreementReference;
        var submitAgreement = submit?.Collaboration?.AgreementRef;

        var includePModeId = sendingPMode?.MessagePackaging?.IncludePModeId == true;

        if (sendingPMode?.AllowOverride == false
            && !string.IsNullOrEmpty(submitAgreement?.Value)
            && !string.IsNullOrEmpty(pmodeAgreement?.Value)
            && !StringComparer.OrdinalIgnoreCase.Equals(pmodeAgreement.Value, submitAgreement.Value))
        {
            throw new NotSupportedException(
                $"SubmitMessage is not allowed by the Sending PMode {sendingPMode.Id} to override AgreementReference.Value");
        }

        if (!string.IsNullOrEmpty(submitAgreement?.Value))
        {
            return Maybe.Just(
                new AgreementReference(
                    submitAgreement.Value,
                    submitAgreement.RefType,
                    includePModeId ? sendingPMode!.Id : null));
        }

        if (!string.IsNullOrEmpty(pmodeAgreement?.Value))
        {
            return Maybe.Just(
                new AgreementReference(
                    pmodeAgreement.Value,
                    pmodeAgreement.Type,
                    includePModeId ? sendingPMode!.Id : null));
        }

        return Maybe<AgreementReference>.Nothing;
    }

    private Service ResolveService(SubmitMessage submit, SendingProcessingMode? sendingPMode)
    {
        var pmodeService = sendingPMode?.MessagePackaging?.CollaborationInfo?.Service;
        var submitService = submit?.Collaboration?.Service;

        if (sendingPMode?.AllowOverride == false
            && !string.IsNullOrEmpty(submitService?.Value)
            && !string.IsNullOrEmpty(pmodeService?.Value)
            && !StringComparer.OrdinalIgnoreCase.Equals(submitService.Value, pmodeService.Value))
        {
            throw new NotSupportedException(
                $"SubmitMessage is not allowed by SendingPMode {sendingPMode.Id} to override CollaborationInfo.Service");
        }

        if (submitService?.Value != null)
        {
            return new Service(submitService.Value, submitService.Type);
        }

        return _sendingPModeMap.ResolveService(sendingPMode);
    }

    private static IEnumerable<MessageProperty> ResolveMessageProperties(SubmitMessage submit, SendingProcessingMode? sendingPMode)
    {
        if (submit.MessageProperties != null)
        {
            foreach (var p in submit.MessageProperties)
            {
                yield return new MessageProperty(p.Name, p.Value, p.Type);
            }
        }

        if (sendingPMode?.MessagePackaging?.MessageProperties != null)
        {
            foreach (var p in sendingPMode.MessagePackaging.MessageProperties)
            {
                yield return new MessageProperty(p.Name, p.Value, p.Type);
            }
        }
    }

    private static string ResolveMpc(SubmitMessage submit, SendingProcessingMode? sendingPMode)
    {
        var pmodeMpc = sendingPMode?.MessagePackaging?.Mpc;
        var submitMpc = submit?.MessageInfo?.Mpc;

        if (sendingPMode?.AllowOverride == false
            && !string.IsNullOrEmpty(submitMpc)
            && !StringComparer.OrdinalIgnoreCase.Equals(Constants.Namespaces.EbmsDefaultMpc, submitMpc)
            && !string.IsNullOrEmpty(pmodeMpc)
            && !StringComparer.OrdinalIgnoreCase.Equals(submitMpc, pmodeMpc))
        {
            throw new NotSupportedException(
                $"SubmitMessage is not allowed by SendingPMode {sendingPMode.Id} to override Mpc");
        }

        if (!string.IsNullOrEmpty(pmodeMpc))
        {
            return !string.IsNullOrEmpty(submitMpc)
            ? submitMpc
            : pmodeMpc;
        }
        else
        {
            return !string.IsNullOrEmpty(submitMpc)
            ? submitMpc
            : Constants.Namespaces.EbmsDefaultMpc;
        }
    }

    private static Party ResolveReceiverParty(SubmitMessage submit, SendingProcessingMode? sendingPMode)
    {
        var pmodeParty = sendingPMode?.MessagePackaging?.PartyInfo?.ToParty;
        var submitParty = submit?.PartyInfo?.ToParty;

        if (sendingPMode?.AllowOverride == false
            && submitParty != null
            && pmodeParty != null
            && !submitParty.Equals(pmodeParty))
        {
            throw new NotSupportedException(
                $"SubmitMessage is not allowed by the SendingPMode {sendingPMode.Id} to override Receiver Party");
        }

        if (submitParty != null)
        {
            var ids = submitParty.PartyIds ?? Enumerable.Empty<Model.Common.PartyId>();
            return new Party(submitParty.Role ?? EbmsDefaultRole, ids.Select(x => new PartyId(x.Id, x.Type)).ToArray());
        }

        return SendingPModeMap.ResolveReceiver(pmodeParty);
    }

    private static Party ResolveSenderParty(SubmitMessage submit, SendingProcessingMode? sendingPMode)
    {
        var pmodeParty = sendingPMode?.MessagePackaging?.PartyInfo?.FromParty;
        var submitParty = submit?.PartyInfo?.FromParty;

        if (sendingPMode?.AllowOverride == false
            && submitParty != null
            && pmodeParty != null
            && !submitParty.Equals(pmodeParty))
        {
            throw new NotSupportedException(
                $"SubmitMessage is not allowed by SendingPMode {sendingPMode.Id} to override Sender Party");
        }

        if (submitParty != null)
        {
            var ids = submitParty.PartyIds ?? Enumerable.Empty<Model.Common.PartyId>();
            return new Party(submitParty.Role ?? EbmsDefaultRole, ids.Select(x => new PartyId(x.Id, x.Type)).ToArray());
        }

        return SendingPModeMap.ResolveSender(pmodeParty);
    }

    private IEnumerable<PartInfo> ResolvePartInfos(SubmitMessage submit, SendingProcessingMode? sendingPMode)
    {
        return (submit.Payloads)
               .Where(p => p != null)
               .Select(p => CreatePartInfo(p, sendingPMode))
               .ToArray();
    }

    private PartInfo CreatePartInfo(Payload submitPayload, SendingProcessingMode? sendingPMode)
    {
        var id = submitPayload.Id ?? _identifierFactory.Create();
        var href = id.StartsWith("cid:") ? id : $"cid:{id}";

        IEnumerable<Model.Core.Schema> schemas =
            (submitPayload.Schemas ?? [])
            .Where(sch => sch != null)
            .Select(sch =>
            {
                // TODO: should we throw or skip?
                if (sch.Location == null)
                {
                    throw new InvalidDataException(
                        "SubmitMessage contains Payload with a Schema that hasn't got a Location");
                }

                return new Model.Core.Schema(sch.Location, sch.Version, sch.Namespace);
            })
            .ToArray();

        IDictionary<string, string> properties = (submitPayload.PayloadProperties ?? [])
            .Where(p => p.Name != null && p.Value != null)
            .Select(p => (p.Name!, p.Value!))
            .Concat(CreatePayloadCompressionProperties(submitPayload, sendingPMode))
            .ToDictionary<(string propName, string propValue), string, string>(
                t => t.propName,
                t => t.propValue,
                StringComparer.OrdinalIgnoreCase);

        return new PartInfo(href, properties, schemas);
    }

    private static IEnumerable<(string propName, string propValue)> CreatePayloadCompressionProperties(
        Payload payload,
        SendingProcessingMode? sendingPMode)
    {
        if ((sendingPMode?.MessagePackaging?.UseAS4Compression) != true)
        {
            return [];
        }

        return
        [
            ("CompressionType", "application/gzip"),
            ("MimeType", !string.IsNullOrEmpty(payload.MimeType)
                ? payload.MimeType
                : "application/octet-stream")
        ];
    }
}
