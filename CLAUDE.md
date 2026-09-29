# GamblingGnomes — Project Conventions

## Tech stack (mirrors ProjectGamble)

- **Unity** + **URP**, **Netcode for GameObjects** (server-authoritative), **New Input System**.
- **DOTween** (`Assets/Plugins/Demigiant`), **Odin Inspector** (`Assets/Plugins/Sirenix`, used selectively), **Facepunch.Steamworks** (`Assets/Plugins/Facepunch.Steamworks`).
- **Addressables**, **Timeline**, **Visual Effect Graph**, **Post-processing** as needed. C# 9.0.

All already installed to match ProjectGamble (`Packages/manifest.json` + `Assets/Plugins/`).

## Folder structure

```
Assets/
  Scripts/
    Game.Runtime/   — all gameplay code (asmdef: Game.Runtime)
    Game.Editor/    — editor-only tooling (asmdef: Game.Editor, includePlatforms: Editor)
  Prefabs/
    UI/             — UI prefabs, with Buttons/ and feature subfolders colocating anim + controller assets
  Scenes/            — Bootstrap.unity (or equivalent) + Gameplay.unity pattern: bootstrap scene loads gameplay
  Fonts/             — one folder per font family
  Textures/
  Materials/
  Configs/           — ScriptableObject data assets
  Resources/         — kept minimal (only assets that must be Resources.Load'ed, e.g. DOTweenSettings)
  Plugins/           — third-party libs (Demigiant, Sirenix, Facepunch.Steamworks)
  Settings/          — URP assets, Input Actions, volume profiles, Build Profiles
```

No scripts outside `Assets/Scripts/`. No per-feature asmdefs — keep the flat `Game.Runtime` + `Game.Editor` split.

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

- Bootstrap owns `NetworkManager`, `GameNetworkManager`, `SteamController`, `GameCamera` + Cinemachine brain, `UIManager`'s canvas. Gameplay owns mode, table, seats, players.
- **Bootstrap code must not reference gameplay types.** Gameplay consumes bootstrap services through their instance or an interface.
- Gameplay survives unload/reload: every `+=` in a spawn/enable hook has its `-=` in the mirror hook; every stateful static resets under `[RuntimeInitializeOnLoadMethod]`.
- Never assume gameplay is loaded; use the async load path. Networked scene ops go through `NetworkManager.SceneManager`.
- **A mode is a prefab the server spawns from the lobby's pick; the gameplay scene holds no mode.** `GameModeController` spawns the `GameModeDatabase` entry's `ModePrefab` (a variant of `GameMode_Poker`: own `_sequence`, `_rules`, `_betItemDatabase`, `_hudPrefab`) from `OnInSceneObjectsSpawned`, never `OnNetworkSpawn`, since the mode lays the table from seats registered at spawn. A scene `NetworkBehaviour` needs a `NetworkObject` on itself or a parent (`PlayerManager` is under `GameModeController`).
- Exactly one **base** `Camera` + `CinemachineBrain`, in bootstrap; everything else (first-person rigs, cutscene rigs) is a `CinemachineCamera`. Never hand-drive `Camera.transform`. The only other `Camera` is `Bootstrap/UICamera`, a URP **Overlay** camera in the base stack, UI only, no brain.

## Netcode rules

- Server is authoritative for spawn/despawn and every rule. Owner-write `NetworkVariable`s only for input-shaped state (look angles, identity).
- **Set `readPerm`/`writePerm` explicitly on every `NetworkVariable`.** `PlayerManager`
- **A replicated value has one seeder.** When a central system takes a value over, delete local `OnNetworkSpawn` seeds in the same change — later components stomp the central one.
- **Saving a prefab with a `NetworkObject` auto-appends it to `DefaultNetworkPrefabs`, even if already listed.** After creating/re-rooting one, re-point dead entries, deduplicate by object, grep the `.asset`.
- Guard server work with `IsServer` and owner work with `IsOwner` at method entry.
- Don't write a `NetworkVariable` every frame; throttle by a meaningful delta.
- **Subscribe to another object's singleton from `Start`, never `OnEnable`** (wake order is undefined; a null `Instance` leaves it deaf). Pair with `OnDestroy`. `UIMainMenu`, `UIPauseMenu`, `UINetworkLoadingBinder`
- **A third-party SDK's callbacks are a singleton too.** Hooking Steam before `SteamClient.Init` loses them silently. `GameNetworkManager` binds from `Start` **and** `SteamController.OnInitialized` behind a flag; `StartHost` reports `OnConnectFailed` when unbound.
- **Hide a loading screen on `OnLoadComplete` for the local client, not `OnLoadEventCompleted`** (never fires for a syncing joiner); `OnSynchronizeComplete` is the backstop. `UINetworkLoadingBinder`
- **Handle late join by reading the current value in `OnNetworkSpawn` and snapping to it**, not only `OnValueChanged`.
- Teardown belongs in `OnNetworkDespawn`, not `OnDestroy`.

## UI structure

### Canvas, layers, rendering
- **`UIManager` is the one canvas (Screen Space - Camera, drawn by `UICamera`); everything on it is on the `UI` layer.** `UICamera`: Overlay at `(0, 5000, 0)`, culls only `UI`, clears depth, post/shadows/AA off, `UI_Renderer` (index 1, no features) so SSAO/hallucination wave skip the HUD. Another layer draws nowhere. Features `Show` into a `UILayer` (`Hud`/`Screen`/`Overlay`), never ship a canvas. `UI_Manager.prefab`
- **A world-space canvas over a head is on `WorldUI` and draws on top.** `sortingOrder` can't beat depth; `PC_Renderer`/`Mobile_Renderer` exclude `WorldUI` from opaque/transparent masks and draw it in the `WorldUIOnTop` Render Objects feature (after transparents, ZTest Always, no depth write). Culling follows the canvas root's layer. `Player_Poker/…/Tag`
- **The `CanvasScaler` lives on the `UIManager` canvas only** (Scale With Screen Size, 1920×1080, Match 0.5); a child scaler rescales everything under it.
- **The cursor is the one thing on its own canvas** — the sole exception to the above. `UI_Cursor.prefab` (in `Bootstrap`): Screen Space - Overlay, sorting order 32767, own scaler, no raycaster, so UI 3D models can't draw over it. `CursorVisualController.Follow` passes a null camera.
- **A full-screen panel meant to cover the HUD is the last sibling** (`SetAsLastSibling`).
- **A runtime-only HUD prefab is not done until rendered**: instantiate under a `ScreenSpaceCamera` canvas with the project scaler in a throwaway scene, fill, `ForceRebuildLayoutImmediate`, render to a `RenderTexture`, write a PNG — catches ellipsised/overflowing text.

### Layout
- **Full-screen panels anchor `(0,0)`–`(1,1)` with zero offsets; corner HUD anchors to its corner.**
- **A stretched frame uses a 9-slice sprite (Single mode, `Image` Sliced); shaped ends stay Simple** (`button_ability_default`). A progress bar slices its track, fill stays Filled.
- **Pad a layout off the art, not the rect** (9-slice overhang, `table_ranking`).
- **A row/column of widgets is an auto-layout group with a `LayoutElement` per child**, never hand anchors; only fixed furniture is anchored.
- **Layout groups: every child needs a size source.** A plain container carries a `LayoutElement`. A nested layout group reports its own size: turn on the parent's `childControlWidth/Height`, never add a `ContentSizeFitter` to the child (`UI_IconCounter`). A `ContentSizeFitter` on the object whose layout it sizes lags a frame; pin heights with `LayoutElement` (`UIPokerRankingPanel`).
- **A button in a layout that controls child size needs its own row**: wrap it in an empty rect with the `LayoutElement` height, button centred at authored size.
- **A tween only moves what no layout group places** (scale, fan openness, a `Body` inside a layout entry). `UIPokerRankingEntryVisual`, `UI_PokerRankingRow`
- **Anything reading a laid-out rect runs in `Rebuild(CanvasUpdate.PostLayout)` via `ICanvasElement`**, never `OnEnable` or a frame later. Register with `CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild`, unregister in `OnDisable`/`OnDestroy`, re-request from `OnRectTransformDimensionsChange`. `UISelectionGroup`
- **A list that can outgrow its frame scrolls or shrinks, decided up front.** Column scrolls: `ScrollRect` whose viewport is itself (`RectMask2D` with softness, matching padding), content top-pinned with vertical `ContentSizeFitter`, layout not controlling child size, `UIScrollToSelection` for key/pad selection only (no list uses it today). Icon row shrinks: fixed-width `HorizontalLayoutGroup` controlling width, each `LayoutElement` with preferred + smaller minimum. `UI_BetBar/ItemPicker/…/EntryRow`, `UI_PokerItemIcon`
- **Overlapping cards in a layout use negative spacing plus `reverseArrangement`** (first card last sibling, leftmost, on top); shorter hands switch the rest off. A text column with a wrapping description needs `childForceExpandWidth`. `UI_PokerHandRow`
- **A fan of cards turns about one shared pivot below them**, spread from angle alone. `PokerCardFanVisual` (`_pivotDrop`, `_fanAngle`), `UIFanLayoutGroup`

