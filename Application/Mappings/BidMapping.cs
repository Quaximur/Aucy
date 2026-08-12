using Aucy.Contracts.OnionClient;
using Aucy.Domain.Entities;
using AutoMapper;

namespace Aucy.Application.Mappings;

public class BidMapping : Profile
{
    public BidMapping()
    {
        CreateMap<Bid, BidDto>()
            .ConstructUsing(src => new BidDto(
                id: src.Id,
                itemConfigId: src.ItemConfigId,
                bidderUserId: src.BidderUserId,
                bidPrice: src.BidPrice,
                remainingQuantity: src.RemainingQuantity
            ));

        CreateMap<Bid, OwnBidDto>()
            .ConstructUsing(src => new OwnBidDto(
                id: src.Id,
                itemConfigId: src.ItemConfigId,
                bidderUserId: src.BidderUserId,
                bidPrice: src.BidPrice,
                remainingQuantity: src.RemainingQuantity,
                totalQuantity: src.TotalQuantity,
                createdAt: src.CreatedAt,
                itemConfig: null
            ))
            .ForMember(dest => dest.ItemConfig, opt =>
            {
                opt.Condition(src => src.ItemConfig != null && src.ItemConfig.IsUnary);
                opt.MapFrom(src => src.ItemConfig);
            });
    }
}
