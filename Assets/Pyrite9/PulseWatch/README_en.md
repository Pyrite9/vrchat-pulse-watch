# Pyrite9 Pulse Watch

Version 0.1.1 (2026-11-01)

A pop-up wristwatch gimmick for VRChat avatars. Drop the prefab into a humanoid avatar and it fits the watch to that avatar's forearm when you build.
Turn your wrist to your eyes as if checking the time and a hologram opens over the watch — your local time, a radar of the people around you, a scan wave, and waypoint and flashlight buttons you press with a finger. Move the wrist away and it folds up.

日本語: `README_jp.md` / 한국어: `README_ko.md` / License: `LICENSE.txt`

## Features

- Turn your wrist to your eyes and a hologram opens over the watch — clock, date and radar. On desktop, use **Keep open** in the menu
- Radar: people around you out to 14 m on a 12-direction × 5-ring grid, with the count of filled cells — only you see it
- On opening, a scan wave runs from your eyes out to 14 m over floors, walls and people — in any world, with its depth light (only you see it)
- Waypoint: with your other hand pointing, press the button under the clock to turn on a laser, then pull that hand's trigger to mark a spot on the floor, a wall or the ceiling — for you only, or shown to others. A name tag sits above the mark, and the disc shows its direction and distance
- Flashlight: a second button shines a spotlight from the watch toward where you look — only you see it
- A compass on the disc's ring, and the time you have spent in this world on the clock
- The clock shows everyone the time in your time zone and your world position; your frame rate is shown only to you
- The disc's outer ring glows while you speak, and blinks red with a mic mark while you are muted
- Left or right wrist, back or inside of the wrist (both switchable from the menu), and an option to hide the hologram from others
- Frosted glass behind the hologram; seen from the side, its light runs down to the wrist — neither copies the screen
- The watch's shaders are warmed up when your avatar first comes into view, so the first opening does not stall
- Fits itself to the avatar's forearm at build time. Choose the accent and waypoint colours, and leave out any feature you do not need (left-out features are not in the build)

## Requirements

- Unity 2022.3 (VRChat)
- VRChat SDK – Avatars 3.10 or later
- Modular Avatar
- NDMF (Non-Destructive Modular Framework)

lilToon is not needed (the watch uses its own shaders).

Tested with: Unity 2022.3.22f1, VRChat SDK Avatars 3.10.5, Modular Avatar 1.18.7, NDMF 1.14.8.
Older versions and preview versions (alpha / beta) have not been tested.

- **PC only.** If you also upload an Android / Quest version of the same avatar, delete or switch off the `PulseWatch` object before building for Android.
- With every feature on, the watch adds 2 lights (flashlight and depth light) to your avatar, and the scan wave and the waypoint laser make its Bounds much larger. Leave out any feature you do not need under **Include** (see "Who sees what — and what it costs" and "Performance").

## Installation

1. **Add the required packages.** In VCC / ALCOM, open your avatar project with "Manage Project" and add **Modular Avatar**.
   NDMF is installed automatically with Modular Avatar. Use stable releases (not previews).
   - If it is not in the list, add its repository first with "Add to VCC" on the Modular Avatar website.
2. **Import this package.** Double-click the `.unitypackage` (or `Assets > Import Package > Custom Package…` in Unity), leave everything checked and press **Import**. The files go to `Assets/Pyrite9/PulseWatch/`.

## How to use

1. In the Project window, open `Assets/Pyrite9/PulseWatch/` and drag `PulseWatch.prefab` onto your avatar in the Hierarchy (the object with the VRC Avatar Descriptor). It becomes a child of the avatar.
   - **Put it directly under the avatar (a child of the root).** If it is under a bone (Hips, etc.), the desktop mark at your feet and the scan's floor float at that bone's height. **If it is outside the avatar (for example, at the top level of the scene), nothing happens.**
   - Do not unpack the prefab (updates would not reach it).