### Prefabs and composition
- **UI prefabs live in `Prefabs/UI/Components/`, `Screens/`, `Panels/`; screens and `UI_Poker` nest them.** A screen root is full-screen with its own `CanvasGroup`. A panel owns what only it uses; a button that opens a panel lives with the buttons and references the panel, wired on the HUD holding both (`UI_ShortcutButtons` `Button_Help` → `UI_HandHelper`, `UIPokerHandHelperButton`). A panel never carries its opener.
- **A list item is a component prefab referenced as `_*Prefab`, never an inactive template child** (`TMP_Dropdown` `Template` excepted). `UI_HallucinationRung`, `UI_PokerBetKind`. Editor preview of a runtime list: `UIPokerHandRankingList` (Build/Clear Preview, `_rebuildAtRuntime`, rows adopted in `Awake`).
- **The second time a widget is drawn, it becomes one prefab plus one driving component.** `UITimerBar` + `UI_TimerBar.prefab`
- **Swapping loose objects for prefab instances: collect the old objects first, re-point references by walking every `SerializedObject` for `objectReferenceValue`.** Instantiate with `PrefabUtility.InstantiatePrefab`; `Object.Instantiate` of a sibling drops the prefab link.
- **A screen with several sections is one component per section** (`UIPokerView` subclasses via `OnBind`/`OnUnbind`).
- **A mode's HUD is a variant of the shared HUD prefab, never a copy.** Switch unwanted bars off, don't delete; bars hide on what the rules forbid. `UI_Poker_Normal`, `UI_Poker_Liar`
- **Panels answering the same question share one slot, each deciding its own visibility so exactly one is up.** "What is the table waiting on" is bottom centre, title over `UITimerBar` (`TurnPanel`; `UI_Poker_Liar` anchors it top centre as a variant override).
- **Screens answering one moment are panels of one `UIPanelStateGroup`; the deciding view sits on an object the group never switches off** (else it disables itself — the `UILoadingScreen` trap). A panel with `UIPopInVisual` pops in on enable and is switched off only in `UIPopInVisual.Hide`'s callback, raycasts off during the fade. `UIPokerBetBar`: a beat that ends hides its own panel before asking the bar to redraw (`Refresh` keeps whichever panel is up; `HandleTargetingChanged`).
- **A screen that hides itself toggles a serialized `_content` child, never its own GameObject** (a disabled root never runs `OnEnable`). `UILoadingScreen`
- **Hide a HUD section that must keep listening by fading its `CanvasGroup` (raycasts off), not deactivating.** `UIPokerHudStateController` fades `UI_Poker/Playing` while the showdown list is non-empty.
- **DOTween's UI module is not in this project: fade a `CanvasGroup` with `DOTween.To(() => group.alpha, a => group.alpha = a, target, duration)`** (`DOFade` gives a misleading compile error).
- **Never add a missing component with `GetComponent() ?? AddComponent()`** — Unity's fake null defeats `??`. Use `if (!component)` and read back.

### Buttons, selection, input on UI
- **`UIButton` resolves state; `UIButtonVisual` subclasses draw it.** `UIButton` has no `Image`/colour/tween and raises `OnStateChanged(previous, current)`; subclasses: `UIButtonSpriteVisual`, `UIButtonScaleVisual`, `UIButtonOffsetVisual`, `UIButtonCanvasGroupVisual` (alpha). The base finds the button via `GetComponentInParent`, owns the subscription, snaps on enable, and snaps when `previous == current`.
- **A `UIButtonVisual` subclass never declares `Reset` or `OnDisable`**; use `OnReset`/`OnDisabled`.
- **A button styled by a visual sets uGUI `Button` Transition to None.**
- **A tint a visual paints at runtime is also authored on the `Image`.** `Button_Plate`
- **A visual that moves a button moves a `Content` child from a rest pose captured in `Awake`, never the raycast root.** `Button_ActionPad`
- **A button returns to rest after a click**: `UIButton.ResetState()` clears held flags; call it from anything that steals the pointer mid-press.
- **A click waits out its press animation**: `UIButton._clickDelay` (0.1 s, unscaled via `AwaitableUtility.WaitUnscaledAsync`, 0 = none), guard released in `finally`, re-checks `this`/`isActiveAndEnabled`/`IsInteractable` after.
- **A non-rectangular button uses `UIAlphaHitArea`** (threshold 0.1), needing texture Read/Write; check `isReadable` first.
- **Hotkeys**: `UIButtonHotkey` presses the `UIButton` (`Submit()`, `IsPressedByKey` from `started`/`canceled`), not the uGUI `Button`. It must not hold an action anything else uses — its `OnDisable` disables the shared action (`UI/Cancel`, `Player/Pause` would die). Show such keys with `UIActionKeyLabel`, which only reads the binding; also use it for keys the EventSystem runs on (`Button_Choose`).
- **`Button_Plate` carries a `UIButtonHotkey` in the base**, so a cloned plate inherits the key: revert `_action` (or remove it) unless the button owns that key, and copy added components (e.g. the `LayoutElement`). `UI_ShortcutButtons/Button_Items`
- **Removing a hotkey component or action leaves prefab-instance overrides** pointing at deleted assets in `m_Modifications`; sweep prefabs/scenes for unresolved fileIDs under the actions asset's guid.
- **A key shown on a face is read from its binding, never typed; size for a word ("Enter") and auto-size, max at the authored size** (a single letter renders as authored). `UIActionKeyLabel`, `Button_Plate`
- **A generic usage binding (`*/{Submit}`) can't name a key per device**; bind each device (`UI/Submit` = `<Gamepad>/buttonSouth` + `<Keyboard>/enter`).
- **A list of choices marks selection once, from the group; uGUI owns focus.** `UISelectionGroup` owns the pointer; `UISelectionItem` supplies `PointerAnchor`, raises `OnPointed` from `ISelectHandler`; hover calls `EventSystem.SetSelectedGameObject`. Consumers use `OnSelectionChanged` only. `UIButtonState.Selected` outranks `Hovered`. Draw selection as tone, not size.
- **A group that hides clears its selection and stashes it in `_lastSelected`**, re-offered on enable behind `_restoreSelectionOnEnable` after checking active/interactable. `UISelectionGroup`
- **A picker marks its first entry on open, for mouse and pad** (with `_followHoverOnMouse` on). Cost off `OnSelectionChanged`, choice off `OnSubmitted`. `UIPokerBetBar`
- **Escape is one key with one handler.** Dismissibles push onto `UIEscapeStack`; `UIPauseMenu` calls `DismissTop()` first and opens only if nothing was dismissed. Newest-first; each handler popped before invoked.

