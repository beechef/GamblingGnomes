# GamblingGnomes — Project Conventions

## Stack and layout

Unity URP, Netcode for GameObjects (server-authoritative), New Input System, DOTween, Odin (selectively), Facepunch.Steamworks; C# 9.0. Scripts only under `Assets/Scripts/`, flat `Game.Runtime` + `Game.Editor` asmdefs, no per-feature asmdefs. `Resources/` holds only what must be `Resources.Load`ed (e.g. DOTweenSettings).

## C# code conventions

**Namespaces** mirror the folder path, rooted at `Game.Runtime` / `Game.Editor` (`Scripts/Game.Runtime/UI/Poker/` → `Game.Runtime.UI.Poker`).

**Naming**:
- PascalCase types; file name = class name. UI `MonoBehaviour`s prefixed `UI` (`UIButton`), no underscore.
- Feature prefix for grouping (all poker classes `Poker*`).
- Suffixes: `*Manager` (singleton coordinator), `*Controller` (logic owner), `*Data` (NetworkBehaviour holding `NetworkVariable`/`NetworkList` — the model), `*Stage` (state-machine step), `*Visual` (presentation only), `*Database` (ScriptableObject lookup), `*Constant`, `*Settings`. Interfaces `I*`.

**Base types**: `NetworkBehaviour` for server-authoritative state; plain `MonoBehaviour` for local/visual; `ScriptableObject` with `[CreateAssetMenu]` for config; structs implementing `INetworkSerializable, IEquatable<T>` for small networked values (`CardData`); abstract bases for pluggable steps (`*Stage` with `StartStage`/`EndStage`; hallucination effect with `Begin`/`Stop`).

**Fields & properties**:
- `[SerializeField] private Type _fieldName;`; `[field: SerializeField] public Type Foo { get; private set; }` for inspector-set, code-read-only.
- `[HideInInspector] public NetworkVariable<T> Foo = new(...);` — public field for NGO variables.
- `[Header("...")]` to group. Constants PascalCase (`MinimumPlayersToStart`).
- No regions, no XML doc comments; comments rare, one line, only for non-obvious logic.

**Patterns**:
- Singletons: hand-rolled `public static X Instance { get; private set; }` set in `Awake()` with duplicate-destroy guard. No `Singleton<T>`.
- Data/logic split: networked state in a `*Data` NetworkBehaviour, exposed via a `Data` property from the controller/stage; UI reads `Data` and subscribes to its events.
- State machines: list of stages + index, `NextStage()` calls `EndStage()`/`StartStage()`.
- Events: plain `event Action`/`Action<T>`, subscribed in `OnNetworkSpawn`/`OnEnable`, unsubscribed symmetrically. Prefer `OnValueChanged`/`OnListChanged` over RPC broadcasts of state.
- RPCs: `[Rpc(SendTo.Server, ...)]`/`[Rpc(SendTo.Owner, ...)]`, never legacy `[ServerRpc]`/`[ClientRpc]`; names end `RPC`.
- Async: `Awaitable`/`Awaitable<T>`, not UniTask/coroutines. Every async op takes `CancellationToken ct = default` and undoes its own setup on cancel; a temporary handler inside an awaitable wrapper is unsubscribed on every path. `async void` only for a Unity/UI event handler, which catches. **A re-entry guard is released in `finally`**, or one throw kills the button for the session (`UIFindLobby`, `UIPauseMenu`, `GameNetworkManager._joiningLobby`). UI handlers pass `destroyCancellationToken`.
- Tweens: cache the `Tween` in a field, `.Kill()` before restarting.
- No DI container / service locator: `[SerializeField]` refs or `Instance` singletons.
- **Never resolve a dependency with `FindFirstObjectByType`/`FindAnyObjectByType`/`Camera.main`** (editor tooling excepted): serialize it, receive it from the owner (`Initialize(owner)`), or use a self-registering registry (`GameCamera`, `PokerSeat`) — the case for a *spawned* prefab.
- No polling. `Update`/`LateUpdate` only for continuous work (look smoothing, camera follow), never to detect change, and never for `GetComponent`, `Instantiate`, `Find*` or allocation. Cache in `Awake`/`Initialize`.
- Odin only where `[SerializeField]`/`[Header]` can't express the need.

## Design principles

Hold code to an industry-standard bar; check whether this codebase or the industry already has the shape before inventing one.

