namespace Wrap.CrazyEmoji.Api.Abstractions;

public interface IDbWordService
{
    Task LoadWordsForRoomAsync(string roomCode, long categoryId, int amount);
    string GetWord(string roomCode);
}
