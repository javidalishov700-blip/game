// The leaderboard, through GameKit's current API (iOS 14+), called from GameCenterNative.cs.
//
// Unity's Social API loads scores and posts them through GKLeaderboard / GKScore calls
// Apple deprecated in iOS 14, and it answers any failure with a bare `false` — the player is
// told "couldn't reach Game Center" and nobody, including the developer, learns whether the
// leaderboard is missing, the game is not yet recognised, or the network dropped. These calls
// use the supported API and hand back GameKit's own error domain and code, which the
// leaderboard screen prints in small type.
//
// Every answer is one JSON string, delivered on the main thread (where Unity wants it).
#import <Foundation/Foundation.h>
#import <GameKit/GameKit.h>

typedef void (*SliceBlastStringCallback)(const char *json);

static void SliceBlastDeliver(SliceBlastStringCallback callback, NSDictionary *object)
{
    if (callback == NULL)
    {
        return;
    }

    NSData *data = [NSJSONSerialization dataWithJSONObject:object options:0 error:nil];
    NSString *json = data != nil
        ? [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding]
        : @"{\"ok\":false,\"error\":\"json\",\"code\":0}";

    // GameKit completes on whatever queue it likes; the block keeps `json` alive until the
    // callback has copied the characters out.
    dispatch_async(dispatch_get_main_queue(), ^{
        callback([json UTF8String]);
    });
}

static NSDictionary *SliceBlastFailure(NSString *stage, NSError *error)
{
    return @{
        @"ok": @NO,
        @"error": stage,
        @"code": @(error != nil ? (int)error.code : 0),
        @"entries": @[]
    };
}

extern "C" {

// Ranks 1..count of the all-time global board, plus the local player's own entry.
void SliceBlastGC_LoadTop(const char *leaderboardId, int count, SliceBlastStringCallback callback)
{
    NSString *identifier = leaderboardId != NULL ? [NSString stringWithUTF8String:leaderboardId] : @"";

    if (@available(iOS 14.0, *))
    {
        if (!GKLocalPlayer.localPlayer.isAuthenticated)
        {
            SliceBlastDeliver(callback, SliceBlastFailure(@"not-signed-in", nil));
            return;
        }

        [GKLeaderboard loadLeaderboardsWithIDs:@[identifier] completionHandler:^(NSArray<GKLeaderboard *> *leaderboards, NSError *error)
        {
            if (error != nil)
            {
                SliceBlastDeliver(callback, SliceBlastFailure(@"find", error));
                return;
            }

            GKLeaderboard *board = leaderboards.firstObject;

            if (board == nil)
            {
                // Game Center answered, but has no leaderboard with this ID for this game.
                SliceBlastDeliver(callback, SliceBlastFailure(@"not-found", nil));
                return;
            }

            NSUInteger length = count > 0 ? (NSUInteger)count : 1;

            [board loadEntriesForPlayerScope:GKLeaderboardPlayerScopeGlobal
                                   timeScope:GKLeaderboardTimeScopeAllTime
                                       range:NSMakeRange(1, length)
                           completionHandler:^(GKLeaderboardEntry *localEntry, NSArray<GKLeaderboardEntry *> *entries, NSInteger total, NSError *entriesError)
            {
                if (entriesError != nil)
                {
                    SliceBlastDeliver(callback, SliceBlastFailure(@"entries", entriesError));
                    return;
                }

                NSString *localId = GKLocalPlayer.localPlayer.gamePlayerID;
                NSMutableArray *rows = [NSMutableArray arrayWithCapacity:entries.count];

                for (GKLeaderboardEntry *entry in entries)
                {
                    NSString *playerId = entry.player.gamePlayerID != nil ? entry.player.gamePlayerID : @"";
                    NSString *name = entry.player.displayName != nil ? entry.player.displayName : @"";

                    [rows addObject:@{
                        @"rank": @((int)entry.rank),
                        @"name": name,
                        @"score": @((long long)entry.score),
                        @"id": playerId,
                        @"local": @([playerId isEqualToString:localId])
                    }];
                }

                NSMutableDictionary *result = [NSMutableDictionary dictionaryWithDictionary:@{
                    @"ok": @YES,
                    @"error": @"",
                    @"code": @0,
                    @"entries": rows,
                    @"hasLocal": @(localEntry != nil)
                }];

                if (localEntry != nil)
                {
                    result[@"localRank"] = @((int)localEntry.rank);
                    result[@"localScore"] = @((long long)localEntry.score);
                }

                SliceBlastDeliver(callback, result);
            }];
        }];
    }
    else
    {
        SliceBlastDeliver(callback, SliceBlastFailure(@"ios-version", nil));
    }
}

// Posts a score. The board keeps the best one (the leaderboard's own setting).
void SliceBlastGC_Submit(const char *leaderboardId, long long score, SliceBlastStringCallback callback)
{
    NSString *identifier = leaderboardId != NULL ? [NSString stringWithUTF8String:leaderboardId] : @"";

    if (@available(iOS 14.0, *))
    {
        if (!GKLocalPlayer.localPlayer.isAuthenticated)
        {
            SliceBlastDeliver(callback, SliceBlastFailure(@"not-signed-in", nil));
            return;
        }

        [GKLeaderboard submitScore:(NSInteger)score
                           context:0
                            player:GKLocalPlayer.localPlayer
                    leaderboardIDs:@[identifier]
                 completionHandler:^(NSError *error)
        {
            if (error != nil)
            {
                SliceBlastDeliver(callback, SliceBlastFailure(@"submit", error));
            }
            else
            {
                SliceBlastDeliver(callback, @{ @"ok": @YES, @"error": @"", @"code": @0, @"entries": @[] });
            }
        }];
    }
    else
    {
        SliceBlastDeliver(callback, SliceBlastFailure(@"ios-version", nil));
    }
}

}
