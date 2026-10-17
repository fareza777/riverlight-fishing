using System.Linq;
using CozyFishing.Audio;
using CozyFishing.Core;
using CozyFishing.Data;
using CozyFishing.Monetization;
using UnityEngine;
using UnityEngine.UI;

namespace CozyFishing.UI
{
    /// <summary>Dockside market: sell the creel, buy rods, bait and bigger creels.</summary>
    public sealed class ShopUI : MonoBehaviour
    {
        static ShopUI instance;
        CanvasGroup group;
        RectTransform page;
        float oldScale = 1f;
        Button rewardedButton;
        Text rewardedStatus;
        AdManager ads;

        void OnEnable() { ads = AdManager.Instance; ads.StateChanged += RefreshAdOffer; }
        void OnDisable() { if (ads != null) ads.StateChanged -= RefreshAdOffer; }

        public static bool IsOpen => instance != null && instance.group.alpha > 0.5f;

        /// <summary>True while showing Felix's wagon instead of Mira's market.</summary>
        public static bool Merchant { get; private set; }

        public static void Open() => Open(false);

        public static void Open(bool merchant)
        {
            Merchant = merchant;
            if (instance == null) { instance = new GameObject("ShopUI").AddComponent<ShopUI>(); instance.Build(); }
            instance.oldScale = Time.timeScale;
            Time.timeScale = 0f;
            instance.group.alpha = 1f; instance.group.blocksRaycasts = instance.group.interactable = true;
            SfxPlayer.Play("shop_bell", 0.7f, 0f);
            instance.Refresh();
        }

        void Close()
        {
            if (AdManager.IsPresenting) return;
            group.alpha = 0f; group.blocksRaycasts = group.interactable = false;
            Time.timeScale = oldScale <= 0f ? 1f : oldScale;
        }

        void Update()
        {
            if (group.alpha > 0.5f && GameInput.Instance != null && GameInput.Instance.CancelPressed) Close();
        }

        void Build()
        {
            var canvas = UIKit.Canvas(transform, "Shop Canvas", 210);
            var root = (RectTransform)canvas.transform;
            group = canvas.gameObject.AddComponent<CanvasGroup>();
            var dim = UIKit.Image(root, "Dim", UIKit.Dim); UIKit.Fill(dim); dim.raycastTarget = true;
            var book = UIKit.Panel(root, "Shop", UIKit.Paper);
            UIKit.Anchor(book.rectTransform, Vector2.zero, Vector2.one, new Vector2(30, 60), new Vector2(-30, -60));
            book.raycastTarget = true;
            var title = UIKit.Label(book.rectTransform, "Title", "", 50, UIKit.Ink, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            title.name = "Title";
            UIKit.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -130), new Vector2(-140, -24));
            UIKit.Button(book.rectTransform, "×", new Vector2(1, 1), new Vector2(-20, -24), new Vector2(92, 92), Close).GetComponentInChildren<Text>().fontSize = 54;
            page = UIKit.Rect(book.rectTransform, "Page");
            UIKit.Anchor(page, Vector2.zero, Vector2.one, new Vector2(30, 30), new Vector2(-30, -150));
        }

