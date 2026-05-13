using Eu.EDelivery.AS4.Builders;

namespace Eu.EDelivery.AS4.UnitTests.Servicehandler.Builder;

/// <summary>
/// Testing <see cref="GenericTypeBuilder"/>
/// </summary>
public class GivenGenericTypeBuilderFacts
{
    public class GivenValidArguments : GivenGenericTypeBuilderFacts
    {
        [Fact]
        public void ThenBuilderCreatesValidType()
        {
            // Arrange
            var typeString = typeof(object).FullName!;
            // Act
            var instance = Default.GenericTypeBuilder.Build<object>(typeString);
            // Assert
            Assert.NotNull(instance);
            Assert.IsType<object>(instance);
        }
    }

    public class GivenInValidArguments : GivenGenericTypeBuilderFacts
    {
        [Fact]
        public void ThenBuilderFailsToCreateTypeForAssemblyName()
        {
            // Arrange
            const string TypeString =
                "Eu.EDelivery.AS4.Transformers.InvalidTransformer, Eu.EDelivery.AS4.Transformers, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            // Act
            Assert.Throws<TypeLoadException>(
                () => Default.GenericTypeBuilder.Build<object>(TypeString));
        }
    }
}
