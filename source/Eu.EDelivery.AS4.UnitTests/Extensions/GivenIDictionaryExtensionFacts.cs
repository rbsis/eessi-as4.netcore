using Eu.EDelivery.AS4.Extensions;

namespace Eu.EDelivery.AS4.UnitTests.Extensions;

/// <summary>
/// Testing <see cref="AS4.Extensions.DictionaryExtensions" />
/// </summary>
public class GivenIDictionaryExtensionFacts
{
    private const string TestKey = "$mandatory-key$";
    private const string TestValue = "$mandatory-value$";
    private readonly IDictionary<string, string> _dictionary;

    public GivenIDictionaryExtensionFacts()
    {
        _dictionary = new Dictionary<string, string> { [TestKey] = TestValue };
    }

    /// <summary>
    /// Testing if the IDictionaryExtensions succeeds
    /// </summary>
    public class GivenIDictionaryExtesionSucceeds : GivenIDictionaryExtensionFacts
    {
        [Fact]
        public void ThenReadMandatoryPropertySucceeds()
        {
            // Act
            var value = _dictionary.ReadMandatoryProperty(TestKey);

            // Assert
            Assert.Same(TestValue, value);
        }

        [Fact]
        public void ThenReadOptionalPropertySucceeds()
        {
            // Arrange
            const string DefaultValue = "$default$";

            // Act
            var value = _dictionary.ReadOptionalProperty("doesn't exist", DefaultValue);

            // Assert
            Assert.Same(DefaultValue, value);
        }
    }

    /// <summary>
    /// Testing if the IDictionaryExtensions fails
    /// </summary>
    public class GivenIDictionaryExtensionsFails : GivenIDictionaryExtensionFacts
    {
        [Fact]
        public void ThenReadMandatoryPropertyFails()
        {
            // Arrange
            const string DoesntExistedKey = "$doesn't existed key$";

            // Act / Assert
            Assert.ThrowsAny<Exception>(() => _dictionary.ReadMandatoryProperty(DoesntExistedKey));
        }
    }
}
