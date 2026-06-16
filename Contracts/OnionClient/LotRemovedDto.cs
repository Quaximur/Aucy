using System;

namespace Aucy.Contracts.OnionClient
{
    public class LotRemovedDto
    {
        // Add ReturningQuantity ItemStacks with ItemConfig.Id, increase Weight and mb UsedCapacity
        public Guid LotId { get; set; }
        public ItemConfigDto ItemConfig { get; set; }
        public long ReturningMoney { get; set; } // Add to Money
        public int ReturningQuantity { get; set; }
    }
}

