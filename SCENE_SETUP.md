# Little Peeps — организация систем в сцене

Как устроен старт и как разложены объекты в `SampleScene`, чтобы сцена была «по полочкам».
Документ держим синхронным с кодом — обновляем при добавлении/переименовании систем и полей.

> Полная сверка с кодом, сценой и префабами: **2026-10-08**. Стройка в реальном времени (режима
> стройки больше нет): **2026-10-15**, сверено со сценой и `Canvas.prefab`. Цикл подношений (престиж
> через пирс убран): **2026-10-16**, сверено со сценой и `Canvas.prefab`. Системы попадают в UI только
> кодом, через `UIServices` (оверрайдов проводки Канваса в сцене больше нет): **2026-10-18**, сверено со сценой
> и `Canvas.prefab`; там же HUD по макету (AgePanel из двух кусков встык, подложка в ResourceUnit). Панель стройки —
> четыре отдельных виджета, у каждого состояния один хозяин; блок цены эры растёт по строкам; лента времени
> `TimePanel`; панель взятых перков (`PerkPanel`, префаб `PerkSlot`) и подсказка слева: **2026-10-19**, сверено с
> `Canvas.prefab`. Лента времени работает (время в отрезках, годы под засечками, подсказка подношения), раннего
> подношения и `OfferingPanel` больше нет: **2026-10-20**, сверено с `Canvas.prefab`. Не описана проводка звука (`SoundSystem` / `SoundDef`):
> система в сцене есть, её поля и звуковые поля префабов сюда ещё не внесены.

## Принцип

