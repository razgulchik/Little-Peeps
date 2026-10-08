# Little Peeps — организация систем в сцене

Как устроен старт и как разложены объекты в `SampleScene`, чтобы сцена была «по полочкам».
Документ держим синхронным с кодом — обновляем при добавлении/переименовании систем и полей.

> Полная сверка с кодом, сценой и префабами: **2026-10-08**. Не вошёл звук ударов и сборов
> (`SoundSystem` / `SoundDef`) — он ещё в работе.

## Принцип

- **Одна сцена.** Каждая система — свой объект в сцене с компонентом. Кодом системы не создаются.
- **`GameBootstrap` — единственная точка регистрации.** Порядок инициализации задаёт он (раздаёт контексты в `Awake`), а не случайный порядок `Awake` у объектов.
- **Зависимости между системами — ссылками в инспекторе.** Каждое поле `[SerializeField]` нужно прокинуть руками. Забыл — `NullReferenceException` на старте.
- **Менеджеры — в контейнерах** `Bootstrap`, `Systems`, `Input`, отдельно от игровых объектов.
- **Пространственные объекты живут в мире:** `Island` (вместе с `GridOverlay`), `ZonePreviewOverlay`, `IslandRise` — не в `Systems`. Пирс в сцену не кладётся вовсе: его ставит `IslandSystem` (см. «Пирс»).

> ⚠️ **Историческое имя поля.** В `GameBootstrap` поле называется `buildingSystem`
> (в инспекторе **«Building System»**), но его **тип — `StructureSystem`**. Имя оставлено, чтобы не
> рвать сериализацию сцены при переименовании `Building→Structure`. Кидаем туда компонент
> `StructureSystem`. В `RunManager` то же самое поле названо уже корректно — `structureSystem`.

## Иерархия сцены

```
SampleScene
├── Main Camera                 → Camera (Orthographic) + AudioListener + CinemachineBrain (Ignore Time Scale)
│                                  + PixelPerfectCamera
├── CameraTarget                → (пустой GO — логическая цель камеры, за ней следует vcam)
├── CameraController            → CameraController        (свой GO: «мозг» камеры, двигает CameraTarget)
├── CinemachineCamera           → CinemachineCamera (vcam): Follow = CameraTarget + CinemachineFollow (damping)
│                                  + CinemachinePixelPerfect + CinemachineImpulseListener   ← тряска при подъёме острова
├── EventSystem                 → EventSystem + InputSystemUIInputModule  (ОБЯЗАТЕЛЕН: uGUI-кнопки + правило «мышь над UI ≠ мышь над миром»)
├── Input                       → InputHandler, GameHotkeys
├── Global Light 2D             → Light2D
├── Bootstrap                   → GameBootstrap, SaveSystem
├── Systems
│   ├── AgeSystem               → AgeSystem          (каталог эпох + CanAdvance)
│   ├── AgeSequencer            → AgeSequencer
│   ├── HarvestVfxSystem        → HarvestVfxSystem + HarvestNumbers   (эффекты сбора; эмиттеры и цифры становятся его детьми)
│   ├── PerkSystem              → PerkSystem
│   ├── PrestigeSystem          → PrestigeSystem
│   ├── PlacementController     → PlacementController
│   ├── RunManager              → RunManager
│   ├── ResourceSystem          → ResourceSystem
│   ├── StructureSystem         → StructureSystem
│   ├── SpawnSystem             → SpawnSystem
│   ├── TapSystem               → TapSystem + TapRadiusVisual + LineRenderer   (кольцо радиуса тапа у курсора;
│   │                              AudioSource для щелчка тапа TapSystem добавляет себе сам в рантайме)
│   ├── UnitPool                → UnitPool           (юниты из пула становятся его детьми)
│   ├── UnitSystem              → UnitSystem
│   └── WaterSystem             → WaterSystem        (море инстанцирует в рантайме из префаба — в сцену воду НЕ класть)
├── Island                      → IslandSystem
│   ├── Grid                    → Grid (компонент)
│   │   ├── Ground              → Tilemap + TilemapRenderer + Rigidbody2D (Static) + TilemapCollider2D
│   │   │                          + CompositeCollider2D   ← суша и граница острова
│   │   ├── GroundTrim          → Tilemap + TilemapRenderer   ← кайма берега: лежит на клетках ВОДЫ, поэтому без коллайдера
│   │   └── Water (выключен)    → Tilemap + TilemapRenderer
│   └── GridOverlay             → MeshFilter + MeshRenderer + GridOverlay   (Transform строго 0,0,0, без поворота/масштаба)
├── SceneGridGizmo (выключен)   → SceneGridGizmo     (сетка только для Scene view, в игре не участвует)
├── ZonePreviewOverlay          → MeshFilter + MeshRenderer + ZonePreviewOverlay   (заливка зоны под карточкой при выборе)
├── IslandRise                  → IslandRisePlayer   (Transform 0,0,0; в рантайме его дети — дублёры тайлов,
│                                  излучатели эффектов, «Rise voices» и CinemachineImpulseSource)
├── Music                       → AudioSource: Audio Resource = трек флейты, Play On Awake, Loop, Priority 0
│                                  ← фоновая музыка без кода: играет с запуска всю сессию, timeScale её не трогает
└── Canvas                      → префаб `Prefabs/Elements/Canvas`: Canvas + CanvasScaler + GraphicRaycaster
    │                             + **UIRoot** + **UIVisibility**. Порядок детей = порядок отрисовки: ниже = поверх
    ├── Prestige                → CanvasGroup
    │   └── PrestigeButton      → Image + Button + CanvasGroup + PrestigeButton   (висит над пирсом)
    │       └── Text (TMP)      → TMP_Text
    ├── Hud                     → CanvasGroup                    ← весь игровой HUD одной группой
    │   ├── AgePanel            → CanvasGroup + AgeAdvancePanel
    │   │   ├── Advance
    │   │   │   └── Frame       → Image
    │   │   │       ├── Cost            → Image + Grid Layout Group + AgeCostPanel  (ценник, юниты в рантайме)
    │   │   │       └── AdvanceButton   → Image + Button
    │   │   │           └── Label       → TMP_Text
    │   │   └── Timeline
    │   │       └── Frame       → Image + AgeTimelinePanel
    │   │           └── Viewport    → RectMask2D
    │   │               └── Content → Vertical Layout Group + Content Size Fitter
    │   │                             (прижат к НИЗУ: карточки эр растут вверх, маска режет сверху)
    │   ├── ResourceBar
    │   │   └── Slots           → Image + Grid Layout Group + ResourcePanel   (ResourceUnit на ресурс, в рантайме)
    │   └── BuildBar
    │       ├── ModeButton      → Image + Button + BuildModeButton      (правый-нижний угол: Play↔Build)
    │       ├── Palette         → CanvasGroup + Image + BuildPanelUI
    │       │   ├── TabConteiner → Vertical Layout Group        (кнопки вкладок в рантайме; ВНУТРИ Palette —
    │       │   │                                                видны только в стройке, своя строка UIVisibility не нужна)
    │       │   └── Viewport    → Image + RectMask2D + BuildPanelScroller
    │       │       └── CardRow → Horizontal Layout Group       (карточки построек в рантайме)
    │       └── Sell            → CanvasGroup
    │           └── SellButton  → Image + Button
    │               └── SelectedFrame   → Image                 (подсветка выбранного инструмента)
    ├── AgeTransition           → Image (чёрный α≈0.42 — подложка баннера) + CanvasGroup (Alpha 0)   ← баннер эпохи;
    │   │                         AgeSequencer проявляет и гасит его целиком, клики ловит только пока он виден
    │   └── BannerText          → TMP_Text                                 ← плашка «Age N»
    ├── Screens                 → ТОЛЬКО RectTransform, растянут на весь Canvas
    │   │                         ← CanvasGroup сюда НЕ вешать: перемножится с альфой экранов
    │   ├── PerkScreen          → CanvasGroup + PerkSelectionUI
    │   │   └── Panel           → Image (затемнение — перехватывает ввод, это модальное окно)
    │   │       ├── TitleText   → TMP_Text
    │   │       └── CardColumn  → Vertical Layout Group         (перк-карточки в рантайме)
    │   └── ZoneScreen          → CanvasGroup + ZoneSelectionUI
    │       └── Panel           → Image (прозрачная, **Raycast Target ВЫКЛ** — иначе при выборе зоны нет зума)
    │           └── CardRow     → Horizontal Layout Group       (зон-карточки в рантайме)
    ├── ref (выключен)          → скрин-референс вёрстки, в игре не участвует
    ├── HintView                → Image + CanvasGroup + HintView + Vertical Layout Group   ← окно подсказок, кликов не ловит
    │   ├── TitleText, BodyText → TMP_Text
    │   ├── CostContainer       → Vertical Layout Group         (строки цены в рантайме)
    │   └── FooterText          → TMP_Text
    ├── MetaScreen              → Image + CanvasGroup + MetaUpgradesUI   ← экран после престижа
    │   ├── TitleText, PointsText → TMP_Text
    │   ├── Scroll View         → Image + ScrollRect
    │   │   ├── Viewport        → Image + Mask
    │   │   │   └── Content     → Vertical Layout Group + Content Size Fitter   (строки мета-перков в рантайме)
    │   │   └── Scrollbar Vertical
    │   └── PlayButton          → Image + Button
    └── ConfirmDialog           → вложенный префаб `Prefabs/Elements/ConfirmDialog`: CanvasGroup + ConfirmDialog
                                  (Backdrop, Panel с текстом и кнопками Yes / No)
```

