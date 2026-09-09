namespace Aucy.Application.Helpers
{
    public interface IPriceTierQuantizer
    {
        public long GetPriceStep(long minPrice, long maxPrice, int priceTierCount);
        public int GetTierIndex(long minPrice, long maxPrice, int priceTierCount, long price);
        public long GetPriceForTier(long minPrice, long maxPrice, int priceTierCount, int tierIndex);
        public long[] GetAllTierPrices(long minPrice, long maxPrice, int priceTierCount);
        public bool IsPriceValid(long minPrice, long maxPrice, int priceTierCount, long price);
        public long RoundToNearestTier(long minPrice, long maxPrice, int priceTierCount, long price);
    }

    public class PriceTierQuantizer : IPriceTierQuantizer
    {
        public long GetPriceStep(long minPrice, long maxPrice, int priceTierCount)
        {
            ValidateParams(minPrice, maxPrice, priceTierCount);

            if (priceTierCount == 1 || minPrice == maxPrice)
                return 0;

            var range = (decimal)maxPrice - minPrice;
            var steps = priceTierCount - 1;
            var step = range / steps;

            return (long)Math.Round(step, MidpointRounding.AwayFromZero);
        }

        public int GetTierIndex(long minPrice, long maxPrice, int priceTierCount, long price)
        {
            ValidateParams(minPrice, maxPrice, priceTierCount);

            if (price < minPrice || price > maxPrice)
                return -1;

            if (priceTierCount == 1)
                return 0;

            var step = GetPriceStep(minPrice, maxPrice, priceTierCount);

            if (step == 0)
                return 0;

            var index = (double)(price - minPrice) / step;
            var roundedIndex = (int)Math.Round(index, MidpointRounding.AwayFromZero);

            return Math.Clamp(roundedIndex, 0, priceTierCount - 1);
        }

        public long GetPriceForTier(long minPrice, long maxPrice, int priceTierCount, int tierIndex)
        {
            ValidateParams(minPrice, maxPrice, priceTierCount);

            if (tierIndex < 0 || tierIndex >= priceTierCount)
                throw new ArgumentOutOfRangeException(nameof(tierIndex),
                    $"Tier index must be between 0 and {priceTierCount - 1}");

            if (priceTierCount == 1 || minPrice == maxPrice)
                return minPrice;

            var price = minPrice + (tierIndex * (maxPrice - minPrice) / (priceTierCount - 1));

            return price;
        }

        public long[] GetAllTierPrices(long minPrice, long maxPrice, int priceTierCount)
        {
            ValidateParams(minPrice, maxPrice, priceTierCount);

            var prices = new long[priceTierCount];
            for (var i = 0; i < priceTierCount; i++)
                prices[i] = GetPriceForTier(minPrice, maxPrice, priceTierCount, i);
            
            return prices;
        }

        public bool IsPriceValid(long minPrice, long maxPrice, int priceTierCount, long price)
        {
            if (price < minPrice || price > maxPrice)
                return false;

            if (priceTierCount == 1)
                return price == minPrice;

            var step = GetPriceStep(minPrice, maxPrice, priceTierCount);

            if (step == 0)
                return price == minPrice;

            var diff = price - minPrice;
            var remainder = diff % step;

            return remainder == 0;
        }

        public long RoundToNearestTier(long minPrice, long maxPrice, int priceTierCount, long price)
        {
            ValidateParams(minPrice, maxPrice, priceTierCount);

            if (price < minPrice)
                return minPrice;

            if (price > maxPrice)
                return maxPrice;

            if (priceTierCount == 1)
                return minPrice;

            var step = GetPriceStep(minPrice, maxPrice, priceTierCount);

            if (step == 0)
                return minPrice;

            var diff = price - minPrice;
            var tiers = (double)diff / step;
            var roundedTiers = (int)Math.Round(tiers, MidpointRounding.AwayFromZero);

            roundedTiers = Math.Clamp(roundedTiers, 0, priceTierCount - 1);

            return GetPriceForTier(minPrice, maxPrice, priceTierCount, roundedTiers);
        }

        private void ValidateParams(long minPrice, long maxPrice, int priceTierCount)
        {
            if (minPrice < 0)
                throw new ArgumentException("Min price cannot be negative", nameof(minPrice));

            if (maxPrice < minPrice)
                throw new ArgumentException("Max price must be greater than or equal to min price", nameof(maxPrice));

            if (priceTierCount <= 0)
                throw new ArgumentException("Price tier count must be greater than 0", nameof(priceTierCount));
        }
    }
}