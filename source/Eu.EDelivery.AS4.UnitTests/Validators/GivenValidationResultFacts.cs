using Eu.EDelivery.AS4.Validators;
using FluentValidation;
using FluentValidation.Results;
using Moq;

namespace Eu.EDelivery.AS4.UnitTests.Validators;

public class GivenValidationResultFacts
{
    [Theory]
    [InlineData("", false, true)]
    [InlineData("not empty string", true, false)]
    public void ResultPathsGetsCorrectlyCalled(string testInstance, bool expectedHappyPath, bool expectedUnhappyPath)
    {
        // Arrange
        var sut = Mock.Of<IValidator<string>>();
        var isValid = !string.IsNullOrEmpty(testInstance);
        Mock.Get(sut).Setup(v => v.Validate(It.IsAny<string>())).Returns(new StubValidationResult(isValid));

        bool happyPathCalled = false, unhappyPathCalled = false;

        // Act
        sut.Validate(testInstance)
           .Result(onValidationSuccess: result => happyPathCalled = true, onValidationFailed: result => unhappyPathCalled = true);

        // Assert
        Assert.Equal(expectedHappyPath, happyPathCalled);
        Assert.Equal(expectedUnhappyPath, unhappyPathCalled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExerciseStubValidationResult(bool expected)
    {
        // Arrange
        var sut = new StubValidationResult(expected);

        // Act
        var actual = sut.IsValid;

        // Assert
        Assert.Equal(expected, actual);
    }

    private class StubValidationResult : ValidationResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StubValidationResult"/> class.
        /// </summary>
        /// <param name="expected"></param>
        public StubValidationResult(bool expected)
        {
            IsValid = expected;
        }

        /// <summary>
        /// Returns true if ... is valid.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is valid; otherwise, <c>false</c>.
        /// </value>
        public override bool IsValid { get; }
    }
}
