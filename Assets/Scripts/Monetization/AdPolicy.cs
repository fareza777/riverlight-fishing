using System;

namespace CozyFishing.Monetization
{
    /// <summary>Foreground-session limits; only explicit location travel counts as an opportunity.</summary>
    public sealed class InterstitialPolicy
    {
        int travels, impressions;
        public double LastClosedTime { get; private set; } = double.NegativeInfinity;

        public void RecordTravel() => travels++;
        public bool CanShow(double seconds) => seconds >= 120 && travels >= 3 && impressions < 4
            && seconds - LastClosedTime >= 180;

        public void RecordClosed(double seconds, bool interstitial)
        {
            LastClosedTime = seconds;
            if (!interstitial) return;
            travels = 0;
            impressions++;
        }
    }

    [Serializable]
    public sealed class DailyAdRewards
    {
        public int day, claimed;
        public bool CanClaim(int currentDay) => day != currentDay || claimed < 3;

        public bool TryClaim(int currentDay)
        {
            if (!CanClaim(currentDay)) return false;
            if (day != currentDay) { day = currentDay; claimed = 0; }
            claimed++;
            return true;
        }
    }

    /// <summary>Only the SDK's earned-reward event may call Earn; closing a video is not a reward.</summary>
    public sealed class RewardedClaim
    {
        public bool Earned { get; private set; }
        public void Earn(Action grant)
        {
            if (Earned) return;
            Earned = true;
            grant?.Invoke();
        }
    }
}
