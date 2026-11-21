# Unity Template

Starter project for new Unity titles. Unity **6000.3.10f1**, 2D URP, New Input System.

Open this folder in Unity Hub (Editor `6000.3.10f1`). First import will restore UPM packages (Addressables, Steamworks.NET, SoftMask, UIEffect, HierarchyDecorator).

## Layout

| Path | What lives here |
|------|-----------------|
| `Assets/_MyToolkit/` | Shared tools. Do not put game-specific code here. |
| `Assets/_Project/` | This game: scripts, scenes, art, audio, generated constants. |
| `Assets/Plugins/` | Zenject, UniTask, DOTween, Odin Inspector |
| `Assets/BetterFolders/` | Project-window folder colors |

Play mode starts at `BootstrapScene` (Build Settings index 0) and loads `MainScene`. Toolbar Shortcuts has Bootstrap Play plus buttons for those scenes.

## `_MyToolkit` modules

All namespaces are `MyToolkit.<Module>` (Editor code: `MyToolkit.<Module>.Editor`). Runtime:

- **AssetLoader** — Addressables + Resources facade
- **Audio** — music, SFX pool, looping SFX, mixer volumes
- **Save** — generic `SaveService<T>`, JSON, migrations
- **SceneLoader** — built-in + Addressables scenes
- **StateMachine** — lightweight state machine
- **UI** — screens, tabs, input rebind, button sounds
- **Registry** — typed scene registries (`ComponentRegistry`)
- **ParticleFx** — pooled VFX
- **DamagePopup** — pooled damage numbers
- **Steam** — Steamworks.NET wrapper (not wired by default)
- **ApplicationLifecycle** — pause / quit / per-second tick
- **Global** (`MyToolkit`) — `Result`, logger, `AsyncGetter`

Editor: source generators (scenes in build, addressable constants, prefab names), custom code-gen, Toolbar Shortcuts.

Assemblies: `MyToolkit.Runtime` (`Scripts/` except `Editor/`), `MyToolkit.Editor`, `MyToolkit.Tests.EditMode` (`Tests/EditMode`). The toolkit must not reference game code: the game implements `ISceneCatalog` (see `_Project/Scripts/Runtime/Bootstrap/SceneCatalog.cs`) and binds it before `SceneLoaderServiceInstaller.Install`. Run the tests from Window > General > Test Runner (EditMode).

## Starting a new game

1. Set **Company / Product / Bundle ID** in Player Settings (`com.lumenwake.<game>`).
2. Replace `GameSaveState` / `GameSaveInstaller` with your save model.
3. Add game installers next to `ProjectContextInstaller`.
4. Enable Steam only when you have an App ID: put `SteamManager` on `ProjectContext`, call `SteamInstaller.Install(Container)`, set `SteamAppId.Value` and `steam_appid.txt` to the same ID (template ships Valve Spacewar `480`).
5. Regenerate constants via the source-generator menu after you add scenes or addressables.
