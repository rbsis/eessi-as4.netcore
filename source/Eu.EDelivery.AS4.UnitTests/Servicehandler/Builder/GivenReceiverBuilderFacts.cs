using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Receivers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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

            var provider = new ServiceCollection()
                .AddOptions()
                .Configure<FileReceiverSettings>(s =>
                {
                    s.BatchSize = 20;
                    s.FileMask = "*.*";
                    s.FilePath = "FilePath";
                    s.PollingInterval = TimeSpan.FromMilliseconds(100);
                })
                .AddSingleton<ILogger<FileReceiver>>(NullLogger<FileReceiver>.Instance)
                .AddSingleton<FileReceiver>()
                .BuildServiceProvider();

            // Act
            var receiver = new ReceiverBuilder(NullLogger<ReceiverBuilder>.Instance, provider).BuildFromConfig(settingReceiver);

            // Assert
            Assert.NotNull(receiver);
            Assert.IsType<FileReceiver>(receiver);
        }

        private static Receiver CreateDefaultReceiverSettings() => new()
        {
            Type = typeof(FileReceiver).AssemblyQualifiedName,
            Setting = [new Setting(key: "FilePath", value: "Test")]
        };
    }
}