- **SOLID + KISS + DRY.** Reusable, assemblable modules, not monoliths.
- **Minimise coupling.** Take dependencies from events or injected references, not concrete singletons.
- **Domain-agnostic mechanism, concrete domain code.** A reusable mechanism has no poker/Steam/lobby word in its API (`PlayerBoneScaleController`, `PlayerActionAnimator`, `PlayerSpawnPoint`); domain code is explicit (`DealCardToPlayer`, `SwapPotToLosers`, `OnPotEntriesChanged`, never `Assign`/`Process`/`OnValueChanged`). **A name must state its purpose on sight; that outranks genericity.**
- **Don't over-generalize.** Two real uses justify an interface/type parameter/hierarchy; one does not. Duplication twice is a signal, three times a refactor.
- **Data, logic and visual are three classes.** Data owns state, no behaviour; logic owns rules and touches no `Renderer`/`Animator`/`Image`/tween; visual renders, never decides a rule or writes authoritative state, and reacts through events/`OnValueChanged`. Reference shapes: `PokerGameData`/`PokerGameMode`/`PokerVisual`, `PlayerData`/`PlayerController`/`PlayerVisual`.
- **Downward references only.** `GameModeController` → `PokerGameMode` → `PokerModule`/`PokerStage`; a child raises an `event`, never calls up. Cross-module talk goes through events or a small shared contract.
- **Template method for extension points.** The base keeps its step non-virtual (guards, ordering, state) and calls a `protected virtual OnX` hook, so subclasses can't skip shared work:

  ```csharp
  private void Tick(float deltaTime)
  {
      // shared work the base always does
      OnTick(deltaTime);
  }

  protected virtual void OnTick(float deltaTime) { }
  ```

  Applies to `Start`/`End`, `Bind`/`Unbind`, `Enable`/`Disable`. `PokerStage` (`StartStage`/`OnStartStage`), `UIPokerView`, `PokerVisual` (`OnBind`/`OnUnbind`).
- **Every system has an explicit lifecycle.** Paired hooks (`OnEnable`/`OnDisable`, `OnNetworkSpawn`/`OnNetworkDespawn`, `OnBind`/`OnUnbind`, or a static `OnInstanceChanged` event); never poll for a dependency in `Update()` and rebind. Unsubscribe in reverse order; null a reference only after the unsubscribe that needs it.
- **Animate with DOTween, not hand-rolled `Lerp`/`Slerp` in `Update`.** Tweens give easing, delay, loops, callbacks and sequences; expose duration and `Ease` as `[SerializeField]` and prefer `SetEase` over hand constants.

## Scenes and lifetimes

`Bootstrap.unity` is persistent; `Gameplay_*.unity` is loaded additively and disposable.

- Bootstrap owns `NetworkManager`, `GameNetworkManager`, `SteamController`, `GameCamera` + Cinemachine brain, `UIManager`'s canvas, `AudioManager`. Gameplay owns mode, table, seats, players.
- **Bootstrap code must not reference gameplay types.** Gameplay consumes bootstrap services through their instance or an interface.
- Gameplay survives unload/reload: every `+=` in a spawn/enable hook has its `-=` in the mirror hook; every stateful static resets under `[RuntimeInitializeOnLoadMethod]`.
- Never assume gameplay is loaded; use the async load path. Networked scene ops go through `NetworkManager.SceneManager`.
- **A mode is a prefab the server spawns from the lobby's pick; the gameplay scene holds no mode.** `GameModeController` spawns the `GameModeDatabase` entry's `ModePrefab` (a variant of `GameMode_Poker`: own `_sequence`, `_rules`, `_betItemDatabase`, `_hudPrefab`) from `OnInSceneObjectsSpawned`, never `OnNetworkSpawn`, since the mode lays the table from seats registered at spawn. A scene `NetworkBehaviour` needs a `NetworkObject` on itself or a parent (`PlayerManager` is under `GameModeController`).
- Exactly one **base** `Camera` + `CinemachineBrain`, in bootstrap; everything else (first-person rigs, cutscene rigs) is a `CinemachineCamera`. Never hand-drive `Camera.transform`. The only other `Camera` is `Bootstrap/UICamera`, a URP **Overlay** camera in the base stack, UI only, no brain.

## Audio

