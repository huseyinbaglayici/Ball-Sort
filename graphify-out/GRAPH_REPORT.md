# Graph Report - Ball-Sort  (2026-11-23)

## Corpus Check
- 187 files · ~110,762 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 1319 file(s) not represented in the graph (top: .meta 772, .asset 514, .prefab 19)
- WARNING: 1319 of 1506 files seen (88%) have no supported extractor and are missing from this graph entirely. It may look complete while representing only part of the codebase.

## Summary
- 2659 nodes · 8398 edges · 146 communities (78 shown, 68 thin omitted)
- Extraction: 87% EXTRACTED · 13% INFERRED · 0% AMBIGUOUS · INFERRED: 1106 edges (avg confidence: 0.85)
- Token cost: 77,182 input · 0 output

## Community Hubs (Navigation)
- Ball Item Visuals
- Daily Reward
- UI Builder Files
- Item Factory and Setup
- FX Builders and Items
- Game Controller Boosters
- Level Editor Tools
- README Concepts
- Sort Board Rules
- Tube Lift and Select
- UI Builder Helpers
- Progress and Rewards
- Complete FX Builder
- Store and Life Refill
- Obstacle Markers
- Bottle Item
- Board Prefab Builder
- Win Title Letters
- Campaign Rules
- Level Tutorial
- Level Editor Window
- Tutorial Service
- Service Registration
- Tube FX Preview
- Win Skeleton Builder
- Button Feedback and Peek
- Feel Showcase
- Board Rules Tests
- Tube Complete FX
- Booster Inventory
- Player Save Data
- Key Flight FX
- Space Background
- Shutter Item
- Settings and Tests
- Campaign Tests
- Level Data
- Level Transitions
- Tap Selection
- State Machine Flow
- LocalizedText Cluster
- Popup Cluster
- LostState Cluster
- OrbitRushScreen Cluster
- LevelOpenRecorder Cluster
- HomeScreen Cluster
- AccretionDisk Cluster
- WonState Cluster
- BoosterStockChanged Cluster
- RescueAdvisor Cluster
- EconomyText Cluster
- BoardLayout Cluster
- Search Cluster
- RescueTests Cluster
- GameUiBuilder Cluster
- LevelCatalog Cluster
- SettingsPopup Cluster
- FrozenTubeItem Cluster
- GameUiBuilder Cluster
- MainFlow Cluster
- PortalView Cluster
- AudioManager Cluster
- LevelSolver Cluster
- OrbitRushBots Cluster
- CampaignEconomyTests Cluster
- ShopPopup Cluster
- CampaignJsonImporter Cluster
- ImportedLevel Cluster
- TweenPresets Cluster
- DebugPanel Cluster
- BallHighlight Cluster
- EventBus Cluster
- GameUiBuilder Cluster
- MechanicSounds Cluster
- ItemType Cluster
- LevelCheckTests Cluster
- ComponentPool Cluster
- DailyRewardPopup Cluster
- BoardLayoutTests Cluster
- SettingsService Cluster
- SettingsTests Cluster
- LevelPreview Cluster
- ShellBreakFx Cluster
- com Cluster
- HapticModule Cluster
- AudioLibrary Cluster
- BuildInfoWriter Cluster
- LostState Cluster
- SettingsRows Cluster
- BallPortalMask Cluster
- PoolService Cluster
- com Cluster
- GameController Cluster
- OrbContacts Cluster
- LiveOpsHome Cluster
- ItemRegistry Cluster
- EconomyHud Cluster
- StoreProduct Cluster
- ResultFx Cluster
- LevelGrouping Cluster
- TweenPresets Cluster
- AudioContactTests Cluster
- AudioEventImporter Cluster
- PoolMonitorWindow Cluster
- SettingsMenu Cluster
- LocalizationService Cluster
- BombBlastFx Cluster
- ProjectSetup Cluster
- ToggleSwitch Cluster
- OrbColorTable Cluster
- LevelSwipeView Cluster
- SettingsArgs Cluster
- SceneNames Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster
- com Cluster

