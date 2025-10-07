namespace Wrap.CrazyEmoji.Api.Abstractions;

public interface IWordService
{
    Task<string> GetRandomWordAsync();
}