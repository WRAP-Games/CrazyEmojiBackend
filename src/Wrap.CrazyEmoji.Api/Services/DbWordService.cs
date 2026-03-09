using Microsoft.EntityFrameworkCore;
using Wrap.CrazyEmoji.Api.Data;
using Wrap.CrazyEmoji.Api.Abstractions;

namespace Wrap.CrazyEmoji.Api.Services;

public class DbWordService : IDbWordService
{
    private readonly Dictionary<string, Queue<string>> _roomWords = new();

    private readonly IDbContextFactory<GameDbContext> _dbFactory;

    public DbWordService(IDbContextFactory<GameDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task LoadWordsForRoomAsync(string roomCode, long categoryId, int amount)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var words = await db.Words
            .Where(w => w.CategoryId == categoryId)
            .OrderBy(_ => Guid.NewGuid())
            .Take(amount)
            .Select(w => w.Text)
            .ToListAsync();

        if (words.Count == 0)
            throw new InvalidOperationException("No words found for category");

        _roomWords[roomCode] = new Queue<string>(words);
    }

    public string GetWord(string roomCode)
    {
        if (!_roomWords.TryGetValue(roomCode, out var queue))
            throw new InvalidOperationException("No words loaded for this room");

        lock (queue)
        {
            if (queue.Count == 0)
            {
                _roomWords.Remove(roomCode);
                throw new InvalidOperationException("No words left for this room");
            }

            return queue.Dequeue();
        }
    }
}