### Views and data
- UI reads the mode's `*Data` and the stage's own methods; never computes a rule or talks to Netcode beyond the mode's public API.
- **A view finds the running stage through the replicated stage id, never `CurrentStage`** (server-only): `GameMode.FindStage(Data.StageId.Value.ToString())`. `UIPokerBetBar`
- **The numbers and actions the UI offers come from the stage's own methods, which the server also calls**; a new stage permission is read by the UI in the same change. `PokerStreetStage.IsBettable`
- **What the rules forbid is hidden; what the player can't afford is dimmed.** A stage asking a different question gets its own bar; each bar decides its visibility from the running stage.
- **A button opening a list of choices stays pressable while any choice is shown**, even if none can be taken — the dimmed entry carries the reason. Items button: `UI_ShortcutButtons/Button_Items` (wired to the bar on `UI_Poker`), label via `UIPokerItemCount` ("ITEM ({0})"); off turn the bar shows only the item picker (`IsActing` gates the menu, `CanReadItems` the picker), `UIPokerBetBar.AnyItemHeld`. The picker draws every held item, forbidden ones dimmed with reason (`UIPokerItemPicker.Rebuild`).
- **A view fed by two network objects subscribes to both** (arrival order differs host vs client). `UIPokerRankingRow`
- **A view counting across players subscribes to every player, not only the roster.** `UIPokerStartPanel`
- **A view draws its state when bound, not only on change.**
- **A list rebuilds by re-binding its views, never destroy-and-reinstantiate** (`Destroy` is deferred).
- **A board reporting a finished moment keeps its own snapshot**, refreshed only from data with content. A winner everyone folded to is still turned over and evaluated; folded players are never on the board. `UIPokerRankingRow`
- **A list replicating one entry at a time schedules a staggered reveal by index**: `UIPokerRankingPanel` gives each row `start + rowsAt + index × _rowStagger`.
- **A reference board asks the rules for its content.** Rows call the hand asset's `GetName`/`GetDescription`/`GetExampleCards`; database is `GameMode.HandDatabase` (list's `_database` is edit-preview only). An `IsHouseHand` (Five of a Kind) is crowned above the grid numbered 0, so standard ranks read 1–10 (`PokerHandDatabase.DisplayRankOf`, the one numbering); the board sizes itself via `ContentSizeFitter` floored by `LayoutElement.minHeight`. `YourHand` beside it (`UIPokerOwnHand`) scores the local player's cards with the mode's evaluator on what the showdown would count and they can see: looked-at hole cards plus the table-turned board, unlooked cards drawn face down. A pop grows from the target's authored pivot. `UIPokerHandRankingList`, `UIPokerHandHelper`, `UI_PokerHandRow`
- **A second currency gets a second readout**, never the same one with a different icon.
- **A notice offers one method per shape, never a `detail` string.** One line on a plank (`UI_ActionNotice`: `notice_plank` sliced left/right, Lacquer 64); names in the line, counts beside their icon; every notice names who it's about ("BOB ALL IN"). `UIPokerActionNotice` (`Show`/`ShowAmount`/`ShowTarget`), `UIPokerActionNoticeFeed`
- **A HUD readout is about the viewing player; the room is read off the room** (rates are over each head). Colour from a serialized `(threshold, colour)` ladder; meter hidden while `Phase == Waiting`. `UIPokerHallucinationBar`, `PokerHallucinationTagVisual`
- **A countdown reads whichever clock is running**: show on `HasTurn || HasStageTimer`, source by `HasTurn`, tick on either. `UIPokerTurnPanel`
- **A countdown is only for a wait somebody is held through**; `UIPokerRankingPanel` has no clock or close button.
- **An announcement lasts exactly as long as its moment**, lifetime and fades read from the beat's own number.
- **A control asking for a state the data already answers is derived instead.** Holding cards drives `IsHaveCardOnHand` directly; subscribe to `OnHoleCardsChanged` and `OnStateChanged` (no fixed order). `PokerHandPoseController`
- **A transition that hides a change must contain it, and one number says when.** `PokerHallucinationController.ApplyDelay` (`PokerHallucinationPacing.CloseDuration`) is when rungs land; `UIPokerHallucinationBlink` is shut on that frame. The blink is a vignette closing from the edges (`UI_Vignette` Canvas Shader Graph on a full-screen `Image`, `_Percent` 0 open → 1 shut, `_Softness` on the material), never a volume or alpha fade. **The eye stays shut for `HoldDuration`, then `OpenDuration`**; `HoldDuration` ≥ `EffectEase` (the one ease every effect gets through `Run`), and rungs are re-applied at its end. `TransitionDuration` = close + hold + open, which consume pacing and roll lead-in wait on. Nothing the switch triggers adds its own delay (`PokerRoomController` swaps at once). A change mid-blink folds into the running beat; release the guard before the trailing re-check.
- **A loading screen owns a minimum visible duration** (`UILoadingScreen._minimumShownDuration`); `Hide` hands the bar the remaining time (`UIProgressBar.SetProgress(value, duration)`).
- **A screen covering a wait blocks input by disabling named action maps, never devices; re-enable only what you disabled.** `UILoadingScreen._blockedActionMaps`
- **Cover a wait from the event that starts it — leaving as well as joining.** `GameNetworkManager.OnConnectStarted`/`OnGameLeaving`, `UINetworkLoadingBinder`. A screen that can be up when a join starts from elsewhere hides itself on `OnConnectStarted`, not only from its own buttons (`UIMainMenu.HideAll`).
- **A Steam invite is accepted through one door, `GameNetworkManager.AcceptInvite`, however it arrives.** Launched by it (`+connect_lobby <id>` on the command line, read once when Steam binds), accepted in the menu, or accepted from another room (`OnGameLobbyJoinRequested`): the current room is left the normal way (`LeaveGame`) before the join, a join or leave already running finishes first, and only the latest invite counts. `JoinLobby` alone refuses while in a room, which is why accepting from another room once did nothing. Inviting is `InviteFriends` (Steam's overlay on `CurrentLobby`), shown by `UIInviteFriendsButton` only while `CanInviteFriends`. `UI_InviteButton`
- **Every start ends in an outcome the UI hears**: failed guards report through the failure event; raise the start event past the guards. `GameNetworkManager.StartHost`
- **Waits while the game may be paused use unscaled time** (`Time.unscaledTime` + `Awaitable.NextFrameAsync`; tweens `SetUpdate(true)`). `UILoadingScreen`, `UIProgressBar`

### UI models
- **A 3D model on the UI goes through `UIModelView`: `Root` → `Tilt` → model centred on `Tilt`, scaled into `_bounds`.** Visuals target `Root`. Fit from `sharedMesh.bounds` through local TRS, drawn renderers only (at 5000 units up `Renderer.bounds` is imprecise). `UIButtonSpinVisual`, `UIButtonOutlineVisual`
- **A model on the HUD is the world prefab itself, with UI paint authored on the prop.** `UIPokerBetKindButton` registers on `PokerBetItemCapRegistry`; `PropUIMaterials` lists unlit `*_UI.mat` per renderer; `UIModelView` calls `SetIsUI(true)` before anything claims a renderer, so `PropMaterialOverrideController` captures it as authored.
- **No light for UI models** — a directional light on `UI` becomes `Gameplay_Poker`'s main light (no `RenderSettings.sun`).

### Shaders and screen effects
- **Build shaders in Shader Graph; full-screen effects are Fullscreen Shader Graphs on a `Full Screen Pass Renderer Feature`.** HLSL only for custom blend/stencil.
- **A custom uGUI shader keeps the stencil block and `UNITY_UI_CLIP_RECT`** (from `UI-Default`).
- **A UI overlay blends via blend state (`UI/Blend`), never by reading the backdrop** (no grab pass). Separable modes only (Normal, Multiply, Screen, Additive, Subtract, Darken, Lighten), picked by `UIBlendShaderGUI`; a stroke is the last sibling of `Content`, `raycastTarget` off. Alpha written separately (`Add`, `One OneMinusSrcAlpha`); fades lerp toward `_BlendNeutral`.
- **With exactly one image behind an overlay, sample both in `UI/Blend Overlay`** (sprite `_MainTex`, stroke `_OverlayTex`), enabling Overlay/Soft Light/Dodge/Burn masked by the base's alpha.
- **Match Photoshop by blending in gamma** (`LinearToGammaSpace`/`GammaToLinearSpace`; project is Linear) and verify by reading pixels back. Only Darken/Lighten match without conversion.
- **Textures for shaders come from the team's VFX sources first**: [Textures for VFX Database](https://simonschreibt.notion.site/Textures-for-VFX-Database-2c72eccccfa84a0eae927d778ad746cc), [Texture Packs](https://simonschreibt.notion.site/Texture-Packs-418b5afc18404414b45ecb1af0e5fee8). Name file, source and licence to the person before downloading; keep the licence beside it under `Assets/Art/VFX/Texture/`, not `Assets/Textures/`.
- **A full-screen effect's renderer feature is off until seen.** `PokerHallucinationFullscreenEffect` enables it, clones the material, fades one float from zero, and zero must be the untouched screen (`Hallucination_Pixelate` lerps columns from `_ScreenParams.x` to `_Cells`, samples cell centres). Adding a feature from script needs `AddObjectToAsset` plus `m_RendererFeatures` and `m_RendererFeatureMap`. Screen effects share the `Hallucination_PostProcess` group (Wave, RGBSplit, Pixelate, FilmNegative, VideoDistort) so one runs. A look URP grading makes is a Volume effect: `Hallucination_FilmNegative` = Color Curves (`Volume_FilmNegative`, LDR), master 1 → 0, RGB curves lifting into orange mask. A pass needing more than strength binds in `PokerHallucinationFullscreenBehaviour.OnPassStarting`, frees in `OnPassDisposed`: `PokerHallucinationVideoBehaviour` loops a `VideoClip` into its own sRGB `RenderTexture`; `Hallucination_VideoTint` blends it in gamma; `Hallucination_VideoDistort` samples at `saturate(y + red × _Strength × _MeltAmount)`. Clips in `Assets/Art/VFX/Video/`.
- **A screen-space distortion tapers to zero at the frame edge.** `Hallucination_Wave`: `sin(PI * x)`, `lerp(uv, 0.5, _EdgeInset)`, `Clamp` only as a net.
- **Hand-written Shader Graph JSON**: no blank line inside an object; generate from C#, not a shell heredoc. The slot type is part of the node (`MultiplyNode` → `DynamicValueMaterialSlot`, `AddNode` → `DynamicVectorMaterialSlot`); a wrong type silently uses defaults. Slot ids: Multiply `A=0 B=1 Out=2`, Sine `In=0 Out=1`, Split `In=0 R=1 G=2 B=3 A=4`, Vector2 `Out=0 X=1 Y=2`, URP Sample Buffer `UV=0 Output=2`. Mesh targets need `VertexDescription.Position`/`Normal`/`Tangent` blocks (URP's `MotionVectors` pass). Verify: `ShaderUtil.ShaderHasError`, resolve every edge, `pass.CompileVariant` on every pass, and read `ShaderUtil.GetShaderData(...).GetPass(0).SourceCode` for operands. `Card_Face`, `Card_Wave`, `Card_Rainbow`
- **A VFX Graph is laid out like `VFX_HallucinationGhost`**: contexts in one column at x 0, exposed parameters at x ≈ −210 beside their slot. A variant is a copy with nodes removed and rows moved up (`VFX_HallucinationAfterImage`).

### Cards (rendering and arrangement)
- **A card is one `MeshRenderer` box, never a `SpriteRenderer` or two quads.** Size, face offsets, hit box authored in the prefab. `Card_Poker`, `PokerCardVisual`
- **Card pictures come from one atlas through one shared material; the picture travels in the mesh, never a `MaterialPropertyBlock`** (breaks SRP Batcher). `Cards.spriteatlasv2` packs 53 faces (Sprite Packer Mode **Sprite Atlas V2**; rotation and tight packing **off**). `PokerCardMeshCache` copies the box per (front, back) with UV2 = atlas rect (−Z front, rest back) and binds the page as global `_CardAtlas` (unexposed). Graphs sample `uv0 * uv2.zw + uv2.xy`. No stale texture slots on card materials.
- **A Joker is a `CardData` `IsValid` accepts (`JokerRank` 15, `IsJoker`), wild only inside `PokerCardAnalysis`**: counts into `WildCount`; hand types ask `HighestGroup`, `TryTwoGroups`, `StraightHigh`, `TryFillFlush`. Suit counters skip it (`PokerDeck.CountRemaining`), `PokerCardDatabase` draws `_joker`, equal cards trading places write nothing (`PokerCardExchangeVisual.HasArrived`). Count: `PokerRuleSettings.JokerCount`.
- **A card group's depth step exceeds card thickness (0.002) and points toward the viewer** (negative along the anchor's forward, since faces read down −Z). `PokerCardGroupVisual._depthStep`, `PokerCardFanVisual`, `PokerCardRowVisual`
- **Transparent cards are ordered by the depth step, not the depth buffer** (`Card_Face` Unlit Transparent, ZWrite off, queue 3000; near ties break on registration order, which differs host vs client). To tell layout from draw order, rebuild the group frame from transforms and measure spacing along the depth axis.
- **An arrangement asks the card its size.** `PokerCardVisual.Size`, `PokerCardGroupVisual.CardSize`; fan keeps only `_pivotDrop`, row steps `CardSize.x + _gap`. `Card_Poker/Face` scale = sprite size in units; new art shape means re-authoring it and the root `BoxCollider`.
- **Card layout is a group; a picked card moves between groups.** `PokerCardGroupVisual` holds cards, resolves the anchor, answers slot pose; `PokerCardRowVisual`/`PokerCardFanVisual` are subclasses. Insert at deal position; closing up is the arrangement's call (`SlotOf`/`SlotCount`: fan closes, row keeps spots).
- **Card orientation follows −Z.** Face-up anchor = `Euler(90, 0, 0)` in seat space; verify `-pivot.forward` is world up. A flat row steps depth positive along the anchor. Bet item anchors stay upright.
- **An anchor resolved from the rendered rig is re-checked, never cached** (`RenderedRig` depends on `IsOwner`). Keep the holder, re-parent when the bone changes. `PokerCardFanVisual.ResolveAnchor`
- **A moving card takes its new parent when it lands.** `ArcTween` flies in world space reading the destination each frame; `Land` reparents. `PokerCardVisual.PlaceAt`
- **Cards moving together leave their group at once, then depart one after another; a card in flight is never snapped.** `PokerHandVisual.LeaveGroup` calls `Remove(card, layout: false)` per mover and lays out once; hand-over staggers `_handOverStagger` (0.15 s) per moved card in a higher slot, rightmost first. `PlaceAt` keeps waiting/travelling cards going.
- **A card's flight waits for the hand animation's cue** (`CardPickUp`/`CardPutDown`/`CardShow`), `_cueTimeout` as backstop. `PokerHandVisual`
- **A dealt card starts on the deck, departs in seat order, and every place/flip honours its wait.** `PokerDeckVisual` registers itself; `PokerHandVisual.AddCard` routes animated adds through it; turn order `PokerDealTurn` from replicated `InMatch` seats. A `PokerDealController` subclass (`PokerDealArcController`) decides wait and flight; the wait lives on the card (`_departAt`). Late join places directly. `PokerDealPacing` holds wait and flight; `PokerDealStage` waits `DealDuration(players, cards)`.
- **A hovered card lifts its art, never its collider**; lift and flip move `Pivot`, summed in `ApplyArtHeight`. `PokerCardVisual`, `Button_ActionPad`
- **A card's hover rim is a sibling mesh**: `Card_Poker/Pivot/HoverOutline`, slightly larger box, `Card_HoverOutline.mat` (`CardSilhouette` alpha), queue 2999. `PokerCardVisual.SetHighlighted`
- **A card is picked by pointing, marked then committed as one set.** Click marks/unmarks; commit when marked count equals what's owed (`ViewableHoleCards` minus turned). Lift decided in `ApplyLift` (chosen > hovered). After the last look, nothing is pickupable; `CanPick` asks `Remaining > 0`. `PokerCardPickController` raycasts through the cursor and takes the nearest of our cards (`RaycastNonAlloc` doesn't sort).
- **Send a batch as one replicated write.** `LookAtHoleCardsRPC(int slots)` takes the set, refuses it whole naming the gate, writes `LookedAtHoleCards` once. `PokerCardPickController`, `PokerPlayerData`
- **A change mask decides what animates, never where a thing belongs.** `PokerHandVisual.HandOver` re-asserts each card's group on every presentation change; masks `_shownFaceUpMask`, `_shownInHandMask`, only the XOR animates; late-join rebuild animates nothing; clear masks with the cards.

