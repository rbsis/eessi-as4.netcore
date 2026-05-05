using Eu.EDelivery.AS4.Extensions;

namespace Eu.EDelivery.AS4.Mappings.Core;

internal static class UserMessageMap
{
    /// <summary>
    /// Maps from a domain model representation to an XML representation of an AS4 usermessage.
    /// </summary>
    /// <param name="model">The domain model to convert.</param>
    internal static Xml.UserMessage Convert(Model.Core.UserMessage model)
    {
        return new Xml.UserMessage
        {
            MessageInfo = new Xml.MessageInfo
            {
                MessageId = model.MessageId,
                RefToMessageId = model.RefToMessageId,
                Timestamp = model.Timestamp.LocalDateTime
            },
            mpc = model.Mpc,
            CollaborationInfo = MapCollaborationInfo(model.CollaborationInfo),
            PartyInfo = MapPartyInfo(model.Sender, model.Receiver),
            PayloadInfo = MapPartInfos(model.PayloadInfo),
            MessageProperties = MapMessageProperties(model.MessageProperties)
        };
    }

    /// <summary>
    /// Maps from an XML representation to a domain model representation of an AS4 usermessage.
    /// </summary>
    /// <param name="xml">The XML representation to convert.</param>
    internal static Model.Core.UserMessage Convert(Xml.UserMessage xml)
    {
        ArgumentException.ThrowIfNullOrEmpty(xml.MessageInfo.MessageId);

        return new Model.Core.UserMessage(
            messageId: xml.MessageInfo.MessageId,
            refToMessageId: xml.MessageInfo.RefToMessageId,
            timestamp: xml.MessageInfo.Timestamp.ToDateTimeOffset(),
            mpc: xml.mpc ?? Constants.Namespaces.EbmsDefaultMpc,
            collaboration: MapCollaborationInfo(xml.CollaborationInfo),
            sender: MapParty(xml.PartyInfo.From),
            receiver: MapParty(xml.PartyInfo.To),
            partInfos: [.. MapPartInfos(xml.PayloadInfo)],
            messageProperties: [.. MapMessageProperties(xml.MessageProperties)]);
    }

    /// <summary>
    /// Maps from a domain model representation of an AS4 usermessage to an XML representation of an AS4 routing usermessage.
    /// </summary>
    /// <param name="model">The domain model to convert.</param>
    internal static Xml.RoutingInputUserMessage ConvertToRouting(Model.Core.UserMessage model)
    {
        var r = new Xml.RoutingInputUserMessage
        {
            MessageInfo = new Xml.MessageInfo
            {
                MessageId = model.MessageId,
                RefToMessageId = model.RefToMessageId,
                Timestamp = model.Timestamp.LocalDateTime
            },
            mpc = model.Mpc,
            CollaborationInfo = MapCollaborationInfo(model.CollaborationInfo),
            PartyInfo = MapPartyInfo(model.Receiver, model.Sender),
            MessageProperties = MapMessageProperties(model.MessageProperties),
            PayloadInfo = MapPartInfos(model.PayloadInfo)
        };

        r.mpc ??= Constants.Namespaces.EbmsDefaultMpc;
        r.CollaborationInfo.Action = r.CollaborationInfo.Action + ".response";

        return r;
    }

    /// <summary>
    /// Maps from an XML representation of an AS4 routing usermessage to a domain model representation of an AS4 usermessage.
    /// </summary>
    /// <param name="xml">The XML representation to convert.</param>
    internal static Model.Core.UserMessage ConvertFromRouting(Xml.RoutingInputUserMessage xml)
    {
        ArgumentNullException.ThrowIfNull(xml.MessageInfo);
        ArgumentException.ThrowIfNullOrEmpty(xml.MessageInfo.MessageId);

        return new Model.Core.UserMessage(
            messageId: xml.MessageInfo.MessageId,
            refToMessageId: xml.MessageInfo?.RefToMessageId,
            timestamp: xml.MessageInfo?.Timestamp.ToDateTimeOffset() ?? DateTimeOffset.Now,
            mpc: string.IsNullOrEmpty(xml.mpc) ? Constants.Namespaces.EbmsDefaultMpc : xml.mpc,
            collaboration: RemoveResponsePostfixToActionWhenEmpty(MapCollaborationInfo(xml.CollaborationInfo)),
            sender: MapParty(xml.PartyInfo.From),
            receiver: MapParty(xml.PartyInfo.To),
            partInfos: [.. MapPartInfos(xml.PayloadInfo)],
            messageProperties: [.. MapMessageProperties(xml.MessageProperties)]);
    }

    private static Model.Core.CollaborationInfo RemoveResponsePostfixToActionWhenEmpty(Model.Core.CollaborationInfo? mapped)
    {
        if (mapped is null)
        {
            return new Model.Core.CollaborationInfo(
                Maybe<Model.Core.AgreementReference>.Nothing,
                Model.Core.Service.TestService,
                Constants.Namespaces.TestAction,
                Model.Core.CollaborationInfo.DefaultConversationId);
        }

        var action = mapped.Action;
        if (!string.IsNullOrWhiteSpace(action)
            && action.EndsWith(".response", StringComparison.OrdinalIgnoreCase))
        {
            return new Model.Core.CollaborationInfo(
                mapped.AgreementReference,
                mapped.Service,
                action.Substring(0, action.LastIndexOf(".response", StringComparison.OrdinalIgnoreCase)),
                mapped.ConversationId);
        }

        return mapped;
    }

