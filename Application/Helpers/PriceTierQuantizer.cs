using System;

namespace Aucy.Application.Helpers
{
    public interface IPriceTierQuantizer
    {
        public int GetQuantizationSize(long minPrice, long maxPrice, int priceTierCount);
        public double GetRatio(long minPrice, long maxPrice, int priceTierCount);
        public int GetIndexForPrice(long minPrice, long maxPrice, int priceTierCount, long price);
        public long GetRepresentativePriceForIndex(long minPrice, long maxPrice, int priceTierCount, int index);
        public long GetRepresentativeForPrice(long minPrice, long maxPrice, int priceTierCount, long price);
        public void ValidatePriceBelongsToTier(long minPrice, long maxPrice, int priceTierCount, long price);
    }

    public class PriceTierQuantizer : IPriceTierQuantizer
    {
        private void ComputeParams(long minPrice, long maxPrice, int priceTierCount, out int quantizationSize, out double ratio)
        {
            if (priceTierCount <= 0)
                throw new ArgumentException("priceTierCount must be greater than 0", nameof(priceTierCount));

            var priceRange = maxPrice - minPrice;

            if (priceRange < priceTierCount)
            {
                quantizationSize = (int)priceRange + 1;
                ratio = 1;
            }
            else
            {
                quantizationSize = priceTierCount;
                ratio = (double)quantizationSize / priceRange;
            }
        }

        public int GetQuantizationSize(long minPrice, long maxPrice, int priceTierCount)
        {
            ComputeParams(minPrice, maxPrice, priceTierCount, out var q, out _);
            return q;
        }

        public double GetRatio(long minPrice, long maxPrice, int priceTierCount)
        {
            ComputeParams(minPrice, maxPrice, priceTierCount, out _, out var r);
            return r;
        }

        public int GetIndexForPrice(long minPrice, long maxPrice, int priceTierCount, long price)
        {
            ComputeParams(minPrice, maxPrice, priceTierCount, out var quantizationSize, out var ratio);

            if (price < minPrice || price > maxPrice)
                throw new ArgumentOutOfRangeException(nameof(price), "Price is out of configured bounds");

            var rawIndex = (price - minPrice) * ratio;
            var index = rawIndex >= quantizationSize ? quantizationSize - 1 : (int)rawIndex;
            return index;
        }

        public long GetRepresentativePriceForIndex(long minPrice, long maxPrice, int priceTierCount, int index)
        {
            ComputeParams(minPrice, maxPrice, priceTierCount, out var quantizationSize, out var ratio);

            if (index < 0 || index >= quantizationSize)
                throw new ArgumentOutOfRangeException(nameof(index));

            long currentMin = minPrice + (long)Math.Ceiling(index / ratio);
            return currentMin;
        }

        public long GetRepresentativeForPrice(long minPrice, long maxPrice, int priceTierCount, long price)
        {
            var idx = GetIndexForPrice(minPrice, maxPrice, priceTierCount, price);
            return GetRepresentativePriceForIndex(minPrice, maxPrice, priceTierCount, idx);
        }

        public void ValidatePriceBelongsToTier(long minPrice, long maxPrice, int priceTierCount, long price)
        {
            var representative = GetRepresentativeForPrice(minPrice, maxPrice, priceTierCount, price);
            if (price != representative)
                throw new ArgumentException($"Price should belong to one of configured tiers. Provided price {price} maps to representative tier price {representative}.");
        }
    }
}
