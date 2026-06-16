using System;

namespace Aucy.Contracts.OnionClient
{
    public class LotCollectedDto
    {
        public Guid LotId { get; set; }
        public long ReturningMoney { get; set; } // Add to Money
        public int DecreasingTotalQuantity { get; set; }
    }

}
