using Eu.EDelivery.AS4.Fe.Exceptions;
using Eu.EDelivery.AS4.Fe.Monitor;
using Eu.EDelivery.AS4.Fe.Pmodes.Model;
using Eu.EDelivery.AS4.Fe.Services;
using Eu.EDelivery.AS4.Fe.SubmitTool;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Model.Submit;
using HttpMultipartParser;
using Microsoft.Extensions.Options;
using NSubstitute;
using Party = Eu.EDelivery.AS4.Model.PMode.Party;

namespace Eu.EDelivery.AS4.Fe.UnitTests;

public class SubmitMessageCreatorTests
{
    protected const string Pmode = "8.1.1-pmode";
    protected readonly IPmodeService _pmodeService;
    protected readonly IOptions<SubmitToolOptions> _options;

    protected SubmitMessageCreatorTests()
    {
        _options = Substitute.For<IOptions<SubmitToolOptions>>();
        _options.Value.Returns(new SubmitToolOptions
        {
            PayloadHttpAddress = "httpaddress",
            ToHttpAddress = "tohttpaddress"
        });

        _pmodeService = Substitute.For<IPmodeService>();
        _pmodeService.GetSendingByNameAsync(Arg.Is(Pmode), Arg.Any<CancellationToken>())
            .Returns(new SendingBasePmode()
            {
                Pmode = new SendingProcessingMode
                {
                    Id = "2143213",
                    MessagePackaging = new SendMessagePackaging
                    {
                        PartyInfo = new PartyInfo
                        {
                            FromParty = new Party
                            {
                                PartyIds = [new() { Id = "fds" }]
                            },
                            ToParty = new Party
                            {
                                PartyIds = [new() { Id = "fdsqfdsfd" }]
                            }
                        }
                    }
                }
            });
    }

    public class CreateSubmitMessages : SubmitMessageCreatorTests
    {
        [Fact]
        public async Task Throws_Exception_When_Pmode_Doesnt_Exist()
        {
            var payload = new MessagePayload
            {
                SendingPmode = "IDONTEXIST",
                Files = []
            };

            var sut = new SubmitMessageCreator(_pmodeService, [], [], _options, Substitute.For<IClient>());

            var error = await Assert.ThrowsAsync<BusinessException>(() => sut.CreateSubmitMessagesAsync(payload, TestContext.Current.CancellationToken));
            Assert.Contains("Could not find PMode", error.Message);
        }

        [Fact]
        public async Task Passed_Payloads_To_The_Correct_Payload_Handler()
        {
            var payloadHandler = Substitute.For<IPayloadHandler>();
            payloadHandler.CanHandle(Arg.Any<string>()).Returns(true);
            payloadHandler.HandleAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns("DOWNLOADURL");

            var dummyMessageHandler = Substitute.For<IMessageHandler>();
            dummyMessageHandler.CanHandle(Arg.Any<string>()).Returns(true);
            await dummyMessageHandler.HandleAsync(Arg.Any<SubmitMessage>(), Arg.Any<string>(), TestContext.Current.CancellationToken);

            var sut = new SubmitMessageCreator(_pmodeService, [payloadHandler], [dummyMessageHandler], _options, Substitute.For<IClient>());

            using var memoryStream = new MemoryStream();
            var payload = new MessagePayload
            {
                Files =
                [
                    new("test", "test", memoryStream)
                ],
                SendingPmode = Pmode
            };

            await sut.CreateSubmitMessagesAsync(payload, TestContext.Current.CancellationToken);

            payloadHandler.Received().CanHandle(Arg.Any<string>());
            await payloadHandler.Received().HandleAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Passed_Message_To_The_Correct_MessageHandler()
        {
            var dummyMessageHandler = Substitute.For<IMessageHandler>();
            dummyMessageHandler.CanHandle(Arg.Any<string>()).Returns(true);
            await dummyMessageHandler.HandleAsync(Arg.Any<SubmitMessage>(), Arg.Any<string>(), TestContext.Current.CancellationToken);

            var sut = new SubmitMessageCreator(_pmodeService, [], [dummyMessageHandler], _options, Substitute.For<IClient>());

            var payload = new MessagePayload
            {
                Files = Enumerable.Empty<FilePart>().ToList(),
                SendingPmode = Pmode
            };

            await sut.CreateSubmitMessagesAsync(payload, TestContext.Current.CancellationToken);

            dummyMessageHandler.Received().CanHandle(Arg.Any<string>());
            await dummyMessageHandler.Received().HandleAsync(Arg.Any<SubmitMessage>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
    }
}
