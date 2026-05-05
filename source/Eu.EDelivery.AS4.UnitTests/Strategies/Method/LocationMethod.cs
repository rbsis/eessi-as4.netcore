namespace Eu.EDelivery.AS4.UnitTests.Strategies.Method;

public class LocationMethod : AS4.Model.PMode.Method
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocationMethod"/> class.
    /// </summary>
    /// <param name="location">The location.</param>
    public LocationMethod(string location)
    {
        Type = "FILE";
        Parameters = [new() { Name = "location", Value = location }];
    }
}