2. Select `PulseWatch` in the Hierarchy to see its inspector. In the first section (Basics), **Menu language** (`EN` / `日本語` / `한국어`) sets the language of the in-game menu, the inspector and the watch's date (default EN: `THU 10.29`; `10.29 (木)` Japanese, `10.29 (목)` Korean).
3. (Optional) Change other settings (see "Settings"): the starting wrist, the colours, the features to include.
4. Try it:
   - Press **Play** in Unity: the watch is built and the inspector's **Last build** is filled in (shown after you stop Play). Wrist detection and the menu do not work in Play mode — check those in VRChat.
   - `VRChat SDK > Show Control Panel > Builder` → **Build & Test**, then switch to the test avatar in VRChat. In VR, turn the wrist with the watch to your eyes. On desktop, turn on **Keep open** in the **Pulse Watch** menu.
   - If everything looks good, **Build & Publish**. Build & Test shows only your own view — you can check how others see it after uploading (as Private).
5. Check the result. When the watch fits, **Last build** shows the avatar name, build time, wrist, size and **Warnings: none**.
   - "Put the watch inside an avatar (under the object with the VRC Avatar Descriptor)." means the watch is outside the avatar. If "No build yet" stays after a build, the build did not reach the watch (check the Console for red compile errors, and check that the watch object is not switched off).
   - Warnings also appear in the Console (`Window > General > Console`) as yellow lines starting with `[Pyrite9 Pulse Watch] WARN` — see "Troubleshooting". Details go to `Logs/Pyrite9_PulseWatch_build.txt` in your project folder.

At build time the watch measures the avatar's forearm, hand and head and fits the watch's position and size (in proportion to the forearm), the back-of-the-hand direction, the eye position, and the index fingertip the laser starts from. Your scene is not changed — only the copy made for the build is.

## Expression Menu

One submenu, **Pulse Watch**, is added to the main menu (the item names follow **Menu language**; the name `Pulse Watch` stays):

| Item | Type | Parameter | What it does | Others |
|---|---|---|---|---|
| Keep open | Toggle | `PulseWatch/Manual` | Keeps the hologram open regardless of wrist detection. This is how you open it on desktop | Local — the result (`Open`) is synced, so it opens on others' screens too |
| Waypoint ▸ Mark | Button | `PulseWatch/WpTap` | VR: turns the laser on (same as pressing the watch button). Desktop: places a mark at your feet | Local — the laser and mark are synced (others see them with `Show to others`) |
| Waypoint ▸ Clear | Button | `PulseWatch/WpClear` | Removes the mark | Local — the result is synced |
| Waypoint ▸ Show to others | Toggle (saved) | `PulseWatch/WpShow` | On: others also see the mark, name tag, laser and the direction on the disc | Synced |
| Flashlight | Toggle | `PulseWatch/Flash` | Turns the flashlight on (same as pressing the watch tab) | Local — others do not see it |
| Hide from others | Toggle (saved) | `PulseWatch/Hide` | On: the watch does not open on others' screens (it still does on yours). The waypoint follows `Show to others`, not this | Synced |
| Settings ▸ Sensitivity: loose / strict | Toggle (saved) | `PulseWatch/Sens` | +10° / −10° on all four open / close angles. Both off = normal | Local — detection runs only on your side |
| Settings ▸ Wear on right wrist | Toggle (saved) | `PulseWatch/Right` | Moves the watch to the right wrist (the default is **Wrist** in the inspector) | Synced |
| Settings ▸ Wear on inside of wrist | Toggle (saved) | `PulseWatch/Inside` | Moves it to the palm side (the tactical style) — then turn the palm to your eyes to read it | Synced |
| Settings ▸ Debug meter | Toggle (saved) | `PulseWatch/Debug` | Only present when **Debug meter** is on in the inspector. For tuning the detection angles. Starts on (bars shown) | Local — others do not see it |

The Flashlight item is not shown when that feature is left out, nor the Waypoint submenu when the waypoint is left out.

Synced parameters: **8 bits** (`Open`, `Right`, `Hide`, `Inside`, `WpOn`, `WpSeq`, `WpLaser`, `WpShow`, all under `PulseWatch/`; 4 bits without the waypoint). An avatar can have up to 256 bits in total; above that the SDK blocks the upload.
Saved (kept when the avatar is loaded again): sensitivity, right wrist, inside of wrist, hide from others, show to others, debug meter. Keep open, the flashlight and the mark are not saved.

