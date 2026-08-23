# Shipping Slice & Blast to TestFlight

Two independent routes exist. Use whichever is available — they produce the same build.

## Route A — Unity Build Automation (original)

Unity builds the `.ipa`, Codemagic uploads it.

1. Unity Cloud → Build Automation → start a build on the `ios-testflight` target.
2. Copy the `.IPA file` link from the finished build.
3. Codemagic → environment variables → set `IPA_URL` (or leave the API key configured).
4. Run the `deliver-ipa` workflow.

**Its weakness:** a build can sit at *Sent to Builder* indefinitely when the free trial's
build minutes run out or no macOS builder is free. Nothing on our side can speed that up —
check Unity Cloud → Organization → Usage before waiting.

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
