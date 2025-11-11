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
    
    [Fact]
    public void Test4_Constructor_SetsUsernameAndConnectionId()
    {
        var player = new Player("sussie", "abc123");
        Assert.Equal("sussie", player.Username);
        Assert.Equal("abc123", player.ConnectionId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Test5_Username_Throws_WhenInvalid(string input)
    {
        var player = new Player();
        Assert.Throws<ArgumentException>(() => player.Username = input);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Test6_ConnectionId_Throws_WhenInvalid(string input)
    {
        var player = new Player();
        Assert.Throws<ArgumentException>(() => player.ConnectionId = input);
    }

    [Fact]
    public async Task Test7_LoadWordsAsync_LoadsWordsCorrectly()
    {
        var service = new WordService();
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("apple\nbanana\napple"));
        await service.LoadWordsAsync(stream);

        Assert.Equal(2, service.Count());
    }

    [Fact]
    public async Task Test8_GetRandomWordAsync_Throws_WhenEmpty()
    {
        var service = new WordService();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetRandomWordAsync());
    }

    [Fact]
    public async Task Test9_GetRandomWordAsync_ReturnsWord_WhenLoaded()
    {
        var service = new WordService();
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("apple\nbanana"));
        await service.LoadWordsAsync(stream);

        var word = await service.GetRandomWordAsync();
        Assert.Contains(word, new[] { "apple", "banana" });
    }


}