namespace LicenseSeat.Tests;

public class LicenseSeatClientTests
{
    [Fact]
    public void Client_CanBeInstantiated()
    {
        var client = new LicenseSeatClient();

        Assert.NotNull(client);
    }
}