> `HintView` по замыслу кода — **последний** ребёнок Canvas, чтобы рисоваться поверх всего. Сейчас после
> него стоят `MetaScreen` и `ConfirmDialog`: подсказки поверх них не нужны, но если появятся — перенести вниз.

## Checklist проводки (критично)

Прокинь все ссылки. **Жирным** — то, без чего старт падает с `NullReferenceException` или ломается build mode.

### Bootstrap и системы

| Объект | Компонент | Поле | Что назначить |
|--------|-----------|------|---------------|
| Bootstrap | **GameBootstrap** | **resourceSystem, islandSystem, unitSystem, spawnSystem** | соответствующие компоненты |
| | | **buildingSystem** | **StructureSystem** (поле зовётся «Building System»!) |
| | | **tapSystem, runManager, prestigeSystem, saveSystem** | соответствующие компоненты |
| | | **ageSystem** | AgeSystem (нужен для эпох) |
| | | ageSequencer, perkSystem | AgeSequencer, PerkSystem |
| | | **placementController** | PlacementController (нужен build mode) |
| | | **uiRoot** | **UIRoot на Canvas** — единственная UI-ссылка бутстрапа; панели разведены внутри префаба |
| | | buildModeCooldown | 5 (сек, дефолт) |
| Bootstrap | SaveSystem | — | (полей нет; запись на диск пока заглушка) |
| Systems/RunManager | **RunManager** | **resourceSystem, islandSystem, structureSystem, spawnSystem** | ResourceSystem, Island, StructureSystem, SpawnSystem (spawnSystem обязателен — иначе NPE в StartNewRun) |
| | | **startConfig** | `ScriptableObjects/StartConfig/Start Config` — сид и правила острова, стартовый биом, дом, пирс, стартовые ресурсы и модификаторы. Пусто = случайный остров без дома и пирса, ноль ресурсов |
| Systems/ResourceSystem | ResourceSystem | logChanges | вкл/выкл лог ресурсов в консоль (дебаг) |
| Systems/StructureSystem | **StructureSystem** | **islandSystem, resourceSystem, spawnSystem** | Island, ResourceSystem, SpawnSystem |
| Systems/SpawnSystem | **SpawnSystem** | **unitPool, unitSystem** | UnitPool, UnitSystem |
| | | islandSystem | Island (впрыскивается в юнитов — задел на будущее island-aware поведение) |
| | | stuckSweepInterval | 0.5 с (дефолт): как часто ищутся застрявшие юниты — их отправляют в ближайший дом |
| Systems/UnitSystem | UnitSystem | — | (полей нет) |
| Systems/UnitPool | UnitPool | — | (полей нет; юниты инстанцируются его детьми) |
| Systems/PerkSystem | PerkSystem | catalogue | `ScriptableObjects/Perks/Perk Catalogue Def` (PerkCatalogueDef — список перков) |
| | | choicesOffered | 3 (дефолт): сколько карточек в выборе перка |
| Systems/PrestigeSystem | **PrestigeSystem** | **saveSystem** | SaveSystem (на `Bootstrap`) |
| | | formula | выплата: очки за эпоху + кривая по всему собранному; платится только то, что бьёт рекорды профиля |
| | | pierUnlockAge | 3 (дефолт): с какой эпохи над пирсом появляется кнопка престижа (номер как у игрока: 3 = Age III) |
| | | upgrades | `Global Upgrade Catalogue Def` (GlobalUpgradeCatalogueDef) — мета-перки в порядке мета-экрана |
| Systems/AgeSystem | **AgeSystem** | **ages, resourceSystem** | список AgeDef-ассетов по порядку (начинается с Age II — у Age I дефа нет); ResourceSystem |
| Systems/AgeSequencer | AgeSequencer | islandSystem | Island |
| | | risePlayer | IslandRisePlayer на `IslandRise` (пусто = зона просто появляется, без подъёма) |
| | | cameraController | CameraController (камера едет к всплывающей зоне) |
| | | inputHandler | InputHandler на `Input` (тапы: ×3, пропуск, закрыть баннер; пусто = без тапов). Слушает `OnAnyClick` — клики и поверх UI, баннер сам UI |
| | | banner, titleLabel | `Canvas/AgeTransition` (CanvasGroup), `Canvas/AgeTransition/BannerText` (TMP_Text) — единственные ссылки из системы внутрь Canvas |
| | | bannerFade, titleHold, bannerTapGrace | 0.5, 2, 0.4 (сек, дефолты): проявление/угасание баннера, сколько он висит, через сколько его можно закрыть тапом |
| IslandRise | **IslandRisePlayer** | **profile** | ассет `ScriptableObjects/Profiles/Island Rise Profile` — все ручки подъёма (тайминги, кривые, эффекты, звук, тряска) |
| | | **islandSystem** | Island |
| | | waterSystem | WaterSystem (опц.; без него тайлы в прыжке не давят на воду) |
| Systems/TapSystem | **TapSystem** | **inputHandler** | InputHandler |
| | | radiusVisual | TapRadiusVisual на этом же объекте (ширина, материал и цвет кольца — на его LineRenderer) |
| | | tapRadius, boostSpeedMultiplier, boostDuration | 0.5, ×2, 5 с (дефолты). Радиус и множитель — БАЗА: перки растят их статами `TapRadius` / `TapBoostMultiplier` |
| | | refreshStamina | выкл (дефолт): тап только ускоряет; вкл — ещё и восполняет выносливость |
| | | tapClip | звук тапа (`Sound/click`); пусто = тапы без звука. AudioSource не нужен — TapSystem создаёт его сам |
| | | tapVolume, tapPitchJitter | 0.6 и ±0.05 (дефолты): громкость щелчка и случайный разброс высоты на каждый тап, 0 = всегда одинаково |
| Systems/PlacementController | **PlacementController** | **inputHandler, structureSystem, resourceSystem, islandSystem, mainCamera, gridOverlay** | соответствующие компоненты (gridOverlay — на `Island/GridOverlay`) |
| | | visuals → validColor, invalidColor, sellHoverColor, moveHoverColor | цвета госта/наведения: гост валид/невалид, красный при продаже, зелёный «можно схватить» в Move (есть дефолты) |
| | | visuals → territoryValidColor, territoryInvalidColor, territorySortingLayer, territorySortingOrder | ореол занимаемой территории госта: зелёный/красный α0.18, слой Ground/1001 (дефолты) |
| Systems/HarvestVfxSystem | HarvestVfxSystem | viewCamera | Main Camera (опц. — пусто = `Camera.main` в `Awake`; нужна только чтобы не играть эффекты за экраном) |
| | | offscreenMargin | 0.1 (дефолт) — запас вокруг экрана в долях вьюпорта |
| | HarvestNumbers | **numberPrefab** | `Prefabs/FX/FX_HarvestNumber` (без него цифр нет, в консоли ошибка) |
| | | motion | `ScriptableObjects/HarvestNumberMotionDef/Harvest Number Motion` — вся подгонка ощущения: lifetime (0.9 с), travel (0, 0.8), кривые полёта/масштаба/альфы, spawnOffset, spawnJitter (0.15, 0.1) |
| | | viewCamera, offscreenMargin | как выше |
| | | maxConcurrent | 60 (дефолт) — потолок живых цифр; сверх него перерабатывается ближайшая к смерти |
| Systems/WaterSystem | **WaterSystem** | **waterPrefab** | `Prefabs/DontTouch/Water` (Modern 2D Water — весь вид моря в префабе) |
| | | coastTilemaps | `Ground`, `GroundTrim` — их контур становится берегом, о который бьётся вода |
| | | foamMinPixels, foamMaxPixels, foamPeriod, foamWholePixels | пена под южным берегом: толщина в арт-пикселях от/до, период пульса (сек), шаг целыми пикселями. В сцене 1 / 2 / 5 / выкл |
| | | sideFoamShader, sideFoamPixels | шейдер `Little Peeps/Sprite Flat Color` и ширина пены по восточному/западному краю (1 px). Пустой шейдер = без боковой пены |
| Input | **InputHandler** | **mainCamera** | Main Camera |
| Input | GameHotkeys | buildModeKey, sellKey, exitToMenuKey | клавиши команд: B / X / Esc (дефолты, правятся в инспекторе); цифры 1–9 — вкладки стройки (зашиты в коде) |
| CameraController | **CameraController** | **cameraTarget, vcam** | пустой `CameraTarget` (его и двигаем); CinemachineCamera — зум = её `Lens > Orthographic Size`, а значение, сохранённое в сцене, = **стартовый зум каждого забега** |
| | | **islandSystem, viewCamera** | Island (кламп по острову); viewCamera = Main Camera (только для перевода drag-пикселей в мир) |
| | | zoomSpeed, minZoom, maxZoom | шаг колеса (2) и пределы ortho size (3…12) — дефолты |
| | | panSpeed, edgePanEnabled, edgeThickness | скорость WASD/стрелок (12), вкл. край-скролл, толщина края в px (12) — дефолты |
| | | boundsMargin | насколько центр (= цель) может уйти за край острова (4) — дефолт |
| Main Camera | **CinemachineBrain** | Ignore Time Scale ✓ | пишет позицию из активной vcam — и при `timeScale = 0` |
| CinemachineCamera | **CinemachineCamera** (vcam) | Follow = CameraTarget; Body = CinemachineFollow (damping ~0.1–0.2) | плавно следует за целью |
| | CinemachineImpulseListener | Channel Mask 1 (дефолт) | без него толчки подъёма острова никто не почувствует; сам источник толчков IslandRise добавляет себе кодом |
| Island | **IslandSystem** | **tilemap, trimTilemap** | `Grid/Ground` (суша с коллайдером), `Grid/GroundTrim` (кайма берега, без коллайдера) |
| | | **tileSet, biomes** | `TileSet/Base Tile Set` — тайлы для биома без своего тайлсета; биомы, из которых эпоха предлагает зоны (Meadow, Forest, Desert) |
| | | **structureSystem** | StructureSystem — через неё зона получает природное содержимое (деревья, реки, стартовый дом) |
| | | cellSize, previewSeed | 1; сид для редакторского превью «Generate Island» (контекстное меню). Забег берёт сид из StartConfig |
| Island/GridOverlay | **GridOverlay** | **islandSystem** | Island |
| | | lineColor, lineWidth | белый α0.6, 0.04 (дефолты) |
| | | occupiedColor | заливка занятых клеток (территория структур), белый α0.12 (дефолт) |
| | | sortingLayerName, sortingOrder | **Ground**, 1000 (чтобы линии были поверх травы) |
| ZonePreviewOverlay | ZonePreviewOverlay | **islandSystem** | Island |
| | | fillAlpha, outlineAlpha, outlineWidth | 0.55, 1, 0.08 (дефолты); цвет заливки — `previewColor` биома |
| | | sortingLayerName, sortingOrder | Ground, 1001 — поверх тайлов и сетки |