## God Nodes (most connected - your core abstractions)
1. `SortBoard` - 117 edges
2. `BallItem` - 101 edges
3. `BallAnimationConfig` - 93 edges
4. `GameController` - 89 edges
5. `BoardView` - 88 edges
6. `BottleItem` - 77 edges
7. `GameUiBuilder` - 72 edges
8. `Project.Contexts.Game.Sort` - 58 edges
9. `OrbitRushService` - 55 edges
10. `LevelData` - 50 edges

## Surprising Connections (you probably didn't know these)
- `Config` --references--> `BallAnimationConfig`  [EXTRACTED]
  Assets/Tests/EditMode/FailTimingTests.cs → Assets/Scripts/Contexts/Game/Sort/BallAnimationConfig.cs
- `LevelEditorWindow` --defines--> `HasSelection`  [EXTRACTED]
  Assets/Editor/LevelEditor/LevelEditorWindow.Solve.cs → Assets/Editor/LevelEditor/LevelEditorWindow.cs
- `LevelEditorWindow` --references--> `LevelGroup`  [EXTRACTED]
  Assets/Editor/LevelEditor/LevelEditorWindow.Solve.cs → Assets/Editor/LevelEditor/LevelGrouping.cs
- `LevelEditorWindow` --references--> `LevelData`  [EXTRACTED]
  Assets/Editor/LevelEditor/LevelEditorWindow.Solve.cs → Assets/Scripts/Contexts/Game/Levels/LevelData.cs
- `LevelEditorWindow` --references--> `LevelPalette`  [EXTRACTED]
  Assets/Editor/LevelEditor/LevelEditorWindow.Solve.cs → Assets/Scripts/Contexts/Game/Levels/LevelPalette.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Unity-free rule classes covered by EditMode tests** — readme_sortboard, readme_campaignrules, readme_livesservice, readme_monetizationservice, readme_rules_first [EXTRACTED 1.00]
- **Layered assembly architecture (Contexts -> Common -> Core)** — readme_contexts_layer, readme_common_layer, readme_core_layer, readme_one_way_dependencies [EXTRACTED 1.00]
- **AI tooling used to build the project** — readme_graphify, readme_caveman, readme_unity_mcp [EXTRACTED 1.00]

## Communities (146 total, 68 thin omitted)

### Community 0 - "Ball Item Visuals"
Cohesion: 0.05
Nodes (20): BallItem, KeyMark, KeyWorldWidth, Arrival, Hover, Land, None, Bomb (+12 more)

### Community 1 - "Daily Reward"
Cohesion: 0.05
Nodes (32): DailyRewardService, CanClaim, CurrentIndex, Model, ShouldAutoShow, Today, OrbitRushService, Gear (+24 more)

### Community 2 - "UI Builder Files"
Cohesion: 0.11
Nodes (15): Project.Contexts.Main, Project.Contexts.Game, Project.Contexts.Application.Economy, Project.Common.Visual, Project.Core.StateMachine, Project.Common.UI.PopUps, Project.Common.UI, Project.Contexts.Store (+7 more)

### Community 3 - "Item Factory and Setup"
Cohesion: 0.08
Nodes (9): ItemFactory, LevelPalette, Count, BoardView, Animation, BottleCount, IsAnimating, ScreenBand (+1 more)

### Community 5 - "Game Controller Boosters"
Cohesion: 0.07
Nodes (10): GameController, CanUseBoosters, IdleState, PlayingState, GameState, Idle, Lost, Playing (+2 more)

### Community 6 - "Level Editor Tools"
Cohesion: 0.09
Nodes (5): Project.Core.Audio, Project.Editor.LevelEditor, Project.Core.Debugging, Project.Contexts.Game.Levels, Project.Core.Pooling

### Community 7 - "README Concepts"
Cohesion: 0.06
Nodes (42): Boosters (Undo, Extra Tube, Free Move), Boot.unity scene, Bootstrapper, Campaign (100 levels x 5 variants), CampaignRules, caveman, Check All Levels, Common (shared UI, tween presets) (+34 more)

### Community 8 - "Sort Board Rules"
Cohesion: 0.09
Nodes (13): Ball, Bomb, SortBoard, BombCount, BottleCount, CanUndo, ContinueUsed, CurtainCount (+5 more)

