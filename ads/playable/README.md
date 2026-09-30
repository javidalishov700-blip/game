# Slice & Blast playable ad

`slice-blast-playable.html` is the whole ad: one file, about 40 KB, no dependencies.

How it plays:

1. **The fail (about 8 seconds).** A hand plays for the viewer and gets it wrong in the most
   annoying way: two perfect drops, one away from a blast ("ONE MORE = BLAST!"), then late, then
   early, the block gets smaller each time, and finally it misses completely. The fall plays in
   slow motion, then "FAILED!" and "YOU CAN DO BETTER?". A tap at any point skips to the next step.
2. **The player's turn.** The goal is always on screen (the dots at the top). Blasts climb
   3, 5, 7, 9, 11 blocks, and each one needs more perfect drops (3, 3, 4, 4, 5), so a good run
   lasts. A missed block gets one retry. The run ends at the 11-block blast, on a second miss,
   or after 75 seconds.
3. **The end card** shows how far up the ladder the player got ("YOU REACHED 7. CAN YOU REACH
   11?") and the PLAY FREE button to the App Store.

## How it hands off to the ad network

The CTA calls, in order: `mraid.open` (AppLovin, Unity, ironSource, Mintegral, most SDKs),
`FbPlayableAd.onCTAClick` (Meta), `playableSDK.openAppStore` (TikTok/Pangle), `ExitApi.exit`
(Google Ads), `dapi.openStoreUrl`, then a plain link. If the page is opened without a network
(a browser), it opens the store link directly.

## Uploading it

Each network wants the file in its own way. Check the network's current playable-ad rules
before uploading, since size limits and required calls change.

- **Google Ads (App campaigns)**: upload an HTML5 asset as a .zip containing the html file. The
  network needs the `ExitApi` script tag in the head; add `<script src="https://tpc.googlesyndication.com/pagead/gadgets/html5/api/exitapi.js"></script>`
  when you build the zip for Google.
- **Meta**: playable ads are added in Ads Manager when creating an App Install ad. Meta requires
  the page to call `FbPlayableAd.onCTAClick()`, which this file already does.
- **TikTok**: upload as a playable creative in TikTok Ads Manager. It uses `playableSDK`.
- **Unity Ads / AppLovin / ironSource / Mintegral**: upload the html (or a zip with it) as a
  playable creative. They inject MRAID, which this file waits for before starting.

Test with the network's own preview tool before spending money: it is the only way to see that
the CTA really opens the store from inside their player.