        void Refresh()
        {
            var gs = GameState.Instance;
            var d = gs.Data;
            transform.GetComponentsInChildren<Text>(true).First(t => t.name == "Title").text = $"{Loc.T(Merchant ? "merchant_title" : "market")}   <size=40>{UIKit.Coins(d.coins)}</size>";
            foreach (Transform c in page) Destroy(c.gameObject);
            var content = UIKit.ScrollList(page, "Items", 14f, out var scroll);
            UIKit.Anchor((RectTransform)scroll.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            if (Merchant)
            {
                Section(content, Loc.T("merchant_stock"), null);
                return;
            }

            // Sell
            int value = d.creel.Sum(c => GameState.Db.Fish(c.fishId)?.PriceFor(c.sizeCm) ?? 0);
            var sellRow = UIKit.Panel(content, "Sell", Color.white);
            sellRow.rectTransform.sizeDelta = new Vector2(0, 150);
            var st = UIKit.Label(sellRow.rectTransform, "Text", $"<b>{Loc.T("sell_creel")}</b>\n<size=26>{Loc.T("creel_contents", d.creel.Count, gs.CreelCapacity)}  ·  {UIKit.Coins(value)}</size>", 32, UIKit.Ink, TextAnchor.MiddleLeft);
            UIKit.Anchor(st.rectTransform, Vector2.zero, Vector2.one, new Vector2(24, 0), new Vector2(-250, 0));
            var sb = UIKit.Button(sellRow.rectTransform, Loc.T("sell_all"), new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(220, 90),
                d.creel.Count > 0 ? () => { int got = gs.SellCreel(); SfxPlayer.Play("coins", 1f, 0f); Toast.Show(Loc.T("sold", got), 2.5f); Refresh(); } : null);
            sb.interactable = d.creel.Count > 0;

            if (AdManager.Supported) RewardOffer(content);
            Orders(content);
            Section(content, Loc.T("rods"), ItemType.Rod);
            Section(content, Loc.T("bait"), ItemType.Bait);
            Section(content, Loc.T("creels"), ItemType.Creel);
            Section(content, Loc.T("decor"), ItemType.Decor);
            Section(content, Loc.T("outfits"), ItemType.Outfit);
        }

        void RewardOffer(RectTransform content)
        {
            var row = UIKit.Panel(content, "Optional ad reward", Color.white);
            row.rectTransform.sizeDelta = new Vector2(0, 210);
            var title = UIKit.Label(row.rectTransform, "Title", "<b>" + Loc.T("ad_reward_title", GameState.AdRewardCoins) + "</b>", 32, UIKit.Teal, TextAnchor.UpperLeft);
            UIKit.Anchor(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(24, 100), new Vector2(-24, -18));
            rewardedStatus = UIKit.Label(row.rectTransform, "Status", "", 25, UIKit.InkSoft, TextAnchor.MiddleLeft);
            UIKit.Anchor(rewardedStatus.rectTransform, Vector2.zero, Vector2.one, new Vector2(24, 18), new Vector2(-350, -90));
            rewardedButton = UIKit.Button(row.rectTransform, Loc.T("ad_watch"), new Vector2(1, 0), new Vector2(-16, 22), new Vector2(310, 86), WatchReward);
            rewardedButton.GetComponentInChildren<Text>().fontSize = 28;
            RefreshAdOffer();
        }

        void RefreshAdOffer()
        {
            if (rewardedButton == null || rewardedStatus == null) return;
            bool allowed = GameState.Instance.CanClaimAdReward;
            var ads = AdManager.Instance;
            rewardedButton.interactable = allowed && ads.RewardedReady;
            rewardedStatus.text = Loc.T(!allowed ? "ad_daily_limit" : ads.RewardedCoolingDown ? "ad_wait"
                : ads.RewardedReady ? "ad_reward_hint" : "ad_unavailable");
        }

        void WatchReward()
        {
            var gs = GameState.Instance;
            if (!gs.CanClaimAdReward) { RefreshAdOffer(); return; }
            bool earned = false;
            if (!AdManager.Instance.TryShowRewarded(() => earned = gs.ClaimAdReward(), () =>
            {
                if (this == null) return;
                if (earned)
                {
                    SfxPlayer.Play("coins", 0.8f, 0f);
                    Toast.Show(Loc.T("ad_reward_received", GameState.AdRewardCoins), 3f);
                }
                else Toast.Show(Loc.T("ad_not_completed"), 2.5f);
                Refresh();
            })) Toast.Show(Loc.T("ad_unavailable"), 2.5f);
            RefreshAdOffer();
        }

        void Orders(RectTransform content)
        {
            var h = UIKit.Label(content, "Header", $"<b>{Loc.T("orders")}</b>", 34, UIKit.Teal, TextAnchor.LowerLeft);
            h.rectTransform.sizeDelta = new Vector2(0, 70);
            var orders = GameState.Instance.Data.orders;
            if (orders.Count == 0)
            {
                var none = UIKit.Label(content, "None", Loc.T("orders_empty"), 28, UIKit.InkSoft, TextAnchor.MiddleLeft);
                none.rectTransform.sizeDelta = new Vector2(0, 60);
                return;
            }
            foreach (var order in orders.ToList())
            {
                var fish = GameState.Db.Fish(order.fishId);
                if (fish == null) continue;
                var row = UIKit.Panel(content, "Order", Color.white);
                row.rectTransform.sizeDelta = new Vector2(0, 150);
                if (fish.Illustration != null)
                {
                    var art = UIKit.Image(row.rectTransform, "Art", Color.white, fish.Illustration);
                    art.preserveAspect = true;
                    UIKit.Place(art.rectTransform, new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(150, 110));
                }
                int have = Progression.InCreel(order.fishId);
                var text = UIKit.Label(row.rectTransform, "Text",
                    $"<b>{order.count}× {fish.DisplayName}</b>\n<size=26>{Loc.T("reward", UIKit.Coins(order.reward))}  ·  {Loc.T("in_creel", have, order.count)}</size>",
                    30, UIKit.Ink, TextAnchor.MiddleLeft);
                UIKit.Anchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(180, 0), new Vector2(-250, 0));
                bool ready = Progression.CanDeliver(order);
                var b = UIKit.Button(row.rectTransform, Loc.T("deliver"), new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(220, 90),
                    ready ? () =>
                    {
                        Progression.Deliver(order);
                        SfxPlayer.Play("coins", 1f, 0f);
                        Toast.Show(Loc.T("delivered", order.reward), 3f);
                        Audio.VoicePlayer.Say("mira_thanks");
                        FindAnyObjectByType<World.NpcMira>()?.Cheer();
                        Refresh();
                    } : null);
                b.interactable = ready;
            }
        }

