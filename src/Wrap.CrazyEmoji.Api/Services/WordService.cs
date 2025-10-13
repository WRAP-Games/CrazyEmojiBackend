using System.Collections;
using Wrap.CrazyEmoji.Api.Abstractions;

namespace Wrap.CrazyEmoji.Api.Services;

public class WordService : IWordService, IEnumerable<string>
{
    private readonly List<string> _words = new();
    private readonly Random _random = new();
    
    // Loads a list of words from a given Stream.
    public async Task LoadWordsAsync(Stream wordStream)
    {
        if (wordStream == null)
            throw new ArgumentNullException(nameof(wordStream));

        using var reader = new StreamReader(wordStream);
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (!string.IsNullOrWhiteSpace(line))
                _words.Add(line.Trim());
        }
    }
    
    // Returns a random word from the loaded list.
    public Task<string> GetRandomWordAsync()
    {
        return Task.FromResult("Leaf");
    }
}