namespace Wrap.CrazyEmoji.Api.GameLogic;

public struct Points : IComparable<Points>
{
    public int Value { get; private set; }

    public Points(int value)
    {
        if (value < 0) throw new ArgumentException("Score cannot be negative", nameof(value));
        Value = value;
    }

    public bool IsZero() => Value == 0;

    public static Points operator +(Points left, Points right) => new Points(left.Value + right.Value);

    public int CompareTo(Points other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();
}