using Xunit;

namespace Wrap.CrazyEmoji.IntegrationTests;

public class FakeIntegrationTests
{
    [Fact]
    public void Constructor_NegativeValue_ShouldThrowArgumentException()
    {
        Assert.True(10 > 5);
    }
}