using Eu.EDelivery.AS4.Model.PMode;

namespace Eu.EDelivery.AS4.UnitTests.Model;

/// <summary>
/// Testing the <see cref="SendingProcessingMode" />
/// </summary>
public class GivenSendingProcessingModeFacts
{
    private static readonly SendingProcessingMode _defaultEmptyPMode = new();

    /// <summary>
    /// Testing the PMode Model for its defaults
    /// </summary>
    public class GivenDefaultSendingProcessingMode : GivenSendingProcessingModeFacts
    {
        [Fact]
        public void KeySizeIsDefaultIfNotDefined()
        {
            // Assert
            Assert.Equal(Encryption.Default.AlgorithmKeySize, _defaultEmptyPMode.Security.Encryption.AlgorithmKeySize);
        }

        [Fact]
        public void ThenEncryptionIsFalse()
        {
            // Assert
            Assert.False(_defaultEmptyPMode.Security.Encryption.IsEnabled);
        }

        [Fact]
        public void ThenErrorHandlingIsFalse()
        {
            // Assert
            Assert.False(_defaultEmptyPMode.ErrorHandling.NotifyMessageProducer);
        }

        [Fact]
        public void ThenExceptionHandlingIsFalse()
        {
            // Assert
            Assert.NotNull(_defaultEmptyPMode.ExceptionHandling);
            Assert.NotNull(_defaultEmptyPMode.ExceptionHandling.NotifyMethod);
            Assert.False(_defaultEmptyPMode.ExceptionHandling.NotifyMessageProducer);
        }

        [Fact]
        public void ThenMessagePackagingIsDefault()
        {
            // Assert
            Assert.True(_defaultEmptyPMode.MessagePackaging.UseAS4Compression);
            Assert.False(_defaultEmptyPMode.MessagePackaging.IsMultiHop);
            Assert.False(_defaultEmptyPMode.MessagePackaging.IncludePModeId);
        }

        [Fact]
        public void ThenOverrideIsFalse()
        {
            // Assert
            Assert.False(_defaultEmptyPMode.AllowOverride);
        }

        [Fact]
        public void ThenPushConfigurationIsDefault()
        {
            // Assert
            Assert.Equal(MessageExchangePatternBinding.Push, _defaultEmptyPMode.MepBinding);
        }

        [Fact]
        public void ThenReceiptHandlingIsFalse()
        {
            // Assert
            Assert.NotNull(_defaultEmptyPMode.ReceiptHandling);
            Assert.NotNull(_defaultEmptyPMode.ReceiptHandling.NotifyMethod);
            Assert.False(_defaultEmptyPMode.ReceiptHandling.NotifyMessageProducer);
        }

        [Fact]
        public void ThenReceiptionAwerenessIsDefault()
        {
            // Assert
            Assert.False(_defaultEmptyPMode.Reliability.ReceptionAwareness.IsEnabled);
            Assert.Equal(5, _defaultEmptyPMode.Reliability.ReceptionAwareness.RetryCount);
            Assert.Equal("00:01:00", _defaultEmptyPMode.Reliability.ReceptionAwareness.RetryInterval);
        }

        [Fact]
        public void ThenReceiptionAwerenessIsNotNull()
        {
            // Assert
            Assert.NotNull(_defaultEmptyPMode.ReceiptHandling);
            Assert.NotNull(_defaultEmptyPMode.ReceiptHandling.NotifyMethod);
        }

        [Fact]
        public void ThenReliabilityIsNotNull()
        {
            // Assert
            Assert.NotNull(_defaultEmptyPMode.Reliability);
            Assert.NotNull(_defaultEmptyPMode.Reliability.ReceptionAwareness);
        }

        [Fact]
        public void ThenSigningIsFalse()
        {
            // Assert
            Assert.NotNull(_defaultEmptyPMode.Security);
            Assert.NotNull(_defaultEmptyPMode.Security.Signing);
            Assert.False(_defaultEmptyPMode.Security.Signing.IsEnabled);
        }
    }

    [Fact]
    public void CanCloneSendingProcessingMode()
    {
        var pmode = new SendingProcessingMode()
        {
            Id = "CloneableTest",
            Security = new AS4.Model.PMode.Security()
            {
                Encryption = new Encryption
                {
                    IsEnabled = true,
                    EncryptionCertificateInformation = new PublicKeyCertificate() { Certificate = "ABCDEFGH" },
                    CertificateType = PublicKeyCertificateChoiceType.PublicKeyCertificate
                }
            }
        };

        var clone = pmode.Clone() as SendingProcessingMode;

        Assert.NotNull(clone);
        Assert.Equal(pmode.Id, clone.Id);
        Assert.Equal(pmode.Security.Encryption.IsEnabled, clone.Security.Encryption.IsEnabled);
        Assert.NotNull(clone.Security.Encryption.EncryptionCertificateInformation);
        Assert.Equal("ABCDEFGH", ((PublicKeyCertificate)clone.Security.Encryption.EncryptionCertificateInformation).Certificate);
    }
}
