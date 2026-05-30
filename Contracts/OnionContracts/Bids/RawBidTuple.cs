using MessagePack;

namespace Aucy.Contracts.OnionContracts;

[MessagePackObject]
public readonly struct RawBidTuple
{
    [Key(0)] public long BidPrice { get; }
    [Key(1)] public int RemainingQuantity { get; }
}
