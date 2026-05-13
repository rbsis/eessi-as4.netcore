using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Receivers;
using Eu.EDelivery.AS4.ServiceHandler.Builder;

namespace Eu.EDelivery.AS4.UnitTests.Servicehandler.Builder;

/// <summary>
/// Testing <see cref="ReceiverBuilder"/>
/// </summary>
public class GivenReceiverBuilderFacts
{
    public class GivenValidArguments : GivenReceiverBuilderFacts
    {
        [Fact]
        public void ThenBuilderCreatesValidReceiver()
        {
            // Arrange
            var settingReceiver = CreateDefaultReceiverSettings();

            // Act
            var receiver = new ReceiverBuilder(Default.GenericTypeBuilder).Build(settingReceiver);

            // Assert
            Assert.NotNull(receiver);
            Assert.IsType<FileReceiver>(receiver);
        }

        private static Receiver CreateDefaultReceiverSettings()
        {
            return new Receiver
            {
                Type = typeof(FileReceiver).AssemblyQualifiedName,
                Setting = [new Setting(key: "FilePath", value: "Test")]
            };
        }
    }
}