### Cursor and pointer
- **The cursor is a counted release, never a toggle.** `PlayerController` holds a base lock; screens pair `CursorController.RequestUnlock`/`ReleaseUnlock`. At the table `TableCursorController` holds a release for life, returning it only while the look button is held. `ReadLookInput` early-outs on `!IsLocked`. `UIPauseMenu`
- **The cursor lock belongs to the mouse alone**: `InputSystemUIInputModule` drops `MouseOrPen` pointers while `Locked`, and the pad cursor is a `Mouse`; on gamepad `PlayerController.ReadLookInput` skips the `IsLocked` gate.
- **The pointer's look is a ranked set of requests on `CursorVisualController`, one child object per look.** `Request(state)` returns a handle; highest `CursorVisualState` wins (`Default` 0, `Interact` 10, `Skull` 20); the arrow hides via `CursorController.RequestArrowHidden`. `Interact` comes from static `UIButton.OnPointerOverChanged`; world objects request it themselves (`PokerCardPickController`).
- **The pad's pointer is `VirtualMouseInput`**; `UIVirtualCursor` only decides when it runs (never on keyboard). A pad gets a cursor only where there's something to point at: `CursorController.IsPointerWanted`, counted, held by `TableCursorController`.
- **An action driving the virtual cursor must not be bindable by it**: build a private `InputAction` on the pad's south button (the rare non-`InputActionReference`). `UIVirtualCursor`
- **Never add/remove an input device inside an input callback**; defer with `Awaitable.NextFrameAsync(destroyCancellationToken)` and collapse changes. `UIVirtualCursor`
- **A device the game invented is not one the player holds**: refuse `InputDevice.native == false`, check `action.activeControl.device` against the scheme. `InputSchemeController.SchemeFor`, `PlayerController.ReadLookInput`
- **Seated play is a second look action (`Table/Look`), not a rebind**, read by `PlayerController` while `_bodyAnchored`.
- **A `PassThrough` action performs on release too**: `if (!context.ReadValueAsButton()) return;`. `PokerCardPickController` on `UI/Click`
- **One act is one action; repoint consumers before deleting a duplicate**, then prove no `action.id` moved and every `InputActionReference` resolves.
- **Anything on the player about *this* machine is owner-gated; ownership is unknown in `OnEnable`.** Bind from `OnNetworkSpawn` behind `IsOwner`, re-bind in `OnEnable` behind `IsSpawned && IsOwner`, idempotent behind one flag. Cue reactions only the victim feels are the same. `TableCursorController`, `PlayerCameraShake`

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

## Gameplay architecture

