using MessagePack;
using System;

namespace Aucy.Contracts.OnionClient
{
    /// <summary>
    /// Add ReturningMoney to the Money.
    /// Decrease Remaining and Total quantity by RemovedQuantity. Bought but uncollected quantity remains until collecting.
    /// </summary>
    [MessagePackObject]
    public class BidRemovedDto
    {
        [Key(0)] public Guid BidId { get; set; }
        [Key(1)] public long ReturningMoney { get; set; }
        [Key(2)] public int RemovedQuantity { get; set; } 

        public BidRemovedDto(Guid bidId,  long returningMoney, int removedQuantity)
        {
            BidId = bidId;
            ReturningMoney = returningMoney;
            RemovedQuantity = removedQuantity;
        }
    }
}