### UI

Вся разводка UI живёт **внутри `Canvas.prefab`**. Наружу из префаба торчит ровно одна ссылка —
`GameBootstrap.uiRoot`. Сам Canvas несёт две таблицы, и они отвечают на разные вопросы:

| компонент | вопрос | что перечисляет |
|---|---|---|
| **UIRoot** | до кого тянется код **снаружи** UI | 8 панелей (см. ниже) |
| **UIVisibility** | кто **когда** виден | CanvasGroup'ы + маска режимов |

Панель, которая разговаривает только событиями, не попадает ни в одну (`ModeButton`), а видимая
по режиму — только во вторую (`Palette`, `Sell`). `ConfirmDialog` и `HintView` нет в таблице видимости:
свою CanvasGroup каждый ведёт сам.

| Объект | Компонент | Поле | Что назначить |
|--------|-----------|------|---------------|
| Canvas | **UIRoot** | **zoneScreen, perkScreen, confirmDialog, metaScreen** | `Screens/ZoneScreen`, `Screens/PerkScreen`, `ConfirmDialog`, `MetaScreen` — их зовут гейплейные стейты |
| | | ageAdvance, ageCost, ageTimeline, prestigeButton | `Hud/AgePanel`, `.../Advance/Frame/Cost`, `.../Timeline/Frame`, `Prestige/PrestigeButton` — им нужен RunContext |
| Canvas | **UIVisibility** | groups | 8 строк, таблица ниже |
| Canvas/Prestige/PrestigeButton | **PrestigeButton** | button, label | Button, `Text (TMP)` |
| | | labelFormat | `Prestige +{0}` — `{0}` = выплата |
| | | zeroPayoutAlpha | 0.6 — прозрачность, пока забег не бьёт рекорд (кнопка всё равно нажимается) |
| | | worldCamera | камера мира, чтобы найти пирс на экране (пусто = `Camera.main`) |
| Canvas/Hud/BuildBar/ModeButton | BuildModeButton | button, modeText, buildLabel, playLabel | Button, TMP_Text, «B», «>» |
| Canvas/Hud/BuildBar/Palette | **BuildPanelUI** | palette | `ScriptableObjects/BuildMode/Build Palette Def` |
| | | placementController, resourceSystem | PlacementController, ResourceSystem (в сцене!) |
| | | cardPrefab, cardContainer | префаб `Prefabs/Elements/BuildCard`, дочерний `Viewport/CardRow` |
| | | tabPrefab, tabContainer | префаб `Prefabs/Elements/BuildTabUi`, дочерний `TabConteiner` |
| | | scroller | BuildPanelScroller на `Viewport` |
| | | sellButton, sellHighlight | `Sell/SellButton` и его `SelectedFrame` |
| Canvas/Hud/ResourceBar/Slots | ResourcePanel | resourceSystem, unitPrefab, container, iconSet | ResourceSystem (в сцене!), `Prefabs/Elements/ResourceUnit`, себя, ResourceIconSet |
| Canvas/Hud/AgePanel | AgeAdvancePanel | ageLabel, nextAgeButton | TMP_Text, `Advance/Frame/AdvanceButton` |
| Canvas/Hud/.../Frame/Cost | AgeCostPanel | unitPrefab, container, iconSet | ResourceUnit, себя (Grid Layout Group), ResourceIconSet |
| | | unaffordableColor, visibilityRoot | цвет суммы, которую не хватает; что гасить после последней эпохи, когда покупать больше нечего |
| Canvas/Hud/.../Timeline/Frame | AgeTimelinePanel | cardPrefab, container | префаб `Prefabs/Elements/AgeCard`, `Viewport/Content` |
| Canvas/Screens/PerkScreen | PerkSelectionUI | cardPrefab, cardContainer | префаб `Prefabs/Elements/PerkCardUI`, `Panel/CardColumn` |
| Canvas/Screens/ZoneScreen | ZoneSelectionUI | cardPrefab, cardContainer | префаб `Prefabs/Elements/ZoneCardUI`, `Panel/CardRow` |
| | | preview, cameraController | ZonePreviewOverlay, CameraController (в сцене!) |
| | | focusOffsetY | 0.15 (дефолт) — насколько выше центра экрана встаёт зона под карточкой |
| Canvas/HintView | HintView | titleText, bodyText, costContainer, footerText | свои дети; секция с пустым содержимым прячется сама |
| | | costPrefab, iconSet, unaffordableColor | ResourceUnit, ResourceIconSet, красный — цена, которую не потянуть |
| | | maxWidth, gap, screenMargin, showDelay, chainWindow | 160, 4, 2, 0.25 с, 0.3 с (дефолты): ширина до переноса, отступ от элемента и от края экрана, задержка появления, окно «перехода» между соседними элементами без задержки |
| Canvas/MetaScreen | **MetaUpgradesUI** | rowPrefab, rowContainer | префаб `Prefabs/Elements/MetaUpgradeRow`, `Scroll View/Viewport/Content` |
| | | pointsText, pointsFormat | `PointsText`, `Prestige points: {0}` |
| | | **playButton** | `PlayButton` — следующий забег |
| Canvas/ConfirmDialog | **ConfirmDialog** | messageText, yesButton, noButton (+ yesLabel, noLabel опц.) | разведены внутри префаба `ConfirmDialog`; текст кнопок в префабе — по умолчанию |

