using MagicOnion;
using System;

namespace Aucy.Contracts.OnionContracts
{
    public interface IInventoryService : IService<IInventoryService>
    {
        public UnaryResult<InventoryDto> CreateInventory(CreateInventoryDto request);
        public UnaryResult<InventoryDto> GetOwnInventory(GetInventoryDto request);
        public UnaryResult<Guid> AddItemStack(AddItemStackDto request);
        public UnaryResult<Guid> RemoveItemStack(RemoveItemStackDto request);
        public UnaryResult<long> AddMoney(long moneyToAdd);
        public UnaryResult<long> RemoveMoney(long moneyToTake);
    }
}