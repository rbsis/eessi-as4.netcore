using Eu.EDelivery.AS4;
using Eu.EDelivery.AS4.Common;
using Eu.EDelivery.AS4.Compression;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Exceptions.Handlers;
using Eu.EDelivery.AS4.Factories;
using Eu.EDelivery.AS4.Http;
using Eu.EDelivery.AS4.Http.Response;
using Eu.EDelivery.AS4.Mappings.PMode;
using Eu.EDelivery.AS4.Mappings.Submit;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Model.Submit;
using Eu.EDelivery.AS4.Receivers;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.Services;
using Eu.EDelivery.AS4.Services.DynamicDiscovery;
using Eu.EDelivery.AS4.Services.Journal;
using Eu.EDelivery.AS4.Services.PullRequestAuthorization;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Strategies.Retriever;
using Eu.EDelivery.AS4.Strategies.Sender;
using Eu.EDelivery.AS4.Strategies.Uploader;
using Eu.EDelivery.AS4.Transformers;
using Eu.EDelivery.AS4.Validators;
using Eu.EDelivery.AS4.Watchers;
using FluentValidation;
using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
// Naming convention according to https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-5.0
namespace Microsoft.Extensions.DependencyInjection;

public static class AS4ServiceCollectionExtensions
{
    public static IServiceCollection AddAS4(this IServiceCollection serviceCollection) => serviceCollection
        .AddAS4Compression()
        .AddAS4ExceptionHandlers()
        .AddAS4Factories()
        .AddAS4Http()
        .AddAS4Mappings()
        .AddAS4ReceiverBuilder()
        .AddAS4Repositories()
        .AddAS4RetrieverStrategies()
        .AddAS4SenderStrategies()
        .AddAS4Serialization()
        .AddAS4Services()
        .AddAS4StepBuilder()
        .AddAS4TransformerBuilder()
        .AddAS4UploaderStrategies()
        .AddAS4Validators()
        .AddAS4Watchers();

    public static IServiceCollection AddAS4Config(this IServiceCollection serviceCollection, string settingsFileName) => serviceCollection
        .AddSingleton<IConfig, Config>(sp =>
        {
            var config = new Config(
                sp.GetRequiredService<ILogger<Config>>(),
                sp.GetRequiredService<IPModeWatcher<ReceivingProcessingMode>>(),
                sp.GetRequiredService<IPModeWatcher<SendingProcessingMode>>());
            config.Initialize(settingsFileName);

            return config;
        });

    private static IServiceCollection AddAS4Http(this IServiceCollection serviceCollection) => serviceCollection
        .AddHttpClient()
        .AddSingleton<IAS4ResponseFactory, AS4ResponseFactory>()
        .AddSingleton<IReliableHttpClient, ReliableHttpClient>();

    private static IServiceCollection AddAS4Compression(this IServiceCollection serviceCollection) => serviceCollection
        .AddSingleton<ICompressStrategy, CompressStrategy>();

    private static IServiceCollection AddAS4ExceptionHandlers(this IServiceCollection serviceCollection) => serviceCollection
        .AddSingleton<InboundExceptionHandler>()
        .AddSingleton<NotifyExceptionHandler>()
        .AddSingleton<OutboundExceptionHandler>()
        .AddSingleton<PullSendAgentExceptionHandler>()
        .AddSingleton<MinderExceptionHandler>()
        .AddSingleton<IExceptionHandlerRegistry, ExceptionHandlerRegistry>();

    private static IServiceCollection AddAS4Factories(this IServiceCollection serviceCollection) => serviceCollection
        .AddSingleton<IIdentifierFactory, IdentifierFactory>();

    private static IServiceCollection AddAS4Mappings(this IServiceCollection serviceCollection) => serviceCollection
        .AddSingleton<ISendingPModeMap, SendingPModeMap>()
        .AddSingleton<ISubmitMessageMap, SubmitMessageMap>();

    private static IServiceCollection AddAS4ReceiverBuilder(this IServiceCollection serviceCollection) => serviceCollection
        .AddSingleton<IReceiverBuilder, ReceiverBuilder>();

    private static IServiceCollection AddAS4Repositories(this IServiceCollection serviceCollection) => serviceCollection
        .AddDbContextFactory<DatastoreContext>()
        .AddHostedService<DatastoreContextMigrator>()
        .AddSingleton<ICertificateRepository, CertificateRepository>()
        .AddSingleton<IDatastoreRepository, DatastoreRepository>()
        .AddSingleton<IAS4MessageBodyStore, AS4MessageStoreProvider>(sp =>
        {
            var messageStoreProvider = new AS4MessageStoreProvider();
            messageStoreProvider.Accept(
                condition: l => l.StartsWith("file:///", StringComparison.OrdinalIgnoreCase),
                persister: new AS4MessageBodyFileStore(sp.GetRequiredService<ISerializerProvider>()));
            return messageStoreProvider;
        });

