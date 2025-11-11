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
        var p1 = new Points(50);
        var p2 = new Points(30);
        var result = p1 + p2;
        Assert.Equal(80, result.Value);
    }

    [Fact]
    public void Test3_CompareTo_ReturnsExpectedOrder()
    {
        var p1 = new Points(10);
        var p2 = new Points(20);
        Assert.True(p1.CompareTo(p2) < 0);
    }
}