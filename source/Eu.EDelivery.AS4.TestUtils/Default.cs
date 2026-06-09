using Eu.EDelivery.AS4.Builders;
using Eu.EDelivery.AS4.Compression;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Exceptions.Handlers;
using Eu.EDelivery.AS4.Factories;
using Eu.EDelivery.AS4.Http.Response;
using Eu.EDelivery.AS4.Mappings.PMode;
using Eu.EDelivery.AS4.Mappings.Submit;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.Services;
using Eu.EDelivery.AS4.Services.DynamicDiscovery;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Steps.Receive.Participant;
using Eu.EDelivery.AS4.Strategies.Sender;
using Eu.EDelivery.AS4.TestUtils.Repositories;
using Eu.EDelivery.AS4.TestUtils.Stubs;
using Eu.EDelivery.AS4.Transformers;
using Eu.EDelivery.AS4.Validators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Eu.EDelivery.AS4.TestUtils;

public static class Default
{
    private static readonly Lazy<GenericTypeBuilder> _lazyGenericTypeBuilder = new(() => new(
        NullLogger<GenericTypeBuilder>.Instance));

    private static readonly Lazy<SoapEnvelopeSerializer> _lazySoapEnvelopeSerializer = new(() => new());

    private static readonly Lazy<MimeMessageSerializer> _lazyMimeMessageSerializer =
        new(() => new MimeMessageSerializer(NullLogger<MimeMessageSerializer>.Instance, _lazySoapEnvelopeSerializer.Value));

    private static readonly Lazy<SerializerProvider> _lazySerializerProvider =
        new(() => new SerializerProvider(_lazySoapEnvelopeSerializer.Value, _lazyMimeMessageSerializer.Value));

    private static readonly Lazy<InMemoryMessageBodyStore> _lazyMessageBodyStore =
        new(() => new(_lazySerializerProvider.Value));

    private static readonly Lazy<IdentifierFactory> _lazyIdentifierFactory = new(() => new(StubConfig.Default));

    private static readonly Lazy<ParameterValidator> _lazyParameterValidator = new(() => new());

    private static readonly Lazy<ReceivingProcessingModeValidator> _lazyReceivingProcessingModeValidator = new(() => new(
        _lazyParameterValidator.Value));

    private static readonly Lazy<DynamicDiscoveryProfileResolver> _lazyDynamicDiscoveryProfileResolver = new(() =>
    {
        var services = new ServiceCollection();
        var provider = services
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton<LocalDynamicDiscoveryProfile>()
            .AddSingleton<OasisDynamicDiscoveryProfile>()
            .AddSingleton<PeppolDynamicDiscoveryProfile>()
            .BuildServiceProvider();
        return new(NullLogger<DynamicDiscoveryProfileResolver>.Instance, provider);
    });

    private static readonly Lazy<SendingProcessingModeValidator> _lazySendingProcessingModeValidator = new(() => new(
        NullLogger<SendingProcessingModeValidator>.Instance, _lazyParameterValidator.Value, _lazyDynamicDiscoveryProfileResolver.Value));

    private static readonly Lazy<SubmitMessageValidator> _lazySubmitMessageValidator = new(() => new());

    private static readonly Lazy<CertificateRepository> _lazyCertificateRepository = new(() => new(StubConfig.Default));

    private static readonly Lazy<FileSender> _lazyFileSender = new(() => new(
        NullLogger<FileSender>.Instance));

    private static readonly Lazy<HttpSender> _lazyHttpSender = new(() => new(
        NullLogger<HttpSender>.Instance, Substitute.For<ISenderHttpClient>()));

    private static readonly Lazy<DeliverSenderProvider> _lazyDeliverSenderProvider = new(() =>
    {
        var services = new ServiceCollection();
        var provider = services
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddKeyedSingleton<IDeliverSender>(FileSender.Key, _lazyFileSender.Value)
            .AddKeyedSingleton<IDeliverSender>(HttpSender.Key, _lazyHttpSender.Value)
            .BuildServiceProvider();
        return new(NullLogger<DeliverSenderProvider>.Instance, provider);
    });

    private static readonly Lazy<AS4MessageTransformer> _lazyAS4MessageTransformer = new(() => new(
        _lazySerializerProvider.Value));

    private static readonly Lazy<DeliverMessageTransformer> _lazyDeliverMessageTransformer = new(() => new(
        NullLogger<DeliverMessageTransformer>.Instance, _lazyAS4MessageTransformer.Value));

    private static readonly Lazy<LogExceptionHandler> _lazyLogExceptionHandler = new(() => new(
        NullLogger<LogExceptionHandler>.Instance));

    private static readonly Lazy<StepBuilder> _lazyStepBuilder = new(() =>
    {
        var services = new ServiceCollection();
        var provider = services
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddAS4Steps()
            .BuildServiceProvider();
        return new(NullLogger<StepBuilder>.Instance, provider);
    });

    private static readonly Lazy<SendingPModeMap> _lazySendingPModeMap = new(() => new(
        _lazyIdentifierFactory.Value));

    private static readonly Lazy<SubmitMessageMap> _lazySubmitMessageMap = new(() => new(
        _lazyIdentifierFactory.Value, _lazySendingPModeMap.Value));

