// UIImpactFeedbackGenerator, called from Haptics.cs. Handheld.Vibrate() — Unity's built-in
// path — plays iOS's legacy AudioServices "system sound" buzz, which is inconsistent on the
// Taptic Engine iPhones this game ships on: some models and iOS versions never fire it at all.
// This is the API Apple has actually meant for in-app haptics since iOS 10.
#import <UIKit/UIKit.h>

extern "C" {

void _SliceBlastHapticImpact(int style)
{
    UIImpactFeedbackStyle feedbackStyle;

    switch (style)
    {
        case 0:
            feedbackStyle = UIImpactFeedbackStyleLight;
            break;
        case 1:
            feedbackStyle = UIImpactFeedbackStyleMedium;
            break;
        default:
            feedbackStyle = UIImpactFeedbackStyleHeavy;
            break;
    }

    // Generators are meant to be short-lived: create, prepare (spins up the Taptic Engine so
    // the impact isn't the first thing waking it), fire, and let it go.
    UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:feedbackStyle];
    [generator prepare];
    [generator impactOccurred];
}

}