## How it works in game

| | VR | Desktop |
|---|---|---|
| Open | Turn the wrist with the watch to your eyes as if checking the time (the watch face within 40° of your eyes + the watch within 45° of the middle of your view, held for 0.15 s). Move it away (past 55° / 60°) and it folds | Menu `Keep open` |
| Waypoint laser | Make the hand without the watch **point (index finger only)**, push the tab under the clock plate in with that index finger, then let go → a laser comes from that hand | — |
| Mark | Aim the laser and pull that hand's **trigger** — a mark where it hits (floor, wall or ceiling, up to 50 m) | Menu `Waypoint ▸ Mark` — at your feet |
| Clear | Hold the tab for 1.2 s (press it again within 2 s to cancel) | Menu `Waypoint ▸ Clear` |
| Flashlight | Press the tab beside the waypoint tab the same way (press again to turn it off) | Menu `Flashlight` |
| Switch wrist | Menu `Settings ▸ Wear on right wrist` / `Wear on inside of wrist` | Same |

- The mark is a floating diamond with a name tag, visible through walls too. The disc shows its direction and distance. There is one mark — marking again moves it.
- The scan wave plays each time the watch opens (not again within 5 s) and spreads from your eyes out to 14 m over 1.4 s.
- A tab can only be pressed while the hand is pointing — brushing it with an open hand or a fist does nothing. If your avatar has a facial expression assigned to that hand gesture, the expression may change while you press (the avatar's own FX gesture expressions).
- The time zone is that of the PC that builds the avatar (or the UTC offset set in the inspector), and everyone sees the same time on your watch: the wearer's. With **This PC's time zone** on, that zone's daylight saving rule (as of the year you build) is baked in too, and the clock switches automatically by date. A UTC offset typed by hand is fixed, without daylight saving.

## Who sees what — and what it costs

For each feature: what you (the wearer) and others see, and what it adds to the avatar. The cost is how much the VRChat SDK performance stats (PC) went down when only that feature was left out of a build on Lapwing. A few parts are shared by two features, so the costs do not add up exactly to the totals under "Performance".

| Feature | You | Others | Cost |
|---|---|---|---|
| Base (always in) — hologram opening / folding, clock and date | Yes | Opens with yours (not with `Hide from others`); the same wearer's time | 4 synced bits, 3 FX layers, 4 contacts (Local Only), 6 renderers, 6 material slots, 22 polygons, 10 VRC constraints, 1.00 MB texture memory (glyph atlas) |
| Prewarm (always in) — draws each watch shader once when your avatar first comes into view, then switches itself off | Nothing to see (a 0.1 mm triangle inside the chest) | The same on their PC — their first look at your watch does not stall either | 1 renderer and 1 Animator (Cull Completely); 1 material slot and 1 polygon per watch shader in the build (5 with every feature on, 1 with all left out) |
| Radar | Cells and the count | No (an empty disc) | 120 contact receivers (Local Only), 121 FX parameters |
| Scan | The wave on opening | No | 4,610 polygons, 3 renderers, 3 material slots, 2 VRC constraints, 1 FX layer, Bounds 30 × 31 × 30 m (the 14 m wave) |
| └ Depth light | On during the wave (lights nothing) | Not switched on | 1 light (directional) |
| Waypoint | Button, laser, reticle, mark, name tag, direction on the disc | The button tab only; the laser, mark, name tag and direction only with `Show to others` on | 4 synced bits, 5 FX layers, 2 VRC Raycasts, 2 contacts (Local Only), 8 renderers (1 skinned), 8 material slots, 28 polygons, 3 VRC constraints, Bounds 67 × 53 × 18 m (the 50 m laser) |
| └ Name tag | Above the mark | As the waypoint | 1 renderer, 1 material slot, 2 polygons, 0.05 MB texture memory |
| Flashlight | The light and the lit tab | No | 1 light (spot), 2 FX layers, 2 contacts (Local Only), 3 renderers, 3 material slots, 18 polygons, 1 VRC constraint |
| Voice ring | Glows as you speak, red when muted | Yes (the blinking is not in sync) | 2 FX layers |
| FPS | Yes | No | None (drawn by the shader) |
| Coordinates | Yes | Yes | None (drawn by the shader) |
| Compass | Yes | Yes | 1 renderer (skinned), 1 material slot, 2 polygons |
| Session time | Yes | No | 1 FX layer |
| Glass blur | Yes | Yes | 4 renderers, 4 material slots, 8 polygons |
| Light drip | Yes | Yes | 1 renderer, 2 material slots (one of them a second slot on the disc), 4 polygons, 0.45 MB texture memory (two pictures of this watch baked at build time) |
| Debug meter (off by default) | When switched on in the menu | No | 1 FX layer, 1 renderer, 1 material slot, 2 polygons |

- **Runs only on the wearer's PC:** wrist detection, the radar, button presses (all 128 contacts are Local Only), the scan wave and depth light, the flashlight. None of these run on others' PCs.
- **Also runs on others' PCs:** synced parameters, FX layers (the animator), VRC constraints, the glass and light drip while the hologram is open (the viewer's GPU — no screen copy), the prewarm (once, when your avatar first comes into their view), and the VRC Raycasts while the waypoint laser is on (each screen works out where the mark lands — even with `Show to others` off).
- While the watch is closed its renderers are off, but the performance stats count what is in the build — the 2 lights count as 2 even before they are switched on on your side.
- On others' screens each copy of your avatar casts its own laser to place the mark, so its position can differ very slightly. Someone who joins later does not see the mark until you mark again.
- Menu language, colours, placement and detection angles are baked into the avatar at build time.

