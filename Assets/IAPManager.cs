using UnityEngine;
using UnityEngine.Purchasing;

public class IAPManager : MonoBehaviour, IStoreListener
{
    public static IAPManager Instance;

    private static IStoreController storeController;
    private static IExtensionProvider storeExtensionProvider;

    private const string REMOVE_ADS_ID = "remove_ads";

    public bool IsNoAds
    {
        get { return PlayerPrefs.GetInt("NO_ADS", 0) == 1; }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePurchasing();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void InitializePurchasing()
    {
        if (storeController != null) return;

        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());

        builder.AddProduct(REMOVE_ADS_ID, ProductType.NonConsumable);

        UnityPurchasing.Initialize(this, builder);
    }

    public void BuyRemoveAds()
    {
        if (storeController == null)
        {
            Debug.Log("IAP 초기화 안 됨");
            return;
        }

        Product product = storeController.products.WithID(REMOVE_ADS_ID);

        if (product != null && product.availableToPurchase)
        {
            storeController.InitiatePurchase(product);
        }
        else
        {
            Debug.Log("상품 구매 불가");
        }
    }

    public void RestorePurchase()
    {
        if (storeController == null) return;

        Product product = storeController.products.WithID(REMOVE_ADS_ID);

        if (product != null && product.hasReceipt)
        {
            PlayerPrefs.SetInt("NO_ADS", 1);
            PlayerPrefs.Save();
            Debug.Log("광고 제거 복원 완료");
        }
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        if (args.purchasedProduct.definition.id == REMOVE_ADS_ID)
        {
            PlayerPrefs.SetInt("NO_ADS", 1);
            PlayerPrefs.Save();

            Debug.Log("광고 제거 구매 완료");
        }

        return PurchaseProcessingResult.Complete;
    }

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        storeController = controller;
        storeExtensionProvider = extensions;

        RestorePurchase();

        Debug.Log("IAP 초기화 완료");
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        Debug.Log("IAP 초기화 실패: " + error);
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        Debug.Log("IAP 초기화 실패: " + error + " / " + message);
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        Debug.Log("구매 실패: " + product.definition.id + " / " + failureReason);
    }
}