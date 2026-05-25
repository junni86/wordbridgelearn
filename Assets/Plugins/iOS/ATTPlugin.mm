// iOS App Tracking Transparency (ATT) 권한 요청 네이티브 플러그인
// iOS 14.5+ 부터 광고 ID(IDFA) 사용을 위해 반드시 필요
// Unity C#에서 [DllImport("__Internal")]으로 호출

#import <Foundation/Foundation.h>
#import <AppTrackingTransparency/AppTrackingTransparency.h>
#import <AdSupport/AdSupport.h>

extern "C" {

    // ATT 권한 요청 다이얼로그 표시
    // iOS 14 미만에서는 다이얼로그 없이 무시됨 (구버전에는 ATT 개념이 없음)
    // 결과 콜백은 받지 않음 - GMA SDK가 status에 맞춰 자동으로 처리하므로 충분
    //
    // 메인 스레드 보장: ATT 다이얼로그는 UI 작업이므로 반드시 메인 스레드에서 호출해야 함.
    // Unity C# 코루틴은 보통 메인 스레드에서 동작하지만, [DllImport] 호출 경로나 향후 변경에
    // 영향받지 않도록 dispatch_async 로 메인 큐에 명시적으로 디스패치하여 안전성을 확보한다.
    void _RequestATTAuthorization() {
        if (@available(iOS 14, *)) {
            dispatch_async(dispatch_get_main_queue(), ^{
                [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status) {
                    // 사용자 응답 결과는 _GetATTAuthorizationStatus()로 폴링하여 확인
                }];
            });
        }
    }

    // 현재 ATT 권한 상태 반환
    // 0 = NotDetermined (아직 다이얼로그 미응답)
    // 1 = Restricted (자녀보호 등으로 제한됨)
    // 2 = Denied (사용자가 거부함)
    // 3 = Authorized (사용자가 허용함)
    int _GetATTAuthorizationStatus() {
        if (@available(iOS 14, *)) {
            return (int)[ATTrackingManager trackingAuthorizationStatus];
        }
        // iOS 13 이하는 ATT 개념이 없으므로 항상 허용 상태로 취급
        return 3;
    }

    // 앱이 UIApplicationStateActive 상태인지 직접 확인
    // ATT 다이얼로그는 반드시 Active 상태일 때만 표시되며, Unity 의 Application.isFocused 와는
    // 시점이 미세하게 다를 수 있어 네이티브에서 직접 확인하는 게 더 정확하다.
    // 반환값: 1 = Active, 0 = Inactive 또는 Background
    //
    // [UIApplication sharedApplication] 호출은 메인 스레드에서 해야 하므로
    // dispatch_sync 로 메인 큐에서 동기 실행 (이미 메인 스레드면 데드락 회피하여 즉시 실행)
    int _GetApplicationActiveState() {
        __block int result = 0;
        void (^block)(void) = ^{
            if ([UIApplication sharedApplication].applicationState == UIApplicationStateActive) {
                result = 1;
            }
        };
        if ([NSThread isMainThread]) {
            block();
        } else {
            dispatch_sync(dispatch_get_main_queue(), block);
        }
        return result;
    }
}