### Community 9 - "Tube Lift and Select"
Cohesion: 0.10
Nodes (6): Reaction, TubeReactions, BallAnimationConfig, ExplodeReadableTime, HiddenReadableTime, ShutterClearTime

### Community 11 - "Progress and Rewards"
Cohesion: 0.11
Nodes (6): ProgressService, Attempt, Level, LevelsWon, TryId, CampaignEconomyTests

### Community 12 - "Complete FX Builder"
Cohesion: 0.16
Nodes (3): CompleteFxBuilder, ResultFxBuilder, WinFxBuilder

### Community 13 - "Store and Life Refill"
Cohesion: 0.09
Nodes (15): StorePurchased, RefillResult, CanPlay, NoCoins, NotNeeded, Refilled, StoreService, BeginnerAvailable (+7 more)

### Community 14 - "Obstacle Markers"
Cohesion: 0.14
Nodes (3): BoxView, CurtainView, ObstacleMarkers

### Community 15 - "Bottle Item"
Cohesion: 0.09
Nodes (13): BottleTapped, BottleItem, Art, BallCount, Balls, Capacity, Gem, Index (+5 more)

### Community 16 - "Board Prefab Builder"
Cohesion: 0.13
Nodes (4): BoardPrefabBuilder, ObstacleSprites, OrbSkin, Entry

### Community 17 - "Win Title Letters"
Cohesion: 0.10
Nodes (5): Stage, TitleLetters, WinFx, FinishFxSettings, WinFxSettings

### Community 18 - "Campaign Rules"
Cohesion: 0.09
Nodes (11): Bootstrapper, AdGroup, CampaignRules, Feature, Challenge, StarterPack, RoadReward, IsEmpty (+3 more)

### Community 19 - "Level Tutorial"
Cohesion: 0.12
Nodes (6): IntroArgs, LevelTutorial, Lesson, None, SameColor, Tap

### Community 20 - "Level Editor Window"
Cohesion: 0.12
Nodes (3): HasSelection, LevelEditorWindow, LevelLayout

### Community 21 - "Tutorial Service"
Cohesion: 0.11
Nodes (14): Intro, IsNone, None, TutorialService, Model, Mechanic, Bomb, FrozenTube (+6 more)

### Community 22 - "Service Registration"
Cohesion: 0.09
Nodes (16): PopupService, LivesService, CanPlay, Count, IsUnlimited, Lives, NextLifeIn, Now (+8 more)

### Community 23 - "Tube FX Preview"
Cohesion: 0.14
Nodes (5): Obstacle, Frozen, Hidden, None, TubeFxPreview

### Community 25 - "Button Feedback and Peek"
Cohesion: 0.09
Nodes (5): ButtonFeedback, Enabled, HoldToPeek, Splash, MainReady

### Community 29 - "Booster Inventory"
Cohesion: 0.14
Nodes (7): BoosterCharge, BoosterInventory, BoosterType, ExtraTube, FreeMove, Undo, BoosterBar

### Community 30 - "Player Save Data"
Cohesion: 0.15
Nodes (10): BatchScope, ContinueReceipt, DailyRewardModel, LivesModel, PlayerData, Model, ProgressModel, TutorialModel (+2 more)

### Community 31 - "Key Flight FX"
Cohesion: 0.12
Nodes (3): KeyFlightFxBuilder, KeyFlightFx, Path

### Community 32 - "Space Background"
Cohesion: 0.12
Nodes (7): SpaceBackground, StarData, StarLayout, TouchRingView, IsGone, CameraFitter, Project.Core.CameraFit

### Community 33 - "Shutter Item"
Cohesion: 0.15
Nodes (5): BadgeRest, ShutterItem, IsPlaying, LockWorld, LockWorldWidth

### Community 34 - "Settings and Tests"
Cohesion: 0.15
Nodes (3): Project.Core.Save, Project.Core.Settings, Project.Tests

### Community 35 - "Campaign Tests"
Cohesion: 0.14
Nodes (3): CampaignTests, Solution, SolutionFile

### Community 36 - "Level Data"
Cohesion: 0.10
Nodes (19): BottleData, Capacity, CurtainData, LevelData, Bottles, Capacity, ColorCount, Curtains (+11 more)