    private static readonly Lazy<AS4ResponseFactory> _lazyAS4ResponseFactory = new(() => new(
        NullLogger<AS4ResponseFactory>.Instance, _lazySerializerProvider.Value));

    private static readonly Lazy<CompressStrategy> _lazyCompressStrategy = new(() => new(
        NullLogger<CompressStrategy>.Instance));

    private static readonly Lazy<PModeRuleEngine> _lazyPModeRuleEngine = new(() => new(
        NullLogger<PModeRuleEngine>.Instance));

    private static readonly Lazy<AS4MessageBodyFileStore> _lazyAS4MessageBodyFileStore = new(() => new(
       _lazySerializerProvider.Value));

    // Builders
    public static GenericTypeBuilder GenericTypeBuilder => _lazyGenericTypeBuilder.Value;

    // Exception
    public static LogExceptionHandler LogExceptionHandler => _lazyLogExceptionHandler.Value;

    // Factories
    public static IdentifierFactory IdentifierFactory => _lazyIdentifierFactory.Value;

    // Mappings
    public static SendingPModeMap SendingPModeMap => _lazySendingPModeMap.Value;

    public static SubmitMessageMap SubmitMessageMap => _lazySubmitMessageMap.Value;

    // Repositories
    public static AS4MessageBodyFileStore AS4MessageBodyFileStore => _lazyAS4MessageBodyFileStore.Value;

    public static CertificateRepository CertificateRepository => _lazyCertificateRepository.Value;

    public static InMemoryMessageBodyStore InMemoryMessageBodyStore => _lazyMessageBodyStore.Value;

    // Serialization
    public static MimeMessageSerializer MimeMessageSerializer => _lazyMimeMessageSerializer.Value;

    public static SoapEnvelopeSerializer SoapEnvelopeSerializer => _lazySoapEnvelopeSerializer.Value;

    public static SerializerProvider SerializerProvider => _lazySerializerProvider.Value;

    // Steps
    public static AS4ResponseFactory AS4ResponseFactory => _lazyAS4ResponseFactory.Value;

    public static StepBuilder StepBuilder => _lazyStepBuilder.Value;

    public static DeliverSenderProvider DeliverSenderProvider => _lazyDeliverSenderProvider.Value;

    public static CompressStrategy CompressStrategy => _lazyCompressStrategy.Value;

    public static PModeRuleEngine PModeRuleEngine => _lazyPModeRuleEngine.Value;

    // Transformers
    public static AS4MessageTransformer AS4MessageTransformer => _lazyAS4MessageTransformer.Value;

    public static DeliverMessageTransformer DeliverMessageTransformer => _lazyDeliverMessageTransformer.Value;

    // Validators
    public static ReceivingProcessingModeValidator ReceivingProcessingModeValidator => _lazyReceivingProcessingModeValidator.Value;

    public static SendingProcessingModeValidator SendingProcessingModeValidator => _lazySendingProcessingModeValidator.Value;

    public static SubmitMessageValidator SubmitMessageValidator => _lazySubmitMessageValidator.Value;

    public static DatastoreRepository NewDatastoreRepository(IDbContextFactory<DatastoreContext> dbContextFactory) => new(
        NullLogger<DatastoreRepository>.Instance, dbContextFactory);

    // Services
    public static ExceptionService NewExceptionService(IDbContextFactory<DatastoreContext> dbContextFactory) => new(
        StubConfig.Default,
        NewDatastoreRepository(dbContextFactory),
        _lazyMessageBodyStore.Value);

    public static OutboundExceptionHandler NewOutboundExceptionHandler(IDbContextFactory<DatastoreContext> dbContextFactory) => new(
        NullLogger<OutboundExceptionHandler>.Instance,
        NewExceptionService(dbContextFactory),
        _lazySerializerProvider.Value);

    public static InboundExceptionHandler NewInboundExceptionHandler(IDbContextFactory<DatastoreContext> dbContextFactory) => new(
        NullLogger<InboundExceptionHandler>.Instance,
        NewExceptionService(dbContextFactory));

    public static InMessageService NewInMessageService(IDbContextFactory<DatastoreContext> dbContextFactory, IAS4MessageBodyStore messageBodyStore) => new(
        NullLogger<InMessageService>.Instance,
        StubConfig.Default,
        NewDatastoreRepository(dbContextFactory),
        NewExceptionService(dbContextFactory),
        IdentifierFactory,
        messageBodyStore);

    public static OutMessageService NewOutMessageService(IDbContextFactory<DatastoreContext> dbContextFactory, IAS4MessageBodyStore messageBodyStore) => new(
        NullLogger<OutMessageService>.Instance,
        StubConfig.Default,
        NewDatastoreRepository(dbContextFactory),
        messageBodyStore,
        SerializerProvider);

    public static PiggyBackingService NewPiggyBackingService(IDbContextFactory<DatastoreContext> dbContextFactory, IAS4MessageBodyStore messageBodyStore) => new(
        NullLogger<PiggyBackingService>.Instance,
        NewDatastoreRepository(dbContextFactory),
        NewMarkForRetryService(dbContextFactory),
        messageBodyStore,
        dbContextFactory,
        SerializerProvider);

    public static MarkForRetryService NewMarkForRetryService(IDbContextFactory<DatastoreContext> dbContextFactory) => new(
        NullLogger<MarkForRetryService>.Instance,
        NewDatastoreRepository(dbContextFactory));
}
