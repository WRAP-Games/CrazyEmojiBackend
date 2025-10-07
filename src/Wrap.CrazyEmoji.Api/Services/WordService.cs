using Wrap.CrazyEmoji.Api.Abstractions;

namespace Wrap.CrazyEmoji.Api.Services;

public class WordService : IWordService
{
    public Task<string> GetRandomWordAsync()
    {
        return Task.FromResult("Leaf");
    }
}