- **Every sound is an `AudioEvent` asset played through `AudioManager`; never an `AudioSource` or FMOD call in game code.** The asset names the FMOD `EventReference`; `AudioManager` plays it through its `IAudioBackend` (`FmodAudioBackend`, the only one), so callers and cues never name FMOD. Edit-mode listening is `Game.Editor.Audio.AudioPreview` (FMOD's editor preview banks), also on the `AudioEvent` inspector.
- **A sound that lasts is played with `AudioManager.Play` and stopped through its `AudioHandle`; one-shots never are.** Music and ambience have one owner, `MusicController` (on `Bootstrap/AudioManager`): menu music outside a table, gameplay ambience inside, swapped on `GameNetworkManager` events. Assets live in `Assets/Configs/Audio/`; banks are a single-platform build in `Desktop/` at the project root. Wired today: every button click (`UIButton.OnAnyClicked` → `UIButtonClickSound` on `UI_Manager`), each dealt card leaving the deck (`PokerDealController._departSound`), the shuffle as a deal stage begins (`PokerDeckVisual._shuffleSound`), and animation cues in `Configs/AnimationAudioCueDatabase` (Bet frame 0, AnNam EatSwallow 27, Die head burst 68). A sound belonging to a gesture rather than a frame is the gesture's `PlayerActionAnimationDatabase.Entry.Sound`, played on every peer even before the rig has the state (the showdown winner's `Laugh` → `gnomelaugh`).

## Netcode rules

- Server is authoritative for spawn/despawn and every rule. Owner-write `NetworkVariable`s only for input-shaped state (look angles, identity).
- **Set `readPerm`/`writePerm` explicitly on every `NetworkVariable`.** `PlayerManager`
- **A replicated value has one seeder.** When a central system takes a value over, delete local `OnNetworkSpawn` seeds in the same change — later components stomp the central one.
- **Saving a prefab with a `NetworkObject` auto-appends it to `DefaultNetworkPrefabs`, even if already listed.** After creating/re-rooting one, re-point dead entries, deduplicate by object, grep the `.asset`.
- Guard server work with `IsServer` and owner work with `IsOwner` at method entry.
- Don't write a `NetworkVariable` every frame; throttle by a meaningful delta.
- **Subscribe to another object's singleton from `Start`, never `OnEnable`** (wake order is undefined; a null `Instance` leaves it deaf). Pair with `OnDestroy`. `UIMainMenu`, `UIPauseMenu`, `UINetworkLoadingBinder`
- **A third-party SDK's callbacks are a singleton too.** Hooking Steam before `SteamClient.Init` loses them silently. `SteamLobbyService.Initialize` (called from `GameNetworkManager.Start`) binds at once **and** on `SteamController.OnInitialized`, behind a flag. Hosting and joining await the call results (`CreateLobbyAsync`/`JoinLobbyAsync`) instead of the `OnLobbyCreated`/`OnLobbyEntered` callbacks, and `StartHost` reports `OnConnectFailed` when the chosen lobby is unavailable.
- **Hide a loading screen on `OnLoadComplete` for the local client, not `OnLoadEventCompleted`** (never fires for a syncing joiner); `OnSynchronizeComplete` is the backstop. `UINetworkLoadingBinder`
- **Handle late join by reading the current value in `OnNetworkSpawn` and snapping to it**, not only `OnValueChanged`.
- Teardown belongs in `OnNetworkDespawn`, not `OnDestroy`.

## Area rules (`.claude/rules/`, loaded when working with matching paths)

- `ui.md` — Canvas, layers, rendering; Layout; Prefabs and composition; Buttons, selection, input on UI; Views and data; Localization and settings; UI models; Cursor and pointer
- `shaders-vfx.md` — Shaders and screen effects
- `cards.md` — Cards (rendering and arrangement); Cards, board, visibility
- `poker-gameplay.md` — Modes, stages and flow; Seats, players, match membership; Betting, pot, eating; Items; Notices
- `hallucination.md` — Hallucination
- `player-animation-camera.md` — Player prefab, components, rigs; Animation; Camera and look

When a task touches an area through Unity MCP without reading a matching file, read that rules file first.

## Inspector (Odin)

**A serialized value that must match something else is picked, never typed.**