#### Таблица UIVisibility

Режимы объявляют сами гейплейные стейты (`UIModeChangedEvent` в их `Enter`), так что источник
правды — стек FSM. Маска `[Flags]`, группы **вкладываются**: альфа перемножается, а негодный к
взаимодействию родитель гасит детей. Поэтому строка нужна только там, где группа отличается от
родительской.

| # | Group | Playing | Build | ZonePick | PerkPick | AgeTransition | PrestigeConfirm | MetaUpgrades |
|---|-------|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| 0 | `Hud` | ✓ | ✓ | — | ✓ | ✓ | ✓ | — |
| 1 | `Hud/AgePanel` | ✓ | — | — | ✓ | ✓ | ✓ | — |
| 2 | `Hud/BuildBar/Palette` | — | ✓ | — | — | — | — | — |
| 3 | `Hud/BuildBar/Sell` | — | ✓ | — | — | — | — | — |
| 4 | `Screens/PerkScreen` | — | — | — | ✓ | — | — | — |
| 5 | `Screens/ZoneScreen` | — | — | ✓ | — | — | — | — |
| 6 | `Prestige` | ✓ | — | — | — | — | — | — |
| 7 | `MetaScreen` | — | — | — | — | — | — | ✓ |

Строка 0 читается как «всё, кроме выбора зоны и мета-экрана» (оба забирают себе весь экран), строка 1 —
ещё и «кроме Build» (в стройке кнопка эпохи и лестница эр не загораживают остров), строка 6 — кнопка
престижа только в живой игре.

> Панели **не прячут сами себя** и не лезут в чужие корни. Если экран не появился — смотри сюда, а не
> в код панели. Пустое поле `group` в строке ловится предупреждением в консоли на старте.

## Префабы

### BaseUnit (юнит)
```
Root          → Rigidbody2D (Dynamic, Gravity Scale 0, Continuous, Freeze Rotation Z) + Unit
├── Visual    → (visualRoot: прячется целиком, пока юнит в доме)
│   ├── Model → SpriteRenderer (Sort Point = Pivot, пивот у ног) + SpriteShadow + ProfessionView
│   │           (+ Animator — выключен, не используется: ходьбу рисует ProfessionView)
│   └── Tired → SpriteRenderer + TiredView   (полоски усталости над головой)
└── Physics   → CircleCollider2D + физматериал `Materials/Unit Physics Material 2D`
```
- Слой **Unit** на всём BaseUnit. Префаб назначается в `UnitDef.prefab`.
- Как юнит выглядит, решает **профессия**: `ProfessionView` крутит кадры `ProfessionDef.frames` текущей профессии.
  Новый юнит — `Unassigned`; профессию он берёт на стойке инструментов (см. ToolRack).
- **Физматериал отскока — только у юнита:** Bounciness 1, Friction 0, **Bounce Combine = Maximum** — юнит
  отскакивает полностью от чего угодно, поэтому постройкам, заборам и зверям материал не нужен.
- Поля `Unit`: def, visualRoot; minBounceAngle (10°) и bounceJitterDegrees (3°) — чтобы не залипать вдоль стен;
  autopilotRadius (12) и autopilotMaxTurnDegrees (90°) — автопилот пьяного к дому; stuckRadius (1) и
  stuckWindow (5 с) — когда юнит считается застрявшим.

### Постройки (Structure)
```
Root          → Rigidbody2D (Static) + Structure  [+ компоненты поведения, таблица ниже]
├── Visual
│   └── Model → SpriteRenderer (пивот у основания) + SpriteShadow + WaterReflection   (+ декор детьми)
└── Physics   → Collider2D (Box / Circle / Polygon — по форме)
```
- Rigidbody2D всегда на ROOT, чтобы `OnCollisionEnter2D`/`OnTriggerEnter2D` доходили до скрипта.
  Препятствие (отскок) или проходимая (триггер) — решает только флаг **Is Trigger** на коллайдере.
