using System.Xml;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Extensions;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Receivers;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.TestUtils.Stubs;
using Eu.EDelivery.AS4.UnitTests.Common;
using Eu.EDelivery.AS4.UnitTests.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Eu.EDelivery.AS4.UnitTests.Receivers;

/// <summary>
/// Testing <see cref="DatastoreReceiver" />
/// </summary>
public class GivenDatastoreReceiverFacts : GivenDatastoreFacts
{
    private static readonly Lazy<AS4MessageStoreProvider> _lazyMessageStoreProviderSerializerProvider =
        new(() =>
        {
            var messageStoreProvider = new AS4MessageStoreProvider();
            messageStoreProvider.Accept(
                condition: l => l.StartsWith("file:///", StringComparison.OrdinalIgnoreCase),
                persister: new AS4MessageBodyFileStore(Default.SerializerProvider));
            messageStoreProvider.Accept(s => true, new StubMessageBodyRetriever(() => Stream.Null));
            return messageStoreProvider;
        });

    /// <summary>
    /// Initializes a new instance of the <see cref="GivenDatastoreReceiverFacts"/> class.
    /// </summary>
    public GivenDatastoreReceiverFacts()
    {
    }

    [Fact]
    public void CatchesInvalidDatastoreCreation()
    {
        // Arrange
        var settings = new DatastoreReceiverSettings
        {
            TableName = "OutMessages",
            Filter = "Operation = ToBeDelivered",
            UpdateField = "Operation",
            UpdateValue = "Sending"
        };

        var receiver = new DatastoreReceiver(
            NullLogger<DatastoreReceiver>.Instance,
            new SaboteurDatastoreContextFactory(),
            _lazyMessageStoreProviderSerializerProvider.Value,
            Options.Create(settings));

        // Act / Assert
        StartReceiverAsTask(receiver, isCalled: false);
    }

    [Fact(Skip = "Multiple update columns are currently not supported")]
    public void ReceiverUpdatesMultipleValuesIfMultipleUpdateSettingsAreSpecified()
    {
        // Arrange
        InsertOutMessageInDatastoreWith(Operation.ToBeDelivered, OutStatus.NotApplicable);

        var receiver =
            DataStoreReceiverWith(
                SettingsToPollOnOutMessages(
                    filter: "Operation = ToBeDelivered",
                    updates: ["Operation", "Status"],
                    values: ["Sending", "Sent"]));

        // Act
        StartReceiver(receiver);

        // Assert
        AssertOutMessageIf(
            m => m.Operation == Operation.Sending,
            message =>
            {
                Assert.Equal(OutStatus.Sent, message?.Status.ToEnum<OutStatus>());
                Assert.Equal(Operation.Sending, message?.Operation);
            });
    }

    [Fact]
    public void ReceivesOutMessage()
    {
        // Arrange
        var expectedStream = Stream.Null;
        const string ExpectedType = Constants.ContentTypes.Soap;

        ArrangeOutMessageInDatastore(Operation.ToBeDelivered, expectedStream, ExpectedType);

        var settings = new DatastoreReceiverSettings
        {
            TableName = "OutMessages",
            Filter = "Operation = \'ToBeDelivered\'",
            UpdateField = "Operation",
            UpdateValue = "Sending"
        };


        var receiver = new DatastoreReceiver(
            NullLogger<DatastoreReceiver>.Instance,
            this,
            _lazyMessageStoreProviderSerializerProvider.Value,
            Options.Create(settings));

        // Act
        var actualMessage = StartReceiver(receiver);

        // Assert
        Assert.Equal(expectedStream, actualMessage.UnderlyingStream);
        Assert.Equal(ExpectedType, actualMessage.ContentType);
    }

    private void ArrangeOutMessageInDatastore(Operation operation, Stream stream, string contentType)
    {
        var stubRetriever = new StubMessageBodyRetriever(() => stream);
        _lazyMessageStoreProviderSerializerProvider.Value.Accept(s => s.Contains("test://"), stubRetriever);

        using var context = GetDataStoreContext();
        var outMessage = new OutMessage("message-id")
        {
            MessageLocation = "test://",
            ContentType = contentType,
            Operation = operation
        };

        context.OutMessages.Add(outMessage);

        context.SaveChanges();
    }

    private void InsertOutMessageInDatastoreWith(Operation operation, OutStatus status)
    {
        using var context = GetDataStoreContext();
        var expectedMessage = new OutMessage("message-id")
        {
            MessageLocation = "ignored location"
        };

        expectedMessage.SetStatus(status);
        expectedMessage.Operation = operation;

        context.OutMessages.Add(expectedMessage);
        context.SaveChanges();
    }

    private IReceiver DataStoreReceiverWith(IEnumerable<Setting> settings)
    {
        var dummySettings = new DatastoreReceiverSettings
        {
            TableName = "",
            Filter = "",
            UpdateField = "",
            UpdateValue = ""
        };

        IReceiver receiver = new DatastoreReceiver(
            NullLogger<DatastoreReceiver>.Instance,
            this,
            _lazyMessageStoreProviderSerializerProvider.Value,
            Options.Create(dummySettings));

        receiver.Configure(settings);

        return receiver;
    }

    private static IEnumerable<Setting> SettingsToPollOnOutMessages(
        string filter,
        IReadOnlyList<string> updates,
        IReadOnlyList<string> values)
    {
        var settings = new List<Setting>
        {
            new("Table", "OutMessages"),
            new("Filter", filter),
        };

        for (var index = 0; index < updates.Count; index++)
        {
            var update = updates[index];
            var attributes = new XmlAttribute[] { new StubXmlAttribute("field", update) };
            var value = values[index];

            settings.Add(new Setting("Update", value) { Attributes = attributes });
        }

        return settings;
    }

    private static void StartReceiverAsTask(IReceiver receiver, bool isCalled)
    {
        Task.Run(() => StartReceiver(receiver, isCalled));
    }

    private static ReceivedMessage StartReceiver(IReceiver receiver, bool isCalled = true)
    {
        using var tokenSource = new CancellationTokenSource();
        var waitHandle = new ManualResetEvent(false);
        var receivedMessage = new ReceivedMessage(Stream.Null);

        receiver.StartReceiving(
            (message, token) =>
            {
                waitHandle.Set();
                tokenSource.Cancel();

                receivedMessage = message;

                return Task.FromResult((MessagingContext)new EmptyMessagingContext());
            },
            tokenSource.Token);

        Assert.Equal(isCalled, waitHandle.WaitOne(TimeSpan.FromSeconds(5)));

        tokenSource.Cancel();
        receiver.StopReceiving();

        return receivedMessage;
    }

    private void AssertOutMessageIf(Func<OutMessage, bool> where, Action<OutMessage?> assertion)
    {
        using var context = GetDataStoreContext();
        var actualMessag = context.OutMessages.FirstOrDefault(where);
        assertion(actualMessag);
    }
}
