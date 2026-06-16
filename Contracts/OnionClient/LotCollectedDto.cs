using MessagePack;
using System;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public class LotCollectedDto
    {
        [Key(0)] public Guid LotId { get; set; }
        [Key(1)] public long ReturningMoney { get; set; } // Add to Money
        [Key(2)] public int DecreasingTotalQuantity { get; set; }
    }

}
