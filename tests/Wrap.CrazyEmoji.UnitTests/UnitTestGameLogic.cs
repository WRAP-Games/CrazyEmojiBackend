using System.Text;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Wrap.CrazyEmoji.Api.Constants;
using Wrap.CrazyEmoji.Api.GameLogic;
using Wrap.CrazyEmoji.Api.Services;

namespace Wrap.CrazyEmoji.UnitTests;

public class UnitTestGameLogic
{
    
    //points tests
    [Theory]
    [InlineData(-1)]
    [InlineData(-20)]
    public void Constructor_NegativeValue_ShouldThrowArgumentException(int invalidValue)
    {
        Assert.Throws<ArgumentException>(() => new Points(invalidValue));
    }
    
    [Fact]
    public void Constructor_ValidValue_ShouldSetValue()
    {
        int value = 5;
        var points = new Points(value);
        Assert.Equal(value, points.Value);
    }

    [Fact]
    public void IsZero_WhenValueIsZero_ShouldReturnTrue()
    {
        var points = new Points(0);
        bool result = points.IsZero();
        Assert.True(result);
    }
    
    [Fact]
    public void IsZero_WhenValueIsNotZero_ShouldReturnFalse()
    {
        var points = new Points(3);
        bool result = points.IsZero();
        Assert.False(result);
    }
    
    [Fact]
    public void AdditionOperator_ShouldReturnCorrectSum()
    {
        var p1 = new Points(4);
        var p2 = new Points(6);
        var result = p1 + p2;
        Assert.Equal(10, result.Value);
    }
    
    [Fact]
    public void CompareTo_ShouldReturnCorrectComparisons()
    {
        var smaller = new Points(3);
        var larger = new Points(8);
        Assert.True(smaller.CompareTo(larger) < 0);
        Assert.True(larger.CompareTo(smaller) > 0);
        Assert.Equal(0, smaller.CompareTo(new Points(3)));
    }
    
    [Fact]
    public void ToString_ShouldReturnValueAsString()
    { 
        var points = new Points(15);
        var result = points.ToString();
        Assert.Equal("15", result);
    }
    
    
    //player role tests
    
    [Fact]
    public void DefaultRole_ShouldBePlayer()
    {
        var player = new Player();
        Assert.Equal(PlayerRole.Player, player.Role);
    }
    
    [Fact]
    public void CanAssignCommanderRole()
    {
        var player = new Player();
        player.Role = PlayerRole.Commander;
        Assert.Equal(PlayerRole.Commander, player.Role);
    }
    
    [Fact]
    public void PlayerRoleEnum_ShouldHaveExpectedValues()
    {
        var commander = PlayerRole.Commander;
        var playerRole = PlayerRole.Player;
        Assert.Equal(0, (int)commander);
        Assert.Equal(1, (int)playerRole);
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

    //room hub tests
    
   
    
    
    

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