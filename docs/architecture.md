# Architecture

The game is split so that everything that decides a result is plain .NET with no graphics
dependency, and everything that draws or reads input lives in one shared MonoGame project used by
all three platform heads.

```
RugbyLeague.Core       domain model, competitions, career loop, match simulation, save format
        ▲
RugbyLeague.Game       screens, UI, 2D/3D rendering, shared Content
        ▲
RugbyLeague.DesktopGL / RugbyLeague.Android / RugbyLeague.iOS   platform heads
```

## Projects

| Project | Target | Contents |
| --- | --- | --- |
| `RugbyLeague.Core` | `net10.0` | No MonoGame references. `Model`, `Data`, `Sim`, `Career`, `Persistence`. |
| `RugbyLeague.Game` | `net10.0` | MonoGame host (`RugbyGame`), screens, `UI`, `Graphics`, `Input`, `Content`. |
| `RugbyLeague.DesktopGL` | `net10.0` | DesktopGL head used for development and screenshots. |
| `RugbyLeague.Android` | `net10.0-android` (min API 32) | `MainActivity` plus resource icons/splash. |
| `RugbyLeague.iOS` | `net10.0-ios` (min 18.0) | `Program.cs` AppDelegate + scene delegate, `Info.plist`, asset catalog. |
| `RugbyLeague.Core.Tests` | `net10.0` | xunit tests over the simulation and career layer. |

The game head owns the content pipeline: each head references
`src/RugbyLeague.Game/Content/Content.mgcb` through a `MonoGameContentReference` item, so the same
compiled `Content` folder is produced for every platform.

## Domain model (`RugbyLeague.Core`)

- **`League`** — identity, tier, country, salary cap, TV/prize money, finals spots, promotion and
  relegation counts, bye points, and the full fixture list. Table building accepts a `throughRound`
  limit so unplayed rounds never award bye points.
- **`Team`** — club identity (name, city, nickname, short code, colours, stadium, capacity,
  reputation), squad, formation, tactics, finances, sponsors, facilities and season record.
- **`Player`** — position, attributes (speed, strength, stamina, handling, kicking, tackling,
  passing, vision, discipline), age, potential, form/sharpness/morale/fitness, contract and injury.
- **`Formation`** — thirteen shirt positions laid out as normalised pitch coordinates, with presets
  and per-slot tweaks. Any club can line up in any formation; the shape shifts both the simulated
  attack/defence balance and the 3D team's starting positions.
- **`Cup` / `Fixture` / `MatchResult` / `StandingsRow`** — knockout brackets, finals series,
  fixtures with byes, results with scorers and events, league tables with points difference.
- **`Tactics`** — playing style plus sliders for kick-early, offloads, line speed and width, and a
  weather model that modifies error and kicking probabilities.

### Career loop (`Career.cs`)

`Career` is the entire save: leagues, clubs, cup, finals series, round, phase, news, sponsor offers,
transfer market, current lineup and the previous season's summary. It is serialisable to JSON with
`System.Text.Json`, so no custom save code is needed.

The season is a state machine:

1. **Regular** — 22 league rounds, plus a Challenge Shield round every third week. Your fixture for
   the week is exposed as a `PendingMatch` (league, cup or finals) and your lineup, tactics and
   weather are baked into a `MatchSetup`.
2. **Finals** — the top 6 (NRL, Super League) and 4 (NRL Championship, The Championship) play a
   seeded knockout for the title.
3. **Off-season** — contract expiries, retirements, youth intake, promotion/relegation inside each
   country (NRL ↔ NRL Championship, Super League ↔ The Championship), then a new season.

`MatchSetup` carries the two team sheets and all inputs the engine needs, so the same fixture can be
simulated headlessly, replayed frame by frame, or handed to the 3D screen.

### Match simulation (`Sim/MatchEngine.cs`)

The engine is a deterministic state machine seeded per fixture. It produces a `MatchResult` and an
optional frame timeline (field position, score, possession, event text) that the top-down screen
animates. A set is six tackles; each carry computes an attack-versus-defence edge from player
attributes, formation, tactics, style, weather, home advantage, fatigue and sin-bins, then resolves
metres, breaks, errors, penalties, 40/20s, drop goals and tries. Kicks, conversions and penalties use
kicking skill and weather. Golden point resolves ties in knockout matches.

Guarantees covered by the tests: matches always complete with a winner when required, scores are
legal and plausible, statistics agree with the scoreline, frames stay inside the pitch, a bye awards
no points, tables order correctly and ignore unplayed rounds, cup brackets re-seed correctly, a
career survives JSON and disk round-trips, and the same seed produces identical results.

## Game layer (`RugbyLeague.Game`)

### Host and loop

