using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;

namespace CozyFishing.Monetization
{
    /// <summary>
    /// Owns the lifetime Remove Ads purchase. The local entitlement is restored immediately,
    /// then reconciled with Google Play through Unity IAP 5 when the store connects.
    /// </summary>
    [DefaultExecutionOrder(-250)]
    public sealed class RemoveAdsPurchase : MonoBehaviour
    {
        static RemoveAdsPurchase instance;

        public static RemoveAdsPurchase Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("Remove Ads Purchase");
                    instance = go.AddComponent<RemoveAdsPurchase>();
                }

                return instance;
            }
        }

        public static bool IsOwned => instance != null && instance.entitlement.Owned;
        public static bool AdsAllowed => instance == null || instance.entitlement.AdsAllowed;
        public bool StoreReady => controller != null;
        public event Action StateChanged;

        readonly RemoveAdsEntitlement entitlement = new();
        StoreController controller;
        bool initializing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() => _ = Instance;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            entitlement.Restore(PlayerPrefs.GetInt(RemoveAdsProduct.PrefKey, 0) == 1);
        }

        void Start() => InitializePurchasing();

        public async void InitializePurchasing()
        {
            if (initializing || controller != null || IsOwned)
                return;

            initializing = true;
            try
            {
                controller = UnityIAPServices.StoreController();
                controller.OnPurchasePending += OnPurchasePending;
                controller.OnPurchaseConfirmed += OnPurchaseConfirmed;
                controller.OnPurchaseFailed += OnPurchaseFailed;
                controller.OnPurchasesFetched += OnPurchasesFetched;
                controller.OnPurchasesFetchFailed += OnPurchasesFetchFailed;

                await controller.Connect();
                controller.FetchProducts(new List<ProductDefinition>
                {
                    new(RemoveAdsProduct.Id, ProductType.NonConsumable)
                });
                controller.FetchPurchases();
            }
            catch (Exception error)
            {
                controller = null;
                Debug.LogWarning($"[IAP] Initialization failed: {error.Message}");
                StateChanged?.Invoke();
            }
            finally
            {
                initializing = false;
            }
        }

        public void BuyRemoveAds()
        {
            if (IsOwned)
                return;

            if (controller == null)
            {
                InitializePurchasing();
                return;
            }

            var product = controller.GetProducts()
                .FirstOrDefault(candidate => candidate.definition.id == RemoveAdsProduct.Id);
            if (product != null)
                controller.PurchaseProduct(product);
            else
                Debug.LogWarning($"[IAP] Product is not available yet: {RemoveAdsProduct.Id}");
        }

        void OnPurchasePending(PendingOrder order)
        {
            GrantOrder(order);
            controller?.ConfirmPurchase(order);
        }

        void OnPurchaseConfirmed(Order order)
        {
            if (order is ConfirmedOrder)
                Debug.Log($"[IAP] Purchase completed: {DescribeOrder(order)}");
            else if (order is FailedOrder failed)
                Debug.LogWarning($"[IAP] Purchase confirmation failed: {failed.FailureReason} {failed.Details}");

            StateChanged?.Invoke();
        }

        void OnPurchasesFetched(Orders orders)
        {
            foreach (var order in orders.PendingOrders)
                GrantOrder(order);

            foreach (var order in orders.ConfirmedOrders)
                GrantOrder(order);

            StateChanged?.Invoke();
        }

        void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
        {
            Debug.LogWarning($"[IAP] Existing purchases could not be restored: {failure.Message}");
            StateChanged?.Invoke();
        }

        void OnPurchaseFailed(FailedOrder failure)
        {
            Debug.LogWarning($"[IAP] Purchase failed: {DescribeOrder(failure)} ({failure.FailureReason}) {failure.Details}");
            StateChanged?.Invoke();
        }

        void GrantOrder(Order order)
        {
            if (order?.CartOrdered == null)
                return;

            foreach (var item in order.CartOrdered.Items())
            {
                if (item?.Product?.definition?.id == RemoveAdsProduct.Id)
                    GrantEntitlement();
            }
        }

        void GrantEntitlement()
        {
            if (!entitlement.Grant())
                return;

            PlayerPrefs.SetInt(RemoveAdsProduct.PrefKey, 1);
            PlayerPrefs.Save();
            StateChanged?.Invoke();
            AdManager.Instance.NotifyEntitlementChanged();
        }

        static string DescribeOrder(Order order)
        {
            var item = order?.CartOrdered?.Items()?.FirstOrDefault();
            return item?.Product?.definition?.id ?? "unknown";
        }
    }
}
