using MessagePack;
using System;

namespace Aucy.Contracts.OnionClient
{
    /// <summary>
    /// Add ReturningMoney to the Money.
    /// Substract CollectedQuantity from Total and Remaining quantity or delete the Lot if it will be 0/0 quantity.
    /// </summary>
    [MessagePackObject]
    public class LotCollectedDto
    {
        [Key(0)] public Guid LotId { get; set; }
        [Key(1)] public long ReturningMoney { get; set; }
        [Key(2)] public int CollectedQuantity { get; set; }

        public LotCollectedDto(Guid lotId, long returningMoney, int collectedQuantity)
        {
            LotId = lotId;
            ReturningMoney = returningMoney;
            CollectedQuantity = collectedQuantity;
        }
    }
}
