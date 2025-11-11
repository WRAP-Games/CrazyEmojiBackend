using System.Text;
using Wrap.CrazyEmoji.Api.Constants;
using Wrap.CrazyEmoji.Api.GameLogic;
using Wrap.CrazyEmoji.Api.Services;

namespace Wrap.CrazyEmoji.UnitTests;

public class UnitTest1
{
    
    //points tests
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
    
    
    //player tests
    [Fact]
    public void Constructor_ValidValues_ShouldAssignProperties()
    {
        string username = "sussie";
        string connectionId = "abc123";

        var player = new Player(username, connectionId);

        Assert.Equal(username, player.Username);
        Assert.Equal(connectionId, player.ConnectionId);
        Assert.Equal(0, player.Points.Value);
        Assert.Equal(PlayerRole.Player, player.Role);
        Assert.False(player.HasGuessed);
        Assert.False(player.GuessedRight);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_InvalidUsername_ShouldThrow(string? username)
    {
        string connectionId = "conn-1";
        Assert.Throws<ArgumentException>(() => new Player(username!, connectionId));
    }
    
    
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_InvalidConnectionId_ShouldThrow(string? connectionId)
    {
        string username = "sussie";
        Assert.Throws<ArgumentException>(() => new Player(username, connectionId!));
    }
    
    [Fact]
    public void Username_SetValidValue_ShouldUpdateProperty()
    {
        var player = new Player("sussie", "id");
        player.Username = "Bob";

        Assert.Equal("Bob", player.Username);
    }
    
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Username_SetInvalidValue_ShouldThrow(string? invalidValue)
    {
        var player = new Player("sussie", "id");

        Assert.Throws<ArgumentException>(() => player.Username = invalidValue!);
    }
    
    [Fact]
    public void ConnectionId_SetValidValue_ShouldUpdateProperty()
    {
        var player = new Player("sussie", "id");
        player.ConnectionId = "new-id";
        Assert.Equal("new-id", player.ConnectionId);
    }
    
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void ConnectionId_SetInvalidValue_ShouldThrow(string? invalidValue)
    {
        var player = new Player("sussie", "id");
        Assert.Throws<ArgumentException>(() => player.ConnectionId = invalidValue!);
    }
    
    [Fact]
    public void Points_InitialValue_ShouldBeZero()
    {
        var player = new Player();
        Assert.Equal(0, player.Points.Value);
    }
    
    [Fact]
    public void CanSetRole()
    {
        var player = new Player();
        player.Role = PlayerRole.Commander;
        Assert.Equal(PlayerRole.Commander, player.Role);
    }
    
    [Fact]
    public void GuessFlags_CanBeUpdated()
    {
        var player = new Player();
        player.HasGuessed = true;
        player.GuessedRight = true;
        Assert.True(player.HasGuessed);
        Assert.True(player.GuessedRight);
    }


    //WordService tests
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