    private static Xml.PartyInfo MapPartyInfo(Model.Core.Party sender, Model.Core.Party receiver)
    {
        static Xml.PartyId[] MapPartyIds(IEnumerable<Model.Core.PartyId> ids)
        {
            if (!ids.Any())
            {
                return [];
            }

            return [.. ids.Select(i => new Xml.PartyId
            {
                Value = i.Id,
                type = i.Type.GetOrElse(() => null!)
            })];
        }

        return new Xml.PartyInfo
        {
            From = new Xml.From
            {
                Role = sender.Role,
                PartyId = MapPartyIds(sender.PartyIds)
            },
            To = new Xml.To
            {
                Role = receiver.Role,
                PartyId = MapPartyIds(receiver.PartyIds)
            }
        };
    }

    private static Xml.CollaborationInfo MapCollaborationInfo(Model.Core.CollaborationInfo model)
    {
        var agreementRef =
            model.AgreementReference
                 .Select(a => new Xml.AgreementRef
                 {
                     Value = a.Value,
                     type = a.Type.GetOrElse(() => null!),
                     pmode = a.PModeId.GetOrElse(() => null!)
                 })
                 .GetOrElse(() => null!);

        return new Xml.CollaborationInfo
        {
            Action = model.Action,
            ConversationId = model.ConversationId,
            Service = new Xml.Service
            {
                Value = model.Service.Value,
                type = model.Service.Type.GetOrElse(() => null!)
            },
            AgreementRef = agreementRef
        };
    }

    private static Model.Core.CollaborationInfo MapCollaborationInfo(Xml.CollaborationInfo xml)
    {
        var agreementRef = xml.AgreementRef == null
            ? Maybe<Model.Core.AgreementReference>.Nothing
            : new Model.Core.AgreementReference(
                    xml.AgreementRef.Value,
                    (xml.AgreementRef.type != null).ThenMaybe(xml.AgreementRef.type!),
                    (xml.AgreementRef.pmode != null).ThenMaybe(xml.AgreementRef.pmode!))
                .AsMaybe();

        var service = xml.Service == null
            ? Model.Core.Service.TestService
            : new Model.Core.Service(
                xml.Service.Value,
                (xml.Service.type != null).ThenMaybe(xml.Service.type!));

        return new Model.Core.CollaborationInfo(
            agreementRef,
            service,
            xml.Action ?? Constants.Namespaces.TestAction,
            xml.ConversationId ?? Model.Core.CollaborationInfo.DefaultConversationId);
    }

    private static Model.Core.Party MapParty(Xml.To? toParty)
    {
        ArgumentNullException.ThrowIfNull(toParty);

        static Model.Core.PartyId MapPartyId(Xml.PartyId id)
        {
            return new(id.Value, (id.type != null).ThenMaybe(id.type!));
        }

        var partyIds = toParty.PartyId
            .Where(x => x != null)
            .Select(MapPartyId)
            .ToArray();

        return new(toParty.Role, partyIds);
    }

    private static Model.Core.Party MapParty(Xml.From? fromParty)
    {
        ArgumentNullException.ThrowIfNull(fromParty);

        static Model.Core.PartyId MapPartyId(Xml.PartyId id)
        {
            return new(id.Value, (id.type != null).ThenMaybe(id.type!));
        }

        var partyIds =
            (fromParty.PartyId ?? [])
            .Where(x => x != null)
            .Select(MapPartyId)
            .ToArray();

        return new(fromParty.Role, partyIds);
    }

    private static Xml.PartInfo[]? MapPartInfos(IEnumerable<Model.Core.PartInfo> parts)
    {
        if (!parts.Any())
        {
            return null;
        }

        Xml.Schema MapSchema(Model.Core.Schema s) => new()
        {
            location = s.Location,
            version = s.Version.GetOrElse(() => null!),
            @namespace = s.Namespace.GetOrElse(() => null!)
        };

        Xml.Property MapProperty(KeyValuePair<string, string> kv) => new() { name = kv.Key, Value = kv.Value };

        return [.. parts.Select(p => new Xml.PartInfo
        {
            href = p.Href,
            Schemas = p.Schemas.Any() ? [.. p.Schemas.Select(MapSchema)] : null,
            PartProperties = p.Properties.Any() ? [.. p.Properties.Select(MapProperty)] : null
        })];
    }

    private static IEnumerable<Model.Core.PartInfo> MapPartInfos(Xml.PartInfo[]? parts)
    {
        if (parts == null)
        {
            yield break;
        }

        foreach (var part in parts)
        {
            if (part == null)
            {
                continue;
            }

            var props = (part.PartProperties ?? [])
                .Where(p => p != null)
                .ToDictionary(p => p.name, p => p.Value);

            var schemas = (part.Schemas ?? [])
                .Where(s => s != null)
                .Select(s => new Model.Core.Schema(
                    s.location,
                    (s.version != null).ThenMaybe(s.version!),
                    (s.@namespace != null).ThenMaybe(s.@namespace!)))
                .ToArray();

            yield return new Model.Core.PartInfo(part.href ?? string.Empty, props, schemas);
        }
    }

    private static Xml.Property[]? MapMessageProperties(IEnumerable<Model.Core.MessageProperty> props)
    {
        if (!props.Any())
        {
            return null;
        }

        return [.. props.Select(p => new Xml.Property { name = p.Name, Value = p.Value, Type = p.Type })];
    }

    private static IEnumerable<Model.Core.MessageProperty> MapMessageProperties(Xml.Property[]? props)
    {
        if (props == null)
        {
            yield break;
        }

        foreach (var p in props)
        {
            if (p == null)
            {
                continue;
            }

            if (p.TypeSpecified)
            {
                yield return new Model.Core.MessageProperty(p.name, p.Value, p.Type);
            }
            else
            {
                yield return new Model.Core.MessageProperty(p.name, p.Value);
            }
        }
    }
}