- `[ValueDropdown]` where valid values are knowable; catalogue values baked into a `*Database` asset (`PlayerActionAnimationDatabase`, `GameModeDatabase`).
- `[Required]` on refs whose absence breaks runtime; `[InfoBox]` with a condition for broken setups (`GameModeDatabase` flags a scene missing from Build Settings).
- **Where a value may name something not authored yet, `[ValueDropdown(..., AppendNextDrawer = true)]`** (`PlayerActionAnimationDatabase.StateName`).
- `[MinValue]`/`[PropertyRange]` on real ranges. **Say on the field when list order matters** (`PokerStageSequence._stages`). Reference objects, not string ids.
- **A shader's `[Enum(...)]` fails silently past seven pairs**; write a `MaterialPropertyDrawer` (`UIBlendModeDrawer`, `[UIBlendMode]`), enum order = shader branch order, noted in both files.
- **A member named by `nameof` must exist in a player build**: keep it unconditional with `#if UNITY_EDITOR` inside the body. `PokerHallucinationBlendShapeEffect.ShapeNames`
- Odin never changes runtime behaviour.
- **Netcode's editor drops Odin attributes on `NetworkBehaviour`s**; `Game.Editor/Inspector/OdinNetworkBehaviourEditor` restores them (Netcode's specific editors still win). Check with `Editor.CreateEditor(component).GetType()`.

## Asset naming and authoring

- Prefabs: `UI_` for UI (`UI_Screen_<Name>` full-screen, `UI_<Name>` components), `Button_` for buttons, none for gameplay. Clips `Animation_<StateName>.anim`, controllers `Animator_<Context>.controller`/`.overrideController`, colocated. Scenes short PascalCase. SO assets named after their class.
- **A font family is a folder of faces plus Dynamic TMP assets**; bold wired into the weight table; created by script with `AddObjectToAsset` for atlas and material; display fonts fall back to `LiberationSans SDF - Fallback` (Dynamic). `Fonts/Skranji/`
- **World pieces with behaviour are prefabs.** `Seat_PokerChair.prefab`, `PokerTableVisual.prefab`. A model turned wrong is fixed in its own prefab (`Seat_PokerChair`).
- **A spawn point sits where the root belongs** (player roots at the feet).
- **Environment props take colliders from the FBX importer (`addColliders`)**; revert removed-collider overrides, delete added-override `BoxCollider`s (`PrefabUtility.IsAddedComponentOverride`). Hand boxes use `MeshFilter.sharedMesh.bounds`; verify `Collider.bounds` ≈ `Renderer.bounds`.
- **Fix "switched off" in the owning prefab, never a scene override**; `PrefabUtility.RevertPropertyOverride` after. **A scene override beats the prefab** — check the scene. Before discarding a re-serialized scene, map each guid to its `.meta`.
- **An editor write is done only when the file on disk says so** (verify with `git diff`/`grep`; stop on `false`/`null`):
  - ScriptableObject: `AssetDatabase.SaveAssetIfDirty(asset)`.
  - Prefab: `PrefabUtility.SavePrefabAsset(go, out bool ok)`; else edit YAML, `ImportAsset(path, ForceUpdate)`, read back via `SerializedObject`.
  - `ProjectSettings/*.asset`: `SaveAssets()` **and** `ExecuteMenuItem("File/Save Project")`.
  - `TextureImporter`: `new SerializedObject(importer)`, `WriteImportSettingsIfDirty`, `ImportAsset(ForceUpdate)`, read the `.meta`.
- **PNGs under `Assets/Textures/**` import as Sprites via `Assets/Settings/Presets/UISprite.preset`; never widen its glob.** `Assets/Art/**/Texture` 3D maps need mipmaps/Repeat. Build presets from a seed PNG.
- **Move assets with `AssetDatabase.MoveAsset`** (keeps guid); check null materials and missing scripts.
- **Split a module by rename plus a new small file**: `MoveAsset` keeps component settings; check what the rest of the old method wrote; shared helpers (`IsStageAllowed`) move to the base (`PokerModule`).
- **Renaming a serialized field drops its value**: `[FormerlySerializedAs]` or rewrite keys; verify via `SerializedObject`.
- **Rename a mechanism when the game outgrows its first use** (`PokerMushroom*` → `PokerItem*` → `PokerBetItem*`); entries differ by prefab (`PokerBetItemDatabase.Entry.WorldPrefab`), never a tint.
- **An event relayed through a generic seam has the seam's signature** (lambdas can't unsubscribe). `PokerScenery.OnInstanceChanged`

## Editor settings that affect how you write code

