namespace Eu.EDelivery.AS4.Fe.UnitTests;

public class BaseTest
{
    public async Task<BaseTest> ExpectExceptionAsync(Func<Task> function, Type exception)
    {
        await Assert.ThrowsAsync(exception, function);
        return this;
    }
}