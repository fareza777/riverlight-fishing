namespace CozyFishing.Monetization
{
    /// <summary>Riverlight production ad units created in the F7 Developer AdMob account.</summary>
    public static class AdMobConfig
    {
        public static bool UseGoogleTestAds => false;
        public const string AndroidInterstitial = "ca-app-pub-6279186647593327/2624107914";
        public const string AndroidRewarded = "ca-app-pub-6279186647593327/3803573628";
        public const double CacheLifetimeSeconds = 50 * 60;
    }
}
