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
    void _RequestATTAuthorization() {
        if (@available(iOS 14, *)) {
            [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status) {
                // 사용자 응답 결과는 _GetATTAuthorizationStatus()로 폴링하여 확인
            }];
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
}
