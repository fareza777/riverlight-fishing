namespace CozyFishing.Monetization
{
    /// <summary>Small, store-independent entitlement used by ads and the purchase flow.</summary>
    public sealed class RemoveAdsEntitlement
    {
        public bool Owned { get; private set; }
        public bool AdsAllowed => !Owned;

        /// <returns>True only the first time the entitlement is granted.</returns>
        public bool Grant()
        {
            if (Owned) return false;
            Owned = true;
            return true;
        }

        public void Restore(bool owned) => Owned = owned;
    }

    public static class RemoveAdsProduct
    {
        public const string Id = "remove_ads";
        public const decimal PriceUsd = 4.99m;
        public const string PriceLabel = "$4.99";
        internal const string PrefKey = "remove_ads_entitlement_v1";
    }
}
