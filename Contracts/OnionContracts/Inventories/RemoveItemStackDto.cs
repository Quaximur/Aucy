using MessagePack;
using System;

namespace Aucy.Contracts.OnionContracts
{
    [MessagePackObject]
    public class RemoveItemStackDto
    {
        [Key(0)] public Guid ItemStackId { get; set; }
        [Key(1)] public int Quantity { get; set; }
    }
}
