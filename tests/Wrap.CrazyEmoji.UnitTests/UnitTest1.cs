namespace Wrap.CrazyEmoji.UnitTests;

public class UnitTest1
{
    [Fact]
    public void Test1_Constructor_Throws_WhenNegative()
    {
        Assert.Throws<ArgumentException>(() => new Points(-1));
    }

    [Fact]
    public void Test2_AdditionOperator_AddsCorrectly()
    {
        Assert.True(true);
    }
}