# Slice & Blast playable ad

`slice-blast-playable.html` is the whole ad: one file, about 40 KB, no dependencies.

How it plays: the first four seconds show the game playing itself, ending in a big blast, so the
payoff is seen before the player has done anything ("WATCH THIS..."). Then the player takes over
("YOUR TURN!") with a visible goal (three perfect drops in a row set off a blast) and a dashed
outline of where the block has to land. A bad drop gets one retry. The end card shows the blast
ladder (3, 5, 7, 9, 11) with "CAN YOU REACH 11?" and the PLAY FREE button, which opens the
App Store page. A tap during the opening demo skips it.

Open it in any phone browser to try it. To change the store link, title or button text, edit the
`CFG` block near the top of the script.

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