### Community 37 - "Level Transitions"
Cohesion: 0.15
Nodes (4): LoadingOverlay, LevelOpenView, IsReady, IsRunning

### Community 38 - "Tap Selection"
Cohesion: 0.11
Nodes (3): BottleDeselected, BottleSelected, MoveMade

### Community 39 - "State Machine Flow"
Cohesion: 0.12
Nodes (12): GameState, HomeState, State, StateMachine, CurrentKey, Counting, HardeningTests, Key (+4 more)

### Community 40 - "LocalizedText Cluster"
Cohesion: 0.17
Nodes (10): DifficultyTag, LocalizedText, Key, MessagePopup, Slot, ResultPopup, DayCell, RacerRow (+2 more)

### Community 41 - "Popup Cluster"
Cohesion: 0.11
Nodes (10): Popup, Entrance, EntranceDelay, IsOpen, PopupEntrance, Burst, None, Settle (+2 more)

### Community 42 - "LostState Cluster"
Cohesion: 0.16
Nodes (9): FailWindow, Left, Opened, Searched, TryId, Version, LostState, FailTimingTests (+1 more)

### Community 43 - "OrbitRushScreen Cluster"
Cohesion: 0.13
Nodes (7): OrbitRushMapLayout, OrbitRushScreen, OrbitRushChanged, LevelOpenSettings, CloseTotal, OpenTotal, LevelSwipeSettings

### Community 44 - "LevelOpenRecorder Cluster"
Cohesion: 0.14
Nodes (4): BootPlayModeStarter, Enabled, FeelShowcaseSceneBuilder, LevelOpenRecorder

### Community 45 - "HomeScreen Cluster"
Cohesion: 0.12
Nodes (7): Chapter, ChapterArt, Count, this[], HomeScreen, Orb, ChapterArtTests

### Community 46 - "AccretionDisk Cluster"
Cohesion: 0.15
Nodes (4): AccretionDisk, IsAlive, TubeArt, Size

### Community 47 - "WonState Cluster"
Cohesion: 0.13
Nodes (8): LevelDifficulty, Hard, Normal, SuperHard, LevelResult, TotalCoins, WonState, WinSounds

### Community 48 - "BoosterStockChanged Cluster"
Cohesion: 0.13
Nodes (15): BoosterStockChanged, CoinsChanged, LevelCompleted, LevelFailed, LivesChanged, LivesChangeReason, Granted, Other (+7 more)

### Community 49 - "RescueAdvisor Cluster"
Cohesion: 0.19
Nodes (6): ContinueOffer, RescueAdvisor, RescueKind, Bomb, NoMove, RescueReport

### Community 50 - "EconomyText Cluster"
Cohesion: 0.18
Nodes (5): EconomyText, LiveOpsText, LocalizationService, LanguageCode, Languages

### Community 51 - "BoardLayout Cluster"
Cohesion: 0.30
Nodes (3): BoardLayout, Fit, Pitch

### Community 52 - "Search Cluster"
Cohesion: 0.13
Nodes (9): BoardSolver, Candidate, Outcome, LimitReached, Solved, Unsolvable, Search, DepthCut (+1 more)

### Community 54 - "GameUiBuilder Cluster"
Cohesion: 0.23
Nodes (3): UiEntrance, Displacement, HomePoster

### Community 55 - "LevelCatalog Cluster"
Cohesion: 0.15
Nodes (9): LevelCatalogBuilder, Entry, LevelCatalog, Count, Levels, LevelMechanics, HelpPage, Note (+1 more)

### Community 57 - "FrozenTubeItem Cluster"
Cohesion: 0.19
Nodes (4): FrozenTubeItem, IsPlaying, RendererBlocks, Block

### Community 59 - "MainFlow Cluster"
Cohesion: 0.16
Nodes (7): MainFlow, Current, IsSwitching, MainState, Game, Home, None

### Community 60 - "PortalView Cluster"
Cohesion: 0.20
Nodes (5): PortalView, Edge, IsGone, OpenEdge, Shape

### Community 61 - "AudioManager Cluster"
Cohesion: 0.16
Nodes (7): AudioBag, Count, AudioEvent, HasClips, AudioManager, MusicMuted, SoundMuted