`RugbyGame` derives from `Microsoft.Xna.Framework.Game`. It creates a `GraphicsDeviceManager`
(1280×720 preferred backbuffer), loads fonts and textures in `LoadContent`, and drives a screen
stack. Each frame:

1. `VirtualViewport.Update` recomputes the virtual canvas from the backbuffer size.
2. `InputState.Update` converts pointer and keyboard state into virtual coordinates and tracks
   presses/releases and multi-touch.
3. The current screen updates.
4. `DrawWorld` lets a screen draw 3D or custom 2D geometry directly to the device.
5. `Draw` draws the screen inside a `SpriteBatch` transformed by the virtual scale.

### Virtual viewport

The design height is a fixed **720 units**; the width is `screenWidth / scale`, never less than 960.
This means no letterboxing: a 16:9 tablet gets 1280 units wide, a tall phone gets a narrower canvas,
and every layout is written in the same units. `UiContext` scales pointer positions back into those
units, so no screen deals with physical pixels.

### UI

`UiContext` is an immediate-mode helper over `SpriteBatch`: nine-slice panels, borders, rounded
buttons, text with alignment and ellipsis, scroll regions with a drag threshold, icons, bars and
tables. Buttons fire on pointer *release* inside the rectangle, which behaves correctly for touch
and mouse alike. Stress testing on a phone-sized canvas is why `Ui.Ellipsis` and the wrapping helpers
exist — long club names must never overflow a card.

### Screens

`ScreenManager` is a stack (`Push`, `Pop`, `ReplaceAll`). Screens are:

`SplashScreen` → `MainMenuScreen` → `TeamSelectScreen` → `CareerHubScreen` (a single screen with
sections: Dashboard, Squad, Tactics, Training, Transfers, Club, Competitions) → `MatchdayScreen` →
`TopDownMatchScreen` or `ThreeDMatchScreen` → `PostMatchScreen`.

`CareerHubScreen` is split into partial files per section. `PlayerScreen` is a reusable player card
overlay. Every screen draws only in virtual units and never assumes a fixed resolution.

### Rendering

- 2D art uses `SpriteBatch` plus generated textures (`panel`, `button`, `pitch`, `grass`, `ball`,
  `shield`, `logo`, `trophy`, `background`).
- The top-down match is drawn in 2D from the engine's frame timeline: pitch markings from
  `pitch.png`, player markers positioned by formation slot and phase of play, a scoreboard, event
  ticker, pause and speed controls.
- The 3D match uses `BasicEffect` with a `VertexPositionColor` mesh built once in `MeshBuilder`
  (pitch, posts, lines, players as low-poly markers) and a `VertexPositionTexture` ground plane.
  The camera follows the ball carrier. No external 3D models are needed.

## Input

`InputState` supports mouse, keyboard (desktop) and multi-touch (phones): it tracks every pointer,
which one started a drag, press/release edges in virtual space, and a movement axis. The 3D screen
uses a virtual joystick plus pass/kick/sprint/tackle buttons; the keyboard equivalents are
arrow keys/WASD, space, K, shift and E.

## Persistence

`SaveGame` serialises the whole `Career` with `System.Text.Json` to
`LocalApplicationData/RugbyLeague/career.json` — on Android and iOS this lands inside the app's
sandboxed data directory. Writes go to a temporary file first and are moved into place, so an
interrupted save never corrupts the career. The game autosaves after every match and before exiting
to the menu.

## Android head

`MainActivity` derives from `MonoGameAndroidGameActivity`, sets the content view to the game and
keeps the screen awake. `ApplicationId` is `uk.co.allthejohnsons.rugbyleaguegame`, minimum API 32,
targeting the latest installed platform. Landscape-only orientation, launcher icons (adaptive on
API 26+) and a splash screen come from `Resources/`.

## iOS head

`Program.cs` holds the app delegate. iOS 27 requires apps to adopt the UIScene lifecycle, and
MonoGame's `iOSGamePlatform` creates a `UIWindow` immediately but has no scene support. The head
therefore:

- declares `UIApplicationSceneManifest` in `Info.plist` with a single `UIWindowSceneSessionRoleApplication`
  configuration named `Default Configuration` whose delegate is `SceneDelegate`;
- implements `SceneDelegate : UIWindowSceneDelegate`, which calls `AppDelegate.StartGame(scene)`;
- in `StartGame`, assigns `game.Window.WindowScene = scene`, runs the game, and makes the window key
  and visible.

Without the scene manifest and delegate the app is killed at launch by UIKit's
`UIApplicationEvaluateRuntimeIssueForNoSceneLifecycleAdoption` trap.

## Determinism

Every random decision in the core comes from a `Random` created from the career seed, the fixture id,
the season year and the round. Given the same career JSON and the same fixture, the simulation
produces the same score, events and frames on every platform. This is what makes the tests reliable
and allows the top-down animation and the final result to agree.
