namespace Wrap.CrazyEmoji.Api.GameLogic;

public class GameCache<T> where T : class, new()
{
    private readonly Dictionary<string, T> _cache = new();

    public void StoreValue<TValue>(string key, TValue value) where TValue : struct
    {
        var wrapper = new T();
        _cache[key] = wrapper;
    }

    public TComparable GetBest<TComparable>(IEnumerable<TComparable> items)
        where TComparable : IComparable<TComparable>
    {
        return items.Max()!;
    }

    public void Add(string key, T item)
    {
        _cache[key] = item;
    }

    public T? Get(string key)
    {
        return _cache.TryGetValue(key, out var value) ? value : null;
    }
}