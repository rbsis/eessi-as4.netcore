using System.Security.Cryptography.X509Certificates;
using Eu.EDelivery.AS4.Extensions;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Validators;
using FluentValidation.Results;
using static Eu.EDelivery.AS4.UnitTests.Validators.ValidationInputGenerators;
using static Eu.EDelivery.AS4.UnitTests.Validators.ValidationOutputAssertions;

namespace Eu.EDelivery.AS4.UnitTests.Validators;

public class GivenReceivingProcessingModeValidatorFacts
{
    [Fact]
    public void StartReceivingPModeIsValid()
    {
        Assert.True(ExerciseValidation(CreateValidPMode()).IsValid);
    }

    [Property]
    public Property ResponseConfigurationShouldBeSpecifiedWhenReplyPatternIsCallback(ReplyPattern pattern)
    {
        return Prop.ForAll(ArbMap.Default
            .GeneratorFor<string>()
            .Select(url => new Protocol { Url = url })
            .Select(p => (PushConfiguration?)new PushConfiguration { Protocol = p })
            .OrNull()
            .ToArbitrary(),
            responseConfig =>
            {
                // Arrange
                var pmode = new ReceivingProcessingMode
                {
                    Id = "receiving-pmode",
                    ReplyHandling =
                    {
                        ReplyPattern = ReplyPattern.Callback,
                        ResponseConfiguration = responseConfig
                    }
                };

                // Act
                var result = ExerciseValidation(pmode);

                // Assert
                return result.IsValid.Equals(
                        !string.IsNullOrEmpty(responseConfig?.Protocol.Url)
                        && pattern == ReplyPattern.Callback)
                    .Label("valid when ReplyPattern = Callback and non-empty 'Url'")
                    .Or(result.IsValid.Equals(pattern != ReplyPattern.Callback)
                              .Label("valid when ReplyPattern != Callback"));
            });
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void ResponseSigningIsRequiredWhenUseNRRFormatIsEnabled(
        bool isEnabled,
        bool useNrrFormat,
        bool expected)
    {
        // Arrange
        var pmode = new ReceivingProcessingMode
        {
            Id = "not-empty-id",
            ReplyHandling =
            {
                ReceiptHandling = { UseNRRFormat = useNrrFormat },
                ResponseSigning =
                {
                    IsEnabled = isEnabled,
                    SigningCertificateInformation = new CertificateFindCriteria
                    {
                        CertificateFindType = X509FindType.FindBySubjectName,
                        CertificateFindValue = "some-certificate-subject-name"
                    }
                }
            }
        };

        // Act
        var result = ExerciseValidation(pmode);

        // Assert
        Assert.True(
            expected == result.IsValid,
            result.AppendValidationErrorsToErrorMessage("Invalid PMode: "));
    }

    [CustomProperty]
    public Property ResponseSigningIsConfigurableViaPrivateCertificateOrCertificateCriteria(
        NonWhiteSpaceString certificateFindValue,
        NonWhiteSpaceString certificate,
        NonWhiteSpaceString password)
    {
        return Prop.ForAll(
            Gen.Elements(Constants.HashFunctions.SupportedAlgorithms.ToArray()).ToArbitrary(),
            Gen.Elements(Constants.SignAlgorithms.SupportedAlgorithms.ToArray()).ToArbitrary(),
            Gen.OneOf(
                Gen.Fresh<object>(() => new CertificateFindCriteria
                {
                    CertificateFindValue = certificateFindValue.Get
                }),
                Gen.Fresh<object>(() => new PrivateKeyCertificate
                {
                    Certificate = certificate.Get,
                    Password = password.Get
                }),
                ArbMap.Default.GeneratorFor<object>())
               .ToArbitrary(),
            (hashFunction, signingAlgorithm, certificateInformation) =>
            {
                // Arrange
                var pmode = new ReceivingProcessingMode
                {
                    Id = "receiving-pmode",
                    ReplyHandling =
                    {
                        ResponseSigning =
                        {
                            IsEnabled = true,
                            HashFunction = hashFunction,
                            Algorithm = signingAlgorithm,
                            SigningCertificateInformation = certificateInformation
                        }
                    }
                };

                // Act
                var result = ExerciseValidation(pmode);

                // Assert
                return result.IsValid.Equals(certificateInformation is CertificateFindCriteria)
                    .Label("configurable via CertificateFindCriteria")
                    .Or(result.IsValid.Equals(certificateInformation is PrivateKeyCertificate)
                              .Label("configurable via PrivateKeyCertificate"));
            });
    }

    [Property]
    public Property PiggyBackReliabilityIsOnlyAllowedWhenReplyPatternIsPiggyBack(ReplyPattern pattern)
    {
        return Prop.ForAll(
            Gen.Fresh<RetryReliability?>(() => new RetryReliability { IsEnabled = false })
               .OrNull()
               .ToArbitrary(),
            reliability =>
            {
                // Arrange
                var pmode = new ReceivingProcessingMode
                {
                    Id = "receiving-pmode",
                    ReplyHandling =
                    {
                        ReplyPattern = pattern,
                        PiggyBackReliability = reliability,
                    }
                };

                // Act
                var result = ExerciseValidation(pmode);

                // Assert
                return result.IsValid.Equals(pattern == ReplyPattern.PiggyBack)
                    .Label("valid when ReplyPattern = PiggyBack")
                    .Or(result.IsValid.Equals(pattern != ReplyPattern.PiggyBack && reliability == null)
                              .Label("valid when ReplyPattern != PiggyBack and no PiggyBackReliability"));
            });
    }

    [Property]
    public Property DecryptionCertificateShouldBeSpecifiedWhenDecryptionIsAllowedOrRequired(
        Limit encryption,
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
                var pmode = CreateValidPMode();
                pmode.Security.Decryption = new Decryption
                {
                    Encryption = encryption,
                    DecryptCertificateInformation = cert.Item2,
                    CertificateType = cert.Item1
                };

                // Act
                var result = ExerciseValidation(pmode);

                // Assert
                var allowedOrRequired =
                    encryption == Limit.Allowed
                    || encryption == Limit.Required;

                var specifiedCertFindCriteria =
                    cert.Item1 == PrivateKeyCertificateChoiceType.CertificateFindCriteria
                    && cert.Item2 is CertificateFindCriteria c
                    && !string.IsNullOrWhiteSpace(c.CertificateFindValue);

                var specifiedPrivateKeyCert =
                    cert.Item1 == PrivateKeyCertificateChoiceType.PrivateKeyCertificate
                    && cert.Item2 is PrivateKeyCertificate k
                    && !string.IsNullOrWhiteSpace(k.Certificate)
                    && !string.IsNullOrWhiteSpace(k.Password);

                var specifiedDecryptionCert = specifiedCertFindCriteria || specifiedPrivateKeyCert;
                return result.IsValid.Equals(specifiedDecryptionCert && allowedOrRequired)
                    .Or(!allowedOrRequired)
                    .Label(
                        $"Validation has {(result.IsValid ? "succeeded" : "failed")} "
                        + $"but decryption certificate {(specifiedDecryptionCert ? "is" : "isn't")} specified "
                        + $"with a {cert.Item1} while the encryption limit is {encryption}. "
                        + $"{(result.IsValid ? string.Empty : result.AppendValidationErrorsToErrorMessage("Validation Failure: "))}");
            });
    }

    [Property]
    public Property ReplyHandlingMustBeSpecifiedWhenThereIsntAForwardElement(
        string responsePMode,
        string forwardPMode)
    {
        var genForward = Gen.OneOf(
            Gen.Constant<object?>(null),
            Gen.Fresh<object?>(() => new Forward { SendingPMode = forwardPMode }),
            Gen.Fresh<object?>(() => new Deliver()));

        var genReplyHandling = Gen.OneOf(
            Gen.Fresh(() => new ReplyHandling
            {
                ResponseConfiguration = new PushConfiguration
                {
                    Protocol = { Url = "http://not/empty/url" }
                }
            }));

        return Prop.ForAll(
            genForward.ToArbitrary(),
            genReplyHandling.ToArbitrary(),
            (messageHandlingImp, replyHandling) =>
            {
                // Arrange
                var pmode = CreateValidPMode();
                pmode.ReplyHandling = replyHandling;
                pmode.MessageHandling.Item = messageHandlingImp;

                // Act
                var result = ExerciseValidation(pmode);

                // Assert
                var specifiedDeliver = messageHandlingImp is Deliver;
                var specifiedForward =
                    messageHandlingImp is Forward f
                    && !string.IsNullOrWhiteSpace(f.SendingPMode);

                var specifiedReplyHandling =
                    replyHandling?.ResponseConfiguration != null;

#pragma warning disable S3358 // Ternary operators should not be nested
                return result.IsValid
                    .Equals(specifiedReplyHandling && specifiedDeliver)
                    .Or(!specifiedReplyHandling && specifiedForward)
                    .Or(specifiedReplyHandling && specifiedForward)
                    .Label(
                        $"Validation has {(result.IsValid ? "succeeded" : "failed")} "
                        + $"but ReplyHandling {(specifiedReplyHandling ? "is" : "isn't")} specified and "
                        + $"MessageHandling is {(specifiedDeliver ? "a Deliver" : specifiedForward ? "a Forward" : "empty")} element. "
                        + $"{(result.IsValid ? string.Empty : result.AppendValidationErrorsToErrorMessage("Validation Failure: "))}");
#pragma warning restore S3358 // Ternary operators should not be nested
            });
    }

    [Property]
    public static Property DeliverReliabilityIsRequiredOnIsEnabledFlag(
        bool isEnabled,
        int retryCount,
        TimeSpan retryInterval)
    {
        return TestRelialityForEnabledFlag(
            isEnabled,
            retryCount,
            retryInterval,
            pmode => pmode.MessageHandling.DeliverInformation!.Reliability);
    }

    [Property]
    public static Property ExceptionReliabilityIsRequiredOnIsEnabledFlag(
        bool isEnabled,
        int retryCount,
        TimeSpan retryInterval)
    {
        return TestRelialityForEnabledFlag(
            isEnabled,
            retryCount,
            retryInterval,
            p => p.ExceptionHandling.Reliability);
    }

    [Property]
    public static Property PiggyBackReliabilityIsRequiredOnIsEnabledFlag(
        bool isEnabled,
        int retryCount,
        TimeSpan retryInterval)
    {
        return TestRelialityForEnabledFlag(
            isEnabled,
            retryCount,
            retryInterval,
            p =>
            {
                p.ReplyHandling.PiggyBackReliability = new RetryReliability();
                return p.ReplyHandling.PiggyBackReliability;
            },
            p => p.ReplyHandling.ReplyPattern = ReplyPattern.PiggyBack);
    }

    private static Property TestRelialityForEnabledFlag(
        bool isEnabled,
        int retryCount,
        TimeSpan retryInterval,
        Func<ReceivingProcessingMode, RetryReliability> getReliability,
        Action<ReceivingProcessingMode>? extraFixtureSetup = null)
    {
        return Prop.ForAll(
            Gen.Frequency(
                   (2, Gen.Constant(retryInterval.ToString())),
                   (1, ArbMap.Default.GeneratorFor<string>()))
               .ToArbitrary(),
            retryIntervalText =>
            {
                // Arrange
                var pmode = CreateValidPMode();
                var r = getReliability(pmode);
                r.IsEnabled = isEnabled;
                r.RetryCount = retryCount;
                r.RetryInterval = retryIntervalText;
                extraFixtureSetup?.Invoke(pmode);

                // Act
                var result = Default.ReceivingProcessingModeValidator.Validate(pmode);

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
    public Property DeliverMethodsRequiresEitherEmptyOrFilledNameAndValueAttributesWhenDeliveryIsEnabled(
        bool isEnabled)
    {
        return Prop.ForAll(
            CreateMethodGen().ToArbitrary(),
            CreateMethodGen().ToArbitrary(),
            (deliver, payloadRef) =>
            {
                // Arrange
                var pmode = CreateValidPMode();
                pmode.MessageHandling.Item = null;
                pmode.MessageHandling.Item = new Deliver
                {
                    IsEnabled = isEnabled,
                    DeliverMethod = deliver,
                    PayloadReferenceMethod = payloadRef
                };

                // Act
                var result = ExerciseValidation(pmode);

                // Assert

                var specifiedDeliver = SpecifiedMethod(deliver);
                var specifiedPayloadRef = SpecifiedMethod(payloadRef);
                return result.IsValid
                    .Equals(isEnabled && specifiedDeliver && specifiedPayloadRef)
                    .Or(!isEnabled)
                    .Label(
                        $"Validation has {(result.IsValid ? "succeeded" : "failed")} " +
                        $"but the DeliverMethod {(specifiedDeliver ? "is" : "isn't")} specified " +
                        $"and the PayloadReferenceMethod {(specifiedPayloadRef ? "is" : "isn't")} specified " +
                        $"while the Delivery is {(isEnabled ? "enabled" : "disabled")}");
            });
    }

    [Property]
    public Property ExceptionHandlingRequiresToHaveSpecifiedMethodWhenTheMessageProducerMustBeNotified(
        bool notifyMessageProducer)
    {
        return Prop.ForAll(
            CreateMethodGen().ToArbitrary(),
            method =>
            {
                // Arrange
                var pmode = CreateValidPMode();
                pmode.ExceptionHandling = new ReceiveHandling
                {
                    NotifyMessageConsumer = notifyMessageProducer,
                    NotifyMethod = method
                };

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

    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
    private static void TestReceivePModeValidation(Action<ReceivingProcessingMode> arrangePMode, bool expected)
    {
        // Arrange
        var pmode = CreateValidPMode();
        arrangePMode(pmode);

        // Act
        var result = ExerciseValidation(pmode);

        // Assert
        Assert.Equal(expected, result.IsValid);
    }

    private static ValidationResult ExerciseValidation(ReceivingProcessingMode fixture)
    {
        return Default.ReceivingProcessingModeValidator.Validate(fixture);
    }

    private static ReceivingProcessingMode CreateValidPMode()
    {
        var method = new Method
        {
            Type = "deliver-type",
            Parameters = [new() { Name = "parameter-name", Value = "parameter-value" }]
        };

        return new ReceivingProcessingMode
        {
            Id = "pmode-id",
            MessageHandling =
            {
                Item = new Deliver
                {
                    IsEnabled = true,
                    DeliverMethod = method,
                    PayloadReferenceMethod = method
                }
            }
        };
    }
}
