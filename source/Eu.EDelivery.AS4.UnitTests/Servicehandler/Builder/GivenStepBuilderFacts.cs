using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Steps.Receive;
using Eu.EDelivery.AS4.Steps.Send;
using Eu.EDelivery.AS4.Steps.Submit;
using Eu.EDelivery.AS4.UnitTests.Steps;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Eu.EDelivery.AS4.UnitTests.Servicehandler.Builder;

/// <summary>
/// Testing <see cref="StepBuilder" />
/// </summary>
public class GivenStepBuilderFacts
{
    private readonly StepBuilder _sut;

    public GivenStepBuilderFacts()
    {
        var services = new ServiceCollection();
        var provider = services
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddAS4Steps()
            .AddSingleton<SinkStep>()
            .AddSingleton(Substitute.For<ICertificateRepository>())
            .BuildServiceProvider();
        _sut = new StepBuilder(Substitute.For<ILogger<StepBuilder>>(), provider);
    }

    public class GivenValidStepSettings : GivenStepBuilderFacts
    {

        [Fact]
        public void BuilderCreatesExpectedAmountOfSteps()
        {
            // Arrange
            var expectedCount = new Random().Next(0, 10);
            var expectedType = typeof(SinkStep);
            var settingSteps = CreatePipelineSteps(expectedCount, expectedType);

            // Act
            var steps = _sut.BuildSteps(settingSteps);

            // Assert
            Assert.Equal(expectedCount, steps.Count());
            Assert.All(steps, s => Assert.Equal(expectedType, s.GetType()));
        }

        private static Step[] CreatePipelineSteps(int amount, Type type)
        {
            var stubStep = new Step { Type = type.AssemblyQualifiedName! };

            return [.. Enumerable.Repeat(stubStep, amount)];
        }

        [Fact]
        public void ThenBuilderCreatesValidStep()
        {
            // Arrange
            var settingSteps = CreateDefaultSettingSteps();

            // Act
            var step = _sut.BuildAsSingleStep(settingSteps);

            // Assert
            Assert.IsType<CompositeStep>(step);
        }

        private static Step[] CreateDefaultSettingSteps()
        {
            return [new Step { Type = typeof(EncryptAS4MessageStep).AssemblyQualifiedName! }];
        }
    }

    public class GivenValidConditionalStepConfig : GivenStepBuilderFacts
    {
        [Fact]
        public void BuildConditionalStepAsList()
        {
            // Arrange
            var config = CreateSimpleConditationalStepConfig();

            // Act
            var steps = _sut.BuildSteps(config);

            // Assert
            var first = steps.First();
            AssertConditionalStep(first);
        }

        [Fact]
        public void BuildConditionanStepAsInstance()
        {
            // Arrange
            var config = CreateSimpleConditationalStepConfig();

            // Act
            var step = _sut.BuildAsSingleStep(config);

            // Assert
            AssertConditionalStep(step);
        }

        private static void AssertConditionalStep(IStep step)
        {
            Assert.NotNull(step);
            Assert.IsType<ConditionalStep>(step);
        }

        private static ConditionalStepConfig CreateSimpleConditationalStepConfig()
        {
            var thenStep = new[] { new Step { Type = typeof(DeterminePModesStep).AssemblyQualifiedName! } };
            var elseStep = new[] { new Step { Type = typeof(VerifySignatureAS4MessageStep).AssemblyQualifiedName! } };

            return new ConditionalStepConfig(c => true, thenStep, elseStep);
        }
    }

    public class GivenInvalidConfigurableStepConfig : GivenStepBuilderFacts
    {
        [Fact]
        public void NonConfigurableStepWithSettingsThrowsConfigurationException()
        {
            var config = CreateInvalidConfigurableStepConfig();

            Assert.Throws<InvalidOperationException>(() => _sut.BuildAsSingleStep(config.NormalPipeline!));
        }

        private static StepConfiguration CreateInvalidConfigurableStepConfig()
        {
            var step = new Step
            {
                Type = typeof(DynamicDiscoveryStep).AssemblyQualifiedName!,
                Setting = [new Setting("SmpProfile", "someValue"),]
            };

            return new StepConfiguration() { NormalPipeline = [step] };
        }
    }
}