- **Domain Reload is off: reset every static in `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`** (`static event` → `null`; `static readonly` exempt). `SteamController`, `UIManager`. Subscribe to other statics' events in `AfterSceneLoad`. `CursorController`
- **Auto Refresh is off: `AssetDatabase.Refresh()` after writing scripts; never `Refresh(ForceUpdate)`** (re-binds `InputActionProperty` wrongly). Reread one asset with `ImportAsset(path)`.
- **A `RunCommand` can't import a new asset yet reports success** (guid resolves, `FindAssets` empty, CS0246); hand it to the person. Never `AllowAutoRefresh()`. A hand-written `.cs.meta` (`fileFormatVersion: 2` + unique `guid`) is adopted.
- **Create folders with `AssetDatabase.CreateFolder` in their own command**; check `IsValidFolder`; reload after `CreateAsset`.
- **Delete with `AssetDatabase.DeleteAssets(paths, failed)`, never the shell or `DeleteAsset`** (a command containing `DeleteAsset` is rejected). Check `GetDependencies(buildScenes, true)` first. A shell-deleted script can freeze compilation (CS2001); diagnose with `AssetPathToGUID(..., OnlyExistingAssets)` and `CompilationPipeline.GetAssemblies()` `sourceFiles`. MPPM clones: delete `Library/VP/mppm*/Library/Bee` and `.csproj`, then `RequestScriptCompilation(CleanBuildCache)`.
- **Confirm the connection is on the main editor** (`Application.dataPath` under `/Library/VP/` = read-only clone). `McpVirtualPlayerGuard`
- **UI text is TextMeshPro**, never `UnityEngine.UI.Text`. **Input is serialized `InputActionReference`**, never `actions["Look"]`.
- **Every UI text auto-sizes**: the authored size is `fontSizeMax`, with a smaller `fontSizeMin`, so a longer string shrinks instead of overflowing (`UI_ColorfulPickBar/Content/Label` 48 → 24). A notice reads as a sentence: verbs lower, names as written, first letter capital (`UIPokerActionNotice.Capitalize`). An item asking someone to answer shows them the response panel on the same plank, never also the item's notice; everyone else reads only the notice (`UIPokerItemResponsePanel`, `UIPokerActionNoticeFeed.AnnounceItemUsed`).
- **The device in hand comes from `InputSystem.onEvent` filtered by `EnumerateChangedControls(device, 0.15f)`**, not `onActionChange`. `InputSchemeController`
- **Every binding carries its scheme `groups`**, or `MaskByGroup` labels are empty; a label's guard treats a device change as a change.
- **Edit `.inputactions` via `asset.ToJson()` and prove no `action.id` moved**; each action has two `InputActionReference` sub-assets — check with `TryGetGUIDAndLocalFileIdentifier`.
- **A scripted scene `NetworkObject` gets `GlobalObjectIdHash = 0`** — remove and re-add the component on the saved object.

## Rules

1. Keep the two-assembly split (`Game.Runtime`, `Game.Editor`).
2. Server-authoritative by default — consistent gameplay state lives in a `NetworkBehaviour`/`NetworkVariable`.
3. No XML doc comments or comment blocks; naming carries meaning.
4. SOLID, KISS, DRY; industry-standard patterns; loose coupling.
5. Extend through `protected virtual OnX` hooks from a non-virtual base method; never make a Unity message (`Awake`, `Update`, `OnDestroy`) `virtual`.
6. Paired lifecycle hooks for binding; never poll for a dependency in `Update()`.
7. Animate with DOTween, not `Lerp`/`Slerp` in `Update()`.
8. Never search the scene for a dependency (`FindFirstObjectByType`, `FindAnyObjectByType`, `Camera.main`).
9. Mechanisms named domain-agnostically, domain code explicitly; no abstraction before a second real use.
10. A serialized value that must match something else is picked from a dropdown, never typed.

## Maintaining this file

**Write a rule down in the same change that establishes it.** It earns its place if it **repeats** (came up twice) or **matters** (getting it wrong fails silently, leaks, or is hard to trace). One-off decisions stay in the code.

Put it in the section it belongs to (area rules go in their `.claude/rules/*.md` file; widen its `paths` if the rule applies elsewhere), cite the file that demonstrates it, and fix any rule it contradicts — a rule that disagrees with the code is worse than none. **Keep each rule short**: bold rule, one to three sentences on the failure and fix, the file that shows it; no debugging stories or measurement logs. When a feature is deleted, delete or generalise its rules in the same change.
