namespace Game.Engine;

/// <summary>Server-owned timing profile; classic defaults remain available for rule tests and console games.</summary>
public sealed record GameTiming(long RollMilliseconds=15_000,long MandatoryMilliseconds=30_000,
    long ActionMilliseconds=90_000,long HandoverMilliseconds=0)
{
    public static GameTiming Standard { get; }=new();
    public static GameTiming RelaxedTesting { get; }=new(60_000,60_000,180_000,30_000);
    internal void Validate()
    {
        if(RollMilliseconds is <=0 or >3_600_000||MandatoryMilliseconds is <=0 or >3_600_000||ActionMilliseconds is <=0 or >3_600_000||HandoverMilliseconds is <0 or >3_600_000)
            throw new ArgumentOutOfRangeException(nameof(GameTiming),"Timers must be positive and at most one hour; handover can be zero.");
    }
}
