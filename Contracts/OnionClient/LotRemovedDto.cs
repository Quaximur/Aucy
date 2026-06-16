using MessagePack;
using System;

namespace Aucy.Contracts.OnionClient
{
    [MessagePackObject]
    public class LotRemovedDto
    {
        // Add ReturningQuantity ItemStacks with ItemConfig.Id, increase Weight and mb UsedCapacity
        [Key(0)] public Guid LotId { get; set; }
        [Key(1)] public ItemConfigDto ItemConfig { get; set; }
        [Key(2)] public long ReturningMoney { get; set; } // Add to Money
        [Key(3)] public int ReturningQuantity { get; set; }
    }
}