- **Одна сцена.** Каждая система — свой объект в сцене с компонентом. Кодом системы не создаются.
- **`GameBootstrap` — единственная точка регистрации.** Порядок инициализации задаёт он (раздаёт контексты в `Awake`), а не случайный порядок `Awake` у объектов.
- **Зависимости между системами — ссылками в инспекторе.** Каждое поле `[SerializeField]` нужно прокинуть руками. Забыл — `NullReferenceException` на старте.
- **Кроме UI: Канвас ссылок на объекты сцены не держит.** Нужные системы элементам Канваса раздаёт код — `GameBootstrap` собирает набор `UIServices`, `UIRoot` отдаёт его каждому `IUIElement` под Канвасом (см. «UI»). Из сцены в Канвас ведёт одна ссылка (`GameBootstrap.uiRoot`), из Канваса в сцену — ни одной. Оверрайд в сцене не виден в Prefab Mode и стирается одним Revert — так однажды панель стройки осталась без `PlacementController`.
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
│   ├── OfferingSystem          → OfferingSystem     (цикл подношений: часы, квота, оплата из запасов)
│   ├── PerkSystem              → PerkSystem
│   ├── PrestigeSystem          → PrestigeSystem
│   ├── PlacementController     → PlacementController
│   ├── RunManager              → RunManager
│   ├── ResourceSystem          → ResourceSystem
│   ├── StructureSystem         → StructureSystem
│   ├── SpawnSystem             → SpawnSystem
│   ├── SoundSystem             → SoundSystem        (звуки ударов, сборов, дверей; проводка не описана — см. выше)
│   ├── TapSystem               → TapSystem + TapRadiusVisual + LineRenderer   (кольцо радиуса тапа у курсора;
│   │                              AudioSource для щелчка тапа TapSystem добавляет себе сам в рантайме)
│   ├── UnitPool                → UnitPool           (юниты из пула становятся его детьми)
│   ├── UnitSystem              → UnitSystem
│   └── WaterSystem             → WaterSystem        (море инстанцирует в рантайме из префаба — в сцену воду НЕ класть)
├── Island                      → IslandSystem
│   ├── Grid                    → Grid (компонент)   ← все тайлмапы под ним — на слое сортировки Island
│   │   ├── Ground              → Tilemap + TilemapRenderer + Rigidbody2D (Static) + TilemapCollider2D
│   │   │                          + CompositeCollider2D   ← суша и граница острова
│   │   ├── GroundTrim          → Tilemap + TilemapRenderer   ← кайма берега: лежит на клетках ВОДЫ, поэтому без коллайдера
│   │   ├── Decor               → Tilemap + TilemapRenderer   ← декор поверх земли (Order 1), без коллайдера
│   │   ├── BordersForest       → Tilemap + TilemapRenderer   ← мягкая граница леса, без коллайдера
│   │   ├── BordersMeadow       → Tilemap + TilemapRenderer   ← мягкая граница луга (арта пока нет), без коллайдера
│   │   └── Water (выключен)    → Tilemap + TilemapRenderer
│   └── GridOverlay             → MeshFilter + MeshRenderer + GridOverlay   (Transform строго 0,0,0, без поворота/масштаба)
├── SceneGridGizmo (выключен)   → SceneGridGizmo     (сетка только для Scene view, в игре не участвует)
├── ZonePreviewOverlay          → MeshFilter + MeshRenderer + ZonePreviewOverlay   (заливка зоны под карточкой при выборе)
├── IslandRise                  → IslandRisePlayer   (Transform 0,0,0; в рантайме его дети — дублёры тайлов,
│                                  излучатели эффектов, «Rise voices» и CinemachineImpulseSource)
├── Music (выключен)            → AudioSource: Audio Resource = трек флейты, Play On Awake, Loop, Priority 0
│                                  ← фоновая музыка без кода: играет с запуска всю сессию, timeScale её не трогает.
│                                    Сейчас объект выключен в сцене — музыки нет
└── Canvas                      → префаб `Prefabs/Elements/Canvas`: Canvas + CanvasScaler + GraphicRaycaster
    │                             + **UIRoot** + **UIVisibility**. Порядок детей = порядок отрисовки: ниже = поверх
    ├── Hud                     → CanvasGroup                    ← весь игровой HUD одной группой
    │   ├── AgePanel            → CanvasGroup + AgeAdvancePanel + Vertical Layout Group + Content Size Fitter
    │   │   │                     ← левый верхний угол, ширина 81, высоту считает Fitter. Два нарезанных куска
    │   │   │                       встык: Spacing −1, потому что строка шва нарисована на обоих. Размеры кускам
    │   │   │                       задаёт раскладка (Control Child Size → Width и Height), руками их не трогать
    │   │   ├── Timeline        → Image (panel_base_age_top, Sliced) + AgeTimelinePanel
    │   │   │   │                 + LayoutElement (Preferred Height 43)                  (81×43)
    │   │   │   └── Viewport    → RectMask2D
    │   │   │       └── Content → Vertical Layout Group + Content Size Fitter
    │   │   │                     (прижат к НИЗУ: карточки эр растут вверх, маска режет сверху)
    │   │   └── Advance         → Image (panel_base_age_expand, Sliced) + Vertical Layout Group
    │   │       │                 ← растёт по строкам цены: Padding 4/4/4/50 (снизу место под кнопку),
    │   │       │                   Upper Center, Control Child Size W+H. Высота 54 + 14 × строк: 68 / 82 / 96
    │   │       │                   при 1–3 строках; 54 на последней эре (Cost выключен) и в редакторе (Cost пуст)
    │   │       ├── Cost        → Image + Grid Layout Group + AgeCostPanel  (ценник, юниты в рантайме; 2 колонки,
    │   │       │                 ячейки 34×13 — свою высоту сетка считает сама)
    │   │       └── AdvanceButton → Image + Button + LayoutElement (Ignore Layout)
    │   │           │             (родные 69×43, по центру, 4 px от низа: прибита к низу Advance и едет с ним)
    │   │           └── Label   → TMP_Text
    │   ├── SellButton          → Image + Button + CanvasGroup + SellButtonUI   (левый нижний угол (2, 17), 34×42)
    │   │   └── SelectedFrame   → Image                 (горит, пока в руке Sell)
    │   ├── BuildTabs           → Vertical Layout Group + Content Size Fitter + CanvasGroup + BuildTabBar
    │   │                         ← слева от палитры (59, 7), ширина 79, растёт вверх; Spacing −3 — соседние
    │   │                           вкладки перекрываются на 3 px, как на макете; кнопки вкладок в рантайме
    │   ├── BuildPalette        → CanvasGroup + Image (card_panel, Sliced) + BuildPaletteUI   (справа внизу (−47, 4), 440×64)
    │   │   └── Viewport        → Image + RectMask2D + BuildPanelScroller   (тянется по ширине палитры)
    │   │       └── CardRow     → Horizontal Layout Group       (карточки построек в рантайме)
    │   ├── BuildToggle         → Image + Button + BuildPanelToggle   (правый нижний угол (−9, 19), 32×40: прячет /
    │   │                         показывает SellButton, BuildTabs и BuildPalette — не режим, игра идёт и так и так)
    │   ├── ResourceBar         → Image + Grid Layout Group + ResourcePanel   (ResourceUnit на ресурс, в рантайме)
    │   │                         ← правый верхний угол, 124×36; ячейки 34×13, три в строку
    │   ├── TimePanel           → Image (time_panel, Sliced, **Raycast Target ВКЛ** — ловит наведение) + CanvasGroup
    │   │   │                     + TimePanel   ← лента времени до подношения, по центру сверху (0, −6), 288×30;
    │   │   │                     своя строка видимости, у CanvasGroup включён Ignore Parent Groups
    │   │   ├── Now             → Image                 (кружок «сейчас», слева, 5..13)
    │   │   ├── Track           → RectMask2D            (маска слоя ромбов: от левого края кружка до большого ромба, 5..281)
    │   │   │   ├── Belt        → RectMask2D            (своя маска: от края кружка до точек, 15..267; линия, засечки
    │   │   │   │                 лет и края линии у засечек подношения — в рантайме)
    │   │   │   └── Riders      → только RectTransform  (ромбики подношений — в рантайме)
    │   │   ├── Years           → RectMask2D            (цифры лет под засечками: 15..267, строки 16..20)
    │   │   │   └── Year        → TMP_Text, выключен    (шаблон цифры: Micro5 11, #BE9F85; копии — в рантайме)
    │   │   ├── Dots            → Image                 («и так далее», справа)
    │   │   ├── Offering        → Point + Icon (Image)  (большой ромб и руки под ним; руки уезжают с маленьким ромбом)
    │   │   └── Tooltip         → Image (HUD/tooltip, Sliced) + CanvasGroup + Vertical Layout Group + OfferingTooltip
    │   │       │                 ← подсказка подношения: видна, пока курсор на ленте; кликов не ловит
    │   │       ├── Header      → TMP_Text              («N years until Offering», m6x11plus 12, влево)
    │   │       └── Costs       → Grid Layout Group     (ResourceUnit на ресурс квоты — в рантайме; 34×13, 2 колонки)
    │   └── PerkPanel           → Grid Layout Group + Content Size Fitter + PerkPanel   ← взятые перки:
    │                             правый верх (−9, −76), ширина 41, растёт вниз; ячейки PerkSlot в рантайме,
    │                             по две в ряд, справа налево
    ├── AgeTransition           → Image (чёрный α≈0.42 — подложка баннера) + CanvasGroup (Alpha 0)   ← баннер эпохи;
    │   │                         его и BannerText перечисляет UIRoot, бутстрап отдаёт их AgeSequencer —
    │   │                         тот проявляет и гасит баннер целиком, клики он ловит только пока виден
    │   └── BannerText          → TMP_Text                                 ← плашка «Age N»
    ├── Screens                 → ТОЛЬКО RectTransform, растянут на весь Canvas
    │   │                         ← CanvasGroup сюда НЕ вешать: перемножится с альфой экранов
    │   │                         ← и НЕ выключать — ни его, ни экраны: их прячет UIVisibility, а выключенный
    │   │                           экран не покажется вовсе (выбор зоны зависнет на паузе)
    │   ├── PerkScreen          → CanvasGroup + PerkSelectionUI
    │   │   └── Panel           → Image (затемнение — перехватывает ввод, это модальное окно)
    │   │       ├── TitleText   → TMP_Text
    │   │       └── CardColumn  → Vertical Layout Group         (перк-карточки в рантайме)
    │   └── ZoneScreen          → CanvasGroup + ZoneSelectionUI
    │       └── Panel           → Image (прозрачная, **Raycast Target ВЫКЛ** — иначе при выборе зоны нет зума)
    │           └── CardRow     → Horizontal Layout Group       (зон-карточки в рантайме)
    ├── HintView                → Image + CanvasGroup + HintView + Vertical Layout Group   ← окно подсказок, кликов не ловит
    │   ├── TitleText, BodyText → TMP_Text
    │   ├── CostContainer       → Vertical Layout Group         (строки цены в рантайме)
    │   └── FooterText          → TMP_Text
    ├── ConfirmDialog           → вложенный префаб `Prefabs/Elements/ConfirmDialog`: CanvasGroup + ConfirmDialog
    │                             (Backdrop, Panel с текстом и кнопками Yes / No)
    ├── FPSmeter
    │   └── FPSText             → TMP_Text + FpsMeter                     ← счётчик кадров
    ├── MetaScreen              → Image + CanvasGroup + MetaUpgradesUI   ← экран после престижа (включён, как Screens)
    │   ├── TitleText, PointsText → TMP_Text
    │   ├── Scroll View         → Image + ScrollRect
    │   │   ├── Viewport        → Image + Mask
    │   │   │   └── Content     → Vertical Layout Group + Content Size Fitter   (строки мета-перков в рантайме)
    │   │   └── Scrollbar Vertical
    │   └── PlayButton          → Image + Button
    └── ref (выключен)          → скрин-референс вёрстки, в игре не участвует
```

> `HintView` и `ConfirmDialog` по замыслу кода — **последние** дети Canvas, чтобы рисоваться поверх всего.
> Сейчас после них стоят `FPSmeter`, `MetaScreen` и выключенный `ref`. Мета-экран открывается уже после ответа и
> подсказок не показывает, так что это не мешает.

## Checklist проводки (критично)

Прокинь все ссылки. **Жирным** — то, без чего старт падает с `NullReferenceException` или ломается стройка.

### Bootstrap и системы

| Объект | Компонент | Поле | Что назначить |
|--------|-----------|------|---------------|
| Bootstrap | **GameBootstrap** | **resourceSystem, islandSystem, unitSystem, spawnSystem** | соответствующие компоненты |
| | | **buildingSystem** | **StructureSystem** (поле зовётся «Building System»!) |
| | | **tapSystem, runManager, prestigeSystem, saveSystem** | соответствующие компоненты |
| | | **ageSystem** | AgeSystem (нужен для эпох) |
| | | **offeringSystem** | `Systems/OfferingSystem`. Пусто — ошибка в консоли, игра идёт без цикла: ни квот, ни перков, ни кары, а значит, забег не закончить |
| | | ageSequencer, perkSystem | AgeSequencer, PerkSystem |
| | | **placementController** | PlacementController — стройка; его же бутстрап отдаёт `TapSystem` кодом, чтобы клик с инструментом в руке не был тапом |
| | | **uiRoot** | **UIRoot на Canvas** — единственная ссылка из сцены в Канвас. Всё, что UI нужно из сцены, бутстрап отдаёт ему кодом, набором `UIServices`: resourceSystem, ageSystem, offeringSystem, runManager, placementController и два поля ниже |
| | | cameraController, zonePreview | `CameraController`, `ZonePreviewOverlay` — бутстрап сам их не использует, только передаёт в UI: выбор зоны двигает камеру и рисует зону на острове. Пусто — предупреждение `[UIServices] … is not assigned`, карточки зон без камеры и превью |
| Bootstrap | SaveSystem | — | (полей нет; запись на диск пока заглушка) |
| Systems/RunManager | **RunManager** | **resourceSystem, islandSystem, structureSystem, spawnSystem** | ResourceSystem, Island, StructureSystem, SpawnSystem (spawnSystem обязателен — иначе NPE в StartNewRun) |
| | | **startConfig** | `ScriptableObjects/StartConfig/Start Config` — сид и правила острова, стартовый биом, дом, пирс, стартовые ресурсы и модификаторы. Пусто = случайный остров без дома и пирса, ноль ресурсов |
| Systems/ResourceSystem | ResourceSystem | logChanges | вкл/выкл лог ресурсов в консоль (дебаг) |
| Systems/StructureSystem | **StructureSystem** | **islandSystem, resourceSystem, spawnSystem** | Island, ResourceSystem, SpawnSystem (без SpawnSystem стройплощадка не видит юнитов на месте и ставит здание сразу) |
| | | siteLook → blueprintColor, groundColor | вид стройплощадки, пока у неё нет своего арта: множитель спрайтов чертежа (белый α0.45 — альфа = насколько прозрачен) и подложка по клеткам (песочный α0.25) — дефолты |
| | | siteLook → groundSortingLayer, groundSortingOrder | Ground, 1000 — подложка под ореолом госта (1001), чтобы гост над площадкой читался |
| | | siteLook → fenceThickness | 0.25 клетки — ширина полосы площадки забора вдоль ребра |
| Systems/SpawnSystem | **SpawnSystem** | **unitPool, unitSystem** | UnitPool, UnitSystem |
| | | islandSystem | Island (впрыскивается в юнитов — задел на будущее island-aware поведение) |
| | | soundSystem | SoundSystem (юниты берут его отсюда для своих звуков; пуф тоже звучит через него) |
| | | stuckSweepInterval | 0.5 с (дефолт): как часто ищутся застрявшие юниты — их отправляют в ближайший дом |
| | | puffFx, puffParticles | «пуф», с которым исчезают лишние жители проданного дома и звери проданной норы: префаб ParticleSystem как общий эмиттер (World space, Looping, без своей эмиссии) и частиц на пуф (8). **В сцене пусто** — исчезают без эффекта |
| | | puffSound | SoundDef пуфа — в сцене `SoundDef/Puff_Air` (3 варианта звука, `Sound/Puff`) |
| Systems/UnitSystem | UnitSystem | — | (полей нет) |
| Systems/UnitPool | UnitPool | — | (полей нет; юниты инстанцируются его детьми) |
| Systems/PerkSystem | PerkSystem | catalogue | `ScriptableObjects/Perks/Perk Catalogue Def` (PerkCatalogueDef — список перков) |
| | | choicesOffered | 3 (дефолт): сколько карточек в выборе перка (особых бывает меньше — сколько осталось) |
| Systems/OfferingSystem | **OfferingSystem** | **schedule** | `ScriptableObjects/OfferingDef/Offering Schedule Def`. Пусто или без циклов — предупреждение, цикл выключен |
| | | **resourceSystem** | ResourceSystem — из неё платится квота |
| Systems/PrestigeSystem | **PrestigeSystem** | **saveSystem** | SaveSystem (на `Bootstrap`) |
| | | formula | выплата: очки за эпоху + кривая по всему собранному; платится только то, что бьёт рекорды профиля. Выплата — при каре богов, в любой эпохе |
| | | upgrades | `Global Upgrade Catalogue Def` (GlobalUpgradeCatalogueDef) — мета-перки в порядке мета-экрана |
| Systems/AgeSystem | **AgeSystem** | **ages, resourceSystem** | список AgeDef-ассетов по порядку (начинается с Age II — у Age I дефа нет); ResourceSystem |
| Systems/AgeSequencer | AgeSequencer | islandSystem | Island |
| | | risePlayer | IslandRisePlayer на `IslandRise` (пусто = зона просто появляется, без подъёма) |
| | | cameraController | CameraController (камера едет к всплывающей зоне) |
| | | inputHandler | InputHandler на `Input` (тапы: ×3, пропуск, закрыть баннер; пусто = без тапов). Слушает `OnAnyClick` — клики и поверх UI, баннер сам UI |
| | | — (баннер) | своих полей нет: баннер (`Canvas/AgeTransition` + `BannerText`) назначен на `UIRoot`, бутстрап отдаёт его кодом (`AgeSequencer.Initialize`) |
| | | bannerFade, titleHold, bannerTapGrace | 0.5, 2, 0.4 (сек, дефолты): проявление/угасание баннера, сколько он висит, через сколько его можно закрыть тапом |
| IslandRise | **IslandRisePlayer** | **profile** | ассет `ScriptableObjects/Profiles/Island Rise Profile` — все ручки подъёма (тайминги, кривые, эффекты, звук, тряска) |
| | | **islandSystem** | Island |
| | | waterSystem | WaterSystem (опц.; без него тайлы в прыжке не давят на воду) |
| Systems/TapSystem | **TapSystem** | **inputHandler** | InputHandler |
| | | radiusVisual | TapRadiusVisual на этом же объекте (ширина, материал и цвет кольца — на его LineRenderer). Кольцо видно, пока игра идёт и рука пуста: с карточкой или Sell в руке клик — стройки, и кольцо прячется |
| | | tapRadius, boostSpeedMultiplier, boostDuration | 0.5, ×2, 5 с (дефолты). Радиус и множитель — БАЗА: перки растят их статами `TapRadius` / `TapBoostMultiplier` |
| | | refreshStamina | выкл (дефолт): тап только ускоряет; вкл — ещё и восполняет выносливость |
| | | tapClip | звук тапа (`Sound/click`); пусто = тапы без звука. AudioSource не нужен — TapSystem создаёт его сам |
| | | tapVolume, tapPitchJitter | 0.6 и ±0.05 (дефолты): громкость щелчка и случайный разброс высоты на каждый тап, 0 = всегда одинаково |
| Systems/PlacementController | **PlacementController** | **inputHandler, structureSystem, resourceSystem, islandSystem, mainCamera, gridOverlay** | соответствующие компоненты (gridOverlay — на `Island/GridOverlay`) |
| | | visuals → validColor, invalidColor, sellHoverColor | цвета госта/наведения: гост валид/невалид, красный при продаже (есть дефолты) |
| | | visuals → territoryValidColor, territoryInvalidColor, territorySortingLayer, territorySortingOrder | ореол занимаемой территории госта: зелёный/красный α0.18, слой Ground/1001 (дефолты) |
| Systems/HarvestVfxSystem | HarvestVfxSystem | viewCamera | Main Camera (опц. — пусто = `Camera.main` в `Awake`; нужна только чтобы не играть эффекты за экраном) |
| | | offscreenMargin | 0.1 (дефолт) — запас вокруг экрана в долях вьюпорта |
| | HarvestNumbers | **numberPrefab** | `Prefabs/FX/FX_HarvestNumber` (без него цифр нет, в консоли ошибка) |
| | | motion | `ScriptableObjects/HarvestNumberMotionDef/Harvest Number Motion` — вся подгонка ощущения: lifetime (0.9 с), travel (0, 0.8), кривые полёта/масштаба/альфы, spawnOffset, spawnJitter (0.15, 0.1) |
| | | viewCamera, offscreenMargin | как выше |
| | | maxConcurrent | 60 (дефолт) — потолок живых цифр; сверх него перерабатывается ближайшая к смерти |
| Systems/WaterSystem | **WaterSystem** | **waterPrefab** | `Prefabs/DontTouch/Water` (Modern 2D Water — весь вид моря в префабе) |
| | | coastTilemaps | `Ground`, `GroundTrim` — их контур становится берегом, о который бьётся вода. `Decor` и `Borders*` сюда НЕ добавлять: форму берега они не меняют |
| | | foamMinPixels, foamMaxPixels, foamPeriod, foamWholePixels | пена под южным берегом: толщина в арт-пикселях от/до, период пульса (сек), шаг целыми пикселями. В сцене 1 / 2 / 5 / выкл |
| | | sideFoamShader, sideFoamPixels | шейдер `Little Peeps/Sprite Flat Color` и ширина пены по восточному/западному краю (1 px). Пустой шейдер = без боковой пены |
| Input | **InputHandler** | **mainCamera** | Main Camera |
| Input | GameHotkeys | buildPanelKey, sellKey, exitToMenuKey | клавиши команд: B — спрятать / показать панель стройки, X — Sell, Esc (дефолты, правятся в инспекторе); цифры 1–9 — вкладки стройки (зашиты в коде) |
| CameraController | **CameraController** | **cameraTarget, vcam** | пустой `CameraTarget` (его и двигаем); CinemachineCamera — зум = её `Lens > Orthographic Size`, а значение, сохранённое в сцене, = **стартовый зум каждого забега** |
| | | **islandSystem, viewCamera** | Island (кламп по острову); viewCamera = Main Camera (только для перевода drag-пикселей в мир) |
| | | zoomSpeed, minZoom, maxZoom | шаг колеса (2) и пределы ortho size (3…12) — дефолты |
| | | panSpeed, edgePanEnabled, edgeThickness | скорость WASD/стрелок (12), вкл. край-скролл, толщина края в px (12) — дефолты |
| | | boundsMargin | насколько центр (= цель) может уйти за край острова (4) — дефолт |
| Main Camera | **CinemachineBrain** | Ignore Time Scale ✓ | пишет позицию из активной vcam — и при `timeScale = 0` |
| CinemachineCamera | **CinemachineCamera** (vcam) | Follow = CameraTarget; Body = CinemachineFollow (damping ~0.1–0.2) | плавно следует за целью |
| | CinemachineImpulseListener | Channel Mask 1 (дефолт) | без него толчки подъёма острова никто не почувствует; сам источник толчков IslandRise добавляет себе кодом |
| Island | **IslandSystem** | **tilemap, trimTilemap** | `Grid/Ground` (суша с коллайдером), `Grid/GroundTrim` (кайма берега, без коллайдера) |
| | | decorTilemap | `Grid/Decor` — декор из списков Decor в тайлсетах; пусто = без декора. Тайлмап должен быть свой (не Ground / GroundTrim / тайлмап границы) — иначе ошибка в консоли и декора нет. Слой сортировки Island, Order 1, материал `Materials/BendMaterial`, без коллайдера |
| | | **tileSet, biomes** | `TileSet/Base Tile Set` — тайлы для биома без своего тайлсета; биомы, из которых эпоха предлагает зоны (Meadow, Forest, Desert) |
| | | **borders** | мягкие границы между биомами, **СВЕРХУ ВНИЗ**: `Grass` → набор пуст → `Grid/BordersMeadow`; `Forest` → `TileSet/Forest Border Set` → `Grid/BordersForest`. Порядок строк = кто кого накрывает (биом без строки — в самом низу, это пустыня). Строку луга держать даже без арта, иначе лес сочтёт луг нижним и наползёт на него. **Слой и Order тайлмапов границ ставит код по этому списку** (слой сортировки — как у `Ground`, то есть Island; над Ground / Decor / GroundTrim), введённое в инспекторе не важно. Повтор биома, общий тайлмап у двух строк или тайлмап земли / каймы — ошибка в консоли, строка не рисуется (общий с декором — не рисуется декор). Тайлмапы границ: материал `Materials/BendMaterial`, как у Ground (иначе не гнутся при подъёме острова), без коллайдера |
| | | **structureSystem** | StructureSystem — через неё зона получает природное содержимое (деревья, реки, стартовый дом) |
| | | cellSize, previewSeed | 1; сид для редакторского превью «Generate Island» (контекстное меню). Забег берёт сид из StartConfig |
| Island/GridOverlay | **GridOverlay** | **islandSystem** | Island. Сетка — **пятно у курсора**, и только пока в руке карточка или Sell; весь остров она не покрывает |
| | | lineColor, lineWidth | в сцене синий (0.08, 0.41, 0.80) α0.64; 0.04 (дефолт — белый α0.6) |
| | | occupiedColor | заливка занятых клеток (территория структур), белый α0.12 (дефолт) |
| | | spotRadius, spotSolid, segmentsPerSide | радиус пятна в клетках (3), доля радиуса в полную силу — дальше тает к краю (0.35), отрезков на сторону клетки — круглее затухание (4) — дефолты |
| | | sortingLayerName, sortingOrder | в сцене **Mesh**, 1000 — сетка поверх всего, юнитов и построек тоже (дефолт в коде — Ground, 1000: поверх тайлов и лежащего на земле, но под юнитами) |
| ZonePreviewOverlay | ZonePreviewOverlay | **islandSystem** | Island |
| | | fillAlpha, outlineAlpha, outlineWidth | 0.55, 1, 0.08 (дефолты); цвет заливки — `previewColor` биома |
| | | sortingLayerName, sortingOrder | Ground, 1001 — поверх тайлов и всего, что лежит на земле |

### UI

Вся разводка UI живёт **внутри `Canvas.prefab`**. Наружу из префаба торчит ровно одна ссылка —
`GameBootstrap.uiRoot`. **Систем в инспекторе Канваса не назначают вовсе — их раздаёт код.** Бутстрап собирает
набор `UIServices` (`ResourceSystem`, `AgeSystem`, `OfferingSystem`, `RunManager`, `PlacementController`,
`CameraController`, `ZonePreviewOverlay`), а `UIRoot.Initialize` находит под Канвасом каждый `IUIElement` —
выключенные тоже — и отдаёт набор ему. Сейчас это `BuildPaletteUI`, `SellButtonUI`, `BuildPanelToggle`,
`ResourcePanel`, `ZoneSelectionUI`, `AgeAdvancePanel`, `AgeCostPanel`, `AgeTimelinePanel`, `TimePanel`, `OfferingTooltip`,
`PerkPanel`. Поэтому у экземпляра Канваса в сцене нет ни
одного оверрайда со ссылкой на объект сцены — и появляться их не должно.

Сам Canvas несёт две таблицы, и они отвечают на разные вопросы:

| компонент | вопрос | что перечисляет |
|---|---|---|
| **UIRoot** | чем код **снаружи** UI управляет сам | 4 экрана + баннер эпохи (см. ниже) |
| **UIVisibility** | кто **когда** виден | CanvasGroup'ы + маска режимов |

Элемент, которому не нужно ни то ни другое, не попадает ни в одну (`BuildToggle`), а видимый
по режиму — только во вторую (`BuildPalette`, `SellButton`, `BuildTabs`, `TimePanel`). `ConfirmDialog` и `HintView` нет в таблице видимости:
свою CanvasGroup каждый ведёт сам.

| Объект | Компонент | Поле | Что назначить |
|--------|-----------|------|---------------|
| Canvas | **UIRoot** | **zoneScreen, perkScreen, confirmDialog, metaScreen** | `Screens/ZoneScreen`, `Screens/PerkScreen`, `ConfirmDialog`, `MetaScreen` — их зовут гейплейные стейты |
| | | ageBanner, ageTitle | `AgeTransition` (CanvasGroup), `AgeTransition/BannerText` (TMP_Text) — баннер эпохи; бутстрап отдаёт их `AgeSequencer`. Пусто — предупреждение, эпохи сменяются без баннера |
| Canvas | **UIVisibility** | groups | 8 строк, таблица ниже |
| Canvas/Hud/BuildToggle | BuildPanelToggle | button, label, hideLabel, showLabel | свой Button; label — TMP_Text надписи (опц., **в префабе пусто**); «B» / «B» — надпись, пока панель видна / спрятана. Хозяин «показана / спрятана»: клик и B переключают, X и 1–9 возвращают спрятанную панель, прятание кладёт инструмент из руки |
| Canvas/Hud/BuildTabs | **BuildTabBar** | palette | `ScriptableObjects/BuildMode/Build Palette Def` — без него нет ни вкладок, ни карточек |
| | | tabPrefab, container | префаб `Prefabs/Elements/BuildTabUi`; container пусто = сам `BuildTabs` (его Vertical Layout Group) |
| Canvas/Hud/BuildPalette | **BuildPaletteUI** | cardPrefab, cardContainer | префаб `Prefabs/Elements/BuildCard`, дочерний `Viewport/CardRow` |
| | | scroller | BuildPanelScroller на `Viewport` |
| Canvas/Hud/SellButton | SellButtonUI | button, selectedFrame | свой Button, дочерний `SelectedFrame` |
| Canvas/Hud/ResourceBar | ResourcePanel | unitPrefab, container, iconSet | `Prefabs/Elements/ResourceUnit`, себя, ResourceIconSet |
| Canvas/Hud/PerkPanel | PerkPanel | slotPrefab | префаб `Prefabs/Elements/PerkSlot` (его PerkSlotUI). Сетка на том же объекте: Cell 20×20, Spacing 1, Fixed Column Count 2, **Start Corner и Child Alignment — Upper Right** (одного Start Corner мало: пока перков меньше двух, сетка сужается до одной колонки и прижимает её по Child Alignment — одиночный перк встал бы влево); Content Size Fitter — Vertical Fit Preferred |
| Canvas/Hud/TimePanel | **TimePanel** | now, belt, riders, hands | `Now`, `Track/Belt`, `Track/Riders`, `Offering/Icon`. Руки ждут там, где стоят в префабе, — по центру большого ромба: с этого столбца стартует маленький ромб, и руки уезжают с ним без рывка |
| | | lineSprite, yearSprite, offeringSprite | `time_line`, `time_icon_year`, `time_icon_halfdecade`. Засечку подношения скрипт режет на три части при старте — её спрайт нельзя паковать в атлас плотно (Tight), только прямоугольником |
| | | segmentWidth, diamondWidth | 58 — длина отрезка-«года» в пикселях (скорость ленты = 58 px за Segment Seconds расписания); 5 — ширина ромбика в середине засечки подношения |
| | | yearTemplate, tooltip | `Years/Year` (шаблон цифры, его якоря и выравнивание скрипт не читает — центрирует сам), `Tooltip` |
| Canvas/Hud/TimePanel/Tooltip | OfferingTooltip | header, costContainer, anchor | `Header`, `Costs` (её Grid Layout Group: Cell 34×13, Spacing 2×3, Fixed Column Count 2, Child Alignment Upper Center), `Offering/Icon` — куда смотрит хвостик |
| | | costPrefab, iconSet, shortColor, highlight | `Prefabs/Elements/ResourceUnit`, ResourceIconSet, #9B211E — ресурса не хватает, #FFD663 — слово Offering |
| | | offset | (−2, −8): правый верхний угол подсказки (с хвостиком) от правого нижнего угла рук. Vertical Layout Group на корне: Padding 5 / 6 / 8 / 3, Spacing 3, Control Child Size — ширина и высота, Force Expand — ширина; **Content Size Fitter не вешать** — размер считает скрипт |
| Canvas/Hud/AgePanel | AgeAdvancePanel | ageLabel, nextAgeButton | TMP_Text, `Advance/AdvanceButton` |
| Canvas/Hud/AgePanel/Advance/Cost | AgeCostPanel | unitPrefab, container, iconSet | ResourceUnit, себя (Grid Layout Group), ResourceIconSet |
| | | unaffordableColor, visibilityRoot | цвет суммы, которую не хватает; что гасить после последней эпохи, когда покупать больше нечего |
| Canvas/Hud/AgePanel/Timeline | AgeTimelinePanel | cardPrefab, container | префаб `Prefabs/Elements/AgeCard`, `Viewport/Content` |
| Canvas/Screens/PerkScreen | PerkSelectionUI | cardPrefab, cardContainer | префаб `Prefabs/Elements/PerkCardUI`, `Panel/CardColumn` |
| Canvas/Screens/ZoneScreen | ZoneSelectionUI | cardPrefab, cardContainer | префаб `Prefabs/Elements/ZoneCardUI`, `Panel/CardRow` |
| | | focusOffsetY | 0.15 (дефолт) — насколько выше центра экрана встаёт зона под карточкой |
| Canvas/HintView | HintView | titleText, bodyText, costContainer, footerText | свои дети; секция с пустым содержимым прячется сама. Встаёт над элементом или слева от него — как попросит источник (`HintShowEvent.Side`; карточки стройки — сверху, ячейки перков — слева) |
| | | costPrefab, iconSet, unaffordableColor | ResourceUnit, ResourceIconSet, красный — цена, которую не потянуть |
| | | maxWidth, gap, screenMargin, showDelay, chainWindow | 160, 4, 2, 0.25 с, 0.3 с (дефолты): ширина до переноса, отступ от элемента и от края экрана, задержка появления, окно «перехода» между соседними элементами без задержки |
| Canvas/MetaScreen | **MetaUpgradesUI** | rowPrefab, rowContainer | префаб `Prefabs/Elements/MetaUpgradeRow`, `Scroll View/Viewport/Content` |
| | | pointsText, pointsFormat | `PointsText`, `Prestige points: {0}` |
| | | **playButton** | `PlayButton` — следующий забег |
| Canvas/ConfirmDialog | **ConfirmDialog** | messageText, yesButton, noButton (+ yesLabel, noLabel опц.) | разведены внутри префаба `ConfirmDialog`; текст кнопок в префабе — по умолчанию. Сейчас это **окно конца цикла**: Yes = «Offer», No = «Accept wrath» (надписи задаёт код). Yes бывает серой (ресурсов не хватает) — у кнопки Yes должен быть вид «отключена» (Disabled Color или спрайт) |

#### Новый элемент Канваса, которому нужна система

1. Компонент реализует `IUIElement` — `public void Initialize(UIServices ui)` — и берёт из `ui` только своё.
2. Кладём его куда угодно под `Canvas`. Ни поля на `UIRoot`, ни перетаскивания, ни оверрайда в сцене.
3. `Initialize` приходит из `GameBootstrap.Awake` и может опередить собственный `Awake` элемента: там только
   запомнить, а рисовать — начиная со `Start`.
4. Нужной системы в наборе нет — одно поле в `UIServices` (названное по типу), одна строка там, где его заполняет
   `GameBootstrap`, и одна в `UIServices.WarnMissing`. Если у бутстрапа нет и самой ссылки — ещё поле на `GameBootstrap`.

Заспавненное в рантайме (карточки, юниты ресурсов) `IUIElement` не реализует: нужное отдаёт тот, кто его создал.
Поле на `UIRoot` нужно в обратном случае — когда игра сама управляет элементом снаружи (экран стейта, баннер).

#### Таблица UIVisibility

Режимы объявляют сами гейплейные стейты (`UIModeChangedEvent` в их `Enter`), так что источник
правды — стек FSM. Маска `[Flags]`, группы **вкладываются**: альфа перемножается, а негодный к
взаимодействию родитель гасит детей. Поэтому строка нужна только там, где группа отличается от
родительской.

Кроме режима у строки есть одна галочка — **Follows Build Panel**: такая группа видна, только если её
режим говорит «да» **и** игрок не спрятал панель стройки (кнопка `BuildToggle` или B). Это всё ещё строка
таблицы, так что у группы один хозяин — кнопка ничего не пишет поверх того, что выставил режим.

| # | Group | Playing | ZonePick | PerkPick | AgeTransition | MetaUpgrades | OfferingEnd | За панелью |
|---|-------|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| 0 | `Hud` | ✓ | — | — | — | — | — | — |
| 1 | `Screens/PerkScreen` | — | — | ✓ | — | — | — | — |
| 2 | `Screens/ZoneScreen` | — | ✓ | — | — | — | — | — |
| 3 | `MetaScreen` | — | — | — | — | ✓ | — | — |
| 4 | `Hud/BuildPalette` | ✓ | — | — | — | — | — | ✓ |
| 5 | `Hud/SellButton` | ✓ | — | — | — | — | — | ✓ |
| 6 | `Hud/BuildTabs` | ✓ | — | — | — | — | — | ✓ |
| 7 | `Hud/TimePanel` | ✓ | — | ✓ | — | — | ✓ | — |

Строка 0 читается как «только живая игра»: на выборе зоны и перка, подъёме острова, мета-экране и в окне конца
цикла HUD скрыт целиком, панель эры и панель перков вместе с ним (своих строк у них нет). Поэтому смена цены и
высоты блока эры игроку не видна: HUD возвращается уже с новыми ценами. Строки 4–6 — три виджета стройки всю
живую игру, пока игрок их не спрятал. Строка 7 — лента времени: у её CanvasGroup включён **Ignore Parent Groups**,
поэтому она не зависит от `Hud` и видна ещё на выборе перка и в окне конца цикла — ромб подношения стоит в кружке
прямо под окном. **В окне конца цикла запасов не видно** — если их нужно видеть, отметить `OfferingEnd` у `Hud`.
Биты бывших режимов `Build` (`1 << 1`) и `PrestigeConfirm` (`1 << 5`) в коде оставлены пустыми, поэтому
маски, сохранённые с ними, читаются как раньше.

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
| **Pier** | — | маркер без полей (см. «Пирс») |

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

**Река** (`Prefabs/Nodes/River`) — одна клетка русла, ставит только генератор (у дефа `isWater` вкл,
`canSell` выкл). Кусок и поворот выбирает код по соседним клеткам реки:
```
Root          → Rigidbody2D (Static) + Structure + RiverVisual
├── Visual    (Y 0.5 — центр клетки: модель крутится вокруг своей позиции)
│   └── Model → SpriteRenderer (Sorting Layer Default — над всеми тайлмапами земли, под всем, что стоит)
│               + SpriteShadow с выключенным Cast Shadow
└── Physics   → BoxCollider2D 1×1, Offset Y 0.5 (не триггер — юниты отскакивают)
```
| Поле `RiverVisual` | Что назначить |
|---|---|
| model | `Visual/Model` (его SpriteRenderer) |
| end | `river_tile_end` — конец, нарисован **открытым на запад** (скругление смотрит на восток) |
| straight | `river_tile_main` — середина, нарисована **запад–восток** |
| corner | `river_tile_corner` — угол, нарисован **соединяющим восток и юг** |
- Спрайты — 16×16, PPU 16, **пивот по центру**: поворот на 90° ложится пиксель в пиксель. Новый арт рисовать
  в тех же ориентациях — иначе повороты перепутаются.
- Спрайт в самом `Model` — любой из трёх, в игре его заменит `RiverVisual`.
- Тень на реке не нужна: тень — ребёнок модели и крутилась бы вместе с куском.

### Звери (Boar, Fox, Alpaka — подвижные ресурсные ноды)
Зверь — **не юнит и не Structure**: у него нет дефа постройки, клетки на гриде и здоровья.
```
Root          → Rigidbody2D (KINEMATIC, Gravity Scale 0) + CollisionTarget + ResourceSource + Animal + AnimalWander
├── VisualRoot
│   └── Model → SpriteRenderer (Sort Point = Pivot, пивот у ног) + SpriteShadow
└── Physics   → Collider2D (Is Trigger = выкл — юнит отскакивает, и этот отскок = «удар»-сбор)
```
- **Body Type строго Kinematic:** юнит (Dynamic) отскакивает от зверя, а сам зверь физикой не останавливается ничем.
  Куда ему можно, решает код: точки назначения (их выдаёт `AnimalSpawner`: клетки суши в радиусе территории) и
  щуп на шаг вперёд — край острова, постройки с `animalsAvoid` и стройплощадки.
- **Сбор — обычный `ResourceSource`**, как у дерева: деф зверя задаёт, кто собирает (workerYields), сколько ударов
  (hitsToDeplete) и что потом — `Despawn` (зверь исчезает, нора выпустит замену через spawnCooldown) или `Regrow`
  (альпака стоит стриженая, пока не отрастёт шерсть). `Animal` полей не имеет — это только связь с норой.
- Поля `AnimalWander`: moveSpeed (1), pauseMin/pauseMax (0.5 / 2 с — шаг→пауза→шаг), fallbackRadius (2 — радиус
  блуждания без норы), visual (что отражать по направлению), personalSpace (0.6), encounterImmunity (1.5 с), lookAhead (0.3).
- Нора / конюшня = обычная Structure + `AnimalSpawner` (см. таблицу выше). Звери выходят при постройке норы и живут
  до конца забега; продали нору — её звери исчезают с «пуфом». На клетки стройплощадки зверь не заходит, а
  стоящий на них уходит сам; площадка в клетках ждёт, пока зверей на ней не останется (площадка забора не ждёт).

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
- **Sell:** клик «выигрывает» забор, когда курсор ближе 0.3 клетки к линии ребра (иначе берётся
  структура в клетке под курсором).

### BuildCard (префаб карточки, инстанцирует BuildPaletteUI в рантайме — при первом открытии её вкладки)
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

### BuildTabUi (префаб вкладки, инстанцирует BuildTabBar в рантайме — по кнопке на непустую вкладку)
```
Root → Button + BuildTabUI + Image (прозрачная)   (79×18; область клика — стоит на месте, корень двигает Layout Group)
└── Visual          → Image (build_tab, Sliced)   (растянут на корень; едет вправо, пока вкладка под курсором или открыта)
    ├── Icon        → Image                 (8×8 в (7, 4) от левого верхнего угла, Preserve Aspect; без спрайта прячется)
    └── Label       → TMP_Text              (m6x11plus 12, #6F6D60, Baseline — базовая линия на 12 px ниже верха;
                                             от x 17 до 1 px от правого края, без переноса)
```
Лучи ловит только корень: у `Visual`, `Icon` и `Label` Raycast Target выключен, поэтому наведение читается с
неподвижного слота, и едущая картинка не уходит из-под курсора. Высота вкладки 18, а шаг в колонке 15:
`BuildTabs` ставит их со Spacing −3, нижняя полоса вкладки уходит под следующую, как на макете.

Поля `BuildTabUI`: button, label, iconImage, selectedIndicator (необязателен — класть внутрь `Visual`),
visual, slideOffset (4 px), slideDuration (0.1 с), slideEase (OutQuad). Подпись и иконка необязательны:
что назначено, то и показывается.

### ResourceUnit (иконка + число; инстанцируют ResourcePanel, AgeCostPanel и HintView в рантайме)
```
Root          → ResourceUnit + LayoutElement (Preferred 34×13)   (своей раскладки нет — дети на якорях)
├── Backing   → Image (base_numbers, Sliced)   (подложка под числом: от x=6 до правого края, высота 8,
│   │                                           по центру иконки; левый край уходит ПОД иконку)
│   └── Label → TMP_Text                       (справа от иконки до конца подложки, по центру, без переноса)
└── Icon      → Image 13×13                    (последним: рисуется поверх подложки)
```
Поля `ResourceUnit`: icon → `Icon`, label → `Label`. Порядок детей значим: подложка рисуется раньше иконки.
Размер ячейки задаёт родитель: Grid Layout Group у ResourceBar и Cost (Cell Size 34×13) берёт свой Cell Size, а
Layout Group, которая управляет размером детей (CostContainer в HintView), берёт Preferred из LayoutElement —
без него ячейка там схлопнется в 0×0. Один префаб и для запасов, и для цены: цифры читаются одинаково.

### PerkSlot (ячейка взятого перка; инстанцирует PerkPanel в рантайме)
```
Root      → Image (perk_place, Simple, 20×20, Raycast ВКЛ — ловит наведение) + PerkSlotUI
├── Icon  → Image 16×16 по центру, Preserve Aspect   (компонент выключен: Image без спрайта рисует белый квадрат,
│                                                      включает PerkSlotUI, когда у перка есть иконка)
└── Label → TMP_Text, Micro5 11, Right + Baseline    (цифра в правом нижнем углу, базовая линия на рамке;
                                                      пока всегда «1» из префаба — функции у неё ещё нет)
```
Поля `PerkSlotUI`: icon → `Icon`. Иконка — та же `PerkDef.icon`, что на карточке выбора (40×40, ужимается в 16×16).
Micro5 размера 11 — пиксель шрифта на пиксель Канваса: цифры 3×5, как на макете; цвет — как у чисел ресурсов.
Label стоит после Icon — цифра рисуется поверх картинки. Подсказка (название + описание) — слева от ячейки.

### Пирс (Pier)
Пирс — обычная **Structure**, но ставит его не игрок, а `IslandSystem`: на каждом забеге из `StartConfig.pier`,
на восточный берег стартовой зоны, и дальше он стоит на месте. Морской проход от него до горизонта
генератор зон держит свободным.
- **Префаб** (`Prefabs/Buildings/Pier`) = Rigidbody2D (Static) + `Structure` + `Pier` + Visual/Model + Physics (BoxCollider2D)
  + пустышка `ButtonAnchor`. `Pier` — пустой маркер: его сейчас никто не читает, оставлен под то, чем пирс станет
  дальше (лодки, своя механика). `ButtonAnchor` — точка, над которой висела кнопка престижа; теперь не используется,
  можно удалить или оставить под будущую кнопку.
- **StructureDef Пирса:** `placement = Cell`, `border = 0` (иначе потребует клетки за краем острова), `size` 1×3,
  `cost` пустой, `canSell` выкл. В палитру стройки не кладётся.
- **Забег пирс больше не заканчивает:** кнопки престижа нет, забег кончается только карой богов в конце цикла
  подношений.

## ScriptableObject-ассеты

Лежат в `Assets/Little Peeps/ScriptableObjects/` по папкам. Меню создания — **Create → LittlePeeps → …**

| Ассет | Меню создания | Главное содержимое |
|-------|---------------|--------------------|
| **StartConfig** | LittlePeeps/StartConfig | islandSeed (0 = случайный), islandRules (правила генерации), startBiome, house, pier, startingResources, startingModifiers. Один на игру — в `RunManager.startConfig` |
| **StructureDef** | LittlePeeps/StructureDef | id, displayName, description, icon, prefab, **placement (Cell = футпринт клеток / Edge = забор на ребре)**, size + **footprintMask** (форма внутри size — рисуется клетками в инспекторе), cost[], requiredAge, lockedUntilPerk (карточка заперта, пока её не откроет перк), allowedTerrain[] (пусто = любой биом), sellRefundPercent (доля возврата при продаже, 0.5), canSell, passable / animalsAvoid, isWater (река: по нему клетки реки стыкуются, а мельница ищет берег), needsWaterSide (мельница), border (расширяет занимаемую территорию: дом=1 → 2×2 занимает 4×4), **maxCount** (0 = без лимита; растёт статом `StructureLimit`), costStepFlat / costStepPercent / exponentialCostGrowth (рост цены за каждую стоящую) |
| **ResourceSourceDef** | LittlePeeps/ResourceSourceDef | resource, workerYields[] (кто и сколько добывает; пусто = никто; «любой рабочий» = перечислить всех), **depletion** (Never / Despawn / Regrow), hitsToDeplete, regrowTime, keepBodyWhileDepleted, **pickupFx** (префаб FX_Pickup_*; пусто = только цифра) + **pickupFxCount**. `pickupFx` — единственный визуал в дефе, и намеренно: он отвечает на вопрос «что именно добыли», а на него нельзя ответить типом ресурса. Один тип дефа для статичных нод и зверей |
| **UnitDef** | LittlePeeps/UnitDef | id, prefab (→ BaseUnit), speed, profession (с какой профессией рождается — `Unassigned`, настоящую даёт стойка) |
| **ProfessionDef** | LittlePeeps/ProfessionDef | type (UnitType), frames[] — кадры ходьбы этой профессии |
| **BiomeDef** | LittlePeeps/BiomeDef | profile (параметры генерации зоны), tileSet, displayName, icon, previewColor (заливка зоны при выборе) |
| **IslandTileSet** | LittlePeeps/IslandTileSet | тайлы земли (светлые/тёмные клетки), углы, края, внутренние углы; **decorLight / decorDark** (декор для светлых / тёмных клеток) и decorChance (доля клеток с декором, 0.3) |
| **TerrainBorderSet** | LittlePeeps/TerrainBorderSet | граница одного биома, кладётся тайлмапом из строки `IslandSystem.borders`. **edgeAllRound** — край по всему краю биома (и к биомам выше, и к воде), а не только к тем, что он накрывает; **overSea** — кайма и на клетки воды вдоль берега. На биом выше кайма не ложится никогда — это его граница. 8 слотов **Over** — кайма на клетке нижнего биома (4 стороны + 4 вогнутых угла); 8 слотов **Edge** — край на своей клетке. В подсказке слота — имя файла художника. Спрайты: **Pivot = Center**, иначе граница съезжает (с Bottom Center — на полклетки вверх). В проекте: `TileSet/Forest Border Set` (обе галочки включены), тайлы в `Tiles/BordersForest` |
| **BuildPaletteDef** | LittlePeeps/BuildPalette | tabs: вкладки нижней панели, у каждой name, icon, structures (список StructureDef). Порядок вкладок = порядок кнопок и цифр 1–9, порядок structures = порядок карточек; пустая вкладка кнопку не получает. Группировка живёт здесь, на StructureDef категории нет |
| **AgeDef** | LittlePeeps/AgeDef | title (только в лог — на баннере номер римскими цифрами), resourceCost[] (цена), modifiers[] (StatModifier — бонусы эпохи), bonusOverride (текст вместо сгенерированного описания). Порядок задаётся списком `AgeSystem.ages`, начиная с Age II |
| **Перки** | LittlePeeps/Perks/Stat Perk, …/Unlock Structure Perk | общее: id, title, description, icon, **grade** (Common — обычные подношения и стартовый выбор; Special — только подношения особых циклов; наборы не смешиваются, а если особых не осталось, особый цикл даёт обычные), weight (шанс выпасть), minAge (с какой эпохи предлагается), requires (предыдущий уровень — цепочки уровней). Stat Perk — modifiers[]; Unlock Structure Perk — structure (снимает `lockedUntilPerk`). Особые сейчас: Good rest, Bigger Houses, Unl Rice Paddy |
| **Offering Schedule** | LittlePeeps/Offering Schedule | цикл подношений, для `OfferingSystem.schedule`: segmentSeconds (длина одного отрезка-«года» в секундах игры, одна на весь забег — лента всегда едет с одной скоростью; пауза не идёт), cycles[] (по порядку, первый элемент = цикл 1: segments — сколько отрезков длится цикл, не меньше одного; resources — квота; ресурс эпохой не проверяется — не просить его раньше, чем обычный игрок до неё дойдёт), growthPastEnd (после списка повторяется последний цикл: квота ×рост за каждый лишний цикл, округление вверх, длина — как есть), specialEvery (каждый N-й цикл особый, 0 = нет). В проекте `OfferingDef/Offering Schedule Def`, **тестовые** значения: 10 с × 6 отрезков = 60 с на цикл, дерево 10 → дерево 20 → еда 10, ×1.25, каждый 3-й; настоящие числа — за Даниилом |
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
  - `OnWorldClick` — ЛКМ, **только если курсор НЕ над UI**. Слушают оба, а забирает один: с карточкой или Sell в руке — `PlacementController` (ставит / продаёт), с пустой рукой — `TapSystem` (буст юнитов + щелчок; он сам спрашивает `PlacementController.HoldsTool`);
  - `OnAnyClick` — любая ЛКМ, и над UI тоже. Слушает `AgeSequencer`: тап ускоряет подъём острова и закрывает баннер эпохи, а баннер сам UI;
  - `OnRightClick` — любая ПКМ: положить инструмент из руки, над панелью тоже.

  Камеру он не трогает.
- **`CameraController`** (на своём GO, НЕ на CameraTarget и НЕ на камере) — двигает transform пустого **CameraTarget** = логическую цель камеры; поллит ввод сам, считает на `unscaledDeltaTime` → работает и на паузах (`timeScale=0`: выбор зоны и перка, подъём острова). Источники: WASD + стрелки, курсор у края экрана, **drag средней кнопкой мыши** (мир тащится под курсором), **колесо — зум** (ortho size vcam в пределах `minZoom…maxZoom`). Цель клампится по `IslandGrid.WorldBounds()` + `boundsMargin`; границы перечитываются на `AgeStartedEvent` (рост острова) и `RunStartedEvent`. Плавность даёт не он, а **Cinemachine vcam**, которая следует за целью с damping; `Main Camera` несёт `CinemachineBrain` и рендерит. `viewCamera` (= Main Camera) нужен скрипту только чтобы перевести drag-пиксели в мир (его `orthographicSize` крутит brain → отражает живой зум). `Confiner2D` — отдельной итерацией позже.
  - **Старт забега** (`Start` и каждый `RunStartedEvent`): камера мгновенно, без damping, встаёт в центр острова с зумом из сцены (`Lens > Orthographic Size` у vcam) — новый забег всегда начинается с одного вида, что бы ни крутили до этого.
- **`GameHotkeys`** (на `Input`) — дискретные команды по нажатию, публикует события в `EventBus`, ни во что не лезет напрямую. UI его не фильтрует: клавиатура не целится в то, что под курсором, поэтому подписчики сами проверяют состояние:
  - **B** → `BuildPanelToggleRequestedEvent` → `BuildPanelToggle` (тот же переключатель, что клик по `BuildToggle`) прячет или показывает `SellButton`, `BuildTabs` и `BuildPalette`. Не режим: игра идёт и так и так; прятание кладёт инструмент из руки;
  - **X** → `SellModeRequestedEvent` → `SellButtonUI` берёт Sell в руку или кладёт — тем же путём, что клик (подсветку рисует по факту, из `PlacementController.ToolChanged`); `BuildPanelToggle` показывает спрятанную панель. Поверх другого экрана (выбор перка, подъём острова…) — no-op: вне живой игры контроллер инструмент не даёт, а панель не показывается;
  - **1–9** (верхний ряд и нампад) → `BuildTabRequestedEvent` → `BuildTabBar` открывает N-ю вкладку, `BuildPanelToggle` показывает спрятанную панель; поверх другого экрана — no-op. Клавиши зашиты в коде, не в инспекторе: цифра = позиция вкладки;
  - **Esc** → `ExitToMenuRequestedEvent` → `GameBootstrap` пока **отклоняет** переход (warning в консоли): у `MainMenuState` нет экрана, и выход в него оставил бы игрока без пути назад. Вернуть — одна строка, когда меню появится.

**Мышь над UI принадлежит UI.** Колесо и начало drag средней кнопкой, как и `OnWorldClick`, ничего не делают, пока курсор над UI (`InputHandler.IsPointerOverUI()` — ответ EventSystem). Начатый на острове drag может пройти и над UI. WASD и край экрана не фильтруются: они ни во что под курсором не целятся.

> ⚠️ «Над UI» = над **любой** графикой с включённым **Raycast Target**, даже прозрачной. Невидимая подложка на весь экран отрезает от игры колесо, тапы и drag. Если окно должно оставлять остров доступным, у его подложки Raycast Target выключен — так сделано у `Screens/ZoneScreen/Panel`, иначе при выборе зоны не работает зум. У модальных окон (затемнение `PerkScreen/Panel`, `MetaScreen`) подложка перехватывает ввод намеренно.

> ⚠️ `CameraController` использует `viewCamera.orthographicSize` (drag) — **камера должна быть Orthographic** (2D-проект, так и есть). линзу vcam тоже держать Orthographic.

## Стройка — как связано

**Режима стройки нет** (с 2026-10-15): игра не встаёт на паузу, юниты не исчезают, переноса построек нет.
Панель стройки открыта всю живую игру, остров работает под ней.

- **Когда работает:** `PlayingState.Enter` → `PlacementController.Begin`, `Exit` → `End`. Любой другой экран (выбор
  зоны или перка, подъём острова, вопрос о престиже, мета-экран) начинается с того, что инструмент из руки кладётся.
- **Что в руке — знает только `PlacementController`.** Виджеты панели лишь просят (`Select` / `SelectSell` /
  `PutDown`) и рисуют подсветку по его событию `ToolChanged`, своих копий не держат; вне живой игры просьбы отклоняются:
  - карточка → **PLACE** (гост под курсором, клик ставит; инструмент остаётся в руке, можно ставить дальше),
  - кнопка Sell → **SELL** (наведение красит постройку, клик продаёт с возвратом `sellRefundPercent` от цены предыдущей копии — при росте цены возвращается то, что за эту копию заплатили),
  - ничего не выбрано → **пустая рука**: клик — это тап (`TapSystem`).
  - ПКМ, повторный клик по выбранной карточке / Sell, спрятанная панель или карточка, упёршаяся в лимит, — назад к пустой руке.
- **Сетка** (`GridOverlay`) — пятно вокруг курсора, только пока в руке карточка или Sell.
- **Постановка = стройплощадка.** Оплаченная постройка не встаёт на голову тем, кто там стоит: сначала появляется
  площадка. Префаба у неё **нет** — `ConstructionSite` собирает её кодом (объект `Site (<имя дефа>)`):
  - стена по клеткам футпринта (Rigidbody2D Static + BoxCollider2D на клетку → один CompositeCollider2D), у забора —
    полоса вдоль ребра шириной `siteLook.fenceThickness`;
  - **выпускает, но не впускает:** юниты, стоявшие на месте в момент постановки, проходят сквозь стену, пока каждый не
    выйдет целиком; остальные отскакивают, как от здания. Удар о площадку ничего не платит;
  - звери на клетки площадки не заходят, стоящие уходят сами; площадка в клетках ждёт и их (забора это не касается);
  - вид: полупрозрачный чертёж здания (его спрайты, умноженные на `siteLook.blueprintColor`) над подложкой
    `siteLook.groundColor` — настраивается на `StructureSystem` (см. checklist);
  - как только внутри никого — на том же месте встаёт настоящий префаб.

  Площадка уже занимает клетки, считается в лимите и цене, а продаётся как готовое здание. Генерируемое
  (содержимое зон, стартовый дом, пирс) встаёт сразу, без площадки. **Отложено:** стройка ударами юнитов снаружи,
  арт лесов, звук стука.
- **Продажа идёт в живой игре:** таверна выпускает гостей пьяными там, где стояла; звери проданной норы исчезают с
  пуфом; лишние жители проданного дома (сколько не помещается в оставшиеся дома) исчезают с пуфом, ближайшие к нему
  первыми. Эффект и звук пуфа — поля `SpawnSystem` (`puffFx`, `puffSound`), в сцене пока пусто.
- **Кнопка `BuildToggle` / клавиша B** только прячут и показывают `SellButton`, `BuildTabs` и `BuildPalette`
  (`BuildPanelToggle` хранит это между экранами и забегами); клавиши X и 1–9 спрятанную панель показывают.
- **`EventSystem` обязателен** — клики над UI до `PlacementController` и `TapSystem` не доходят (`InputHandler` не пускает их в `OnWorldClick`, спрашивая EventSystem), а uGUI-кнопки без него не нажимаются. ПКМ работает и над панелью.

## Порядок выполнения скриптов — НЕ трогаем

Script Execution Order настраивать **не нужно** (это костыль). Инициализация независима от порядка by design:

- Unity гарантирует, что **все `Awake` отработают раньше любого `Start`**.
- `GameBootstrap` делает всю проводку и старт рана в своём `Awake`. К моменту любого `Start` всё уже связано, а остров сгенерирован — какой объект инициализируется первым, не важно.
- **Правило для новых систем:** не читай «вколотое» состояние (контексты, другие системы) в своих `Awake`/`OnEnable` — только начиная со `Start`. Подписки на события и `GetComponent` в `Awake`/`OnEnable` — можно.
- **То же для элементов Канваса (`IUIElement`):** `Initialize(UIServices)` вызывается из `GameBootstrap.Awake` и может прийти раньше собственного `Awake` элемента — в нём только запомнить нужное, рисовать со `Start`. Поэтому карточки появляются не раньше `Start` (первую вкладку объявляет `BuildTabBar.Start`).
- **События, отправленные внутри `GameBootstrap.Awake`, элементы Канваса могут не услышать** — их `OnEnable` ещё не
  отработал. Так бывает с первым `RunStartedEvent` и первым `UIModeChangedEvent` (`Playing`). Поэтому текущий забег
  читают в `Initialize` (`RunManager.CurrentRun`), а виджеты стройки стартуют так, будто режим уже `Playing` —
  тем же правилом, что `UIVisibility` в своём `Awake`. Пришло событие — ничего не меняет, пропущено — состояние уже верное.

## Что должно произойти при Play

1. `GameBootstrap.Awake`: загрузка MetaContext (с диска пока не читается — каждый запуск с чистого профиля), проводка систем
   и UI (набор `UIServices` → каждому `IUIElement` под Канвасом, баннер → `AgeSequencer`);
   `RunManager.StartNewRun()` → RunContext, стартовые модификаторы и ресурсы из StartConfig плюс купленные мета-перки,
   `IslandSystem.GenerateForRun` → стартовая зона с домом и пирсом; мета-перк Start Age сразу пропускает эпохи (остров
   растёт без анимации). App FSM → `Boot → GameplayContainer → Playing`. Камера — в центре острова, музыка — с первого кадра.
2. На экране: юниты вылетают из домов и отскакивают; `Unassigned` берут инструмент на стойке и работают профессией;
   удар по ноде платит по её `workerYields`; вокруг нор гуляют звери; уставшие возвращаются в дом. Тап — буст юнитов в
   радиусе со щелчком.
3. Если мета-перк дал стартовый выбор перка — он открывается сразу.
4. Панель стройки внизу открыта всё время, игра под ней идёт: карточка в руке → сетка пятном у курсора и гост,
   клик ставит стройплощадку, которая становится зданием, когда с места все уйдут; Sell продаёт; ПКМ — пустая рука,
   клик снова тап. B прячет / показывает панель.
5. **Next Age** (`AgeAdvancePanel`) активна, когда хватает ресурсов: клик → выбор зоны → зона поднимается из моря →
   баннер «Age N» → снова игра (перка за эпоху нет). Тап ускоряет подъём, второй — к финалу, тап по баннеру закрывает его.
6. **Цикл подношений** идёт с первой секунды: лента сверху ползёт к кружку, под засечками годы с начала забега (1, 2,
   3, 4 …); на любом экране поверх игры часы и лента стоят. Когда до подношения остаётся ≈4,6 года, в большом ромбе
   справа появляется маленький и выезжает, руки — вместе с ним. Курсор на ленте → подсказка «N years until Offering»
   и квота (красным — чего не хватает).
7. Ромб дошёл до кружка → пауза и окно «Cycle N is over…»: **Offer** (серая, если не хватает) → выбор перка (в особом
   цикле — особые перки) и следующий цикл; **Accept wrath** → очки престижа → мета-экран (тратим очки на мета-перки) →
   **Play** → новый забег с цикла 1 и года 1; камера снова в центре острова.

## Project settings — важное (не в сцене, но влияет)

- **Physics 2D → Layer Collision Matrix:** Unit×Unit **OFF** (юниты проходят сквозь друг друга), Unit×Default **ON**.
- **Physics 2D → Bounce Threshold = 0.1** (иначе скольжение на малых скоростях).
- **Y-сортировка:** на URP 2D Renderer-ассете (`Assets/Settings/Renderer2D.asset`) — Transparency Sort Mode = Custom Axis, Axis (0,1,0). Юниты и постройки — на одном Sorting Layer (`Entities`) с равным Order, сортируются по Y динамически.
- **Sorting Layers** снизу вверх: Water, Island, Ground, Default, Entities, Overlay, Mesh. **Island** — все тайлмапы острова (земля, кайма, декор, границы); **Ground** — то, что лежит на земле (рисовое поле, пни, стерня, щебень, пустые кусты), а ещё превью зоны, подсветка территории госта (Order 1001) и подложка стройплощадки (Order 1000). Сетка стройки — на Mesh, поверх всего. Так лежащее на земле всегда над тайлами, какой бы Order ни стоял у тех и других.
- **Global Light 2D → Target Sorting Layers:** должны быть отмечены все слои, на которых что-то рисуется, Island в том числе. Новый слой сортировки сам туда может не попасть — тогда всё на нём чёрное.

## Бонусы (RunStats) — коротко

Прибавки к параметрам (`ProductionGlobal`, `ResourceYield`, `UnitSpeed`, `HouseCapacity`, `TapRadius`, …) задаются списками
`StatModifier`: в `AgeDef.modifiers`, `StartConfig.startingModifiers`, в Stat-перках и в Stat-мета-перках. Слой живёт в
`RunContext.stats` и сбрасывается на престиже. База — в конфигах и компонентах (`UnitDef.speed`, `ResourceSourceDef.workerYields`,
`Spawner.capacity` …), бонус — отдельным слоем. Подробности для дизайнера: **BONUS_SYSTEM_GUIDE.md**.

## Что ещё заглушка

- **Главное меню:** `MainMenuState` без экрана, Esc переход отклоняет.
- **Сохранение на диск:** `SaveSystem.Save/Load` — TODO. Очки престижа и мета-перки живут до закрытия игры.
