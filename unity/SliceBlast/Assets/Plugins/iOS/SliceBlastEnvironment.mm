// Tells the game whether it is running from TestFlight (or any other sandbox install) rather
// than from the App Store. A TestFlight install's App Store receipt is named "sandboxReceipt";
// an App Store install's is "receipt". Used only to keep the developer test-ads switch out of
// reach of real players.
#import <Foundation/Foundation.h>

extern "C" {

int _SliceBlastIsSandboxInstall()
{
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
    NSURL *receipt = [[NSBundle mainBundle] appStoreReceiptURL];
#pragma clang diagnostic pop
    return (receipt != nil && [[receipt lastPathComponent] isEqualToString:@"sandboxReceipt"]) ? 1 : 0;
}

}
