using Aucy.Contracts.OnionContracts;
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
                itemQuantity: src.ItemQuantity,
                createdAt: src.CreatedAt
            ));

        CreateMap<Lot, LotWithItemConfigDto>()
            .ConstructUsing(src => new LotWithItemConfigDto(
                id: src.Id,
                ownerUserId: src.OwnerUserId,
                itemConfigId: src.ItemConfigId,
                unitPrice: src.UnitPrice,
                itemQuantity: src.ItemQuantity,
                createdAt: src.CreatedAt,
                itemConfig: null!
            ))
            .ForMember(dest => dest.ItemConfig, opt => opt.MapFrom(src => src.ItemConfig)); ;
    }
}