## Performance

Every feature on (the defaults, debug meter off). The difference in the VRChat SDK performance stats (PC) between Lapwing with and without the watch (0.1.1, VRChat SDK 3.10.5).

| Stat | Added by the watch |
|---|---|
| Polygons | 4,693 (4,610 of them the scan wave) |
| Bounds | the avatar's size (Lapwing 1.00 × 1.65 × 0.60 m) → **66.83 × 59.84 × 30.00 m** — the waypoint laser and the scan wave. Leave both out and it stays about the avatar's size (the prewarm's 0.6 m box on the chest can add a few cm) |
| Skinned Meshes / Meshes | 2 / 23 |
| Material Slots | 30 (5 of them the prewarm) |
| Bones | 1 |
| Lights | **2** (flashlight and depth light — both switched on only on the wearer's side) |
| Contacts | 0 (the 128 contacts — 126 receivers, 2 senders — are all Local Only, which this stat leaves out) |
| Constraints / Constraint Depth | 18 / 18 (all VRC constraints) |
| Raycasts | 2 |
| Texture Memory | 1.50 MB (0.45 MB of it the light drip's baked pictures) |
| Animators | 1 (the prewarm's, Cull Completely — it stops while nobody sees it) |
| PhysBones · Particle Systems · Trail / Line Renderers · Cloths · Physics Colliders / Rigidbodies · Audio Sources | 0 |
| Synced parameters | 8 bits (of 256) |
| FX layers / FX parameters | 15 / 151 (including unsynced internal values, mostly the radar cells) |
| GrabPass | 0 |

- The 13 menu icons (256×256; 14 with the debug meter) are only used by the Expression Menu and are not in Texture Memory.

## Settings (Pulse Watch component)

Names as they appear in the inspector with Menu language set to English. Double-click a gauge to reset it to its default.

| Section | Setting | Meaning |
|---|---|---|
| Basics | Menu language | `EN` / `日本語` / `한국어` — the menu labels and the watch's date (`THU 10.29` / `10.29 (木)` / `10.29 (목)`, applied at build) and the inspector's language. Default EN |
| | Wrist | Left (default) / Right — the wrist the watch is on at first. Menu **Settings ▸ Wear on right wrist** switches it while playing (saved) |
| | Accent colour | The hologram's accent (hands, gauges, highlights) and the scan wave. Default yellow #FFE00D; **Default** resets it |
| | Waypoint colour | The waypoint's laser, mark, name tag and reticle, its arrow on the disc and the waypoint button. Default sky blue #4CD9FF. The hologram is light on dark glass, so **dark colours show dim** (the inspector warns) |
| Include | Radar | People within 14 m — 120 Local Only contacts |
| | Scan | Wave on opening — 1 FX layer |
| | └ Depth light | A directional light that lets the wave run over floors and walls in worlds without a depth texture (only during the wave, only on your side). It lights nothing and casts no visible shadow. 1 real Light — left out along with the scan |
| | Waypoint | Button, laser and mark — 4 synced bits, 2 VRC Raycasts, 5 FX layers |
| | └ Name tag | The name above the mark. **Leave it empty to use the display name signed in to the VRChat SDK**, baked at build (rebuild after a name change). Type a name to use it instead (up to 32 characters). **If someone else builds or uploads the avatar for you, type your name** — an empty field bakes their name. If it is empty and nobody is signed in to the VRChat SDK on the PC that builds the avatar, the mark has no name tag |
| | Flashlight | Tab and spot lamp — 1 real Light, 2 FX layers |
| | Voice ring | Glow while speaking, red when muted — 2 FX layers |
| | FPS | Frame rate on the clock (your screen only) |
| | Coordinates | The watch's world position next to the date (X Y Z, 0.1 m) |
| | Compass | North (the world's +Z) on the disc's ring |
| | Session time | How long you have been in this world (counted from when the avatar loaded; your screen only) — 1 FX layer |
| | Glass blur | A smoky glass behind the disc and the clock that catches the surroundings softly (the world's reflection probe, blurred). No screen copy |
| | Light drip | Seen from the side, the light runs down to your wrist; gone when you look at the watch. Reads two pictures of this watch baked at build time (about 0.5 MB) and draws the time, radar, mute and voice live — no screen copy. Works with the glass off too |
| Placement (× forearm) | Along the forearm | Where the watch sits from the elbow (0) to the wrist joint (1) (0.6–1, default 0.88) |
| | Face height | Height of the watch face above the forearm's axis (0.03–0.2, default 0.09). Raise it if the face sinks into a thick sleeve |
| Detection (°) | Face — open / close | The watch face turned toward your eyes within this angle opens it (default 40); beyond the close angle it closes (default 55) |
| | Gaze — open / close | The watch near the middle of your view (head forward vs. the direction to the watch) within this angle opens it (default 45); beyond the close angle it closes (default 60) |
| | Hold time (s) | Both conditions have to hold this long before it opens (0–1, default 0.15) |
| | Debug meter | On: adds **Settings ▸ Debug meter** to the menu; bars above the forearm show how close the face and gaze are, with the open and close marks (only you see them). Turn it off once the angles are set |
| Time | This PC's time zone | On (default): uses the time zone of the PC that builds the avatar (standard offset and daylight saving rule) — in a daylight saving zone the clock switches by date. This PC's UTC offset is shown below |
| | └ UTC offset (min) | Used when the above is off (fixed, no daylight saving). Korea / Japan +540, UK 0, US Eastern −300, India +330 (steps of 15) |

- Placement values are fractions of the forearm length, so they fit any avatar size.
- In Detection, 'close' has to be larger than 'open' — otherwise the watch flickers (the inspector warns).
- A feature switched off is left out of the build — no objects, contacts, layers or parameters. An indented setting (└) is dimmed and left out while the one above it is off.

The inspector also shows (in the menu language):

- **Version**: once a day it asks GitHub for the latest version and tells you when there is a newer one (see "Notes").
- **Last build**: the avatar, time, wrist, size, clock time zone, feature count, synced bits, FX layer count, contact count and warnings of the last Play / Build & Test / upload. Buttons open the build log and this README. **OK** folds away a warning you have seen, for that avatar.
- **Changelog**: this version's changes (read from this README).

## Troubleshooting

Console warnings are in English. **Last build** in the inspector shows the same English text.

| Console message (`[Pyrite9 Pulse Watch] WARN …`) or symptom | What to do |
|---|---|
| Right after importing, red errors such as `error CS0246: The type or namespace name 'nadena' could not be found`, and Play / builds fail | Modular Avatar (and NDMF) is not installed. Add Modular Avatar in VCC / ALCOM; the errors go away after Unity recompiles. |
| `not a humanoid avatar — watch not built` | Only humanoid avatars (Rig: Humanoid, VRC Avatar Descriptor on the root) are supported. |
| `head bone missing — watch not built`, `lower arm / hand bone missing — watch not built` | Check the avatar's humanoid bone mapping (head, lower arms, hands). |
| `L/R: index / little finger bones missing — back of the hand assumed …`, `L/R: back of the hand undefined — using up` | Without finger bones, the direction of the back of the hand was estimated. If the watch sits on the side or the inside of the wrist, check the finger bone mapping. |
| `L/R: index finger bones missing — the laser starts … past the hand` | Without index finger bones, the laser starts in front of the hand. It still works. |
| `name tag: no font has "…" — left out of the tag` | No font on this PC can draw those characters in the name, so they were left out of the tag. Use another name or drop those characters. |
| `the watch is under the armature (…) — put it right under the avatar root …` | The prefab is under a bone. It still builds, but the desktop mark at your feet and the scan's floor float at that bone's height. Drag it to be a direct child of the avatar root. |
| `… out of range — … used` | A number in the inspector was outside its range (typed by hand or set by a script) and was pulled back to the nearest allowed value. Check that setting. |
| `… Pulse Watches on one avatar — only the first is built` | One watch per avatar. Delete the extra one. |
| `shader … not found`, `glyph atlas missing …`, `… menu icons missing …`, `VRC… not found` | Some package files are missing, or the SDK is too old. Import the package again; if that does not help, update the VRChat SDK. |
| `Build error: …` (Last build), with a red error in the Console | The watch is not included; the rest of the avatar still builds. Please send the red error and `Logs/Pyrite9_PulseWatch_build.txt` (see "Contact"). |
| The Pulse Watch menu does not appear | The prefab is outside the avatar, the watch object is switched off, or the build stopped with a "— not built" warning (see above). After fixing, build and upload again. |
| The menu or the date is in the wrong language | Set **Menu language** in the inspector and build again. |
| In VR it does not open when I look at the wrist / opens too easily | Menu **Settings ▸ Sensitivity: loose** (does not open) or **strict** (opens too easily). To fine-tune, change the angles in the inspector's **Detection**, and turn on **Debug meter** to tune them in VR while watching the bars. If you wear it on the inside of the wrist, turn the palm to your eyes to open it. |
| It does not open on desktop | Wrist detection does not work on desktop. Turn on **Keep open** in the menu. |
| The watch face sinks into the sleeve | Select the Pulse Watch object: the Scene view shows the disc's position on the wrist — red from every side means it is inside the sleeve. Raise **Placement ▸ Face height** in the inspector (the disc follows at once). |
| Pressing a tab does nothing | Make the hand **without** the watch point (index finger only), push the tab in with that fingertip, then let go. An open hand or a fist does not press it. |
| The laser is on but no mark is placed | Pull the trigger of the hand the laser comes from. A surface has to be within 50 m. The laser turns itself off if you do not fire it for 10 seconds. |
| Nobody shows on the radar | The radar picks up other people's head contacts; someone has to be within 14 m. On others' screens it is an empty disc by design. |
| The scan wave shows only a ring and does not run over floors and walls | With **Depth light** off, worlds without a depth texture show only the ring and a floor grid. The surface scan does not show in mirrors or orthographic cameras. |
| The flashlight flickers on and off very fast in one particular world (even when you stand still) | That world has more real-time lights than Unity allows to light a single object, and its own lights keep changing, so the flashlight drops in and out of the set of lights Unity picks each frame. It is the world's light limit — it cannot be fixed on the avatar side. |
| The clock is one hour off | You live in a different time zone from the PC that built the avatar, typed a UTC offset by hand (no daylight saving), or the zone's daylight saving rule changed after the build (some zones change it every year). Check **Time** in the inspector, then build and upload again. |
| The Performance rank dropped after adding the watch | Check the 2 lights (flashlight and depth light) and the Bounds (waypoint laser, scan wave) — see "Performance". Each light goes when you switch its feature off under **Include**; the Bounds goes back to the avatar's size only with both the waypoint and the scan off. |
| The SDK says the parameters are over 256 bits | The watch uses 8 synced bits (4 without the waypoint). Free bits by removing or un-syncing other gimmicks' parameters. |

## Updating / removing

- **Update:** import the new package over the old one (leave everything checked and press Import). Your settings are kept — they are stored on the `PulseWatch` object in your scene. Do not change files inside `Assets/Pyrite9/PulseWatch/` (prefab, shaders, textures) — an update overwrites them.
- **Remove from an avatar:** delete the `PulseWatch` object from the avatar (a watch switched off in the Hierarchy is also left out of the build).
- **Remove from the project:** remove the watch from your avatars first, then delete the `Assets/Pyrite9/PulseWatch/` folder. If an avatar still shows "Missing (Mono Script)" on a `PulseWatch` object, delete that object — the SDK will not upload an avatar with missing scripts.
- The inspector reads its changelog from this README — keep the README files in the package folder.

## Changelog

- **0.1.1** (2026-11-01): The first opening no longer stalls — smaller watch shaders (first draw on a cold shader cache about 1.4–1.9 s → about 0.35 s in our test) and a prewarm that draws them once when the avatar first comes into view. Glass and light drip without GrabPass (no screen copies): the glass shows the world's reflection probe blurred, the drip reads pictures of the watch baked at build time and draws the time, radar, mute and voice live, and no longer needs the glass. While the watch is closed the wearer's side no longer works out the radar cells.
- **0.1.0** (2026-10-30): First release.

## Notes

- Humanoid avatars only. One watch per avatar.
- Uses up to 15 FX layers (every feature on, debug meter off). The layers follow your avatar's Write Defaults setting (Modular Avatar "Match Avatar Write Defaults").
- Does not use the Action, Base, Additive or Gesture layers, so it works alongside GoGo Loco and emotes. The hand gestures (GestureLeft / GestureRight) are only read.
- Wrist detection turns angles into contact distances, so it runs on your side only; others receive only the result (open or closed).
- The flashlight casts no shadows. The depth light is on only for the 1.4 s of the scan wave.
- When you open the Pulse Watch inspector in the Unity Editor, it asks GitHub (`api.github.com`) for this package's latest version once a day. No personal data is sent; click the version text to check again right away.
- For the performance numbers see "Performance", and for each feature's cost "Who sees what — and what it costs".
- The clock's digit and letter texture is a bitmap rendered from Noto Sans KR (SIL Open Font License 1.1); no font file is included. The name tag is drawn with this PC's fonts at build time.

## Contact

- Bug reports and questions: a message on Booth (https://pyrite9.booth.pm/) or X **@Pyrite9_VRC**.
- Please include: the version (in the Pulse Watch inspector), the warnings from **Last build** or the Console lines starting with `[Pyrite9 Pulse Watch]` (and any red error), the file `Logs/Pyrite9_PulseWatch_build.txt` from your project folder, your Unity / VRChat SDK / Modular Avatar / NDMF versions, and whether you use VR or desktop.

## License (summary — the full text is in `LICENSE.txt`)

- Free. You may use and modify it, and upload avatars that use it (public or private).
- You may use those avatars in videos, streams and screenshots, including monetised ones.
- You may install it on someone else's avatar (e.g. commissions) only if that person downloaded this package themselves.
- You may not redistribute, share or sell the files, original or modified — including inside avatar data or assets you sell or give away.
- Credit is not required, but appreciated ("Pyrite9 Pulse Watch © Pyrite9").
- Provided as is, without warranty. The terms in the version you downloaded apply to that version.
- The glyph texture was rendered from Noto Sans KR (SIL OFL 1.1).
- The package is also on GitHub: https://github.com/Pyrite9/vrchat-pulse-watch (viewing and forking it there are allowed).

© Pyrite9