        void Section(RectTransform content, string header, ItemType? type)
        {
            var gs = GameState.Instance;
            var h = UIKit.Label(content, "Header", $"<b>{header}</b>", 34, UIKit.Teal, TextAnchor.LowerLeft);
            h.rectTransform.sizeDelta = new Vector2(0, 70);
            string hero = gs.Data.character;
            var items = GameState.Db.items.Where(i => (i.price > 0 || gs.Owns(i.id)) && i.merchantOnly == Merchant
                                                      && (type == null || i.type == type)
                                                      && (i.type != ItemType.Outfit || i.forCharacter == hero)).OrderBy(i => i.price).ToList();
            if (items.Count == 0) { Object.Destroy(h.gameObject); return; }
            foreach (var item in items)
            {
                bool owned = item.type != ItemType.Bait && gs.Owns(item.id);
                bool equipped = item.id == gs.Data.rod || item.id == gs.Data.creelItem || item.id == gs.Data.outfit;
                string action; UnityEngine.Events.UnityAction click = null;
                if (owned && item.type == ItemType.Decor) action = Loc.T("in_cabin");
                else if (owned && equipped) action = Loc.T("equipped");
                else if (owned) { action = Loc.T("equip"); click = () => { gs.Equip(item); Refresh(); }; }
                else
                {
                    action = Loc.T("buy");
                    if (gs.Data.coins >= item.price)
                        click = () =>
                        {
                            if (gs.Buy(item))
                            {
                                SfxPlayer.Play("coins", 0.8f, 0f);
                                Toast.Show(Loc.T(item.type == ItemType.Decor ? "bought_decor" : "bought", item.Name), 2.5f);
                                Player.PlayerAvatar.CelebrateLocal();
                            }
                            Refresh();
                        };
                }
                PauseMenu.ItemRow(content, item, action, click, owned ? null : UIKit.Coins(item.price));
            }
        }
    }
}