- **SpriteShadow** — тень строится из спрайта в `Start`, руками ничего не рисуется: castShadow, offsetPixels (0, 1),
  alpha (30/255). У госта стройки тени нет by design.
- **WaterReflection** — отражение в воде. На префабы ставить ЕГО, а не `Reflector` пакета воды (тот работает в
  edit mode и мусорит в сцену). Поле mirrorOffsetPixels — сдвиг отражения.

Компоненты поведения (на корне, комбинируются):

| Компонент | Поле | Что назначить |
|-----------|------|---------------|
| **Spawner** (дом: выпускает юнитов) | spawnSystem | SpawnSystem в сцене *(или впрыснется в рантайме при постройке)* |
| | unitDef | UnitDef — какой юнит живёт в доме |
| | capacity | число слотов (≥1) — база, перки растят статом `HouseCapacity` |
| | restDuration | 3 с — отдых внутри перед вылетом |
| | launchSpeedMultiplier, launchBoostDuration, launchGap, launchJitterDegrees | параметры вылета (2.5, 1, 0.1, 12°) |
| **ResourceSource** (платит ресурс за удар) | def | ResourceSourceDef |
| | resourceSystem | ResourceSystem *(или впрыснется в рантайме)* |
| | fxAnchor | пустышка-ребёнок, откуда вылетают колосок и цифра (пусто = корень). У пшеницы ≈ `Y 0.5` — верхний край поля |
| **ResourceSourceView** (вид ноды: готова / собрана) | readyRoot, harvestedRoot | дочерние визуалы двух состояний (см. ниже) |
| | swapStateVisuals | вкл — показывать строго один из двух рутов |
| | fadeOutTime, fadeCurve, fadeScaleY | как тает Ready-визуал при сборе (0.2 с, прямая 1→0, масштаб по Y). **На префабе, а не в дефе** — поле и дерево вправе исчезать с разной скоростью |
| **AnimalSpawner** (нора / конюшня) | spawnSystem, resourceSystem | SpawnSystem, ResourceSystem *(или впрыснутся в рантайме при постройке)* |
| | animalPrefab | префаб зверя (см. «Звери») |
| | maxAnimals | макс. живых зверей одновременно (1) — база, перки растят статом `DenCapacity` |
| | spawnCooldown | секунд до замены собранного зверя (5) |
| | territoryRadiusCells | радиус территории в клетках вокруг футпринта (2) |
| | releaseGap, releaseJitterDegrees | выход зверя через свободную сторону норы: зазор 0.3, разброс 12° |
| **ToolRack** (стойка инструментов) | profession | ProfessionDef — инструмент какой профессии лежит на стойке |
| | availableRoot, takenRoot | визуалы «инструмент на месте» / «взят» |
| **Tavern** | capacity, stayDuration | 2 места, 4 с внутри |
| | coinSource | ResourceSourceDef: кого пускают и сколько монет платит гость (по его workerYields) |
| | fxAnchor | откуда вылетает монета |
| | drunkDuration, drunkSpeedMultiplier | 10 с опьянения, скорость ×0.6 |
| | releaseGap, releaseJitterDegrees | выход гостя (0.1, 12°) |
| | autopilot | уставшие гости идут домой на автопилоте (настройки — в `Unit`) |
| **Mill** (мельница, только у реки) | secondsPerSack, capacity | 4 с на мешок, 5 мешков в запасе; полная мельница не мелет |
| **RicePaddy** (рисовое поле) | growTime | 30 с роста; растёт, только пока на поле никого нет |
| **VisitZone** (проход рынка) | hitsPerVisit | 3 засчитанных удара за заход — база, стат `MarketVisitHits` сверху |
| **Pier** | buttonAnchor | точка над пирсом, где висит кнопка престижа |

> Природные источники (дерево, пшеница, камень) — это `Structure` + `ResourceSource` (+ `ResourceSourceView`) **без** `Spawner`.
> Юниты появятся только если у `Spawner` заполнены `spawnSystem` + `unitDef` (цикл стартует в `Spawner.Start()`).

**Ноды, которые отрастают** (`depletion = Regrow` в дефе), имеют два визуальных рута — `ResourceSourceView`
показывает один из них по состоянию. Арт каждого состояния (спрайт + Sorting Layer + пивот) живёт в префабе, не в дефе:
```
Root                → Rigidbody2D (Static) + Structure + ResourceSource + ResourceSourceView
├── Visual
│   ├── Model_Ready     → SpriteRenderer (Sorting Layer Entities, пивот у основания) + SpriteShadow + WaterReflection
│   └── Model_Harvested → SpriteRenderer (Sorting Layer Ground — юниты всегда проходят поверх)
└── Physics         → Collider2D            (гасится, пока нода собрана)
```
> Коллайдер НЕ кладётся внутрь рутов: `CollisionTarget` собирает коллайдеры в `Awake` через
> `GetComponentsInChildren`, который не видит выключенные объекты. Начальное состояние выставит код;
> выключенный в префабе `Model_Harvested` просто убирает мигание в первый кадр.
> `Never`-источники (не истощаются) и `Despawn` (исчезают) второй рут не используют.

**Лес** (`BaseForest`) = `Structure` + `DualVisual` с двумя раскладками деревьев (`Visual Left` / `Visual Right`) —
какая включится, решает чётность ряда, чтобы соседние леса вставали «кирпичиком». Каждое дерево внутри —
вложенный префаб `BaseTree` со своими `ResourceSource` + `ResourceSourceView`.

### Звери (Boar, Fox, Alpaka — подвижные ресурсные ноды)
Зверь — **не юнит и не Structure**: у него нет дефа постройки, клетки на гриде и здоровья.
```
Root          → Rigidbody2D (KINEMATIC, Gravity Scale 0) + CollisionTarget + ResourceSource + Animal + AnimalWander
├── VisualRoot
│   └── Model → SpriteRenderer (Sort Point = Pivot, пивот у ног) + SpriteShadow
└── Physics   → Collider2D (Is Trigger = выкл — юнит отскакивает, и этот отскок = «удар»-сбор)
```
- **Body Type строго Kinematic:** юнит (Dynamic) отскакивает от зверя, а сам зверь при движении проходит сквозь
  постройки и рельеф — валидируются только точки назначения (их выдаёт `AnimalSpawner`: клетки суши в радиусе территории).
- **Сбор — обычный `ResourceSource`**, как у дерева: деф зверя задаёт, кто собирает (workerYields), сколько ударов
  (hitsToDeplete) и что потом — `Despawn` (зверь исчезает, нора выпустит замену через spawnCooldown) или `Regrow`
  (альпака стоит стриженая, пока не отрастёт шерсть). `Animal` полей не имеет — это только связь с норой.
- Поля `AnimalWander`: moveSpeed (1), pauseMin/pauseMax (0.5 / 2 с — шаг→пауза→шаг), fallbackRadius (2 — радиус
  блуждания без норы), visual (что отражать по направлению), personalSpace (0.6), encounterImmunity (1.5 с), lookAhead (0.3).
- Нора / конюшня = обычная Structure + `AnimalSpawner` (см. таблицу выше). Звери уничтожаются на входе в build mode и
  пересоздаются на выходе — как юниты; переезд норы перевозит и территорию (спавнер читает `instance.Cell`).

