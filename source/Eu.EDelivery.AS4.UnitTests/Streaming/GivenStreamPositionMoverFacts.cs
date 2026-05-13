using System.Text;
using Eu.EDelivery.AS4.Streaming;
using MimeKit.IO;

namespace Eu.EDelivery.AS4.UnitTests.Streaming;

/// <summary>
/// Testing <see cref="StreamUtilities"/>
/// </summary>
public class GivenStreamPositionMoverFacts
{
    [Fact]
    public void ThenStreamGetsSetToZeroIfStreamIsSeekable()
    {
        // Arrange
        using var stubStream = GetNotZeroPositionStream();
        // Act
        stubStream.MovePositionToStreamStart();

        // Assert
        AssertEqualsZero(stubStream);
    }

    [Fact]
    public void ThenStreamGetsSetToZeroIfStreamIsNonClosableStream()
    {
        // Arrange
        using var stubStream = new NonCloseableStream(GetNotZeroPositionStream());
        // Act
        stubStream.MovePositionToStreamStart();

        // Assert
        AssertEqualsZero(stubStream.InnerStream);
    }

    [Fact]
    public void ThenStreamGetsSetToZeroIfStreamIsFilteredStream()
    {
        // Arrange
        using var stubStream = new FilteredStream(GetNotZeroPositionStream());
        // Act
        stubStream.MovePositionToStreamStart();

        // Assert
        AssertEqualsZero(stubStream.Source);
    }

    private static void AssertEqualsZero(Stream stream) => Assert.Equal(0, stream.Position);

    [Fact]
    public void TestNonZeroPositionStreamFixture()
    {
        // Act
        using Stream actualStream = GetNotZeroPositionStream();
        // Assert
        Assert.NotEqual(0, actualStream.Position);
    }

    private static MemoryStream GetNotZeroPositionStream()
    {
        var stubStream = new MemoryStream(Encoding.UTF8.GetBytes("ignored string"));
        stubStream.ReadByte();

        return stubStream;
    }
}
