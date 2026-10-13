namespace Aucy.Application.Helpers
{
    public interface IPriceTierQuantizer
    {
        bool TryGetPriceStep(
            long minPrice,
            long maxPrice,
            int priceTierCount,
            out long priceStep);

        bool TryGetTierIndex(
            long minPrice,
            long maxPrice,
            int priceTierCount,
            long price,
            out int tierIndex);

        bool TryGetPriceForTier(
            long minPrice,
            long maxPrice,
            int priceTierCount,
            int tierIndex,
            out long tierPrice);

        bool TryGetAllTierPrices(
            long minPrice,
            long maxPrice,
            int priceTierCount,
            out long[] tierPrices);

        bool IsPriceValid(
            long minPrice,
            long maxPrice,
            int priceTierCount,
            long price);

        bool TryRoundToNearestTier(
            long minPrice,
            long maxPrice,
            int priceTierCount,
            long price,
            out long roundedPrice);
    }

    public class PriceTierQuantizer : IPriceTierQuantizer
    {
        public bool TryGetPriceStep(
            long minPrice,
            long maxPrice,
            int priceTierCount,
            out long priceStep)
        {
            priceStep = default;

            if (!ValidateParams(minPrice, maxPrice, priceTierCount))
                return false;

            if (priceTierCount == 1 || minPrice == maxPrice)
            {
                priceStep = 0;
                return true;
            }

            var range = (decimal)maxPrice - minPrice;
            var steps = priceTierCount - 1;
            var step = range / steps;

            priceStep = (long)Math.Round(step, MidpointRounding.AwayFromZero);

            return true;
        }

        public bool TryGetTierIndex(
            long minPrice,
            long maxPrice,
            int priceTierCount,
            long price,
            out int tierIndex)
        {
            tierIndex = default;

            if (!ValidateParams(minPrice, maxPrice, priceTierCount))
                return false;

            if (price < minPrice || price > maxPrice)
                return false;

            if (priceTierCount == 1)
            {
                tierIndex = 0;
                return true;
            }

            if (!TryGetPriceStep(
                    minPrice,
                    maxPrice,
                    priceTierCount,
                    out var step))
            {
                return false;
            }

            if (step == 0)
            {
                tierIndex = 0;
                return true;
            }

            var index = (double)(price - minPrice) / step;
            var roundedIndex = (int)Math.Round(index, MidpointRounding.AwayFromZero);

            tierIndex = Math.Clamp(roundedIndex, 0, priceTierCount - 1);

            return true;
        }

        public bool TryGetPriceForTier(
            long minPrice,
            long maxPrice,
            int priceTierCount,
            int tierIndex,
            out long tierPrice)
        {
            tierPrice = default;

            if (!ValidateParams(minPrice, maxPrice, priceTierCount))
                return false;

            if (tierIndex < 0 || tierIndex >= priceTierCount)
                return false;

            if (priceTierCount == 1 || minPrice == maxPrice)
            {
                tierPrice = minPrice;
                return true;
            }

            tierPrice = minPrice + (tierIndex * (maxPrice - minPrice) / (priceTierCount - 1));

            return true;
        }

        public bool TryGetAllTierPrices(
            long minPrice,
            long maxPrice,
            int priceTierCount,
            out long[] tierPrices)
        {
            tierPrices = default!;

            if (!ValidateParams(minPrice, maxPrice, priceTierCount))
                return false;

            var prices = new long[priceTierCount];

            for (var i = 0; i < priceTierCount; i++)
            {
                if (!TryGetPriceForTier(
                        minPrice,
                        maxPrice,
                        priceTierCount,
                        i,
                        out prices[i]))
                {
                    tierPrices = default!;
                    return false;
                }
            }

            tierPrices = prices;
            return true;
        }

        public bool IsPriceValid(
            long minPrice,
            long maxPrice,
            int priceTierCount,
            long price)
        {
            if (!ValidateParams(minPrice, maxPrice, priceTierCount))
                return false;

            if (price < minPrice || price > maxPrice)
                return false;

            if (priceTierCount == 1)
                return price == minPrice && minPrice == maxPrice;

            if (!TryGetPriceStep(
                    minPrice,
                    maxPrice,
                    priceTierCount,
                    out var step))
            {
                return false;
            }

            if (step == 0)
                return price == minPrice;

            var diff = price - minPrice;
            var remainder = diff % step;

            return remainder == 0;
        }

        public bool TryRoundToNearestTier(
            long minPrice,
            long maxPrice,
            int priceTierCount,
            long price,
            out long roundedPrice)
        {
            roundedPrice = default;

            if (!ValidateParams(minPrice, maxPrice, priceTierCount))
                return false;

            if (price < minPrice)
            {
                roundedPrice = minPrice;
                return true;
            }

            if (price > maxPrice)
            {
                roundedPrice = maxPrice;
                return true;
            }

            if (priceTierCount == 1)
            {
                roundedPrice = minPrice;
                return true;
            }

            if (!TryGetPriceStep(
                    minPrice,
                    maxPrice,
                    priceTierCount,
                    out var step))
            {
                return false;
            }

            if (step == 0)
            {
                roundedPrice = minPrice;
                return true;
            }

            var diff = price - minPrice;
            var tiers = (double)diff / step;

            var roundedTiers = (int)Math.Round(
                tiers,
                MidpointRounding.AwayFromZero);

            roundedTiers = Math.Clamp(
                roundedTiers,
                0,
                priceTierCount - 1);

            return TryGetPriceForTier(
                minPrice,
                maxPrice,
                priceTierCount,
                roundedTiers,
                out roundedPrice);
        }

        private bool ValidateParams(
            long minPrice,
            long maxPrice,
            int priceTierCount)
        {
            if (minPrice <= 0)
                return false;

            if (maxPrice < minPrice)
                return false;

            if (priceTierCount <= 0)
                return false;

            return true;
        }
    }
}