### Modes, stages and flow
- **A mode whose players carry different pieces brings its own player variant** (`GameModeDatabase` `PlayerPrefab`, read by `PlayerManager`). `Player_Poker_Indian` swaps `HandFan` for `HandHead` (`PokerCardHeadVisual`, head bone, facing out), drops `PokerHandPoseController`; the head row is concealed on the holder's screen (`OnCardAdded`/`OnCardRemoved`); groups the hand doesn't carry skip cues (`FollowsHandCues`). Landed cards take the group's scale (`Land` writes `RestScale`, `ArcTween` resizes; `_cardScale`). Who may read: `PokerPlayerData.HiddenFromHolder`, stamped by the deal (`_hideFromHolder`).
- **A rule variant is another asset, not another class**; a new `*Stage` class only when the step's shape differs.
- **Stages reached by reference (module-pushed, `PokerGameMode.InsertStage`) are declared in `PokerModule.CollectReferencedStages`.**
- **A stage id fits in 29 bytes; an unset id is the asset name** (`FixedString32Bytes`; longer throws on spawn). `PokerStage` shows an error box (`PokerStage_Liar_Consume`).
- **Every stage exit is named and in the sequence.** `PokerStageMachine.GoTo` only finds sequence entries and wraps to entry 0 (waiting room), so the last stage names `_nextStage`. `PokerShowdownStage`: `_nextHandStage` / `_matchOverStage`. On inserting or removing a stage, grep the preset for hand-named exits (`_handOverStage`, …). An optional branch sits in the sequence and passes through when its fact is absent (`PokerAllInStage` after the river, only while an all-in cap is in the pot). Pre-deal steps can't hand over to a showdown; the normal round is Waiting → Deal → FirstStreet, both streets handing to the reveal.
- **A module list of stage references is a second naming of the sequence**; re-point it when the sequence changes (empty often means "all").
- **A module narrows what a stage allows through the mode**: `PokerStreetStage.CanFold`/`StakeSize` ask `PokerGameMode.IsActionAllowed`/`ModifyStakeSize`; a changed answer raises `NotifyActionRulesChanged` on every peer. `PokerItemModule`
- **A rule about what a player carries runs on `OnStageStarting`** (before `StartStage` spends); nobody is `Active` yet, so ask "seated and alive". `PokerStageMachine`, `PokerModule.OnStageStarting`
- **A house rule changing what a player can answer hangs off the turn**: `PokerModule.OnTurnBegan`, raised from `PokerGameMode.BeginTurn`.
- **`CanAct`/`IsInHand` only answer during a hand**; the street uses `CanBet` (seated and conscious) for opening, advancing, finishing and `HandleAction`. `PokerStreetStage`
- **A step whose UI asks a different question is a replicated phase**, not inferred from the turn.
- **An event that changes no rule is a replicated flag on a module; push a stage only when the question changes.** Chance events: pick the moment when the stage opens, roll when it arrives; at-risk stages are picked assets with a chance each. A full-screen darkening is a non-raycasting panel over the HUD.
- **A turn on a clock must always end.** Street: fold (if timeout and allowed) → drawn bet → all in (`PokerStreetStage.AnswerTimedOutTurn`); silent all-in answer folds where allowed else all in (`PokerAllInStage.Settle`).
- **One field decides whether a step is on a clock**: `_turnDuration` ≤ 0 = no countdown/timeout/bar; views read `PokerGameData.HasTurnClock`. `PokerAllInStage` `_duration` ≤ 0 (off in Liar).
- **Everybody answering at once is its own stage.** `PokerCardLookStage` gives no turn, ends when the last look is spent; `_duration` ships off.
- **What may be reached for is opened by the asking beat and closed when it ends.** `PokerHandVisual.SetPickupable` for the look stage, never flagging held cards. `_putEveryCardInHand` picks up every card still on the table, counted off cards held, via `PokerPlayerData.ServerLookAtEveryHoleCard`, opening nothing (`PokerCardLookStage.PlayersChoose`). `Refresh` never re-places a card waiting on its cue. Hold the camera on the seat's card anchor for the whole stage.
- **Never unsubscribe inside the handler of the act the subscription must outlive.** `PokerCardPickController` compares the raw `FixedString32Bytes` stage id in its `Update`.
- **A step assuming "a hand begins here" breaks when a round begins elsewhere**; bets and the deal go in seat order.
- **The reveal is a beat before the ranking**: `PokerCardRevealStage` (one `ServerRevealHand` per hand in play, `_duration`; folded stay face down).
- **A board shown for a countdown comes down with it**: `PokerShowdownStage.FinishShowdown` clears `Data.Showdown`.
- **Answers given in secret are not announced**: `PokerStage.AnnouncesActions` off. `PokerAllInStage`
- **Ending a hand and a match are different events.** `EndHand` puts away cards, turn and module rounds, raises `OnHandEnded`. `EndGame` also raises `OnMatchEnded`, sets `Phase = Finished` (unlocks every chair). When `CanDealAnotherHand` holds, `PokerShowdownStage` deals into `_nextHandStage`. Name hooks after the moment that raises them.
- **`EndGame` is the cleanup choke point**, sweeping every registered player's cards.
- **A match ends on screen; everything resets behind a closed eye.** When `CanDealAnotherHand` is false, `PokerBetItemConsumeStage`/`PokerShowdownStage` go to `_matchOverStage` (`PokerMatchOverStage`): stamps `SurvivorClientId`, `Phase = MatchOver` (`UIPokerMatchOverScreen`), raises `PokerGameData.MatchResetting`; after the close duration resets (`EndGame`, `ServerResetMatchStats`, `ResetPot`) and goes idle at once with the flag up; drops it `_blinkHoldDuration` (2.8 s, > round trip + blink close + hold) later on `PokerGameMode.destroyCancellationToken`. A blink hiding a server change is a replicated flag, never client-timed.
- **Match and hand state reset separately, each component its own half.** Blood and hallucination carry between hands; `PokerPlayerData.ServerResetForMatch` (vitals), `PokerBetItemConsumeController.ServerResetForMatch` (eaten record). Grep every clearer of a kept value. Starting values agree with ceilings (`_startingHealth`/`_maxHealth`).
- **Read a reused stage against each meaning.** Normal round: `PokerWaitingStage._resetMatchStats` off (`PokerMatchOverStage` resets); it deals the next round after `_nextRoundDelay` while `CanDealAnotherHand`, and `UIPokerStartPanel` hides the host button then. `StartGame` runs each round so a new chair joins the next one. Idle shot `State_Ready`.
- **A guard that lets `Awake` return early owes later methods matching guards** (NGO spawns disabled components; `Destroy` is deferred). `PokerGameMode` (duplicate table, `_stageMachine`)
- **A setting no preset uses stays at its off value** (`PokerItemModule._itemsPerMatch` 0); delete only code that can't run correctly or be selected — check `FindAssets("t:...")` and serialized values first.
- **A serialized field added after assets exist takes its C# initializer on every asset**; set it where needed and grep the `.asset`. A new bound folds the old value in (`PlayerController.ActiveYawLimits`: unset `(0,0)` falls back).
- **Timing that depends on each other lives in one pacing asset per flow, read by logic and visual**: `PokerHallucinationPacing` (blink + effect ease), `PokerDeathPacing` (shot, fall, head), `PokerDealPacing` (wait + flight + rest), `PokerConsumePacing` (eating), `PokerRollPacing` (death rolls, on the roller). Animation durations are read off `AnimationClip.length`. Stand-alone beats (reveal, showdown board, `_exitDelay`) stay on their stage; *how much* lives in effect assets and the tiers; *look* in the meter prefab.
- **A server RPC that refuses names the gate**; warn when a setup can never work; silent skips only for things allowed to be missing.
- **An act with a delayed effect waits on something that can die** (`PokerGameMode`'s `destroyCancellationToken`), applies the effect at the end, and is an unawaited `async Awaitable`, not `async void`.

### Seats, players, match membership
- **The server hands out chairs through one entry point.** `PokerSeat.CanInteract`/`CanStand` false; `PokerGameMode.ServerSeatArrivals` → `SeatInteractable.SeatServer`. Mid-hand arrivals sit as `Waiting`; seat-order helpers filter on `CanAct`/`IsInHand`.
- **Chairs fill across the table**: `PokerGameMode.SpreadSeatIndex` interleaves halves, larger first (`(count + 1) / 2`); assert each count visits every chair once.
- **Seat count is replicated table state laid out by arithmetic.** `ServerLayTable` reads `PokerRuleSettings.SeatCount`, writes `PokerGameData.ActiveSeatCount`; `PokerSeatRingVisual` spaces and switches off the rest. Chairs are moved, never spawned (runtime scene `NetworkObject` gets `GlobalObjectIdHash = 0`). Facing from position (`Atan2(-x, -z)`); radius measured in `Awake` (`_radius` 0 → measured, currently `1.1634302`); `_placeSeats` off keeps hand placement.
- **A component that hides objects serializes them and keys on authored index, never a spawn-order registry** (`PokerSeat.All` misses pre-hidden chairs). Both use `seat.SeatIndex`; the ring keeps `_seats`. Only Play mode catches this.
- **A chair is not a place in the match.** `PokerGameMode.StartGame` stamps `PokerPlayerData.InMatch` on seated dealable players. `CanBet`/`CanBeFed` = `InMatch && IsAlive`; `IsPlayingThisMatch`/`CanDealAnotherHand` count stamped players. Not in `CanBeDealtIn`. Late arrivals `Waiting`, not `Dead`, keep cameras and HUD.
- **Joining commits you to the match**: `PokerGameMode.IsCommittedToMatch` releases only once you can't be dealt in.
- **Out of the game is `IsAlive` (`HallucinationRate < MaxHallucination`); dealt in is `PokerGameMode.CanBeDealtIn`.** Never re-derive. The deal and `PokerWaitingStage` mark refused players `Dead`; `DealablePlayerCount` uses the same predicate; refused players keep their chair.
- **The host's seat always counts toward `CanStartMatch`**, even under; `CanDealAnotherHand` still refuses.
- **Shots about your own place need `InMatch`** (not `IsInHand`). `PokerStageCameraBinder._requiresPlaceInMatch`
- **What tells players apart replicates as an index owned by the spawner**: `PlayerData.ColorIndex` → `PlayerColorDatabase`; `PlayerManager` claims the lowest free, frees on disconnect.
- **Everything about a place at the table is a transform on the chair prefab**: `PokerSeat.CardAnchor`, `BetItemAnchor`; `PokerBetItemPotVisual` places caps by `OwnerClientId`. Measure against the table's `Renderer.bounds`. No plausible fallbacks (`CardAnchor` → `SitAnchor` put cards at the ankles).
- **`PokerSeat.AheadAnchor` is at seated-eye height (`0.819`, as `Player/FirstPersonCamera`) at ring radius**, no fallback.
- **Where something goes is a prefab transform, not bone + code offset.** `PokerTurnArrowVisual` only turns; `PokerTableVisual/PokerTurnArrow/Arrow` at local y 0.425 (table top 0.4231 + 2 mm).

### Cards, board, visibility
- **Visibility is a per-card display rule.** Cards replicate to everyone; providers `PokerPlayerData.AddHandVisibilityProvider(Func<data, slot, bool>)`, `PokerGameData.AddCommunityVisibilityProvider`, plus `Notify*RulesChanged`. Providers read owner-read state (`PokerItemKnowledge.KnownCards`, owner-registered). Views call `IsHoleCardVisible(slot)`/`IsCommunityCardVisible(i)`; `PokerHandVisual`/`PokerBoardVisual` keep per-slot shown masks.
- **Where a card is is derived**: `IsHoleCardInHand(slot)` = `!IsFolded && !HandRevealed && HasLookedAt(slot)`. `LookedAtHoleCards` is Everyone-read (lifting is public), changed only by `LookAtHoleCardsRPC` or the deal; `IsHoleCardVisible` (asks `IsOwner`) keeps faces private.
- **What a player may look at is server-enforced, stamped before the cards go out**: `PokerDealStage` writes `ViewableHoleCards` before dealing. `_dealIntoHand` stamps `LookedAtHoleCards` first so cards fly deck → fan (Liar); those leave rightmost first (`PokerDeckVisual.Deal(…, intoHand)`) with an extra `PokerDealPacing.IntoHandRoundGap` per round, carried on `PokerDealTurn.IntoHand` so `DealDuration` and the board delay count it.
- **A card shown to the table stays in the hand; the table reads it off the known-cards row over the head.** `PokerPlayerData.ShownHoleCards` only feeds `UI_KnownCardsRow`, never changes visibility/in-hand. Timed shows are a `ShowCard` table rule; `PokerItemModule` turns a card back down when a street opens with no rule covering it (`ServerShowCard`, `_shownStreets`). Private peeks use the same row.
- **Folding sets status, cards and pose together in `PokerPlayer.ServerFold`, gesture included.** Mucking changes status only (`IsFolded` hides cards and raises `OnHoleCardPresentationChanged`; never `HoleCards.Clear()`). Ask `IsInHand`, never `CardCount > 0`.
- **The board is `PokerGameData.CommunityCards` + `RevealedCommunityMask`**, dealt face down, turned by streets (`_communityCardsToReveal`, own places: flop 0–2, turn 3, river 4; server counts `_streetTurnedCount`) or items (`ServerRevealCommunityCard`; `ServerConcealCommunityCard` before a street owns it). A card an item lays mid-trade is withheld from street turns (`ServerAddWithheldCommunityCard`/`ServerReleaseCommunityCard`, `PokerItemPlaceOnBoard`). Showdown scores only `IsCommunityCardRevealed`.
- **`PokerBoardVisual` (`PokerTableVisual/Board`) is a `PokerCardRowVisual` spaced for `CommunityCardCount`** (`SpaceFor` from the first card; `_minimumSlots` 5 fallback), dealt via `PokerDeckVisual.DealBoard` (hand size from the running deal stage). Board turn is per screen: local chair's yaw (re-read on seat change and first board card), pivot at the ring centre (the `Deck`'s spot, not the root, offset 0.1), sliding `_viewerOffset` toward the chair. `SetStanding` tweens it upright (`UIPokerBoardStandButton` on `UI_ShortcutButtons` — Items, View Board, Helper — switching itself off; hotkey `Table/ViewBoard` on V, its own action).
- **Replacing what a replicated list holds writes each slot, never clear-and-refill** (reads as a deal).
- **Cards that change places are announced, flown, then written.** Server raises the event, waits the pacing flight, writes slots; screens hide real cards, fly stand-ins, show each on slot change (with backstop). `PokerItemModule.ServerExchangeCardsAsync`, `PokerCardExchangeVisual`
- **Name an event after what changed.** `OnHoleCardPresentationChanged` (feeds `IsHoleCardVisible`/`IsHoleCardInHand`) vs `OnStateChanged`. `PokerPlayerData`, `PokerHandVisual`
- **Before adding a replicated fact, check whether an existing one implies it** (e.g. `HandRevealed`).