### FX_Pickup_* (эффект сбора — по одному на источник)
Префаб с одной `ParticleSystem`: колосок из поля, бревно из дерева, монета с рынка. Кладётся в
`ResourceSourceDef.pickupFx`. **Не в сцену и не в префаб ноды** — систему инстанцирует
`HarvestVfxSystem`, по одному общему эмиттеру на деф, и переиспользует до конца сессии.

Три требования, которые система проверяет и о нарушении пишет в консоль с именем ассета:

| Настройка | Значение | Почему |
|-----------|----------|--------|
| **Main → Simulation Space** | **World** | эмиттер один на все поля и переезжает в точку каждого сбора; в Local уже летящие колоски утащило бы за ним |
| **Main → Looping** | **ON** | система играется один раз и дальше кормится вручную; не-зацикленная сама остановится после Duration и молча проглотит все последующие сборы |
| **Emission → Rate over Time / Distance** | **0** | иначе эмиттер будет сыпать частицы в точке последнего сбора между ударами (это warning, не ошибка) |

Сколько частиц за сбор — не в префабе, а в `ResourceSourceDef.pickupFxCount`.
Смещение относительно якоря ноды — **Shape → Position** внутри самого префаба: якорь отвечает
«откуда вообще идёт отклик у этого объекта», точная посадка эффекта — дело эффекта.
Слой сортировки — в модуле **Renderer**: `Overlay`, `Order in Layer` = 0 (цифра идёт поверх, см. ниже).

В плеймоде живые эмиттеры видны детьми объекта `Systems/HarvestVfxSystem` как `PickupFx_Wheat`, `PickupFx_Tree` —
удобно крутить на живую, но правки на них не сохраняются, это копии; править надо префаб.

### FX_HarvestNumber (всплывающая цифра — одна на всю игру)
```
Root → TextMeshPro (МИРОВОЙ, НЕ TextMeshProUGUI)
```
Кладётся в `HarvestNumbers.numberPrefab`. Тип поля в коде — `TextMeshPro`, поэтому UGUI-вариант
просто не назначится: он живёт на Canvas, а Canvas перестраивает весь батч на любое движение.

- **Extra Settings → Order in Layer = 100**, слой `Overlay`. Слой старше порядка: если развести
  цифру и партиклы по разным слоям, `Order in Layer` уже ничего не решит. Зазор в сотню — чтобы
  потом было куда втиснуть эффект между ними, не перенумеровывая всё.
- **Масштаб префаба** придётся сильно уменьшить: мировой TMP огромен рядом с клеткой в 1 юнит.
  Код его не затирает — `scaleCurve` умножает масштаб префаба, а не заменяет его.
- Шрифт, размер, цвет и обводка — целиком дело префаба. Движение и тайминги — в ассете
  `Harvest Number Motion` (HarvestNumberMotionDef), чтобы ощущение пережило смену рендерера (см. ARCHITECTURE).

### BaseFence / заборы (Structure на РЕБРЕ грида)
Забор не занимает клетку — он стоит на **ребре** (границе между двумя клетками). Это один префаб с
**двумя позами-детьми**; нужную включает `DualVisual` по ориентации ребра.
```
Root              → Rigidbody2D (Static) + Structure + DualVisual (firstRoot = Horizontal, secondRoot = Vertical)
├── Horizontal
│   ├── Visual/Model → SpriteRenderer + SpriteShadow
│   └── Physics      → EdgeCollider2D (линия вдоль ребра)
└── Vertical
    ├── Visual/Model → SpriteRenderer + SpriteShadow
    └── Physics      → EdgeCollider2D
```
- Обе позы **отцентрованы в local (0,0)**: рут ставится в середину ребра (`IslandGrid.EdgeToWorld`), и
  спрайт+коллайдер ложатся ровно на линию решётки. H-ребро = горизонтальная линия, V-ребро = вертикальная.
- `DualVisual` только показывает одну позу из двух; какую — решает код постройки. Коллайдер **неактивной** позы
  гаснет вместе с её GameObject — отдельного кода не нужно.
- **Коллайдеры:** `Is Trigger` выкл, физматериал не нужен (отскок даёт материал юнита). Юнит —
  `Collision Detection = Continuous`, поэтому сквозь линию забора не туннелирует.
- В `StructureDef` забора выставить **placement = Edge** (`size`/`border` для рёбер не используются).
- Тот же префаб служит превью: ghost = его инстанс с выключенными коллайдерами и поведением, тинт
  green/red на обоих рендерерах.
- **Move/Sell:** клик «выигрывает» забор, когда курсор ближе 0.3 клетки к линии ребра (иначе берётся
  структура в клетке под курсором).

### BuildCard (префаб карточки, инстанцируется BuildPanelUI в рантайме)
```
Root → Image + Button + CanvasGroup + BuildCardUI
├── Shadow                → Image
└── AnimatedVisual
    ├── Artwork           → CanvasGroup
    │   ├── Thickness     → Image
    │   ├── SelectionFrame (выкл)       → Image
    │   ├── CardBody      → Image
    │   ├── ItemContainer → Image
    │   │   ├── IconSelectionPlaceholder (выкл) → Image
    │   │   └── BuildingIcon            → Image
    │   ├── ResourcePanel → Image
    │   │   ├── ResourceIcon            → Image
    │   │   └── Cost                    → TMP_Text
    │   └── ShineViewport → RectMask2D
    │       └── Shine (выкл)            → Image
    └── LockedOverlay (выкл)            → Image
        └── LockedText                  → TMP_Text
```
Поля `BuildCardUI` названы по детям (cardBody → `CardBody`, iconImage → `BuildingIcon`, costText → `Cost`,
lockedOverlay → `LockedOverlay` и т. д.); `countText` («Count: 1 of 2») — опционален. Размеры, наведение,
наклон и блик — в ассете `ScriptableObjects/BuildMode/Build Card Motion Profile` (поле motionProfile), иконки
ресурсов — ResourceIconSet (iconSet), точка подсказки — hintAnchor.

### BuildTabUi (префаб вкладки, инстанцируется BuildPanelUI в рантайме — по кнопке на непустую вкладку)
```
Root → Button + BuildTabUI + Image          (область клика — стоит на месте, корень двигает Layout Group)
└── Visual          → Image (фон)           (едет вправо, пока вкладка под курсором или открыта)
    ├── Label       → TMP_Text              (название вкладки)
    └── Image       → Image                 (иконка вкладки; без спрайта прячется)
```
Поля `BuildTabUI`: button, label, iconImage, selectedIndicator (необязателен — класть внутрь `Visual`),
visual, slideOffset (4 px), slideDuration (0.1 с), slideEase (OutQuad). Подпись и иконка необязательны:
что назначено, то и показывается.

### Пирс (Pier)
Пирс — обычная **Structure**, но ставит его не игрок, а `IslandSystem`: на каждом забеге из `StartConfig.pier`,
на восточный берег стартовой зоны, и дальше он стоит на месте. Морской проход от него до горизонта
генератор зон держит свободным.
- **Префаб** (`Prefabs/Buildings/Pier`) = Rigidbody2D (Static) + `Structure` + `Pier` + Visual/Model + Physics (BoxCollider2D)
  + пустышка `ButtonAnchor`. `Pier` — маркер: по нему кнопка престижа находит пирс забега и висит над `buttonAnchor`.