    private static IServiceCollection AddAS4RetrieverStrategies(this IServiceCollection serviceCollection) => serviceCollection
        .AddSingleton<IRetrieverHttpClient, RetrieverHttpClient>()
        .AddKeyedSingleton<IPayloadRetriever, FilePayloadRetriever>(FilePayloadRetriever.Key)
        .AddKeyedSingleton<IPayloadRetriever, HttpPayloadRetriever>(HttpPayloadRetriever.Key)
        .AddKeyedSingleton<IPayloadRetriever, TempFilePayloadRetriever>(TempFilePayloadRetriever.Key)
        .AddSingleton<IPayloadRetrieverProvider, PayloadRetrieverProvider>();

    private static IServiceCollection AddAS4SenderStrategies(this IServiceCollection serviceCollection) => serviceCollection
        .AddSingleton<ISenderHttpClient, SenderHttpClient>()
        .AddKeyedTransient<IDeliverSender, FileSender>(FileSender.Key)
        .AddKeyedTransient<IDeliverSender, HttpSender>(HttpSender.Key)
        .AddSingleton<IDeliverSenderProvider, DeliverSenderProvider>()
        .AddKeyedTransient<INotifySender, FileSender>(FileSender.Key)
        .AddKeyedTransient<INotifySender, HttpSender>(HttpSender.Key)
        .AddSingleton<INotifySenderProvider, NotifySenderProvider>();

    private static IServiceCollection AddAS4Serialization(this IServiceCollection serviceCollection) => serviceCollection
        .AddKeyedSingleton<ISerializer, SoapEnvelopeSerializer>(Constants.ContentTypes.Soap)
        .AddKeyedSingleton<ISerializer, MimeMessageSerializer>(Constants.ContentTypes.Mime)
        .AddSingleton<ISerializerProvider, SerializerProvider>();

    private static IServiceCollection AddAS4Services(this IServiceCollection serviceCollection) => serviceCollection
        .AddSingleton<IDynamicDiscoveryProfileResolver, DynamicDiscoveryProfileResolver>()
        .AddSingleton<LocalDynamicDiscoveryProfile>()
        .AddSingleton<OasisDynamicDiscoveryProfile>()
        .AddSingleton<PeppolDynamicDiscoveryProfile>()
        .AddKeyedSingleton<IJournalLogger, JournalDatastoreLogger>(typeof(JournalDatastoreLogger))
        .AddSingleton<IPullAuthorizationMapProvider>(new FilePullAuthorizationMapProvider(""))
        .AddSingleton<IPullAuthorizationMapService, PullAuthorizationMapService>()
        .AddSingleton<IExceptionService, ExceptionService>()
        .AddSingleton<IInMessageService, InMessageService>()
        .AddSingleton<IMarkForRetryService, MarkForRetryService>()
        .AddSingleton<IOutMessageService, OutMessageService>()
        .AddSingleton<IPiggyBackingService, PiggyBackingService>();

    private static IServiceCollection AddAS4StepBuilder(this IServiceCollection serviceCollection) => serviceCollection
        .AddSingleton<IStepBuilder, StepBuilder>();

    private static IServiceCollection AddAS4TransformerBuilder(this IServiceCollection serviceCollection) => serviceCollection
       .AddSingleton<ITransformerBuilder, TransformerBuilder>();

    private static IServiceCollection AddAS4UploaderStrategies(this IServiceCollection serviceCollection) => serviceCollection
        .AddSingleton<IUploaderHttpClient, UploaderHttpClient>()
        .AddKeyedSingleton<IAttachmentUploader, FileAttachmentUploader>(FileAttachmentUploader.Key)
        .AddKeyedSingleton<IAttachmentUploader, EmailAttachmentUploader>(EmailAttachmentUploader.Key)
        .AddKeyedSingleton<IAttachmentUploader, PayloadServiceAttachmentUploader>(PayloadServiceAttachmentUploader.Key)
        .AddSingleton<IAttachmentUploaderProvider, AttachmentUploaderProvider>();

    private static IServiceCollection AddAS4Validators(this IServiceCollection serviceCollection) => serviceCollection
        .AddSingleton<IValidator<Parameter>, ParameterValidator>()
        .AddSingleton<IValidator<ReceivingProcessingMode>, ReceivingProcessingModeValidator>()
        .AddSingleton<IValidator<SendingProcessingMode>, SendingProcessingModeValidator>()
        .AddSingleton<IValidator<SubmitMessage>, SubmitMessageValidator>();

    private static IServiceCollection AddAS4Watchers(this IServiceCollection serviceCollection) => serviceCollection
        .AddSingleton<IPModeWatcher<ReceivingProcessingMode>, PModeWatcher<ReceivingProcessingMode>>()
        .AddSingleton<IPModeWatcher<SendingProcessingMode>, PModeWatcher<SendingProcessingMode>>();

}
