using Eu.EDelivery.AS4.Extensions;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.UnitTests.Model.PMode;
using Eu.EDelivery.AS4.Validators;
using FluentValidation.Results;
using FsCheck;
using FsCheck.Xunit;
using static Eu.EDelivery.AS4.UnitTests.Validators.ValidationInputGenerators;
using static Eu.EDelivery.AS4.UnitTests.Validators.ValidationOutputAssertions;

namespace Eu.EDelivery.AS4.UnitTests.Validators;

public class GivenSendingProcessingModeValidatorFacts
{
    [Fact]
    public void EitherPushConfigurationOrDynamicDiscoveryMustBeSpecified()
    {
        // Arrange
        var pmode = new SendingProcessingMode
        {
            Id = "sending-pmode"
        };

        // Act
        var result = ExerciseValidation(pmode);

        // Assert
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(
        @"<?xml version=""1.0"" encoding=""utf-8""?>
              <PMode xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
                xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
                xmlns=""eu:edelivery:as4:pmode"">
                <Id>dynamicdiscovery-pmode</Id>
                <DynamicDiscovery>
                    <SmpProfile/>
                </DynamicDiscovery>
              </PMode>", false)]
    [InlineData(
        @"<?xml version=""1.0"" encoding=""utf-8""?>
              <PMode xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
                xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
                xmlns=""eu:edelivery:as4:pmode"">
                <Id>dynamicdiscovery-pmode</Id>
                <DynamicDiscovery/>
              </PMode>", true)]
    [InlineData(
        @"<?xml version=""1.0"" encoding=""utf-8""?>
              <PMode xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
                xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
                xmlns=""eu:edelivery:as4:pmode"">
                <Id>dynamicdiscovery-pmode</Id>
                <DynamicDiscovery>
                </DynamicDiscovery>
              </PMode>", true)]
    [InlineData(
        @"<?xml version=""1.0"" encoding=""utf-8""?>
              <PMode xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
                xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
                xmlns=""eu:edelivery:as4:pmode"">
                <Id>dynamicdiscovery-pmode</Id>
                <DynamicDiscovery>
                    <SmpProfile>
                        Eu.EDelivery.AS4.Services.DynamicDiscovery.LocalDynamicDiscoveryProfile, Eu.EDelivery.AS4, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
                    </SmpProfile>
                </DynamicDiscovery>
              </PMode>", true)]
    public void DynamicDiscoveryInvalidWithEmptySmpProfile(string xml, bool expected)
    {
        // Arrange
        var pmode = AS4XmlSerializer.FromString<SendingProcessingMode>(xml)!;

        // Act
        var result = ExerciseValidation(pmode);

        // Assert
        Assert.True(
            expected == result.IsValid,
            result.AppendValidationErrorsToErrorMessage("Invalid SendingPMode: "));
    }

    [Property]
    public Property EncryptionCertificateShouldBeSpecifiedWhenEncryptionIsEnabledForANonDynamicDiscoverySetup(
        bool isEnabled,
        string findValue,
        string certificate)
    {
        var genDynamicDiscoveryProfile =
            Gen.Fresh<DynamicDiscoveryConfiguration?>(() => new DynamicDiscoveryConfiguration())
               .OrNull();

        return Prop.ForAll(
            CreateEncryptionCertificateInfoGen(findValue, certificate).ToArbitrary(),
            genDynamicDiscoveryProfile.ToArbitrary(),
            (cert, dynamicDiscovery) =>
            {
                // Arrange
                var pmode = ValidSendingPModeFactory.Create();
                pmode.DynamicDiscovery = dynamicDiscovery;
                pmode.Security.Encryption = new Encryption
                {
                    IsEnabled = isEnabled,
                    EncryptionCertificateInformation = cert.Item2,
                    CertificateType = cert.Item1
                };

                // Act
                var result = ExerciseValidation(pmode);

                // Assert
                var specifiedDynamicDiscoveryProfile = pmode.DynamicDiscoverySpecified;
                var specifiedCertFindCriteria =
                    cert.Item1 == PublicKeyCertificateChoiceType.CertificateFindCriteria
                    && cert.Item2 is CertificateFindCriteria c
                    && !string.IsNullOrWhiteSpace(c.CertificateFindValue);

                var specifiedPublicKeyCert =
                    cert.Item1 == PublicKeyCertificateChoiceType.PublicKeyCertificate
                    && cert.Item2 is PublicKeyCertificate k
                    && !string.IsNullOrWhiteSpace(k.Certificate);

                var specifiedEncryptionCert = specifiedCertFindCriteria || specifiedPublicKeyCert;

                var property = result.IsValid
                    .Equals(specifiedEncryptionCert && isEnabled && !specifiedDynamicDiscoveryProfile)
                    .Or(!isEnabled)
                    .Or(specifiedDynamicDiscoveryProfile)
                    .Label(
                        $"Validation has {(result.IsValid ? "succeeded" : "failed")} " +
                        $"but encryption certificate {(specifiedEncryptionCert ? "is" : "isn't")} specified " +
                        $"with a {cert.Item1} while the encryption is {(isEnabled ? "enabled" : "disabled")} " +
                        $"for a {(specifiedDynamicDiscoveryProfile ? "configured" : "non-configured")} Dynamic Discovery");

                return property;
            });
    }

    private static Gen<Tuple<PublicKeyCertificateChoiceType, object?>> CreateEncryptionCertificateInfoGen(
        string findValue,
        string certificate)
    {
        var genCertFindCriteria = Gen.OneOf(
            Gen.Constant(Tuple.Create(
                PublicKeyCertificateChoiceType.CertificateFindCriteria,
                (object?)null)),
            Gen.Fresh(() => Tuple.Create(
                PublicKeyCertificateChoiceType.CertificateFindCriteria,
                (object?)new CertificateFindCriteria { CertificateFindValue = findValue })));

        var genPublicKeyCert = Gen.OneOf(
            Gen.Constant(Tuple.Create(
                PublicKeyCertificateChoiceType.PublicKeyCertificate,
                (object?)null)),
            Gen.Fresh(() => Tuple.Create(
                PublicKeyCertificateChoiceType.PublicKeyCertificate,
                (object?)new PublicKeyCertificate { Certificate = certificate })));

        return Gen.OneOf(genCertFindCriteria, genPublicKeyCert);
    }

    [Property]
    public Property SigningCertificateShouldBeSpecifiedWhenSigningIsEnabled(
        bool isEnabled,
        string findValue,
        string certificate,
        string password)
    {
        return Prop.ForAll(
            CreatePrivateKeyCertificateGen(findValue, certificate, password)
                .ToArbitrary(),
            cert =>
            {
                // Arrange
                var pmode = ValidSendingPModeFactory.Create();
                pmode.Security.Signing = new Signing
                {
                    IsEnabled = isEnabled,
                    SigningCertificateInformation = cert.Item2,
                    CertificateType = cert.Item1
                };

                // Act
                var result = ExerciseValidation(pmode);

                // Assert
                var specifiedCertFindCriteria =
                    cert.Item1 == PrivateKeyCertificateChoiceType.CertificateFindCriteria
                    && cert.Item2 is CertificateFindCriteria c
                    && !string.IsNullOrWhiteSpace(c.CertificateFindValue);

                var specifiedPrivateKeyCert =
                    cert.Item1 == PrivateKeyCertificateChoiceType.PrivateKeyCertificate
                    && cert.Item2 is PrivateKeyCertificate k
                    && !string.IsNullOrWhiteSpace(k.Certificate)
                    && !string.IsNullOrWhiteSpace(k.Password);

                var specifiedCertInfo = specifiedCertFindCriteria || specifiedPrivateKeyCert;
                return result.IsValid
                    .Equals(specifiedCertInfo && isEnabled)
                    .Or(!isEnabled)
                    .Label(
                        $"Validation has {(result.IsValid ? "succeeded" : "failed")} " +
                        $"but signing certificate {(specifiedCertInfo ? "is" : "isn't")} specified " +
                        $"with a {cert.Item1} while the signing is {(isEnabled ? "enabled" : "disabled")}");
            });
    }

    [Property]
    public Property TlsCertificateShouldBeSpecifiedWhenTlsIsEnabled(
        bool isEnabled,
        string clientCertFindValue,
        string password,
        string certificate)
    {
        return Prop.ForAll(
            CreateTlsCertificateInfoGen(clientCertFindValue, password, certificate)
                .ToArbitrary(),
            tls =>
            {
                // Arrange
                var pmode = ValidSendingPModeFactory.Create();
                pmode.PushConfiguration!.TlsConfiguration = new TlsConfiguration
                {
                    IsEnabled = isEnabled,
                    ClientCertificateInformation = tls.Item2,
                    CertificateType = tls.Item1
                };

                // Act
                var result = ExerciseValidation(pmode);

                // Assert
                var specifiedClientCertRef =
                    tls.Item1 == TlsCertificateChoiceType.ClientCertificateReference
                    && tls.Item2 is ClientCertificateReference clientCertRef
                    && !string.IsNullOrWhiteSpace(clientCertRef.ClientCertificateFindValue);

                var specifiedPrivateKeyCert =
                    tls.Item1 == TlsCertificateChoiceType.PrivateKeyCertificate
                    && tls.Item2 is PrivateKeyCertificate privateKeyCert
                    && !string.IsNullOrWhiteSpace(privateKeyCert.Certificate)
                    && !string.IsNullOrWhiteSpace(privateKeyCert.Password);

                var specifiedCert = specifiedClientCertRef || specifiedPrivateKeyCert;
                return result.IsValid
                    .Equals(specifiedCert && isEnabled)
                    .Or(!isEnabled)
                    .Label(
                        $"Validation has {(result.IsValid ? "succeeded" : "failed")} " +
                        $"but TLS client certificate {(specifiedCert ? "is" : "isn't")} specified " +
                        $"with a {tls.Item1} while the TLS configuration is {(isEnabled ? "enabled" : "disabled")}");
            });
    }

    private static Gen<Tuple<TlsCertificateChoiceType, object?>> CreateTlsCertificateInfoGen(
        string clientCertFindValue,
        string password,
        string certificate)
    {
        var genClientCertRef = Gen.OneOf(
            Gen.Constant(Tuple.Create(
                TlsCertificateChoiceType.ClientCertificateReference,
                (object?)null)),
            Gen.Fresh(() => Tuple.Create(
                TlsCertificateChoiceType.ClientCertificateReference,
                (object?)new ClientCertificateReference { ClientCertificateFindValue = clientCertFindValue })));

        var genPrivateKeyCert = Gen.OneOf(
            Gen.Constant(Tuple.Create(
                TlsCertificateChoiceType.PrivateKeyCertificate,
                (object?)null)),
            Gen.Fresh(() => Tuple.Create(
                TlsCertificateChoiceType.PrivateKeyCertificate,
                (object?)new PrivateKeyCertificate { Password = password, Certificate = certificate })));

        return Gen.OneOf(genClientCertRef, genPrivateKeyCert);
    }

    [Theory]
    [InlineData(128, 128)]
    [InlineData(192, 192)]
    [InlineData(256, 256)]
    [InlineData(200, 128)]
    public void ValidSendingPModeIfKeySizeIs(int beforeKeySize, int afterKeySize)
    {

        var pmode = ValidSendingPModeFactory.Create();
        pmode.Security.Encryption.IsEnabled = true;
        pmode.Security.Encryption.AlgorithmKeySize = beforeKeySize;

        // Act
        ExerciseValidation(pmode);

        // Assert
        Assert.True(pmode.Security.Encryption.AlgorithmKeySize == afterKeySize);
    }

    [Fact]
    public void SendConfigurationMayBeIncompleteWhenDynamicDiscovery()
    {
        var pmode = new SendingProcessingMode
        {
            Id = "Test",
            MepBinding = MessageExchangePatternBinding.Pull,
            PushConfiguration = null,
            DynamicDiscovery = new DynamicDiscoveryConfiguration()
        };

        var result = ExerciseValidation(pmode);

        Assert.True(result.IsValid, result.AppendValidationErrorsToErrorMessage("Failed validation:"));
    }

    [Property]
    public static Property UrlShouldBePresentWhenSMPIsDisabled(string url)
    {
        var pmode = new SendingProcessingMode
        {
            Id = "ignored",
            PushConfiguration = new PushConfiguration { Protocol = { Url = url } }
        };

        var result = ExerciseValidation(pmode);

        var urlPresent = url != null;
        return (result.IsValid == urlPresent).ToProperty();
    }

    [Property]
    public Property RetryReliabilityShouldBePresentWhenIsEnabled(
        bool isEnabled,
        int retryCount,
        TimeSpan retryInterval)
    {
        return new Func<SendingProcessingMode, RetryReliability>[]
        {
            p => p.ReceiptHandling.Reliability,
            p => p.ErrorHandling.Reliability,
            p => p.ExceptionHandling.Reliability
        }
        .Select(f => TestRelialityForEnabledFlag(isEnabled, retryCount, retryInterval, f))
        .Aggregate((p1, p2) => p1.And(p2));
    }

    private static Property TestRelialityForEnabledFlag(
        bool isEnabled,
        int retryCount,
        TimeSpan retryInterval,
        Func<SendingProcessingMode, RetryReliability> getReliability)
    {
        return Prop.ForAll(
            Gen.Frequency(
                Tuple.Create(1, Arb.From<string>().Generator),
                Tuple.Create(2, Gen.Constant(retryInterval.ToString())))
               .ToArbitrary(),
            retryIntervalText =>
            {
                // Arrange
                var pmode = ValidSendingPModeFactory.Create();
                var r = getReliability(pmode);
                r.IsEnabled = isEnabled;
                r.RetryCount = retryCount;
                r.RetryInterval = retryIntervalText;

                // Act
                var result = ExerciseValidation(pmode);

                // Assert
                var correctConfigured =
                    retryCount > 0
                    && r.RetryInterval.AsTimeSpan() > default(TimeSpan);

                var expected =
                    !isEnabled && !correctConfigured
                    || !isEnabled
                    || correctConfigured;

                return expected.Equals(result.IsValid)
                    .Label(result.AppendValidationErrorsToErrorMessage(string.Empty))
                    .Classify(result.IsValid, "Valid PMode")
                    .Classify(!result.IsValid, "Invalid PMode")
                    .Classify(correctConfigured, "Correct Reliability")
                    .Classify(!correctConfigured, "Incorrect Reliability")
                    .Classify(isEnabled, "Reliability is enabled")
                    .Classify(!isEnabled, "Reliability is disabled");
            });
    }

    [Property]
    public Property RetryCountAndRetryIntervalShouldBeSpecifiedWhenReceptionAwarnessIsEnabled(
        bool isEnabled,
        int retryCount,
        TimeSpan retryInterval)
    {
        return Prop.ForAll(
            Gen.Frequency(
                Tuple.Create(1, Arb.Generate<string>()),
                Tuple.Create(2, Gen.Constant(retryInterval.ToString())))
               .ToArbitrary(),
            retryIntervalText =>
            {
                // Arrange
                var pmode = ValidSendingPModeFactory.Create();
                var r = new ReceptionAwareness
                {
                    IsEnabled = isEnabled,
                    RetryCount = retryCount,
                    RetryInterval = retryIntervalText
                };
                pmode.Reliability.ReceptionAwareness = r;

                // Act
                var result = ExerciseValidation(pmode);

                // Assert
                var validRetryCount = r.RetryCount > 0;
                var validRetryInterval = r.RetryInterval.AsTimeSpan() > default(TimeSpan);
                return result.IsValid
                    .Equals(isEnabled
                            && validRetryCount
                            && validRetryInterval)
                    .Or(!isEnabled)
                    .Label(
                        $"Validation has {(result.IsValid ? "succeeded" : "failed")} " +
                        $"but the RetryCount {(validRetryCount ? ">" : "<=")} 0 (was {r.RetryCount}) and " +
                        $"RetryInterval {(validRetryInterval ? ">" : "<=")} {default(TimeSpan)} (was {r.RetryInterval}) " +
                        $"while the ReceptionAwareness is {(isEnabled ? "enabled" : "disabled")}");
            });
    }

    [Property]
    public static Property NotifyMethodShoudBeSpecifiedForReceiptHandlingWhenWeMustNotifyMessageProducer(
        bool notifyMessageProducer)
    {
        return NotifyMethod_Should_Be_Specified_When_We_Notify_MessageProducer(
            notifyMessageProducer,
            pmode => pmode.ReceiptHandling);
    }

    [Property]
    public static Property NotifyMethodShouldBeSpecifiedForErrorHandlingWhenWeMustNotifyMessageProducer(
        bool notifyMessageProducer)
    {
        return NotifyMethod_Should_Be_Specified_When_We_Notify_MessageProducer(
            notifyMessageProducer,
            pmode => pmode.ErrorHandling);
    }

    [Property]
    public static Property NotifyMethodShouldBeSpecifiedForExceptionHandlingWhenWeMustNotifyMessageProducer(
        bool notifyMessageProducer)
    {
        return NotifyMethod_Should_Be_Specified_When_We_Notify_MessageProducer(
            notifyMessageProducer,
            pmode => pmode.ExceptionHandling);
    }

    private static Property NotifyMethod_Should_Be_Specified_When_We_Notify_MessageProducer(
        bool notifyMessageProducer,
        Func<SendingProcessingMode, SendHandling> getHandling)
    {
        return Prop.ForAll(
            CreateMethodGen().ToArbitrary(),
            method =>
            {
                // Arrange
                var pmode = ValidSendingPModeFactory.Create();
                var sendHandling = getHandling(pmode);
                sendHandling.NotifyMessageProducer = notifyMessageProducer;
                sendHandling.NotifyMethod = method;

                // Act
                var result = ExerciseValidation(pmode);

                // Assert
                var specifiedNotifyMethod = SpecifiedMethod(method);
                return result.IsValid
                    .Equals(notifyMessageProducer && specifiedNotifyMethod)
                    .Or(!notifyMessageProducer)
                    .Label(
                        $"Validation has {(result.IsValid ? "succeeded" : "failed")} " +
                        $"but the NotifyMethod {(specifiedNotifyMethod ? "is" : "isn't")} specified " +
                        $"while the NotifyMessageProducer is {(notifyMessageProducer ? "enabled" : "disabled")}");
            });
    }

    private static ValidationResult ExerciseValidation(SendingProcessingMode pmode)
    {
        return Default.SendingProcessingModeValidator.Validate(pmode);
    }
}
