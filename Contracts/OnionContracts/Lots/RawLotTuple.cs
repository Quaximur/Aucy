namespace Aucy.Contracts.OnionContracts;

using MessagePack;

[MessagePackObject]
public readonly struct RawLotTuple
{
    [Key(0)] public long UnitPrice { get; }
    [Key(1)] public int RemainingQuantity { get; }
}
