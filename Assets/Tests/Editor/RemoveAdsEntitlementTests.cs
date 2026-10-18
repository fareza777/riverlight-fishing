using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace CozyFishing.Tests
{
    // The game remains in Unity's predefined Assembly-CSharp, so these editor tests
    // use reflection in the same way as the existing monetization policy tests.
    public sealed class RemoveAdsEntitlementTests
    {
        static Type GameType(string name)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("CozyFishing.Monetization." + name))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, $"The game must implement {name}.");
            return type;
        }

        static object NewEntitlement() => Activator.CreateInstance(GameType("RemoveAdsEntitlement"));

        static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);

        static object Call(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name).Invoke(target, args);

        [Test]
        public void NewEntitlementAllowsAds()
        {
            var entitlement = NewEntitlement();
            Assert.That(Property(entitlement, "Owned"), Is.False);
            Assert.That(Property(entitlement, "AdsAllowed"), Is.True);
        }

        [Test]
        public void GrantingEntitlementDisablesAds()
        {
            var entitlement = NewEntitlement();
            Assert.That(Call(entitlement, "Grant"), Is.True);
            Assert.That(Property(entitlement, "Owned"), Is.True);
            Assert.That(Property(entitlement, "AdsAllowed"), Is.False);
        }

        [Test]
        public void GrantingTwiceIsIdempotent()
        {
            var entitlement = NewEntitlement();
            Call(entitlement, "Grant");
            Assert.That(Call(entitlement, "Grant"), Is.False);
            Assert.That(Property(entitlement, "Owned"), Is.True);
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void RestoringEntitlementReconstructsAdState(bool owned, bool adsAllowed)
        {
            var entitlement = NewEntitlement();
            Call(entitlement, "Restore", owned);
            Assert.That(Property(entitlement, "Owned"), Is.EqualTo(owned));
            Assert.That(Property(entitlement, "AdsAllowed"), Is.EqualTo(adsAllowed));
        }

        [Test]
        public void ProductMetadataMatchesTheStoreListing()
        {
            var productType = GameType("RemoveAdsProduct");
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            Assert.That(productType.GetField("Id", flags).GetValue(null), Is.EqualTo("remove_ads"));
            Assert.That(productType.GetField("PriceUsd", flags).GetValue(null), Is.EqualTo(4.99m));
            Assert.That(productType.GetField("PriceLabel", flags).GetValue(null), Is.EqualTo("$4.99"));
        }
    }
}
