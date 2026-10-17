namespace CozyFishing.Monetization
{
    /// <summary>Official Google sample units. Replace all IDs and disable test mode before monetizing.</summary>
    public static class AdMobConfig
    {
        public static bool UseGoogleTestAds => true;
        public const string AndroidInterstitial = "ca-app-pub-3940256099942544/1033173712";
        public const string AndroidRewarded = "ca-app-pub-3940256099942544/5224354917";
        public const double CacheLifetimeSeconds = 50 * 60;
    }
}