### Betting, pot, eating
- **Street, Bet, BetItem, PotEntry and Item are five different things**: Street = betting round (`PokerStreetStage`), Bet = act (`PokerActionType.Bet`), BetItem = cap staked and eaten, PotEntry = ledger line, Item = usable card (`docs/items-design.md`). "Wager" retired. Sizes are `*Size` (`_stakeSize`, `_biteSize`), never `_itemsPer*`. `CONTEXT.md`
- **The pot is the ledger, written only by `PokerTableUtility`.** `PokerGameData.PotEntries`: one `PokerPotEntry` (owner, phase, `ItemType`) per cap, the only stake record. Writes: `PlaceBet`, `SwapPotToLosers`, `DiscardStakesOf`, `ServerTakePotEntry`, `ResetPot`. **A cap leaving alone is being eaten** (`PokerBetItemPotVisual` flies it to the mouth), so settlements clear and re-add, never `RemoveAt`.
- **Something changing hands changes owner, never gets copied**: `SwapPotToLosers` resets and re-adds stamped with new owners. `PokerWaitingStage` calls `ResetPot`.
- **Settlement: `PokerShowdownStage._settlement`** — `SwapToLosers` (normal: every loser gets a full copy of the winner's stake; losers' own caps gone; folders eat only their opening cap; only players dealt in, `IsInHand` or `IsFolded`) or `OwnStake` (Liar: winners' caps discarded, others eat their stake). Ties all win; the one nearest `PokerGameMode.HandOpenerClientId` (last winner, else host on the first hand, each falling to the next dealt player) is `LastWinnerClientId`; every street starts from the opener. Payout choices are enums, never two bools.
- **A stake's kind and size are separate**: player picks kind, `PokerStreetStage._stakeSize` sets count.
- **Feedable ≠ stakeable**: `PokerBetItemDatabase.Entry.Bettable` (off for Colorful), read by the random draw and `IsBettable`.
- **An action's `amount` can carry an identity**: `Bet` a kind, `Target` a seat, via `PokerGameMode.SubmitActionRPC(action, amount)`.
- **All in pays the street's bet then the all-in cap; matching means ending with as many caps.** `PokerAllInStage.MatchAllIn` tops up with drawn kinds, all-in cap last.
- **A named-kind value is an enum; a set is a list, never a bitmask.** `PokerBetItemType`, `Consumed` `NetworkList` via `PokerBetItemUnit` (for `IEquatable`); database rows name their kind; never renumber, retired kinds keep numbers.
- **Each mode brings its own item database; draw kinds from `GameMode.BetItemDatabase` (and hands from `GameMode.HandDatabase`)**, never a serialized copy. `PokerBetItemPotVisual`, `UIPokerPotPanel`, `UIPokerHallucinationMeter`
- **Keep one registry per kind of spawned thing**: every cap spawner feeds `PokerBetItemCapRegistry`; deregister on destroy (eaten caps keep paint); `Remove` compares with `is null`.
- **A player eating is the player's own controller.** `PokerBetItemConsumeController` (`Player_Poker/BetItems`) keeps the eaten list; `ServerEatPlate(PokerBiteRule)` swallows the player's ledger caps paced by `_pacing` (`PokerConsumePacing`, in `Items`), raises `OnPlateFinished` once. `ServerStopEating` pays the current mouthful and flushes a queued roll. Price stays with `PokerBetItemHallucinationEffect`. Renames: `AssetDatabase.MoveAsset` + `[FormerlySerializedAs]`.
- **A round's consequence is its own beat**: `PokerBetItemConsumeStage` walks seats, asks each with `HasPlate` to `ServerEatPlate` with `_biteRule`, waits `OnPlateFinished` + `_handoverDuration`. `_eatWholePlate` = one mouthful per plate except `_eatenOnTheirOwn` kinds (Colorful) (on in `PokerStage_Liar_Consume`, off in normal). `CanEat` = alive; the dead are skipped.
- **One mouthful is bite, impact, then the world changes.** `PokerBetItemConsumeController` takes caps off at the bite into `_pendingItems`, calls `ConsumeServer` after the impact (`EatPlateAsync`). Impact owed from `PokerBetItemEffect.PreviewHallucinationGain` (exact; chance outcomes excluded).
- **A beat paced around a change waits for what it sets off, asking the owner**: rate read around the effect, `PokerHallucinationController.BlinkWait`, `PokerDeathPoseController.DeathWait`. Bite length and gap tuned separately in `PokerConsumePacing`.
- **How hard a cap lands is a dose queued on the eater**: `PokerBetItemConsumeController.ServerQueueDoses(dose, caps)`; each cap draws a dose (default 1), 0 skips entirely. BetItems never names Items. `PokerItemMushroomDose`
- **One effect asset serves every kind** (`PokerBetItemEffect.ConsumeServer(gameMode, eater, itemType)`); first taste costs more (`HasConsumed`); no record reads as never eaten.
- **Hallucination is its own currency and never touches blood**; the ceiling eliminates. `Health` is blood, counted as damage against `MaxHealth`.
- **Going under writes the ceiling, never a flag**: `PokerBetItemColorfulEffect` rolls against the new rate and writes `MaxHallucination`.
- **A roll is shown before it is paid; `PokerHallucinationRollController` is the one place rolls happen.** Colorful caps queue (`ServerQueueRoll(against, rateBefore)`), items roll at once (`ServerRoll`); both on the roller's `PokerRollPacing`, both wait `ServerOutcomeRemaining`. `RollRPC` sends roll, lead-in, sweep, hold; ceiling written after. Lead-in stretches over a blink. `OnRollSettled(roll, fatal)` for feedback. `ServerStopEating` flushes unstarted rolls.
- **The winner names the Colorful victim in public** (`PokerColorfulPickStage`, seat via `PokerActionType.Target`); timeout feeds the winner; self allowed; players at the ceiling excluded.
- **How something ended is one enum value**, not a bool plus fields.

### Items
- **Items are a module: the table deals, gates and remembers; the card only acts.** `PokerItemModule.GetAvailability` answers picker and server (hidden if forbidden, dimmed with reason). A `PokerItem` asset = config + `OnGetAvailability`/`OnUseServer`, stateless. Table rules are `PokerItemTableRule` keyed by `StreetSerial` (only counts up; next street = serial + 1). Holdings/knowledge owner-read on the player (`PokerItemInventory.Items`, `PokerItemKnowledge`); only the count is public. Dealing: `_itemsPerMatch` (first deal of match), `_itemsPerHand` + `_loserBonus`, `_colorfulSurvivorItems` (to `PokerColorfulPickStage.FedClientId` if `IsAlive`, when consume ends). `docs/items-design.md`, `docs/baccarat-poker-design.md`
- **Item availability runs on every client, so reads only replicated state.** Deck count is `PokerGameData.DeckRemaining` (from `PokerDeck.OnRemainingChanged`); the deck stays a server queue, never a `NetworkList`. `PokerItemExtraDraw`
- **A move made by looking is aimed on the owner and named on the server**: send the aim on change; server validates and lights it; the cast asks the server predicate of every body it passes; size the hit buffer for everything.
- **A player or card is picked by pointing, and what is lit is what the click commits.** `PokerTargetPointer` (owner-only, `Player_Poker/Table`) takes a `PokerTargetQuery`, returns a `PokerTarget`; raycasts bodies via `AimTarget` and cards via hands (`PokerPlayer.HandVisual.SlotOf`) and board. Filters are server predicates (`PokerColorfulPickStage.CanBeFed`, item `Accepts*`). `PokerColorfulPickController`/`PokerItemTargetingController` only decide when. Own body never lit; self-pick is your HUD meter name (`UIPokerColorfulSelfPick`). Resolve the stage only on id/turn change (no per-frame `FixedString.ToString()`).
- **A local hover is ORed with replicated state, never written into it.** `PlayerVisual.SetLocalOutlined`
- **A `CharacterController` is not a hit volume** (owner-only); `AimTarget` is a `SphereCollider` on the body rig's `Chest_M`.
- **Diagnose a cast that finds nothing by drawing it**: colour-code rays (own body distinct), log hits once per change.

### Notices
- **What the table is told is an event on `PokerNoticeChannel`, never a replicated value.** `ServerAnnounce` (everyone), `ServerTell` (`SendTo.SpecifiedInParams`). `PokerNotice` carries data; the feed picks wording. `UIPokerActionNoticeFeed`

### Hallucination
- **A hallucination is a ladder of `(threshold, pool, drawCount)` rows**; rungs draw from their pool, effects stack, re-climbing re-draws; no repeat within a climb; controller owner-only. `PokerHallucinationTiers`, `PokerHallucinationController`
- **Mutually exclusive effects share a group** (rolls one member; live rungs one group per category; excludes within one draw only, so conflicting pairs live in one rung's pool). `PokerHallucinationGroupEffect`, `Hallucination_HeadSize`
- **Effects making one look together are a composite** (runs/stops all, lingers for the longest ease-out). `PokerHallucinationCompositeEffect`, `Hallucination_MotionTrail` (after image + `Hallucination_MotionBlur`, URP Motion Blur, Camera and Objects)
- **An effect in no rung never runs, silently.** `Player_Poker` runs `PokerHallucinationTiers` (`_Sample` is scratch); verify pools via `SerializedObject`. `Hallucination_MushroomPeople`
- **A `ScriptableObject` is config; what runs is a scene object.** Asset holds config and `Attach`; a `PokerHallucinationEffectBehaviour` child per rung holds state (`Stop`, `OnDestroy` backstop, `LingerSeconds`). Unity messages declared once in the base with hooks (`OnDisposed`). `PokerHallucinationEffect`
- **What an effect acts on is a separate asset**: a target lists `PlayerBone`s (pairs in one target); face bones bound on both rigs in `Player.prefab`; unbound bones skipped. `PokerHallucinationTarget`, `HallucinationTarget_OtherEars`
- **A prop is asked to wear one of its own looks.** Counted requests, last caller wins, `OnVariantChanged`; swaps `sharedMesh` on the shared skeleton, never hides bones (`Root_M` takes the camera). Name variants for what the prop becomes. `PropVariantController`, `PokerHallucinationVariantEffect`
- **A look can be an animation**: `PropVariantAnimator` sets `IsTransformed` while `Transformed` (read on enable). `Animator_Mushroom.controller` + override per kind, `AlwaysAnimate`; after the transform, `Dance1`–`Dance4` loops picked by `DanceIndex` from `PropAnimatorRandomIndex` on enable (local).
- **An asset can't reference a scene object; the scene registers by enum key.** `PokerScenery` (`PokerSceneryGroup`), `PokerHallucinationSceneryTarget`
- **A room is authored in the scene and switched on, never spawned.** Controller holds `(PokerRoomVariant, objects)`, registers, counts requests, swaps at once (blink hides it), switches others off before the chosen on, warns on enable about missing entries. A room may name its `_skybox`; empty keeps the sky the active scene had when the controller enabled, handed back on disable (the active scene is `Bootstrap`, not gameplay). Room scenery is cheap by rule: no renderer casts shadows (no room light does), a prop moved by `WorldMotionLoop` (whose offsets are in the object's own axes, so an FBX with a Z-up root sits under an unrotated pivot, and a slide along one world axis is that axis in local space) carries no collider, which physics would otherwise rebuild every frame, and emissive panels carry the look so a room holds a handful of realtime lights, not one per fixture. `PokerRoomController`, `PokerHallucinationRoomEffect`, `Rooms/Backroom`
- **Something attached to a bone is placed in the pose it's seen in**: play `Sit` in Play mode, set the world pose, read local pose back into the prefab, judge from a render. `ExtraArm` = `Hand_Attach` (own `EZSoftBone` chain) under body rig `Scapula_R`, rising behind the right shoulder.
- **An afterimage is a baked mesh drawn by a mesh particle, never VFX sampling a Skinned Mesh Renderer.** `BakeMesh(mesh, true)` excludes renderer scale: place with full `localToWorldMatrix`. `PokerHallucinationGhostBehaviour` bakes each drawn skin (relative to body root, world axes) once a bone moved `_motionThreshold`, into one of four snapshot meshes in turn, sends `Echo` with `position` and `meshIndex`; born at `Alpha`, times `AlphaOverLife`. `_pose`: `Current` (drifting ghost) or `Previous` (`Hallucination_AfterImage`, held pose, no velocity/`Drift`). `_echoLifetime` capped under its snapshot's rebake (four intervals, three for `Previous`); Output Particle Mesh `MeshCount` 4 (`Mesh0`–`Mesh3`). World space, manual 200 m bounds on the origin. Judge orientation from above, in Play mode. `VFX_HallucinationGhost`, `Hallucination_Ghost`
- **A card hallucination reaches the board through the card target**: `PokerHallucinationCardTarget` `_includeBoard` adds `PokerBoardVisual.Cards` (scope doesn't apply), re-resolving on `PokerHandVisual.OnAnyHandChanged`/`PokerBoardVisual.OnAnyBoardChanged`.
- **A distortion Replaces its subject's material; only a tint can Add.** `Card_Wave` (Replace, clamps bent UV to the card before remapping), `Card_Rainbow` (Add).
- **Order transparent passes by render queue**: on URP set `_QueueOffset` and read it back (rainbow 3001, faces 3000). `Card_Rainbow.mat`
- **An added card pass samples its own texture at `uv0`**; only card graphs read UV2. `Card_Pattern.mat` (`Sprites/Default`, `_MainTex`)
- **Add and Replace are different operations** (`PropPaintMode`, `PlayerMaterialOverrideController`).
- **A renderer's authored materials are captured once by one owner**: `PropMaterialOverrideController.Claim(renderer)`; callers null-check.
- **A renderer on a prop that isn't part of its look carries `PropPaintIgnore`.** `PokerHallucinationMaterialBehaviour.Paint`
- **A blend shape is driven by name through `PlayerBlendShapeController`**, re-resolved on `OnAppearanceChanged`, modifiers like the bone stack; names matched fully, then by the channel after the last dot. Read what the art shipped before choosing a mechanism (`Hallucination_Breasts` → `PokerHallucinationBlendShapeEffect`, guid kept).
- **A tween's per-frame callback does no lookups**; bind at resolve. `PokerHallucinationScaleEffect`
- **One meter drawn in two places, reading only replicated state**: `UI_HallucinationMeter` bound by `UIPokerHallucinationBar` and `PokerHallucinationTagVisual` (binds from `Start`). The skull sweep is one tween, one ease. `UIPokerHallucinationMeterFeedback` answers `OnRungCrossed`/`OnRollSettled` on `Bar/Visual`, not `Bar`.
- **FOV has one writer, `PlayerCameraFovController`** (on `Player/FirstPersonCamera`): callers `Add` a modifier and ease its `Weight`; weighted pulls sum. `Hallucination_WideView` (to 100) kept out of the ladder; the wide-eye rung is `Hallucination_Fisheye` (URP Lens Distortion, intensity 0.7, scale 1).

### Player prefab, components, rigs
- **Props stay cosmetic.** Generic pieces (`PlayerActionAnimator`, `PlayerAnimatorStateController`, `PlayerBoneScaleController`) live in `Game.Runtime.Player`, replicate only the act; meaning and grant `NetworkVariable`s belong to mode-side controllers. No per-feature state on `PokerPlayerData`/`PokerGameData`.
- **Feature components sit on named children of the player prefab** (`Cards`, `BetItems`, `Hallucination`, `Body`, `Table`); only the model and owner (`PokerPlayerData`, `PokerPlayer`) stay on the root. No `[RequireComponent(typeof(PokerPlayerData))]`; resolve with `GetComponentInParent`. Move via `AddComponent` + `EditorUtility.CopySerialized`, repoint by sweeping `SerializedObject`s, destroy originals **last**, diff the full type-and-path list.
- **From a bone, use `PlayerRigController.FindOnBody<T>`** (warns on failure), not `GetComponentInParent`. Routing to a body is the target's statement: `PokerHallucinationTarget.ResolvesBodies` (true only on `PokerHallucinationPlayerTarget`), gated in `PokerHallucinationMaterialBehaviour`. Prove hierarchy claims by asking the prefab.
- **Two rigs, one act; bones and hold points are references, never name searches.** Owner renders `Gnome_HandOnly`, others `Gnome_rig`; `PlayerBoneRig`/`PlayerRigController` (`RenderedRig`, `RenderedHead`); `PlayerBone.HoldRight` bound on both rigs (`BoneName` is an editor hint `OnValidate` resolves). `PokerBetItemCarryController`. Append new `PlayerBone` values at the **end**.
- **The gnome models under `Player.prefab` `Models` are FBX instances; lists about the model don't propagate.** After a model update check `PlayerVisual._slots`, each `PlayerModel`'s meshes, `PlayerFingerVisual._fingers` (`PlayerFingerBinder`). Renaming a bone orphans what hangs on it (`PrefabUtility.GetAddedGameObjects`); re-seat from the previous FBX via git, applying `Inverse(newBone rot) × oldBone rot`.
- **A Generic avatar binds clips by path; animation FBXs are re-exported against the current rig** (`Gnome_Model_Rig.fbx`; clips in `Assets/Art/Character/Gnome/Anim/`). Unbound head curves look like a spinning camera. Diagnose by writing into a bone and seeing if it returns next frame; sample the clip first. Delete compatibility shims once their condition passes.
- **A controller holds clips by sub-asset fileID**: a replaced clip set leaves dangling motions (`state.motion == null` with `m_Motion` set); pick replacements from the previous controller in git. New takes arrive loop off; set `loopTime` on idles/holds. `Animator_Gnome` mapping:
  - `_Idle`/`Sit` → `Anim_IdleLoop`; `Sit_FlipUp` → `Anim_CamBaiLen`; `Sit_Peek` → `Anim_CamBaiIdle`; `Sit_Show` → `Anim_Show`; `Sit_IdleRandom` → `Anim_IdleRandom`; `Sit_PeekRandom` → `Anim_CamBaiIdleRandom`.
  - `Sit_FlipDown` → `Assets/Prefabs/Animation_Sit_FlipDown.anim`, `Anim_CamBaiLen` reversed — **rebuild whenever `Anim_CamBaiLen` changes** (negative speed clamps on frame 0).
  - `_Bet` → `Anim_Bet_NoCard`, `_BetHoldingCards` → `Anim_Bet`; `_Fold` → `_FoldLoop`; `_ConsumeItem` → `Anim_AnNam`; `_ShuffleCards` empty for art.
- **Swapping a rig: repoint before deleting, map roots by hand, take a null baseline, unpack instances before lifting props off bones.**
- **A skinned mesh deforms by bone index**: compare bone names in order against the model asset renderer with the same `sharedMesh`; fix with `Assets → Player → Repair Skin Bones` (`BoneRemapper` preserves scrambles).
- **A variant rebuild keeps the guid but kills fileIDs; a variant can't reparent or delete inherited objects.** `Player_Poker` (variant of `Player`) adds `Poker*` components, anchors, overrides; children differ by active flag (`Non_Gnome` hat on for Poker). Transplant with `CopySerialized` + path remap, save to the same path, repoint holders (network prefab list, scene `PlayerManager`).
- **Rebuild a prefab from the object and verify by diffing the complete `GetComponentsInChildren<Component>(true)` type-and-path list**, never a summary.
- **A rig in a preview scene proves nothing** (`NewPreviewScene` runs no `Awake`); judge in Play mode beside the source FBX under `Animator_Gnome`.
- **After a rig swap, check `avatar` on every Animator**, including unrendered rigs.
- **An Animator with all renderers disabled freezes under `CullUpdateTransforms`**; hidden rigs that are measured, drive props or feed lookups use `AlwaysAnimate`. `PlayerVisual`
- **What a player is drawn as is model + outfit + version, resolved by `PlayerModel` into a mesh per slot and material per submesh.** Model local (`_defaultModel`); outfit replicated server-written `PlayerOutfitId` (fallback model's first; empty list wears nothing); version (`PlayerLookVersion`) local, unauthored paints Cartoon (`PlayerModel_Gnome`'s FBX materials). Switching models never touches bones: `PlayerVisual` writes `sharedMesh` per `PlayerSlot` (`Body`, `Head`, `Outfit`, `Hat`, `HandBody`, `HandOutfit`); empty slot hides, bone-count mismatch warns. Requests via `PlayerAppearanceController` (last-caller-wins per axis); hallucination overrides outrank. `WriteMaterials` writes exactly `subMeshCount`. A piece with its own texture is its own slot. Colour placement: `PlayerModel.Tints`, read by `PlayerColorVisual`. Retiring a material: sweep skin, FBX `.meta` external remaps, dead overrides (`PrefabUtility.RemoveUnusedOverrides`).
- **A runtime tint needs a shader with the property; check `Material.HasProperty`.** Hat (`Cone_Hat`, URP Simple Lit `_BaseColor`) carries it; author it on the prefab too.
- **Only `PlayerVisual` writes `sharedMaterials`, composing skin, override and outline in one pass**; `PlayerMaterialOverrideController` picks the winner (`SetMaterialOverride`). The outline pass is appended to what the renderer wears (read, append, write; re-hang after `ApplySkin`), always worn, toggled by colour (`_idleOutlineColor` clear vs `_outlineColor`), authored clear on the prefab. All slots including the hand rig wear it; distinct from the crosshair `Interaction` outline. `Player_Outline_Always.mat` queue **2900** (on the material, not `Interactable_Outline`, whose crosshair material stays 3000).
- **A body part that can be lost collapses at its bone via `PlayerBoneScaleController`**, never `renderer.enabled`. `PlayerFingerVisual` zero-multiplies `…Finger2_L/R` on **both** rigs.
- **Blood is drawn as fingers**: 8 (`_startingHealth`/`_maxHealth` 8 on `Player_Poker`); `PokerBloodFingerVisual` uses `MaxHealth - Health`, reads in `OnNetworkSpawn`; `PlayerFingerVisual` takes a count.
- **Several effects on one value compose through modifiers.** `PlayerBoneScaleController`: per-bone stack created with the first modifier, removed with the last, `base × ∏mul + ∑add` (`Vector3`, neutrals 1/0); `Add` returns a live `PlayerBoneScaleModifier`. Writes in `LateUpdate` after the Animator, before the brain, rest × multiplier (clips write constant scale 1). Test against base ≠ 1 (×2 then ×0.5). Count a clip's curves before blaming it.
- **The Animator restores rotations, not positions**: cache rest `localPosition`, write it back at the top of `LateUpdate`.
- **Anything posing a bone the camera hangs off runs between `PlayerController` (order 0) and the brain (order 100).**
- **Teleport through `NetworkTransform.Teleport`** (authority, `OwnerNetworkTransform`, `CharacterController` disabled around it). `PlayerController.Teleport`

### Animation
- **One-shot gestures go through `PlayerActionAnimator`**: ids in `PlayerActionIds`, mapping in `PlayerActionAnimationDatabase` (only ids with a state: `Fold`, `Bet`, `Laugh`, `Idle`, `ConsumeItem`, `Impact`, `SnapFingers`), states on `Gestures`; missing states skipped. Poses stay on base with `PlayerSeatController`. `AlternateWhen`/`AlternateStateName` (`Bet` → `_BetHoldingCards` while `IsHaveCardOnHand`); clip-timed code keys off `PokerPlayerData.IsHoldingCards`.
- **Layers: `Base → Gestures → Smile → Death`; `Death` last.** Adding a layer renumbers later ones — grep for layer indices (`PlayerActionAnimationDatabase` rows `Layer = 1`). Held celebration clips loop; parameter names in one place (`PokerWinnerPoseController`).
- **An act lasting as long as a decision is a replicated bool**: state with no exit transition clamps on its last frame; three clips (start one-shot, arrive-and-stay, leave on falling bool). `PlayerAnimatorStateController`
- **Named animator flags replicate through `PlayerAnimatorStateController`**: server-written `NetworkList<PlayerAnimatorState>`, applied to both rigs, snapped on late join; `ServerSetBool` idempotent, skips missing params. Name a rig flag for its meaning and drive it from the answering state (`IsHaveCardOnHand` by `PokerHandPoseController`).
- **A flag raised by one beat and dropped by another is dropped on every exit**: the winner's smile is dropped by the Colorful pick and by `PokerWaitingStage`.
- **A gameplay marker is an animation event in the clip asset.** FBX: `ModelImporterClipAnimation.events` in `.fbx.meta` (via `defaultClipAnimations` when `clipAnimations` is empty; confirm controller fileIDs survive). `.anim`: `AnimationUtility.SetAnimationEvents`. Each calls `PlayerAnimationEventRelay.OnAnimationCueEvent` with the cue name, `DontRequireReceiver`; the relay on each rig re-raises a C# event behind the drawn-rig gate. Keep a timeout backstop that outlasts the clip (clip length + margin: `PokerColorfulPickStage` `_serveClip` `Anim_BungTay` + `_serveCueMargin`). Grep the clip when adding a listener. Markers:
  - `Anim_CamBaiLen` CardPickUp@28; `Animation_Sit_FlipDown` CardPutDown@17; `Anim_Show` CardShow@0
  - `Anim_Bet_NoCard` and `Anim_Bet` BetGrab@14 / BetRelease@26
  - `Anim_AnNam` EatGrab@2 / EatSwallow@27 / EatShake@84
  - `Anim_BungTay` SnapServe@42; `Anim_Impact` ImpactShake@1; `Anim_Smile` SmileShake@0
- **A VFX tied to an animation moment is a cue row in `AnimationVfxCueDatabase`**, installed as runtime events (`InstallEvents` strips previous first, `DontRequireReceiver`, removed on `Application.quitting`), previewed through the same spawn call; one `AnimationVfxPlayer` per animator, gated on an enabled renderer. Fixture: `Assets/Tests/AnimationVfx/AnimationVfxTest.prefab`. Fold base `AnimationCueDatabase` into its one subclass next time it's touched.
- **Hand-IK weight lives in `PlayerHandIkController._states`**, one curve per gesture over normalized time. Release markers go on the frame the hand is at the spot; `PokerBetItemCarryController` grabs/releases on markers.
- **A contact frame is measured inside the clip carrying the marker**, sampling with `clip.SampleAnimation` on the animator's own object; ask when it must leave to land when the hand is ready. `CardPickUp` frame 28 of 30 lands on the still `Anim_CamBaiIdle` hold (exit 0.9 + 0.05 s, state ends at normalized 0.94); `PokerHandVisual._cueTimeout` 3 s vs cue ~1.43 s. A perfectly still bone is unfinished art or a beat elsewhere (`Anim_Show` right wrist).
- **Measure seated timing/fit with the Animator running**: instantiate `Player_Poker`, `Animator.Play("Sit")`, `SetBool`/`CrossFade`, step `Update(1/48)`; `SampleAnimation` leaves the root at bind pose (17 cm off).
- **Art-checklist animations wired to existing moments**:
  - **Seated fidgets**: `PlayerIdleVariation` on `Player/Animation`, local, `IdleVariation` every 6–12 s only in `Sit`/`Sit_Peek`.
  - **Showing**: `PokerHandPoseController` writes `IsShowingCards` (= `HandRevealed`) before `IsHaveCardOnHand`; holding states try `→ Sit_Show` before `→ Sit_FlipDown` (requires `!IsShowingCards`).
  - **Betting**: `PokerStreetStage` plays `Bet`; `PokerBetItemPotVisual` hands caps staked on `FirstStreet`/`SecondStreet` to the staker's `PokerBetItemCarryController` between `BetGrab`/`BetRelease`; `Relayout` skips carried caps.
  - **Eating**: one `ConsumeItem` per mouthful; `BiteDuration` = `Anim_AnNam` length (`PokerConsumePacing._biteClip`), impact wait `_impactClip`; carried `EatGrab` → `EatSwallow`. Early cues: `PokerBetItemCarryController` grabs at once within `_lateCueWindow`.
  - **Going under**: `IsDead` from `PokerDeathPoseController` drives the `Death` layer (override, weight 1). Shot, pose and head wait `BlinkWait` (dying player's `TransitionDuration` when a rung is crossed); pose after `PokerDeathPacing.PoseDelay`; every machine calls `PlayerController.SetHeadLookSuspended`. `PokerDeathVisual` hides `_hiddenSlots` (Head, Hat) via `PlayerVisual.SetSlotHidden` after `HeadVanishDelay`; late join headless at once. `PokerDeathPoseController` owns death timing; `PokerBetItemConsumeStage` waits `DeathWait`.
  - **Winner / Colorful pick**: showdown plays `Laugh`; `Serve(target, chooser)` plays `SnapFingers` and `ServerSetFocus(target)` only for a real choice, after `ClearTurn`, before the stage finishes.
- **A camera shake is a Cinemachine impulse on `FirstPersonCamera`, never a bone key.** `PlayerCameraShakePreset`: 6D `NoiseSettings`, strength, frequency, attack/duration/decay, Legacy impulse, **own** `CinemachineImpulseDefinition` each; pooled events are ours only while carrying our signal. `CameraShakeNoise_6D` = 6D Shake, position ×0.1, rotation ×6. Timing read off `Head_M` angular speed in the clip; markers on looping clips fire each loop (`SmileShake`).

### Camera and look
- **Animation moves the body, input moves the view.** `Player/FirstPersonCamera` on the root at the seated eye faces `root × Euler(pitch, yaw, 0)` (written in `PlayerController.LateUpdate`) and never reads a bone.
- **Look drives `Chest_M` standing, `Head_M` seated**: `AngleAxis(yaw) * AngleAxis(pitch) * localRotation` on the animated pose, axes from the animated parent each frame; `ActiveLookTransform` picks.
- **Look limits live only on `PlayerController`**: `_pitchLimits`, `_yawLimits` (seated), `_focusYawLimits` (`ActiveYawLimits` widens to ≥ `_yawLimits`). Seats hold only their animation state and call `ResetLook()`. Standing yaw turns the body; seated turns the bone and replicates.
- **`_bodyAnchored` and `PlayerLookMode` are separate** (`SetLookModeOverride`/`ClearLookModeOverride`, set on every peer, released never restored).
- **A mode is a branch inside the controller, not a second `NetworkBehaviour`.** `PlayerController`
- **Mouse delta is never scaled by `deltaTime`; a stick is.** Look is polled, branching on `InputSchemeController.IsGamepad`. `PlayerController.ReadLookInput`
- **An aim others should see steers the look**: `PlayerController.SetLookTarget` at `_lookTurnSpeed`; input wins while turning and for `_manualLookReleaseDelay`; nothing steers during a counted hold (`BeginManualLook`/`EndManualLook`, held by `TableCursorController`).
- **Aim solves by inverting the composition, never `LookRotation`**: `SolveLookAngles` pitch first (root nearer current), then yaw, reading the bone at the top of `LateUpdate`, solving twice; aim against `PlayerCameraFollow.RestingRotation`.
- **Each camera behaviour is a `PlayerCameraState` subclass on its own child under `Cameras`**: non-virtual `Enter`/`Exit` with `OnEnter`/`OnExit`; states resolve their own target; `PlayerCameraController` picks from a request stack via handles. `PlayerCameraFreeflyState`, `PlayerCameraLookAtState`
- **When one thing replaces another in a slot, outgoing teardown runs first.** `PlayerCameraController.Apply`
- **Freefly raises nothing, only hands the look back.** A shot goes live by enabling its `CinemachineCamera` **component**; idle shots disabled, priority dropped, shipped disabled. Blend = brain's `DefaultBlend` in `Bootstrap`.
- **The camera knows the round; the round knows nothing of the camera.** A shot = state + `PokerStageCameraBinder` listing picked `PokerStage` assets; binders release and request in one `Update` pass. Nothing in `Game.Runtime.GameMode.Poker.Stages` references a camera type.
- **A turn-dependent shot re-aims inside one state**: `PokerStreetCameraState` frames a `PokerSeatAnchor` on your turn, the decider otherwise, else `FocusClientId`; `PokerAllInStage` walks pending players every `_focusDwell`. `_requiresPlaceInMatch` only for own-seat shots (`State_OwnCards`).
- **`CurrentTurnClientId` and `FocusClientId` are separate**; `BeginTurn` writes both; `PokerBetItemConsumeStage` sets only focus; the machine clears focus at each handover.
- **A shot waits `_aimDelay` (unscaled) before swinging**, in `PlayerCameraLookAtState.Aim`; the null-target retry waits none.
- **A shot at a seated player turns the head, never a fixed height.**
- **A shot about *you* aims at `SelfFocusPoint`** (child of the player root); others aim at `PlayerRigController.FocusPoint`; pick by `IsOwner`.
- **Watching someone cuts to *their* `PlayerSpectatorCamera` while turning the watcher's head**; the owner is never cut from their eyes (`PokerFocusPlayerCameraState`). Death cuts every screen, owner included, for `PokerDeathPacing.ShotDuration`, starting `BlinkWait` after `HallucinationRate` crosses `MaxHallucination`, only on the crossing. `PokerDeathCameraState`, `PokerDeathCameraBinder`. Spectator blends in `Assets/Settings/CameraBlends.asset` (in 0.35 s, out 0.5 s); retune `PlayerSpectatorBodyController` delays (0.15/0.45) with them.
- **While any spectator camera is live the owner's full body is drawn; `PlayerSpectatorBodyController` is the only `SetRenderAllBody` writer**, via `PlayerSpectatorCamera.AnyLive`/`OnAnyLiveChanged` after `_showBodyDelay`/`_hideBodyDelay`; `RenderedRig` still answers by ownership.

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

Put it in the section it belongs to, cite the file that demonstrates it, and fix any rule it contradicts — a rule that disagrees with the code is worse than none. **Keep each rule short**: bold rule, one to three sentences on the failure and fix, the file that shows it; no debugging stories or measurement logs. When a feature is deleted, delete or generalise its rules in the same change.