- **StructureDef Пирса:** `placement = Cell`, `border = 0` (иначе потребует клетки за краем острова), `size` 1×3,
  `cost` пустой, `canSell` и `canMove` выкл. В палитру стройки не кладётся.
- **Престиж — кнопка, а не клик по пирсу:** `Canvas/Prestige/PrestigeButton` появляется с эпохи
  `PrestigeSystem.pierUnlockAge` и висит над пирсом.

## ScriptableObject-ассеты

Лежат в `Assets/Little Peeps/ScriptableObjects/` по папкам. Меню создания — **Create → LittlePeeps → …**

| Ассет | Меню создания | Главное содержимое |
|-------|---------------|--------------------|
| **StartConfig** | LittlePeeps/StartConfig | islandSeed (0 = случайный), islandRules (правила генерации), startBiome, house, pier, startingResources, startingModifiers. Один на игру — в `RunManager.startConfig` |
| **StructureDef** | LittlePeeps/StructureDef | id, displayName, description, icon, prefab, **placement (Cell = футпринт клеток / Edge = забор на ребре)**, size + **footprintMask** (форма внутри size — рисуется клетками в инспекторе), cost[], requiredAge, lockedUntilPerk (карточка заперта, пока её не откроет перк), allowedTerrain[] (пусто = любой биом), sellRefundPercent (доля возврата при продаже, 0.5), canSell, canMove, passable / animalsAvoid, isWater, needsWaterSide (мельница), border (расширяет занимаемую территорию: дом=1 → 2×2 занимает 4×4), **maxCount** (0 = без лимита; растёт статом `StructureLimit`), costStepFlat / costStepPercent / exponentialCostGrowth (рост цены за каждую стоящую) |
| **ResourceSourceDef** | LittlePeeps/ResourceSourceDef | resource, workerYields[] (кто и сколько добывает; пусто = никто; «любой рабочий» = перечислить всех), **depletion** (Never / Despawn / Regrow), hitsToDeplete, regrowTime, keepBodyWhileDepleted, **pickupFx** (префаб FX_Pickup_*; пусто = только цифра) + **pickupFxCount**. `pickupFx` — единственный визуал в дефе, и намеренно: он отвечает на вопрос «что именно добыли», а на него нельзя ответить типом ресурса. Один тип дефа для статичных нод и зверей |
| **UnitDef** | LittlePeeps/UnitDef | id, prefab (→ BaseUnit), speed, profession (с какой профессией рождается — `Unassigned`, настоящую даёт стойка) |
| **ProfessionDef** | LittlePeeps/ProfessionDef | type (UnitType), frames[] — кадры ходьбы этой профессии |
| **BiomeDef** | LittlePeeps/BiomeDef | profile (параметры генерации зоны), tileSet, displayName, icon, previewColor (заливка зоны при выборе) |
| **IslandTileSet** | LittlePeeps/IslandTileSet | тайлы земли (светлые/тёмные клетки), углы, края, внутренние углы |
| **BuildPaletteDef** | LittlePeeps/BuildPalette | tabs: вкладки нижней панели, у каждой name, icon, structures (список StructureDef). Порядок вкладок = порядок кнопок и цифр 1–9, порядок structures = порядок карточек; пустая вкладка кнопку не получает. Группировка живёт здесь, на StructureDef категории нет |
| **AgeDef** | LittlePeeps/AgeDef | title (только в лог — на баннере номер римскими цифрами), resourceCost[] (цена), modifiers[] (StatModifier — бонусы эпохи), bonusOverride (текст вместо сгенерированного описания). Порядок задаётся списком `AgeSystem.ages`, начиная с Age II |
| **Перки** | LittlePeeps/Perks/Stat Perk, …/Unlock Structure Perk | общее: id, title, description, icon, weight (шанс выпасть), requires (предыдущий уровень — цепочки уровней). Stat Perk — modifiers[]; Unlock Structure Perk — structure (снимает `lockedUntilPerk`) |
| **PerkCatalogueDef** | LittlePeeps/Perk Catalogue | perks — список перков для `PerkSystem.catalogue` |
| **Мета-перки** | LittlePeeps/Meta/Stat Upgrade, …/Start Resources, …/Start Perk Pick, …/Start Age | общее: id, title, description, maxLevel. Stat Upgrade — modifiers[]; Start Resources — resources; Start Age — zoneBiome (каким биомом растут пропущенные эпохи) |
| **GlobalUpgradeCatalogueDef** | LittlePeeps/Meta/Upgrade Catalogue | upgrades — мета-перки в порядке мета-экрана, для `PrestigeSystem.upgrades` |
| Island Rise Profile | LittlePeeps/Island Rise Profile | все ручки подъёма острова: тайминги, кривые прыжка и сплющивания, эффекты, звуки, тряска |
| Harvest Number Motion | LittlePeeps/Harvest Number Motion | полёт всплывающей цифры: lifetime, travel, кривые, разброс |
| Build Card Motion Profile | LittlePeeps/UI/Build Card Motion Profile | размеры и анимации карточки стройки |
| ResourceIconSet | LittlePeeps/ResourceIconSet | icons — иконка на каждый ресурс (HUD, ценники, подсказки) |

> Координаты сетки **знаковые** и не зависят от того, какие клетки существуют: центр клетки `c` = мир `(c+0.5)·cellSize`.
> Дом 2×2 на `cell=(-1,-1)` занимает `(-1,-1)…(0,0)` и центрируется в мировом нуле.

## Управление (камера + горячие клавиши) — как связано

Три слоя ввода, расцеплённые между собой:

- **`InputHandler`** — низкоуровневый роутер мыши: ЛКМ/ПКМ → мировые координаты → три события:
  - `OnWorldClick` — ЛКМ, **только если курсор НЕ над UI**. Слушают `TapSystem` (буст юнитов + щелчок) и `PlacementController` (build mode);
  - `OnAnyClick` — любая ЛКМ, и над UI тоже. Слушает `AgeSequencer`: тап ускоряет подъём острова и закрывает баннер эпохи, а баннер сам UI;
  - `OnRightClick` — любая ПКМ: отмена в build mode, над панелью тоже.

  Камеру он не трогает.
- **`CameraController`** (на своём GO, НЕ на CameraTarget и НЕ на камере) — двигает transform пустого **CameraTarget** = логическую цель камеры; поллит ввод сам, считает на `unscaledDeltaTime` → работает и в build mode (`timeScale=0`). Источники: WASD + стрелки, курсор у края экрана, **drag средней кнопкой мыши** (мир тащится под курсором), **колесо — зум** (ortho size vcam в пределах `minZoom…maxZoom`). Цель клампится по `IslandGrid.WorldBounds()` + `boundsMargin`; границы перечитываются на `AgeStartedEvent` (рост острова) и `RunStartedEvent`. Плавность даёт не он, а **Cinemachine vcam**, которая следует за целью с damping; `Main Camera` несёт `CinemachineBrain` и рендерит. `viewCamera` (= Main Camera) нужен скрипту только чтобы перевести drag-пиксели в мир (его `orthographicSize` крутит brain → отражает живой зум). `Confiner2D` — отдельной итерацией позже.
  - **Старт забега** (`Start` и каждый `RunStartedEvent`): камера мгновенно, без damping, встаёт в центр острова с зумом из сцены (`Lens > Orthographic Size` у vcam) — новый забег всегда начинается с одного вида, что бы ни крутили до этого.
