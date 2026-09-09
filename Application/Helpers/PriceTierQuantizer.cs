using System;

namespace Aucy.Application.Helpers
{
    public interface IPriceTierQuantizer
    {
        public double GetRatio(long minPrice, long maxPrice, int priceTierCount);
        public int GetIndexForPrice(long minPrice, long maxPrice, int priceTierCount, long price);
        public long GetRepresentativePriceForIndex(long minPrice, long maxPrice, int priceTierCount, int index);
        public long GetRepresentativeForPrice(long minPrice, long maxPrice, int priceTierCount, long price);
        public bool ValidatePriceBelongsToTier(long minPrice, long maxPrice, int priceTierCount, long price);
    }

    public class PriceTierQuantizer : IPriceTierQuantizer
    {
        private void ComputeParams(long minPrice, long maxPrice, int priceTierCount, out double ratio)
        {
            if (priceTierCount <= 0)
                throw new ArgumentException("priceTierCount must be greater than 0", nameof(priceTierCount));

            var priceRange = maxPrice - minPrice;

            // avoid division by zero
            ratio = priceRange == 0 ? 1.0 : (double)priceTierCount / priceRange;
        }

        public double GetRatio(long minPrice, long maxPrice, int priceTierCount)
        {
            ComputeParams(minPrice, maxPrice, priceTierCount, out var r);
            return r;
        }

        public int GetIndexForPrice(long minPrice, long maxPrice, int priceTierCount, long price)
        {
            ComputeParams(minPrice, maxPrice, priceTierCount, out var ratio);

            if (price < minPrice || price > maxPrice)
                return -1;

            var rawIndex = (price - minPrice) * ratio;
            var index = rawIndex >= priceTierCount ? priceTierCount - 1 : (int)rawIndex;
            return index;
        }

        public long GetRepresentativePriceForIndex(long minPrice, long maxPrice, int priceTierCount, int index)
        {
            ComputeParams(minPrice, maxPrice, priceTierCount, out var ratio);

            // clamp index into valid range
            if (index < 0) 
                index = 0;
            if (index >= priceTierCount) 
                index = priceTierCount - 1;

            long currentMin = minPrice + (long)Math.Ceiling(index / ratio);
            return currentMin;
        }

        public long GetRepresentativeForPrice(long minPrice, long maxPrice, int priceTierCount, long price)
        {
            var idx = GetIndexForPrice(minPrice, maxPrice, priceTierCount, price);
            return GetRepresentativePriceForIndex(minPrice, maxPrice, priceTierCount, idx);
        }

        public bool ValidatePriceBelongsToTier(long minPrice, long maxPrice, int priceTierCount, long price)
        {
            var representative = GetRepresentativeForPrice(minPrice, maxPrice, priceTierCount, price);
            return price == representative;
        }
    }
}
