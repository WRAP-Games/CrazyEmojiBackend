namespace Wrap.CrazyEmoji.IntegrationTests;

public class FakeIntegrationTests
{
    [Fact]
    public void Constructor_NegativeValue_ShouldThrowArgumentException()
    {
        10.ShouldBeGreaterThan(5);
    }
}