### Community 62 - "LevelSolver Cluster"
Cohesion: 0.15
Nodes (10): LevelSolver, Outcome, Invalid, NotProven, Solved, Unsolvable, Report, Summary (+2 more)

### Community 63 - "OrbitRushBots Cluster"
Cohesion: 0.24
Nodes (5): OrbitRushModel, OrbitRushBots, OrbitRushRacer, Model, PlayerRank

### Community 64 - "CampaignEconomyTests Cluster"
Cohesion: 0.15
Nodes (8): ContinueResult, AlreadyUsed, Applied, NotEnoughCoins, Stale, ContinueService, CanAfford, Price

### Community 66 - "CampaignJsonImporter Cluster"
Cohesion: 0.18
Nodes (5): CampaignJsonImporter, SlotEdit, Solution, SolutionFile, LevelAssetStore

### Community 67 - "ImportedLevel Cluster"
Cohesion: 0.16
Nodes (7): BottleDto, FileDto, ImportedLevel, LevelDto, LevelJsonImporter, SlotDto, VariantDto

### Community 71 - "EventBus Cluster"
Cohesion: 0.19
Nodes (3): LivesArgs, EventBus, EventBusReset

### Community 74 - "ItemType Cluster"
Cohesion: 0.14
Nodes (12): ItemType, Ball, BombBlast, Bottle, CompleteFx, FrozenTube, KeyFlightFx, None (+4 more)

### Community 76 - "ComponentPool Cluster"
Cohesion: 0.21
Nodes (8): ComponentPool, AvailableCount, GrowCount, InUseCount, MaxCount, Name, TotalCreated, PoolHandle

### Community 77 - "DailyRewardPopup Cluster"
Cohesion: 0.22
Nodes (3): BoosterFxSettings, CoinBurst, DailyRewardPopup

### Community 79 - "SettingsService Cluster"
Cohesion: 0.23
Nodes (5): SettingsModel, SettingsService, HapticsEnabled, MusicMuted, SoundMuted

### Community 80 - "SettingsTests Cluster"
Cohesion: 0.19
Nodes (5): Entry, LocalizationTable, LanguageCode, NativeName, SettingsTests

### Community 81 - "LevelPreview Cluster"
Cohesion: 0.29
Nodes (4): LevelPreview, SlotHit, Paints, PopupCoinSpot

### Community 83 - "com Cluster"
Cohesion: 0.15
Nodes (12): com.unity.ide.rider@3.0.40, com.unity.modules.accessibility@1.0.0, com.unity.modules.imgui@1.0.0, com.unity.modules.uielements@1.0.0, com.unity.modules.unitywebrequestwww@1.0.0, scopedRegistries, com.esotericsoftware.spine.spine-csharp, com.unity.ide.rider (+4 more)

### Community 84 - "HapticModule Cluster"
Cohesion: 0.21
Nodes (6): HapticModule, Enabled, HapticType, Heavy, Light, Medium

### Community 85 - "AudioLibrary Cluster"
Cohesion: 0.26
Nodes (4): AudioLibrary, Events, Entry, EventEntry

### Community 86 - "BuildInfoWriter Cluster"
Cohesion: 0.18
Nodes (6): BuildInfoWriter, callbackOrder, BuildInfo, Build, Label, Version

### Community 87 - "LostState Cluster"
Cohesion: 0.32
Nodes (3): ResultArgs, ResultOption, ShopArgs

### Community 88 - "SettingsRows Cluster"
Cohesion: 0.24
Nodes (5): AppLinks, PrivacyUrl, SupportEmail, TermsUrl, SettingsRows

### Community 89 - "BallPortalMask Cluster"
Cohesion: 0.32
Nodes (3): BallPortalMask, IsActive, IsSplit

### Community 91 - "com Cluster"
Cohesion: 0.17
Nodes (12): dependencies, com.esotericsoftware.spine.spine-csharp@https://github.com/EsotericSoftware/s..., com.unity.modules.androidjni@1.0.0, com.unity.modules.animation@1.0.0, com.unity.modules.imageconversion@1.0.0, com.unity.modules.jsonserialize@1.0.0, com.unity.test-framework@1.6.0, com.unity.modules.androidjni (+4 more)

