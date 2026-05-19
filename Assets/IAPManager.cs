using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension; // iOS Apple 확장 사용을 위해 추가

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

    // 디버깅용 토스트 헬퍼 — ToastManager가 아직 생성 전이거나 씬에 없을 수 있으니 null 안전 호출
    void DebugToast(string message)
    {
        Debug.Log("[IAP] " + message);
        if (ToastManager.Instance != null)
        {
            ToastManager.Instance.Show(message);
        }
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
            DebugToast("IAP 초기화 안 됨 — 구매 불가");
            return;
        }

        Product product = storeController.products.WithID(REMOVE_ADS_ID);

        if (product != null && product.availableToPurchase)
        {
            DebugToast($"구매 시도: {product.definition.id} / 가격 {product.metadata.localizedPriceString}");
            storeController.InitiatePurchase(product);
        }
        else
        {
            string reason = product == null ? "상품 정보 없음" : "availableToPurchase=false";
            DebugToast($"상품 구매 불가 ({reason})");
        }
    }

    // 구매 복원
    // iOS: 반드시 IAppleExtensions.RestoreTransactions()를 호출해야 영수증이 다시 발급되어
    //      ProcessPurchase가 재호출됨. 호출하지 않으면 앱 재설치 시 복원 불가.
    // Android: hasReceipt가 영구 보존되므로 로컬 체크로 충분.
    public void RestorePurchase()
    {
        if (storeController == null)
        {
            DebugToast("IAP 초기화 안 됨 — 복원 불가");
            return;
        }

#if UNITY_IOS && !UNITY_EDITOR
        // iOS는 Apple StoreKit에 트랜잭션 복원 요청
        var apple = storeExtensionProvider != null ? storeExtensionProvider.GetExtension<IAppleExtensions>() : null;
        if (apple != null)
        {
            DebugToast("iOS StoreKit 복원 요청 중...");
            // Unity IAP 5.x: RestoreTransactions 콜백 시그니처가 Action<bool, string>으로 변경됨
            //   success: 복원 요청 성공 여부 (실제 영수증 존재 여부와는 별개)
            //   error: 실패 시 에러 메시지 (성공 시 null/빈 문자열)
            // 복원된 영수증은 ProcessPurchase 콜백에서 별도로 처리됨
            apple.RestoreTransactions((success, error) =>
            {
                string detail = string.IsNullOrEmpty(error) ? "" : " / err=" + error;
                DebugToast("iOS 복원 결과: success=" + success + detail);
            });
        }
        else
        {
            DebugToast("Apple 확장을 가져오지 못함");
        }
#else
        // Android/Editor: 로컬 영수증 존재 여부만 확인
        Product product = storeController.products.WithID(REMOVE_ADS_ID);

        if (product != null && product.hasReceipt)
        {
            PlayerPrefs.SetInt("NO_ADS", 1);
            PlayerPrefs.Save();
            DebugToast("광고 제거 복원 완료");
        }
        else
        {
            DebugToast("복원할 영수증 없음");
        }
#endif
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        if (args.purchasedProduct.definition.id == REMOVE_ADS_ID)
        {
            PlayerPrefs.SetInt("NO_ADS", 1);
            PlayerPrefs.Save();

            DebugToast("광고 제거 구매 완료");
        }

        return PurchaseProcessingResult.Complete;
    }

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        storeController = controller;
        storeExtensionProvider = extensions;

        // 초기화 시 로컬 영수증 자동 체크 (iOS의 Apple ID 팝업 방지를 위해 StoreKit 호출은 하지 않음)
        // 사용자가 명시적으로 "구매 복원" 버튼을 누를 때만 RestorePurchase()로 StoreKit 호출
        Product cachedProduct = storeController.products.WithID(REMOVE_ADS_ID);
        bool hasReceipt = cachedProduct != null && cachedProduct.hasReceipt;
        if (hasReceipt)
        {
            PlayerPrefs.SetInt("NO_ADS", 1);
            PlayerPrefs.Save();
        }

        DebugToast($"IAP 초기화 완료 (영수증 보유: {hasReceipt})");
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        DebugToast("IAP 초기화 실패: " + error);
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        DebugToast("IAP 초기화 실패: " + error + " / " + message);
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        DebugToast("구매 실패: " + product.definition.id + " / " + failureReason);
    }
}