# Shipping Slice & Blast to TestFlight

Two independent routes exist. Use whichever is available — they produce the same build.

Once a build is on TestFlight, [APP_STORE.md](APP_STORE.md) covers the rest: hosting the
privacy policy, the metadata to paste into App Store Connect, the privacy and age-rating
answers, and how to capture the screenshots.

## Route A — Unity Build Automation (original)

Unity builds the `.ipa`, Codemagic uploads it.

1. Unity Cloud → Build Automation → start a build on the `ios-testflight` target.
2. Copy the `.IPA file` link from the finished build.
3. Codemagic → environment variables → set `IPA_URL` (or leave the API key configured).
4. Run the `deliver-ipa` workflow.

### When a build sits at *Sent to Builder*

That status means the job was dispatched but no machine in the pool can serve it. Waiting
does not fix it. Two settings on the build target decide this, and both are worth comparing
against a target whose builds used to succeed:

- **Unity version.** If the target asks for an editor version the Mac fleet does not carry,
  the job waits forever for a machine that will never exist. Pick any version the dropdown
  actually offers — this project has no serialised assets beyond an empty scene and builds
  its materials at runtime, so any Unity 6 editor works.
- **Machine type.** A tier the plan does not include queues indefinitely. Use the standard
  Mac option.

Billing is a separate question and usually *not* the cause: Unity Cloud → Organization →
Cost and usage shows spend and Mac minutes, and Usage allowance shows the free-tier bars.
If those read zero, the queue is a scheduling problem, not a quota one.

## Route C — build the Xcode project on your own Windows machine

The path that needs no CI licence and no build farm at all. Unity on Windows generates the
iOS Xcode project perfectly well; only compiling and signing it needs a Mac, and Codemagic
already does that half.

**One-time:** Unity Hub → Installs → the 6000.3.22f1 editor → Add modules → **iOS Build
Support** (~2 GB). And in Codemagic, add `GITHUB_TOKEN` to the `unity` environment group (a
fine-grained PAT with *Contents: read*), same as Route B.

**Every build:**

1. Open the project in Unity, then **Slice & Blast → Build iOS Xcode Project**. It writes the
   project to `unity/SliceBlast/ios/`.
2. Zip that `ios` folder (right-click → Send to → Compressed folder). Around 30–60 MB.
3. GitHub → repository → Releases → either edit the existing `ios-xcode-latest` release or
   create a new one with that exact tag → attach the zip → publish.
4. Codemagic → run the **xcode-to-testflight** workflow.

### Code signing (one-time, in the Codemagic UI)

The workflow signs with the certificate and profile stored in Codemagic, not with one
downloaded at build time — a certificate fetched from App Store Connect contains only its
public half, so signing with it fails with *"Cannot save Signing Certificates without
certificate private key"*.

Codemagic → **Teams / Personal account → Code signing identities**:

- **iOS certificates** → Upload the `.p12` (the one exported with its private key) and enter
  its password.
- **iOS provisioning profiles** → Upload the `.mobileprovision` for
  `com.javidalishov.sliceblast`, App Store distribution.

The `ios_signing` block in `codemagic.yaml` then picks both up automatically.

`ci/fetch-xcode-project.sh` locates `Unity-iPhone.xcodeproj` wherever it sits inside the
archive, so it does not matter how the zip is wrapped.

## Route B — GitHub Actions + Codemagic (no Unity build farm)

Unity only needs a Mac to *compile* the Xcode project, not to *generate* it. So the two
halves run in different places, both on free tiers:

| half | where | why |
|---|---|---|
| Unity → Xcode project | GitHub Actions, `ubuntu-latest` | Linux minutes are cheap and always available |
| Xcode project → signed `.ipa` → TestFlight | Codemagic, `mac_mini_m2` | the signing certificate and ASC key are already set up there |

### One-time setup

**GitHub** → repository → Settings → Secrets and variables → Actions → New repository secret:

| secret | value |
|---|---|
| `UNITY_EMAIL` | the Unity account e-mail (same one Codemagic uses) |
| `UNITY_PASSWORD` | its password |
| `UNITY_LICENSE` | *optional* — contents of a `.ulf` licence file, if the account has one |

**GitHub** → Settings → Developer settings → Personal access tokens → Fine-grained tokens →
Generate: repository access = this repository, permission **Contents: read**. Copy the token.

**Codemagic** → environment variables → group `unity` → add `GITHUB_TOKEN` = that token,
marked secure.

### Every build

1. GitHub → Actions → **iOS Xcode project** → *Run workflow* (branch:
   `claude/slice-blast-core-sihw5b`). ~10–20 minutes; the second run is faster because the
   Unity `Library` folder is cached.
   It publishes the project as the rolling release `ios-xcode-latest`.
2. Codemagic → run the **xcode-to-testflight** workflow. ~10 minutes, and it submits to
   TestFlight itself.

### If the Unity step fails

- **`manifest unknown` / image not found** — GameCI has no Docker image for that editor
  version yet. Re-run the workflow and type an older version in the `unityVersion` input
  (e.g. a `6000.0.x` LTS). This project has no serialised assets beyond an empty scene, so
  it opens in any Unity 6 editor.
- **Licence activation failed** — the first step of the job prints which of the three
  secrets are present. All builds need `UNITY_EMAIL` + `UNITY_PASSWORD` at minimum.

## What the build configures for itself

The repository deliberately does not contain `ProjectSettings.asset`, so every build path —
ours, Unity Build Automation's, or a third-party CI calling the default pipeline — runs
`SliceBlastBuild.PrepareProject()` from an `IPreprocessBuildWithReport` hook. That applies the
bundle id, portrait orientation, IL2CPP, the icon, the generated materials, the scene list and
the disabled Unity splash. Nothing depends on editor state that is not in git.
