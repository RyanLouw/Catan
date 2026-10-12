namespace Game.Engine;

public interface IClock { long Milliseconds { get; } }
public sealed class SystemClock : IClock
{
    private readonly System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
    public long Milliseconds => watch.ElapsedMilliseconds;
}
public sealed class ManualClock : IClock
{
    public long Milliseconds { get; private set; }
    public void Advance(long milliseconds)
    {
        if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
        Milliseconds = checked(Milliseconds + milliseconds);
    }
}
/// <summary>Stable, cloneable PRNG: failed transactions cannot consume randomness.</summary>
internal sealed class RandomSource(ulong state)
{
    private ulong state = state == 0 ? 0x9e3779b97f4a7c15UL : state;
    public RandomSource Clone() => new(state);
    public int Next(int upper)
    {
        if (upper <= 0) throw new ArgumentOutOfRangeException(nameof(upper));
        ulong limit = ulong.MaxValue - ulong.MaxValue % (uint)upper;
        ulong n;
        do { state ^= state >> 12; state ^= state << 25; state ^= state >> 27; n = state * 2685821657736338717UL; } while (n >= limit);
        return (int)(n % (uint)upper);
    }
    public void Shuffle<T>(IList<T> values)
    {
        for (int i = values.Count - 1; i > 0; i--) { int j = Next(i + 1); (values[i], values[j]) = (values[j], values[i]); }
    }
}
