
using Aucy.Application.Helpers;
using Xunit;

namespace Aucy.Application.Tests
{
    public class PriceTierQuantizerTests
    {
        private readonly IPriceTierQuantizer _quantizer = new PriceTierQuantizer();

        [Fact]
        public void TryGetPriceStep_BasicCase_ReturnsTrueAndStep()
        {
            var ok = _quantizer.TryGetPriceStep(100, 200, 5, out var step);

            Assert.True(ok);
            Assert.Equal(25, step);
        }

        [Fact]
        public void TryGetPriceStep_SingleTierOrEqualMinMax_ReturnsTrueAndZero()
        {
            Assert.True(_quantizer.TryGetPriceStep(100, 200, 1, out var s1));
            Assert.Equal(0, s1);

            Assert.True(_quantizer.TryGetPriceStep(100, 100, 5, out var s2));
            Assert.Equal(0, s2);
        }

        [Fact]
        public void TryGetTierIndex_InRange_RoundsAwayFromZero()
        {
            Assert.True(_quantizer.TryGetTierIndex(100, 200, 5, 100, out var indexAtMin));
            Assert.True(_quantizer.TryGetTierIndex(100, 200, 5, 112, out var indexLow));
            Assert.True(_quantizer.TryGetTierIndex(100, 200, 5, 113, out var indexHigh));

            Assert.Equal(0, indexAtMin);
            Assert.Equal(0, indexLow);
            Assert.Equal(1, indexHigh);
        }

        [Fact]
        public void TryGetTierIndex_OutOfRange_ReturnsFalse()
        {
            Assert.False(_quantizer.TryGetTierIndex(100, 200, 5, 99, out _));
            Assert.False(_quantizer.TryGetTierIndex(100, 200, 5, 201, out _));
        }

        [Fact]
        public void TryGetPriceForTier_ReturnsTrueAndPrice()
        {
            Assert.True(_quantizer.TryGetPriceForTier(100, 200, 5, 2, out var price));
            Assert.Equal(150, price);
        }

        [Fact]
        public void TryGetPriceForTier_InvalidIndex_ReturnsFalse()
        {
            Assert.False(_quantizer.TryGetPriceForTier(100, 200, 5, -1, out _));
            Assert.False(_quantizer.TryGetPriceForTier(100, 200, 5, 5, out _));
        }

        [Fact]
        public void TryGetAllTierPrices_ReturnsTrueAndAllPrices()
        {
            Assert.True(_quantizer.TryGetAllTierPrices(100, 200, 5, out var prices));

            Assert.Equal(new long[] { 100, 125, 150, 175, 200 }, prices);
        }

        [Fact]
        public void IsPriceValid_BehavesAsExpected()
        {
            Assert.True(_quantizer.IsPriceValid(100, 200, 5, 100));
            Assert.True(_quantizer.IsPriceValid(100, 200, 5, 125));
            Assert.True(_quantizer.IsPriceValid(100, 200, 5, 100));
            Assert.True(_quantizer.IsPriceValid(100, 200, 5, 200));

            Assert.False(_quantizer.IsPriceValid(100, 200, 5, 113));
            Assert.False(_quantizer.IsPriceValid(100, 200, 5, 101));
            Assert.False(_quantizer.IsPriceValid(100, 200, 5, 199));
            Assert.False(_quantizer.IsPriceValid(100, 200, 5, -100), "price should be > 0");
            Assert.False(_quantizer.IsPriceValid(100, 200, 5, 0), "price should be > 0");
            Assert.False(_quantizer.IsPriceValid(0, 0, 5, 0), "min and max price should be > 0");
            Assert.False(_quantizer.IsPriceValid(0, 0, 0, 0), "min price, max price, price and tierCount should be > 0");
            Assert.False(_quantizer.IsPriceValid(1, 2, 0, 0), "price and tierCount should be > 0");
            Assert.False(_quantizer.IsPriceValid(1, 2, 1, 0), "price should be > 0");
            Assert.False(_quantizer.IsPriceValid(1, 2, 1, 1), "if min != max there should be at least 2 tiers (1 was provided)");
        }

        [Fact]
        public void TryRoundToNearestTier_RoundsAwayFromZeroAndClamps()
        {
            Assert.True(_quantizer.TryRoundToNearestTier(100, 200, 5, 100, out var r1));
            Assert.Equal(100, r1);

            Assert.True(_quantizer.TryRoundToNearestTier(100, 200, 5, 112, out var r2));
            Assert.Equal(100, r2);

            Assert.True(_quantizer.TryRoundToNearestTier(100, 200, 5, 113, out var r3));
            Assert.Equal(125, r3);

            Assert.True(_quantizer.TryRoundToNearestTier(100, 200, 5, 1000, out var r4));
            Assert.Equal(200, r4);

            Assert.True(_quantizer.TryRoundToNearestTier(100, 200, 5, -1000, out var r5));
            Assert.Equal(100, r5);
        }

        [Fact]
        public void TryMethods_ReturnFalseOnInvalidArguments()
        {
            // negative min price
            Assert.False(_quantizer.TryGetPriceStep(-1, 100, 5, out _));

            // max less than min
            Assert.False(_quantizer.TryGetPriceStep(200, 100, 5, out _));

            // non-positive tier count
            Assert.False(_quantizer.TryGetPriceStep(100, 200, 0, out _));
        }
    }
}
