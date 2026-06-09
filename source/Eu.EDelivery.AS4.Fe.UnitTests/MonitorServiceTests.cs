using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Fe.Mappers;
using Eu.EDelivery.AS4.Fe.Monitor.Model;
using Eu.EDelivery.AS4.Fe.Pmodes;
using Eu.EDelivery.AS4.Fe.Services;
using Eu.EDelivery.AS4.Fe.Settings;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.TestUtils.Stubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Eu.EDelivery.AS4.Fe.UnitTests;

public class MonitorServiceTests : BaseTest, IDbContextFactory<DatastoreContext>
{
    private const string InEbmsMessageId1 = "ebmsMessageId1";
    private const string InEbmsMessageId2 = "InEbmsMessageId2";
    private const string InEbmsRefToMessageId1 = "ebmsRefToMessageId1";
    private const string InEbmsRefToMessageId2 = "InEbmsRefToMessageId2";
    private const string OutEbmsMessageId1 = "OutEbmsMessageId1";
    private const string OutEbmsMessageId2 = "OutEbmsMessageId2";
    private const string OutEbmsRefToMessageId1 = "OutEbmsRefToMessageId1";
    private const string OutEbmsRefToMessageId2 = "OutEbmsRefToMessageId2";
    private const string InException = "THIS IS EXCEPTION 1";
    private const string MessageLocation = "some-location";
    private const string MessageBody1 = "TEST";
    private const string Exception = @"[9acd3265 - cd3a - 4903 - 9ec4 - 694fc4433c34@mindertestbed.org]Decryption failed
   at Eu.EDelivery.AS4.Steps.Receive.DecryptAS4MessageStep.TryDecryptAS4Message() in AS4.NET\source\Steps\Eu.EDelivery.AS4.Steps\Receive\DecryptAS4MessageStep.cs:line 109
   at Eu.EDelivery.AS4.Steps.Receive.DecryptAS4MessageStep.ExecuteAsync(InternalMessage internalMessage, CancellationToken cancellationToken) in AS4.NET\source\Steps\Eu.EDelivery.AS4.Steps\Receive\DecryptAS4MessageStep.cs:line 66
   at Eu.EDelivery.AS4.Steps.CompositeStep.<ExecuteAsync>d__2.MoveNext() in AS4.NET\source\AS4\Eu.EDelivery.AS4\Steps\CompositeStep.cs:line 43
--- End of stack trace from previous location where exception was thrown ---
   at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task task)
   at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task task)
   at System.Runtime.CompilerServices.TaskAwaiter`1.GetResult()
   at Eu.EDelivery.AS4.Steps.Receive.ReceiveExceptionStepDecorator.<ExecuteAsync>d__4.MoveNext() in AS4.NET\source\Steps\Eu.EDelivery.AS4.Steps\Receive\ReceiveExceptionStepDecorator.cs:line 54
Failed to decrypt data element
   at Eu.EDelivery.AS4.Security.Strategies.EncryptionStrategy.TryDecryptEncryptedData(EncryptedData encryptedData) in AS4.NET\source\AS4\Eu.EDelivery.AS4\Security\Strategies\EncryptionStrategy.cs:line 288
   at Eu.EDelivery.AS4.Security.Strategies.EncryptionStrategy.DecryptMessage() in AS4.NET\source\AS4\Eu.EDelivery.AS4\Security\Strategies\EncryptionStrategy.cs:line 271
   at Eu.EDelivery.AS4.Model.Core.SecurityHeader.Decrypt(IEncryptionStrategy encryptionStrategy) in AS4.NET\source\AS4\Eu.EDelivery.AS4\Model\Core\SecurityHeader.cs:line 124
   at Eu.EDelivery.AS4.Steps.Receive.DecryptAS4MessageStep.TryDecryptAS4Message() in AS4.NET\source\Steps\Eu.EDelivery.AS4.Steps\Receive\DecryptAS4MessageStep.cs:line 104
";

    private readonly ReceivingProcessingMode _pmode;
    private readonly MonitorService _sut;
    private readonly DbContextOptions<DatastoreContext> _options;

    protected MonitorServiceTests()
    {
        _pmode = new ReceivingProcessingMode() { Id = "monitorServiceTestPModeId" };

        _options = new DbContextOptionsBuilder<DatastoreContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        SetupDataStore();

        var datastoreRepository = Substitute.For<IDatastoreRepository>();
        var bodyStore = new StubMessageBodyRetriever(() => Stream.Null);
        var mapper = new MessageMapper();

        _sut = new MonitorService(
            this,
            SetupPmodeSource(),
            datastoreRepository,
            bodyStore,
            mapper,
            mapper,
            mapper,
            mapper);
    }

    public DatastoreContext CreateDbContext()
    {
        return new DatastoreContext(NullLogger<DatastoreContext>.Instance, StubConfig.Default, _options);
    }

    private static As4PmodeSource SetupPmodeSource()
    {
        var sourceOptions = Substitute.For<IOptionsSnapshot<PmodeSettings>>();
        return new As4PmodeSource(StubConfig.Default, sourceOptions);
    }

    protected virtual void SetupDataStore()
    {
        using var context = CreateDbContext();
        context.Database.EnsureCreated();

        var pmodeString = AS4XmlSerializer.ToString(_pmode);
        var pmodeId = _pmode.Id;

        {
            var message = new InMessage(ebmsMessageId: InEbmsMessageId1)
            {
                EbmsRefToMessageId = InEbmsRefToMessageId1,
                InsertionTime = DateTime.UtcNow.AddMinutes(-1),
            };
            message.SetStatus(InStatus.Created);
            message.SetPModeInformation(pmodeId, pmodeString);
            context.InMessages.Add(message);
        }

        {
            var message = new InMessage(ebmsMessageId: InEbmsMessageId2)
            {
                EbmsRefToMessageId = InEbmsRefToMessageId2,
                InsertionTime = DateTime.UtcNow.AddMinutes(-1)
            };
            message.SetStatus(InStatus.Received);
            context.InMessages.Add(message);
        }

        {
            var message = new OutMessage(OutEbmsMessageId1)
            {
                EbmsRefToMessageId = OutEbmsRefToMessageId1,
                InsertionTime = DateTime.UtcNow.AddMinutes(-1)
            };
            message.SetStatus(OutStatus.Created);

            context.OutMessages.Add(message);
        }

        {
            var message = new OutMessage(OutEbmsMessageId2)
            {
                EbmsRefToMessageId = OutEbmsRefToMessageId2,

                InsertionTime = DateTime.UtcNow.AddMinutes(-1)
            };
            message.SetStatus(OutStatus.Created);
            context.OutMessages.Add(message);
        }

        var inEx1 = Entities.InException.ForEbmsMessageId(InEbmsMessageId1, InException);
        inEx1.InsertionTime = DateTime.UtcNow.AddMinutes(-1);
        context.InExceptions.Add(inEx1);

        var inEx2 = Entities.InException.ForEbmsMessageId(InEbmsMessageId1, InException);
        inEx2.InsertionTime = DateTime.UtcNow.AddMinutes(-1);
        context.InExceptions.Add(inEx2);

        var inEx3 = Entities.InException.ForMessageBody(MessageLocation, InException);
        inEx3.InsertionTime = DateTime.UtcNow.AddMinutes(-1);
        context.InExceptions.Add(inEx3);

        var outEx1 = Entities.OutException.ForEbmsMessageId(OutEbmsRefToMessageId1, InException);
        outEx1.InsertionTime = DateTime.UtcNow.AddMinutes(-1);
        context.OutExceptions.Add(outEx1);

        var outEx2 = OutException.ForEbmsMessageId(InEbmsRefToMessageId1, Exception);
        outEx2.InsertionTime = DateTime.UtcNow.AddMinutes(-1);
        context.OutExceptions.Add(outEx2);

        var outEx3 = OutException.ForMessageBody(MessageLocation, Exception);
        outEx3.InsertionTime = DateTime.UtcNow.AddMinutes(-1);
        context.OutExceptions.Add(outEx3);

        context.SaveChanges();

        foreach (var inMessage in context.InMessages)
        {
            inMessage.SetPModeInformation(pmodeId, pmodeString);
        }

        foreach (var outMessage in context.OutMessages)
        {
            outMessage.SetPModeInformation(pmodeId, pmodeString);
        }

        foreach (var inException in context.InExceptions)
        {
            inException.SetPModeInformation(pmodeId, pmodeString);
        }

        foreach (var outException in context.OutExceptions)
        {
            outException.SetPModeInformation(pmodeId, pmodeString);
        }

        context.SaveChanges();
    }

    public class GetMessages : MonitorServiceTests
    {
        [Fact]
        public async Task Throws_Exception_When_No_Direction_Is_Specified()
        {
            await ExpectExceptionAsync(() => _sut.GetMessagesAsync(new MessageFilter() { Direction = [] }, TestContext.Current.CancellationToken), typeof(ArgumentException));
        }

        [Fact]
        public async Task Throws_Exception_When_Parameter_Is_Null()
        {
            await ExpectExceptionAsync(() => _sut.GetMessagesAsync(null, TestContext.Current.CancellationToken), typeof(ArgumentNullException));
        }

        [Fact]
        public async Task Gets_All_In_And_Outbound_Messages()
        {
            var filter = new MessageFilter()
            {
                Direction = [Direction.Inbound, Direction.Outbound]
            };

            var result = await _sut.GetMessagesAsync(filter, TestContext.Current.CancellationToken);

            Assert.Equal(4, result.Messages.Count());
            Assert.Equal(2, result.Messages.Count(x => x.Direction == Direction.Inbound));
            Assert.Equal(2, result.Messages.Count(x => x.Direction == Direction.Outbound));
        }

        [Fact]
        public async Task Get_Only_Inboud_Messages()
        {
            var filter = new MessageFilter
            {
                Direction = [Direction.Inbound]
            };
            var result = await _sut.GetMessagesAsync(filter, TestContext.Current.CancellationToken);

            Assert.Equal(2, result.Messages.Count());
            Assert.True(result.Messages.All(message => message.Direction == Direction.Inbound));
        }

        [Fact]
        public async Task HasExceptions_Of_Message_Should_Be_True_When_Exceptions_Are_Available()
        {
            var result = await _sut.GetMessagesAsync(new MessageFilter(), TestContext.Current.CancellationToken);

            Assert.True(result.Messages.First(msg => msg.EbmsMessageId == InEbmsMessageId1).HasExceptions);
            Assert.False(result.Messages.First(msg => msg.EbmsRefToMessageId == InEbmsRefToMessageId2).HasExceptions);
        }

        [Fact]
        public async Task No_Filter_Should_Return_All_Messages()
        {
            var filter = new MessageFilter();
            var result = await _sut.GetMessagesAsync(filter, TestContext.Current.CancellationToken);

            Assert.Equal(1, result.Page);
            Assert.Equal(4, result.Total);
        }

        [Fact]
        public async Task Pmode_Should_Only_Contain_Pmode_Number()
        {
            var result = await _sut.GetMessagesAsync(new MessageFilter(), TestContext.Current.CancellationToken);
            var message = result.Messages.FirstOrDefault(x => x.EbmsRefToMessageId == InEbmsRefToMessageId1);

            Assert.NotNull(message);
            Assert.Equal(_pmode.Id, message.PModeId);
        }

        [Fact]
        public async Task Should_Filter_Data_When_Existing_MessageId_Is_Supplied()
        {
            var filter = new MessageFilter
            {
                EbmsRefToMessageId = InEbmsRefToMessageId1
            };

            var result = await _sut.GetMessagesAsync(filter, TestContext.Current.CancellationToken);

            Assert.Equal(1, result.Page);
            Assert.Equal(1, result.Total);
        }

        [Fact]
        public async Task Results_Should_Have_The_Inboud_Direction()
        {
            var result = await _sut.GetMessagesAsync(new MessageFilter()
            {
                Direction = [Direction.Inbound]
            }, TestContext.Current.CancellationToken);

            Assert.True(result.Messages.All(message => message.Direction == Direction.Inbound));
        }

        [Fact]
        public async Task Results_Should_Have_The_Outbound_Direction()
        {
            var result = await _sut.GetMessagesAsync(new MessageFilter()
            {
                Direction = [Direction.Outbound]
            }, TestContext.Current.CancellationToken);

            Assert.True(result.Messages.All(message => message.Direction == Direction.Outbound));
        }

        [Fact]
        public async Task Status_Should_Be_Mapped()
        {
            var result = await _sut.GetMessagesAsync(new MessageFilter(), TestContext.Current.CancellationToken);
            Assert.True(result.Messages.All(msg => !string.IsNullOrEmpty(msg.Status)));
        }

        public class GetPmodeNumber : MonitorServiceTests
        {
            [Fact]
            public void Returns_Pmode_Number_From_Pmode_String()
            {
                var pmodeContent = File.ReadAllText(@"receivingpmode.xml");
                var result = _sut.GetPmodeNumber(pmodeContent);
                Assert.Equal("8.1.2-basePmode", result);
            }
        }

        public class GetExceptions : MonitorServiceTests
        {
            [Fact]
            public async Task Throws_Exception_When_Parameters_Is_Null()
            {
                await ExpectExceptionAsync(() => _sut.GetExceptionsAsync(null, TestContext.Current.CancellationToken), typeof(ArgumentNullException));
            }

            [Fact]
            public async Task Filter_Should_Filter_The_Data()
            {
                var filter = new ExceptionFilter()
                {
                    EbmsRefToMessageId = InEbmsMessageId1,
                    Direction = [Direction.Inbound],
                };
                var result = await _sut.GetExceptionsAsync(filter, TestContext.Current.CancellationToken);

                Assert.Equal(2, result.Messages.Count());
                Assert.Equal(InEbmsMessageId1, result.Messages.First().EbmsRefToMessageId);
            }

            [Fact]
            public async Task Filter_Should_Return_Nothing_When_No_Match()
            {
                var filter = new ExceptionFilter
                {
                    EbmsRefToMessageId = "IDONTEXIST"
                };
                var result = await _sut.GetExceptionsAsync(filter, TestContext.Current.CancellationToken);

                Assert.False(result.Messages.Any());
            }

            [Fact]
            public async Task Return_All_Directions()
            {
                var result = await _sut.GetExceptionsAsync(new ExceptionFilter(), TestContext.Current.CancellationToken);

                Assert.Equal(6, result.Messages.Count());
            }

            [Fact]
            public async Task Throws_Exception_When_No_Direction()
            {
                var result = await ExpectExceptionAsync(() => _sut.GetExceptionsAsync(new ExceptionFilter() { Direction = [] }, TestContext.Current.CancellationToken),
                    typeof(ArgumentException));
            }

            [Fact]
            public async Task Exception_Short_Should_Not_Contain_The_Full_Exception()
            {
                var result = await _sut.GetExceptionsAsync(new ExceptionFilter
                {
                    EbmsRefToMessageId = InEbmsRefToMessageId1
                }, TestContext.Current.CancellationToken);

                Assert.Equal("Decryption failed", result.Messages.First().ExceptionShort);
            }
        }

        public class Hash : MonitorServiceTests
        {
            [Fact]
            public async Task Message_Should_Contain_Md5_Hash()
            {
                var inMessageResult = await _sut.GetMessagesAsync(new MessageFilter { Direction = [Direction.Inbound] }, TestContext.Current.CancellationToken);
                var outMessageResult = await _sut.GetMessagesAsync(new MessageFilter { Direction = [Direction.Outbound] }, TestContext.Current.CancellationToken);

                Assert.True(inMessageResult.Messages.All(msg => !string.IsNullOrEmpty(msg.Hash)));
                Assert.True(outMessageResult.Messages.All(msg => !string.IsNullOrEmpty(msg.Hash)));
            }
        }

        public class GetRelatedMessages : MonitorServiceTests
        {
            private readonly string _outEbmsMessage3 = Guid.NewGuid().ToString();
            private const string ForwardedMessageId = "ForwardedMessage1";

            protected override void SetupDataStore()
            {
                using var context = CreateDbContext();
                context.Database.EnsureCreated();

                context.InMessages.Add(new InMessage(ebmsMessageId: InEbmsMessageId1)
                {
                    EbmsRefToMessageId = InEbmsRefToMessageId1,
                });
                context.InMessages.Add(new InMessage(ebmsMessageId: InEbmsRefToMessageId1));

                context.OutMessages.Add(new OutMessage(ebmsMessageId: InEbmsRefToMessageId1));

                context.InMessages.Add(new InMessage(ebmsMessageId: "RANDOM")
                {
                    EbmsRefToMessageId = InEbmsMessageId1,
                });
                context.InMessages.Add(new InMessage(ebmsMessageId: InEbmsMessageId2));

                context.OutMessages.Add(new OutMessage(ebmsMessageId: OutEbmsMessageId1)
                {
                    EbmsRefToMessageId = OutEbmsRefToMessageId1
                });
                context.OutMessages.Add(new OutMessage(ebmsMessageId: OutEbmsMessageId2)
                {
                    EbmsRefToMessageId = OutEbmsMessageId1
                });
                context.InMessages.Add(new InMessage(ebmsMessageId: Guid.NewGuid().ToString())
                {
                    EbmsRefToMessageId = OutEbmsMessageId1
                });
                context.InMessages.Add(new InMessage(ebmsMessageId: OutEbmsRefToMessageId1)
                {
                    EbmsRefToMessageId = Guid.NewGuid().ToString()
                });

                context.OutMessages.Add(new OutMessage(ebmsMessageId: _outEbmsMessage3));

                context.OutMessages.Add(new OutMessage(ebmsMessageId: Guid.NewGuid().ToString())
                {
                    EbmsRefToMessageId = _outEbmsMessage3
                });
                context.InMessages.Add(new InMessage(ebmsMessageId: Guid.NewGuid().ToString())
                {
                    EbmsRefToMessageId = _outEbmsMessage3
                });

                context.InMessages.Add(new InMessage(ebmsMessageId: Guid.NewGuid().ToString()));

                context.OutMessages.Add(new OutMessage(Guid.NewGuid().ToString()));

                // Forwareded message
                var newinMessage = new InMessage(ForwardedMessageId)
                {
                    Operation = Operation.Forwarded
                };
                context.InMessages.Add(newinMessage);
                var newOutMessage = new OutMessage(ForwardedMessageId)
                {
                    Operation = Operation.ToBeSent
                };
                context.OutMessages.Add(newOutMessage);

                var pmodeId = _pmode.Id;
                var pmodeString = AS4XmlSerializer.ToString(_pmode);

                foreach (var inMessage in context.InMessages)
                {
                    inMessage.SetPModeInformation(pmodeId, pmodeString);
                }
                foreach (var outMessage in context.OutMessages)
                {
                    outMessage.SetPModeInformation(pmodeId, pmodeString);
                }

                context.SaveChanges();
            }

            [Fact]
            public async Task Returns_All_Related_Messages()
            {
                var result = await _sut.GetRelatedMessagesAsync(Direction.Inbound, InEbmsMessageId1, TestContext.Current.CancellationToken);
                Assert.Equal(3, result.Messages.Count());
            }

            [Fact]
            public async Task OutMessages_Should_Return_All_Related_Messages()
            {
                var result = await _sut.GetRelatedMessagesAsync(Direction.Outbound, OutEbmsMessageId1, TestContext.Current.CancellationToken);
                Assert.Equal(3, result.Messages.Count());
            }

            [Fact]
            public async Task OutMessages_Without_RefTo_Message_Returns_Related_Messages()
            {
                var result = await _sut.GetRelatedMessagesAsync(Direction.Outbound, _outEbmsMessage3, TestContext.Current.CancellationToken);
                Assert.Equal(2, result.Messages.Count());
            }

            [Fact]
            public async Task Throws_Exception_When_Parames_Are_Null()
            {
                await Assert.ThrowsAsync<ArgumentException>(() => _sut.GetRelatedMessagesAsync(Direction.Outbound, "", TestContext.Current.CancellationToken));
            }

            [Fact]
            public async Task ForwardedMessage_ShouldBeReturned()
            {
                var result = await _sut.GetRelatedMessagesAsync(Direction.Inbound, ForwardedMessageId, TestContext.Current.CancellationToken);
                Assert.NotNull(result);
                Assert.Single(result.Messages);
            }
        }

        public class DownloadMessageBody : MonitorServiceTests
        {
            [Fact]
            public async Task Throws_Exception_When_Parameters_Are_Invalid()
            {
                await ExpectExceptionAsync(() => _sut.DownloadMessageBodyAsync(Direction.Inbound, 0, TestContext.Current.CancellationToken), typeof(ArgumentOutOfRangeException));
            }
        }

        public class DownloadExceptionBody : MonitorServiceTests
        {
            [Fact]
            public async Task Throws_Exception_When_Parameters_Are_Invalid()
            {
                await ExpectExceptionAsync(() => _sut.DownloadExceptionMessageBodyAsync(Direction.Inbound, 0, TestContext.Current.CancellationToken), typeof(ArgumentOutOfRangeException));
            }

            [Theory]
            [InlineData(Direction.Inbound)]
            [InlineData(Direction.Outbound)]
            public async Task Gets_The_MesageBody(Direction direction)
            {
                using var context = CreateDbContext();

                long id = 0;
                switch (direction)
                {
                    case Direction.Inbound:
                        id = context.InExceptions.Where(x => x.MessageLocation != null).Select(x => x.Id).First();
                        break;
                    case Direction.Outbound:
                        id = context.OutExceptions.Where(x => x.MessageLocation != null).Select(x => x.Id).First();
                        break;
                }

                var result = await _sut.DownloadExceptionMessageBodyAsync(direction, id, TestContext.Current.CancellationToken);
                Assert.NotNull(result);
            }
        }
    }
}