- **`GameHotkeys`** (на `Input`) — дискретные команды по нажатию, публикует события в `EventBus`, ни во что не лезет напрямую. UI его не фильтрует: клавиатура не целится в то, что под курсором, поэтому подписчики сами проверяют состояние:
  - **B** → `BuildModeToggleRequestedEvent` (тот же путь, что кнопка build mode → `GameplayContainerState`, с 5-сек кулдауном);
  - **X** → `SellModeRequestedEvent` → `BuildPanelUI` тогглит инструмент Sell тем же путём, что кнопка (подсветка + контроллер синхронны); вне build mode — no-op;
  - **1–9** (верхний ряд и нампад) → `BuildTabRequestedEvent` → `BuildPanelUI` открывает N-ю вкладку; вне build mode — no-op. Клавиши зашиты в коде, не в инспекторе: цифра = позиция вкладки;
  - **Esc** → `ExitToMenuRequestedEvent` → `GameBootstrap` пока **отклоняет** переход (warning в консоли): у `MainMenuState` нет экрана, и выход в него оставил бы игрока без пути назад. Вернуть — одна строка, когда меню появится.

**Мышь над UI принадлежит UI.** Колесо и начало drag средней кнопкой, как и `OnWorldClick`, ничего не делают, пока курсор над UI (`InputHandler.IsPointerOverUI()` — ответ EventSystem). Начатый на острове drag может пройти и над UI. WASD и край экрана не фильтруются: они ни во что под курсором не целятся.

> ⚠️ «Над UI» = над **любой** графикой с включённым **Raycast Target**, даже прозрачной. Невидимая подложка на весь экран отрезает от игры колесо, тапы и drag. Если окно должно оставлять остров доступным, у его подложки Raycast Target выключен — так сделано у `Screens/ZoneScreen/Panel`, иначе при выборе зоны не работает зум. У модальных окон (затемнение `PerkScreen/Panel`, `MetaScreen`) подложка перехватывает ввод намеренно.

> ⚠️ `CameraController` использует `viewCamera.orthographicSize` (drag) — **камера должна быть Orthographic** (2D-проект, так и есть). линзу vcam тоже держать Orthographic.

## Build mode — как связано

- **Вход/выход** по `BuildModeButton` (правый-нижний угол) **или клавише B** → событие `BuildModeToggleRequestedEvent` → `GameplayContainerState` переключает внутренний FSM `Playing↔BuildMode` и держит 5-сек кулдаун на повторный вход.
- **`BuildModeState.Enter`**: `Time.timeScale=0` + `SpawnSystem.DespawnAllAndResetSpawners` (юниты в пул, зверей их норы уничтожают сами). **`Exit`**: `SpawnSystem.WarmupAllSpawners` (юниты и звери пересоздаются из зданий) + `Time.timeScale=1`. Оба вида спавнеров идут через один реестр `IStructureSpawner`.
- **Инструмент = что выбрано в панели** (`PlacementController`):
  - карточка → **PLACE** (гост под курсором, клик ставит),
  - кнопка Sell → **SELL** (наведение красит постройку, клик продаёт с возвратом `sellRefundPercent` от цены предыдущей копии — при росте цены возвращается то, что за эту копию заплатили),
  - ничего не выбрано → **MOVE** (клик берёт постройку, она тащится за курсором; клик ставит, ПКМ — отмена на исходное место).
- `GridOverlay` показывает/прячет сетку на вход/выход.
- **`EventSystem` обязателен** — клики над UI до `PlacementController` не доходят (`InputHandler` не пускает их в `OnWorldClick`, спрашивая EventSystem), а uGUI-кнопки без него не нажимаются. ПКМ-отмена работает и над панелью.

## Порядок выполнения скриптов — НЕ трогаем

Script Execution Order настраивать **не нужно** (это костыль). Инициализация независима от порядка by design:

- Unity гарантирует, что **все `Awake` отработают раньше любого `Start`**.
- `GameBootstrap` делает всю проводку и старт рана в своём `Awake`. К моменту любого `Start` всё уже связано, а остров сгенерирован — какой объект инициализируется первым, не важно.
- **Правило для новых систем:** не читай «вколотое» состояние (контексты, другие системы) в своих `Awake`/`OnEnable` — только начиная со `Start`. Подписки на события и `GetComponent` в `Awake`/`OnEnable` — можно.

## Что должно произойти при Play

1. `GameBootstrap.Awake`: загрузка MetaContext (с диска пока не читается — каждый запуск с чистого профиля), проводка систем;
   `RunManager.StartNewRun()` → RunContext, стартовые модификаторы и ресурсы из StartConfig плюс купленные мета-перки,
   `IslandSystem.GenerateForRun` → стартовая зона с домом и пирсом; мета-перк Start Age сразу пропускает эпохи (остров
   растёт без анимации). App FSM → `Boot → GameplayContainer → Playing`. Камера — в центре острова, музыка — с первого кадра.
2. На экране: юниты вылетают из домов и отскакивают; `Unassigned` берут инструмент на стойке и работают профессией;
   удар по ноде платит по её `workerYields`; вокруг нор гуляют звери; уставшие возвращаются в дом. Тап — буст юнитов в
   радиусе со щелчком.
3. Если мета-перк дал стартовый выбор перка — он открывается сразу.
4. Build mode (кнопка или B) → пауза + сетка + панель: ставим / продаём / двигаем постройки; выход → юниты респавнятся.
5. **Next Age** (`AgeAdvancePanel`) активна, когда хватает ресурсов: клик → выбор зоны → зона поднимается из моря →
   баннер «Age N» → выбор перка. Тап ускоряет подъём, второй — к финалу, тап по баннеру закрывает его.
6. С эпохи `pierUnlockAge` над пирсом — кнопка престижа → «End this run?» → Yes → очки престижа → мета-экран
   (тратим очки на мета-перки) → **Play** → новый забег; камера снова в центре острова.

## Project settings — важное (не в сцене, но влияет)

- **Physics 2D → Layer Collision Matrix:** Unit×Unit **OFF** (юниты проходят сквозь друг друга), Unit×Default **ON**.
- **Physics 2D → Bounce Threshold = 0.1** (иначе скольжение на малых скоростях).
- **Y-сортировка:** на URP 2D Renderer-ассете (`Assets/Settings/Renderer2D.asset`) — Transparency Sort Mode = Custom Axis, Axis (0,1,0). Юниты и постройки — на одном Sorting Layer (`Entities`) с равным Order, сортируются по Y динамически.
- **Sorting Layers** снизу вверх: Water, Ground, Default, Entities, Overlay, Mesh.

## Бонусы (RunStats) — коротко

Прибавки к параметрам (`ProductionGlobal`, `ResourceYield`, `UnitSpeed`, `HouseCapacity`, `TapRadius`, …) задаются списками
`StatModifier`: в `AgeDef.modifiers`, `StartConfig.startingModifiers`, в Stat-перках и в Stat-мета-перках. Слой живёт в
`RunContext.stats` и сбрасывается на престиже. База — в конфигах и компонентах (`UnitDef.speed`, `ResourceSourceDef.workerYields`,
`Spawner.capacity` …), бонус — отдельным слоем. Подробности для дизайнера: **BONUS_SYSTEM_GUIDE.md**.

## Что ещё заглушка

- **Главное меню:** `MainMenuState` без экрана, Esc переход отклоняет.
- **Сохранение на диск:** `SaveSystem.Save/Load` — TODO. Очки престижа и мета-перки живут до закрытия игры.
