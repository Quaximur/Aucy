using Aucy.Contracts.OnionClient;
using Aucy.Domain.Entities;
using AutoMapper;

namespace Aucy.Application.Mappings;

public class LotMapping : Profile
{
    public LotMapping()
    {
        CreateMap<Lot, LotDto>()
            .ConstructUsing(src => new LotDto(
                id: src.Id,
                ownerUserId: src.OwnerUserId,
                itemConfigId: src.ItemConfigId,
                unitPrice: src.UnitPrice,
                remainingQuantity: src.RemainingQuantity
            ));

        // Use with .Include(l => l.ItemConfig) in EF Core query
        CreateMap<Lot, OwnLotDto>()
            .ConstructUsing(src => new OwnLotDto(
                id: src.Id,
                ownerUserId: src.OwnerUserId,
                itemConfigId: src.ItemConfigId,
                unitPrice: src.UnitPrice,
                remainingQuantity: src.RemainingQuantity,
                totalQuantity: src.TotalQuantity,
                createdAt: src.CreatedAt,
                itemConfig: null
            ))
            .ForMember(dest => dest.ItemConfig, opt => opt.MapFrom(src => src.ItemConfig));
    }
}