### Community 93 - "OrbContacts Cluster"
Cohesion: 0.35
Nodes (6): ContactKind, Return, Select, Transfer, OrbContact, OrbContacts

### Community 95 - "ItemRegistry Cluster"
Cohesion: 0.29
Nodes (5): Item, Entry, ItemRegistry, Prefabs, IPoolable

### Community 97 - "StoreProduct Cluster"
Cohesion: 0.24
Nodes (5): StoreCatalog, StoreProduct, StoreSection, Coins, Offers

### Community 99 - "LevelGrouping Cluster"
Cohesion: 0.36
Nodes (3): LevelGroup, LevelGrouping, LevelVariant

### Community 102 - "AudioEventImporter Cluster"
Cohesion: 0.25
Nodes (4): AudioEventImporter, Catalog, CatalogEvent, Catalog

## Knowledge Gaps
- **277 isolated node(s):** `Enabled`, `callbackOrder`, `HasSelection`, `Paints`, `Solved` (+272 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 437 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **68 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `GameController` connect `Game Controller Boosters` to `UI Builder Files`, `Item Factory and Setup`, `Sort Board Rules`, `UI Builder Helpers`, `Progress and Rewards`, `Store and Life Refill`, `Bottle Item`, `Service Registration`, `Win Skeleton Builder`, `Feel Showcase`, `Booster Inventory`, `Level Transitions`, `Tap Selection`, `State Machine Flow`, `LocalizedText Cluster`, `LostState Cluster`, `LevelOpenRecorder Cluster`, `AccretionDisk Cluster`, `WonState Cluster`, `EconomyText Cluster`, `BoardLayout Cluster`, `GameUiBuilder Cluster`, `LevelCatalog Cluster`, `MainFlow Cluster`, `AudioManager Cluster`, `CampaignEconomyTests Cluster`, `ShopPopup Cluster`, `GameUiBuilder Cluster`, `MechanicSounds Cluster`, `DailyRewardPopup Cluster`, `HapticModule Cluster`, `GameController Cluster`, `ItemRegistry Cluster`, `EconomyHud Cluster`, `SettingsMenu Cluster`?**
  _High betweenness centrality (0.107) - this node is a cross-community bridge._
- **Are the 12 inferred relationships involving `SortBoard` (e.g. with `LevelSolver.Check()` and `GameController.BeginLevel()`) actually correct?**
  _`SortBoard` has 12 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Enabled`, `callbackOrder`, `HasSelection` to the rest of the system?**
  _277 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Ball Item Visuals` be split into smaller, more focused modules?**
  _Cohesion score 0.04847937368262571 - nodes in this community are weakly interconnected._
- **Why does `BallItem` connect `Ball Item Visuals` to `Space Background`, `Item Factory and Setup`, `FX Builders and Items`, `TweenPresets Cluster`, `BallHighlight Cluster`, `Tube Lift and Select`, `UI Builder Helpers`, `BombBlastFx Cluster`, `Complete FX Builder`, `FrozenTubeItem Cluster`, `DailyRewardPopup Cluster`, `Bottle Item`, `Board Prefab Builder`, `Obstacle Markers`, `ShellBreakFx Cluster`, `BallPortalMask Cluster`, `Tube Complete FX`, `ItemRegistry Cluster`?**
  _High betweenness centrality (0.083) - this node is a cross-community bridge._
- **Should `Daily Reward` be split into smaller, more focused modules?**
  _Cohesion score 0.05472837022132797 - nodes in this community are weakly interconnected._
- **Why does `SortBoard` connect `Sort Board Rules` to `Item Factory and Setup`, `Game Controller Boosters`, `Level Editor Tools`, `Obstacle Markers`, `Level Tutorial`, `Tube FX Preview`, `Win Skeleton Builder`, `Board Rules Tests`, `Campaign Tests`, `Level Data`, `Tap Selection`, `RescueAdvisor Cluster`, `EconomyText Cluster`, `Search Cluster`, `RescueTests Cluster`, `LevelSolver Cluster`, `CampaignEconomyTests Cluster`, `MechanicSounds Cluster`, `LevelCheckTests Cluster`?**
  _High betweenness centrality (0.078) - this node is a cross-community bridge._