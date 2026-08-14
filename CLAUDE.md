# GamblingGnomes — Project Conventions

## Tech stack (mirrors ProjectGamble)

- **Unity** with **URP** (`com.unity.render-pipelines.universal`).
- **Netcode for GameObjects** (`com.unity.netcode.gameobjects`) for multiplayer — server-authoritative model.
- **New Input System** (`com.unity.inputsystem`).
- **DOTween** (`Assets/Plugins/Demigiant`) for tweening.
- **Odin Inspector** (`Assets/Plugins/Sirenix`) for inspector tooling — used selectively, not everywhere.
- **Facepunch.Steamworks** (`Assets/Plugins/Facepunch.Steamworks`) for Steam integration.
- **Addressables**, **Timeline**, **Visual Effect Graph**, **Post-processing** as needed.
- C# language version: 9.0.

All of the above packages/plugins have already been installed/copied into this project to match ProjectGamble (`Packages/manifest.json` + `Assets/Plugins/`).

## Folder structure

Follow ProjectGamble's layout:

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
  Plugins/           — third-party libs dropped in as source/DLL (Demigiant, Sirenix, Facepunch.Steamworks)
  Settings/          — URP assets, Input Actions, volume profiles, Build Profiles
```

Do not scatter scripts outside `Assets/Scripts/`. Do not create per-feature asmdefs — keep the flat two-assembly split (`Game.Runtime` + `Game.Editor`) unless the project grows enough to justify splitting.

## C# code conventions

**Namespaces**: mirror the folder path exactly, rooted at `Game.Runtime` / `Game.Editor` (e.g. a script in `Scripts/Game.Runtime/UI/Poker/` → `namespace Game.Runtime.UI.Poker`).

**Naming**:
- PascalCase for all types; file name matches class name.
- UI `MonoBehaviour`s prefixed `UI` in code (`UIButton`, `UIManager`) — no underscore in the C# identifier.
- Feature/domain prefix for grouping instead of relying only on namespaces (e.g. all poker-specific classes start with `Poker*`).
- Suffixes carry meaning: `*Manager` (singleton-style coordinators), `*Controller` (behavior/logic owner), `*Data` (NetworkBehaviour holding `NetworkVariable`/`NetworkList` state — the "model"), `*Stage` (state-machine step), `*Visual` (presentation-only component), `*Database` (ScriptableObject lookup table), `*Constant`, `*Settings`.
- Interfaces prefixed `I` (`IGameMode`).

**Base types**:
- `NetworkBehaviour` for anything with server-authoritative state.
- Plain `MonoBehaviour` for local/visual-only components.
- `ScriptableObject` (with `[CreateAssetMenu]`) for static config/data assets.
- Structs implementing `INetworkSerializable, IEquatable<T>` for small networked value types (like `CardData`).
- Abstract base classes for pluggable step/ability systems (e.g. a `*Stage` base with `StartStage`/`EndStage`, subclassed per concrete stage; an ability base with lifecycle hooks like `OnInitialize/OnActivateServer/OnActivateClient/OnDeInitialize`).

**Fields & properties**:
- `[SerializeField] private Type _fieldName;` — underscore-prefixed camelCase for private serialized fields.
- `[field: SerializeField] public Type Foo { get; private set; }` for "inspector-set, code-read-only" public members.
- `[HideInInspector] public NetworkVariable<T> Foo = new(...);` — public field (not property) for NGO NetworkVariables, hidden from inspector.
- `[Header("Section Name")]` to group inspector fields logically (e.g. "Player Info", "Game State", "References").
- Constants in PascalCase, not SCREAMING_CASE (`private const int MinimumPlayersToStart = 2;`).
- No regions. No XML doc comments. Comments are rare — only for non-obvious business logic, one line max.

**Patterns**:
- Singleton managers: hand-rolled `public static X Instance { get; private set; }`, set in `Awake()` with a duplicate-destroy guard. No generic `Singleton<T>` base.
- Data/logic split: put networked state in a dedicated `*Data` `NetworkBehaviour` (model), expose it via a `Data` property from the owning controller/stage (logic), keep UI as a separate view that reads `Data` and subscribes to its change events.
- State machines: a list of stage objects + current index, `NextStage()` calling `EndStage()`/`StartStage()` on transition.
- Event-driven sync: plain `event Action`/`event Action<T>`, subscribed in `OnNetworkSpawn`/`OnEnable`, unsubscribed symmetrically in `OnNetworkDespawn`/`OnDisable`. Prefer `NetworkVariable<T>.OnValueChanged` / `NetworkList<T>.OnListChanged` for state→view sync over manual RPC broadcasts of state.
- RPCs: use the modern NGO attribute style `[Rpc(SendTo.Server, ...)]` / `[Rpc(SendTo.Owner, ...)]`, not legacy `[ServerRpc]`/`[ClientRpc]`. Method names keep the `RPC` suffix (`ActivateRPC`, `ReadyRPC`).
- Async: use Unity's native `Awaitable`/`Awaitable<T>`, not UniTask or coroutines, for new async code.
- Tweens: cache `Tween` references in a field, `.Kill()` before restarting.
- No DI container / service locator. Wire dependencies via `[SerializeField]` inspector refs or static `Instance` singletons.
- Odin Inspector (`[ValueDropdown]`, `[FoldoutGroup]`, etc.) only where plain `[SerializeField]`/`[Header]` can't express the need (dynamic dropdowns, foldout grouping) — not as the default on every class.

## Design principles

Hold code to an industry-standard design bar rather than inventing bespoke structure. When facing a design choice, reach for the solution many teams already converge on; check whether this codebase (or the wider industry) already has the shape for it before writing a new mechanism.

- **SOLID + KISS + DRY.** Split into clear modules that can be reused and assembled, not monoliths.
- **Minimise coupling.** A component that needs a dependency takes it from an event or an injected reference rather than reaching into a concrete singleton — a module that can only work in the one place it grew up in is not reusable.
- **Template method for extension points.** A base class keeps its own step non-virtual and calls a `protected virtual` hook from inside it, so a subclass extends behaviour without the parent ever being edited, and shared work can't be skipped by a subclass that forgets `base.Foo()`:

  ```csharp
  private void Tick(float deltaTime)
  {
      // shared work the base always does
      OnTick(deltaTime);
  }

  protected virtual void OnTick(float deltaTime) { }
  ```

  The outer method carries the invariants (guards, ordering, state flags); the `OnX` hook carries only the subclass's own work. Applies to every extension point, not just tick — `Start`/`End`, `Bind`/`Unbind`, `Enable`/`Disable`. See `PokerStage` (`StartStage`/`OnStartStage`), `UIPokerView` and `PokerVisual` (`OnBind`/`OnUnbind`).
- **Every system has an explicit lifecycle.** Bind and unbind through paired hooks — `OnEnable`/`OnDisable`, `OnNetworkSpawn`/`OnNetworkDespawn`, `OnBind`/`OnUnbind`, or a static `OnInstanceChanged`-style event. Never poll for a dependency in `Update()` and rebind when you notice it changed: that hides the binding in the per-frame path and leaves no single point where the subscription is released, so handlers leak. Unsubscribe in the reverse order of subscribing, and null a reference only after the unsubscribe that needs it.
- **Animate with DOTween, not hand-rolled interpolation.** A per-frame `Vector3.Lerp`/`Quaternion.Slerp` toward a moving target bakes the curve into decay maths and offers no hook for "then do this". A tween makes easing, duration, delay, loops, callbacks and sequences tunable after the fact. Expose duration and `Ease` as `[SerializeField]` so they can be retuned without touching code, and prefer `SetEase` over hand-tuned constants.

## Gameplay architecture

Distilled while building the ability system — new features follow these.

- **Public act, private grant.** A cheat ability is two halves: a cosmetic act everyone sees (glasses on, neck stretched across the table) and an owner-read grant only the holder's client learns. The normal and cheat variants of a card share one display name and look identical from outside — the guessing game depends on it. The grant is gated on the public act and reclaimed by its controller the moment the act ends.
- **Props stay cosmetic.** Generic player pieces (`PlayerGlassesController`, `PlayerHeadStretchController`, `PlayerActionAnimator`) live in `Game.Runtime.Player`, replicate only the act itself, and carry no game meaning. What the act means belongs to a mode-side controller (`PokerBoardPeekController`, `PokerPeekController`) that owns the grant `NetworkVariable` and installs the rule that reads it. Never put per-ability state on shared data (`PokerPlayerData`, `PokerGameData`).
- **Feature components sit on named child GameObjects** of the player prefab (`Animation`, `Glasses`, `HeadStretch`), not piled on the root. Wiring stays serialized; fallback lookups search from `NetworkObject` down, never assume one shared GameObject.
- **Visibility is a display rule.** Cards replicate to everyone; who may look is decided client-side through provider hooks (`PokerPlayerData.AddHandVisibilityProvider`, `PokerGameData.AddCommunityVisibilityProvider`) plus a `Notify*RulesChanged` event the views listen to. The rule is the only thing hiding the data — a deliberate trade, made once.
- **Two rigs, one act.** The owner renders `Gnome_HandOnly`, everyone else `Gnome_rig`. Bone positions come from `PlayerBoneRig`/`PlayerRigController` (`RenderedRig`, `RenderedHead`), never per-feature name lookups. Replicated acts play on whichever rig each client renders, so every seat sees the same thing.
- **One-shot gestures go through `PlayerActionAnimator`**: ids in `PlayerActionIds`, mapping in `PlayerActionAnimationDatabase`, states on the animator's `Gestures` layer. Missing states are skipped silently, so triggers can land before the art does. Poses and locomotion stay on the base layer with `PlayerSeatController`.
- **Anything posing a bone the camera hangs off runs between `PlayerController` (order 0) and the Cinemachine brain (order 100).** The Animator restores bones every frame, so a LateUpdate write after the brain's sample is not a frame late — it is never seen at all.
- **Stages reached by reference rather than by sequence** (overlays, module-pushed stages) must be declared in `PokerModule.CollectReferencedStages`, so every peer clones them at build time — clients look running stages up by id. Overlays exit with `PopOverlay`, never `FinishStage`.
- **Numbers the UI offers are the stage's own methods** (`StakeCeiling`, `ClampBet`, …): server clamps and bar sliders call the same code, so a client can never offer what the server will refuse.
- **`EndGame` is the cleanup choke point.** It sweeps every registered player's cards, not only the seated — whoever left a seat mid-hand is outside every stage's reset sweep.

## Asset naming conventions

- Prefabs: `UI_` prefix for UI prefabs (`UI_Screen_<Name>` for full-screen panels, `UI_<Name>` for components). No prefix for gameplay prefabs. `Button_` prefix for button prefabs.
- Animation clips: `Animation_<StateName>.anim`. Animator controllers: `Animator_<Context>.controller` / `.overrideController`. Keep these colocated with the prefab they belong to.
- Scenes: short PascalCase purpose names (`Bootstrap.unity`, `Gameplay.unity`).
- ScriptableObject assets: PascalCase matching the class name.

## Editor settings that affect how you write code

- **Domain Reload is disabled** (Scene Reload only). Static fields, static events, and singleton `Instance` properties survive between Play sessions. Every class holding static state must reset it via `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` — see `SteamController`, `GameNetworkManager`, `GameModeController`.
- **Auto Refresh is disabled.** After writing scripts outside the Editor, trigger `AssetDatabase.Refresh()` manually before expecting a recompile.
- **UI uses TextMeshPro**, never legacy `UnityEngine.UI.Text` — `TextMeshProUGUI`, `TMP_InputField`, `TMP_Dropdown`, and `TMP_Text` in code.
- **Input uses `InputActionReference` serialized fields**, never string lookups like `_playerInput.actions["Look"]` — see `PlayerController`.
- Scene-placed `NetworkObject`s created programmatically get `GlobalObjectIdHash = 0` and fail to spawn. If that happens, remove and re-add the `NetworkObject` component on the saved scene object to regenerate it.

## Rules

1. Keep the two-assembly split (`Game.Runtime`, `Game.Editor`); don't introduce new asmdefs without a real modularity need.
2. Server-authoritative by default — any gameplay state that must be consistent across clients lives in a `NetworkBehaviour`/`NetworkVariable`, not in a locally-mutated field trusted from the client.
3. No XML doc comments or comment blocks; let naming carry the meaning.
4. Follow SOLID, KISS and DRY; prefer industry-standard patterns over bespoke structure, and keep modules loosely coupled and reusable.
5. Extend through `protected virtual OnX` hooks called from a non-virtual base method — never edit a parent class to accommodate a subclass.
6. Every system binds and unbinds through explicit paired lifecycle hooks; never poll for a dependency in `Update()`.
7. Animate with DOTween tweens, not hand-rolled `Lerp`/`Slerp` stepped in `Update()`.
