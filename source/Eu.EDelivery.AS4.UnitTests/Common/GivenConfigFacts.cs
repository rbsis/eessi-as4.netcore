using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.Watchers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Eu.EDelivery.AS4.UnitTests.Common;

public class GivenConfigFacts
{
    [Fact]
    public void InitializeWithDefaultRetryPollingInterval()
    {
        TestConfigInitialization(
            alterSettings: settings =>
                settings.RetryReliability = new SettingsRetryReliability { PollingInterval = null },
            onInitialized: config =>
                Assert.Equal(
                    TimeSpan.FromSeconds(5),
                    config.RetryPollingInterval));
    }

    private static void TestConfigInitialization(
        Action<Settings> alterSettings,
        Action<AS4.Common.Config> onInitialized)
    {
        var testSettingsFileName = Path.Combine(
            AS4.Common.Config.ApplicationPath, "config", "test-settings.xml");

        var originalSettingsFileName = Path.Combine(
            AS4.Common.Config.ApplicationPath, "config", "settings.xml");

        var settings = AS4XmlSerializer
            .FromString<Settings>(File.ReadAllText(originalSettingsFileName));

        alterSettings(settings!);

        File.WriteAllText(
            testSettingsFileName,
            AS4XmlSerializer.ToString(settings));

        File.Copy(
            originalSettingsFileName,
            testSettingsFileName,
            overwrite: true);

        Directory.CreateDirectory(Path.Combine(AS4.Common.Config.ApplicationPath, "config", "send-pmodes"));
        Directory.CreateDirectory(Path.Combine(AS4.Common.Config.ApplicationPath, "config", "receive-pmodes"));

        var sut = new AS4.Common.Config(
            NullLogger<AS4.Common.Config>.Instance,
            Substitute.For<IPModeWatcher<ReceivingProcessingMode>>(),
            Substitute.For<IPModeWatcher<SendingProcessingMode>>());

        // Act
        sut.Initialize(testSettingsFileName);

        // Assert
        onInitialized(sut);

        // TearDown
        File.Delete(testSettingsFileName);
    }
}
