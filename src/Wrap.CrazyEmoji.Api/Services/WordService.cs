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
        if (_words.Count == 0)
            throw new InvalidOperationException("Word list not loaded. Call LoadWordsAsync() first.");

        var word = _words[_random.Next(_words.Count)];
        return Task.FromResult(word);
    }
}