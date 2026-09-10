// StoreKit, behind the same kind of scripting define the ads plugin sits behind. Unity IAP
// is a package, not a built-in: without it the UnityEngine.Purchasing namespace does not
// exist, and an unguarded reference would fail to compile the moment it landed — breaking
// rebuilds of the version already in review for a feature that was not ready.
//
// With SLICEBLAST_IAP_ENABLED off, everything below is an inert stub: the shop's buttons
// still draw, a tap reports failure, and nothing else changes. Turning it on is two steps —
// install "In-App Purchasing" from the Package Manager, then let SliceBlastBuild bake the
// define — which is exactly the sequence the ads plugin already went through.
//
// Products, which must match App Store Connect character for character:
//   com.javidalishov.sliceblast.removeads       non-consumable
//   com.javidalishov.sliceblast.coins.small     consumable
//   com.javidalishov.sliceblast.coins.medium    consumable
//   com.javidalishov.sliceblast.coins.large     consumable
using System;
using SliceBlast.Meta;
using SliceBlast.UI;
using UnityEngine;
#if SLICEBLAST_IAP_ENABLED
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
#endif

namespace SliceBlast.Store
{
    public sealed class IapManager : MonoBehaviour
#if SLICEBLAST_IAP_ENABLED
        , IDetailedStoreListener
#endif
    {
        public const string RemoveAdsProductId = "com.javidalishov.sliceblast.removeads";

        private static IapManager _instance;

        /// <summary>Creates the singleton on first use; never returns null.</summary>
        public static IapManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject host = new GameObject("IapManager");
                    _instance = host.AddComponent<IapManager>();
                    DontDestroyOnLoad(host);
                    _instance.Initialize();
                }

                return _instance;
            }
        }

        /// <summary>
        /// The manager if one already exists, otherwise null. Unsubscribing during teardown
        /// has to go through this rather than Instance: touching Instance while the app is
        /// quitting would construct a GameObject Unity is in the middle of tearing down.
        /// </summary>
        public static IapManager Existing => _instance;

        /// <summary>True when a purchase succeeded, false when it failed or was cancelled.</summary>
        public event Action<bool> PurchaseFinished;

        /// <summary>A product's localised price string, once the catalogue has loaded.</summary>
        // Nothing raises this in the stubbed build — there is no catalogue to read prices
        // from — and an unraised event is CS0067. Silenced rather than faked.
#pragma warning disable 67
        public event Action<string, string> PriceResolved;
#pragma warning restore 67

        public bool IsReady { get; private set; }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Grants what a product is worth. Shared by the live purchase path and by restore,
        /// so a restored non-consumable takes exactly the same route as a fresh one.
        /// </summary>
        private void Grant(string productId)
        {
            if (productId == RemoveAdsProductId)
            {
                PlayerProfile.SetAdsRemoved(true);
                PlayerProfile.Flush();
                return;
            }

            for (int i = 0; i < ShopScreen.CoinPackIds.Length; i++)
            {
                if (ShopScreen.CoinPackIds[i] == productId)
                {
                    PlayerProfile.AddCoins(ShopScreen.CoinPackAmounts[i]);
                    PlayerProfile.Flush();
                    return;
                }
            }
        }

#if SLICEBLAST_IAP_ENABLED
        private IStoreController _controller;
        private IExtensionProvider _extensions;
        private bool _initializing;

        private void Initialize()
        {
            if (_initializing || IsReady)
            {
                return;
            }

            _initializing = true;

            ConfigurationBuilder builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            builder.AddProduct(RemoveAdsProductId, ProductType.NonConsumable);

            for (int i = 0; i < ShopScreen.CoinPackIds.Length; i++)
            {
                builder.AddProduct(ShopScreen.CoinPackIds[i], ProductType.Consumable);
            }

            UnityPurchasing.Initialize(this, builder);
        }

        public void Purchase(string productId)
        {
            if (!IsReady || _controller == null)
            {
                // The store never came up — a network failure on a cold launch, or a device
                // with purchases restricted. Reported as a plain failure so the shop plays
                // its "no" and the player is not left waiting on a callback that never comes.
                PurchaseFinished?.Invoke(false);
                return;
            }

            Product product = _controller.products.WithID(productId);

            if (product == null || !product.availableToPurchase)
            {
                PurchaseFinished?.Invoke(false);
                return;
            }

            _controller.InitiatePurchase(product);
        }

        /// <summary>
        /// iOS requires an explicit, user-initiated restore path for non-consumables. On any
        /// other platform this is a no-op that still reports back, so the caller has one shape
        /// of response to handle.
        /// </summary>
        public void Restore()
        {
            if (!IsReady || _extensions == null)
            {
                PurchaseFinished?.Invoke(false);
                return;
            }

            IAppleExtensions apple = _extensions.GetExtension<IAppleExtensions>();

            if (apple == null)
            {
                PurchaseFinished?.Invoke(false);
                return;
            }

            apple.RestoreTransactions((success, error) =>
            {
                // Success here only means the request itself completed; anything actually
                // owned arrives separately through ProcessPurchase, which is what grants it.
                PurchaseFinished?.Invoke(success);
            });
        }

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            _controller = controller;
            _extensions = extensions;
            _initializing = false;
            IsReady = true;

            foreach (Product product in controller.products.all)
            {
                if (product != null && product.metadata != null)
                {
                    PriceResolved?.Invoke(product.definition.id, product.metadata.localizedPriceString);
                }
            }
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            OnInitializeFailed(error, null);
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            _initializing = false;
            IsReady = false;
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            string id = args != null && args.purchasedProduct != null
                ? args.purchasedProduct.definition.id
                : string.Empty;

            Grant(id);
            PurchaseFinished?.Invoke(true);

            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason reason)
        {
            PurchaseFinished?.Invoke(false);
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription description)
        {
            PurchaseFinished?.Invoke(false);
        }
#else
        private void Initialize()
        {
        }

        public void Purchase(string productId)
        {
            PurchaseFinished?.Invoke(false);
        }

        public void Restore()
        {
            PurchaseFinished?.Invoke(false);
        }
#endif
    }
}
