# Little Peeps — организация систем в сцене

Как устроен старт и как разложить объекты в `SampleScene`, чтобы сцена была «по полочкам».
Документ держим синхронным с кодом — обновляем при добавлении/переименовании систем и полей.

> Последняя сверка с кодом: **2026-06-19** (после Phase 4 build mode: sell + move).

## Принцип

- **Один сцена-проект.** Системы — это синглтоны-объекты в сцене (подход A). Не создаём их кодом.
- **`GameBootstrap` — единственная точка регистрации.** Порядок инициализации задаёт он (раздаёт контексты в `Awake`), а не случайный порядок `Awake` у объектов.
- **Зависимости между системами — ссылками в инспекторе.** Каждое поле `[SerializeField]` нужно прокинуть руками. Забыл — `NullReferenceException` на старте.
- **Контейнеры с префиксом `@`** всплывают наверх иерархии и читаются как «менеджеры», а не игровые объекты.
- **Пространственные объекты живут в мире.** `IslandSystem`, `GridOverlay`, `Pier` — в мире, не в `@Systems`.

> ⚠️ **Историческое имя поля.** В `GameBootstrap` поле называется `buildingSystem`
> (в инспекторе **«Building System»**), но его **тип — `StructureSystem`**. Имя оставлено, чтобы не
> рвать сериализацию сцены при переименовании `Building→Structure`. Кидаем туда компонент
> `StructureSystem`. В `RunManager` то же самое поле названо уже корректно — `structureSystem`.

## Рекомендуемая иерархия

```
SampleScene
├── @Bootstrap                  → GameBootstrap, SaveSystem
├── @Systems
│   ├── RunManager              → RunManager
│   ├── ResourceSystem          → ResourceSystem
│   ├── StructureSystem         → StructureSystem
│   ├── SpawnSystem             → SpawnSystem
│   ├── UnitSystem              → UnitSystem
│   ├── UnitPool                → UnitPool          (юниты из пула становятся его детьми)
│   ├── PerkSystem              → PerkSystem
│   ├── PrestigeSystem          → PrestigeSystem
│   ├── AgeSequencer            → AgeSequencer
│   ├── TapSystem               → TapSystem
│   └── PlacementController     → PlacementController
├── @Input                      → InputHandler
├── Island                      → IslandSystem
│   └── Grid                    → Grid (компонент)
│       └── Tilemap             → Tilemap + TilemapRenderer + TilemapCollider2D
│                                  + CompositeCollider2D + Rigidbody2D (Static)   ← граница острова
├── GridOverlay                 → GridOverlay        (Transform строго 0,0,0, без поворота/масштаба)
├── Main Camera                 → Camera
├── EventSystem                 → EventSystem + InputSystemUIInputModule  (ОБЯЗАТЕЛЕН для UI и кликов)
├── Canvas                      → Canvas + GraphicRaycaster
│   ├── ResourceBar
│   │   └── ResourceUI ×N       → ResourceUI            (по одному на ресурс; опц. на этом этапе)
│   ├── AgeUI                   → AgeUI                 (опц.)
│   ├── BuildModeButton         → BuildModeButton       (правый-нижний угол: Play↔Build)
│   ├── BuildPanel              → BuildPanelUI + CanvasGroup
│   │   ├── CardContainer       → Horizontal Layout Group   (сюда BuildPanelUI кладёт карточки в рантайме)
│   │   └── SellButton          → Button
│   │       └── SelectedHighlight                       (объект-подсветка, выключен по умолчанию)
│   └── PerkSelectionUI         → PerkSelectionUI       (опц.)
└── Pier                        → Pier + Collider2D (isTrigger = true)
```

> Можно навесить все менеджеры на один объект `@Systems` — компактнее, но инспектор огромный.
> Рекомендую по объекту на систему: видно в иерархии и можно отключать поштучно.

## Checklist проводки (критично)

Прокинь все ссылки. **Жирным** — то, без чего старт падает с `NullReferenceException` или ломается build mode.

### Bootstrap и системы

| Объект | Компонент | Поле | Что назначить |
|--------|-----------|------|---------------|
| @Bootstrap | **GameBootstrap** | **resourceSystem, islandSystem, unitSystem, spawnSystem** | соответствующие компоненты |
| | | **buildingSystem** | **StructureSystem** (поле зовётся «Building System»!) |
| | | **tapSystem, runManager, prestigeSystem, saveSystem** | соответствующие компоненты |
| | | ageSequencer, perkSystem | AgeSequencer, PerkSystem |
| | | **placementController** | PlacementController (нужен build mode) |
| | | perkSelectionUI | PerkSelectionUI (опц., можно пусто) |
| | | buildModeCooldown | 5 (сек, дефолт) |
| @Bootstrap | SaveSystem | — | (полей нет) |
| @Systems/RunManager | **RunManager** | **resourceSystem, islandSystem, structureSystem** | ResourceSystem, IslandSystem, StructureSystem |
| | | startingLayout | StartingLayoutDef-ассет (опц., но без него нет стартовых построек) |
| @Systems/ResourceSystem | ResourceSystem | logChanges | вкл/выкл лог ресурсов в консоль (дебаг) |
| @Systems/StructureSystem | **StructureSystem** | **islandSystem, resourceSystem, spawnSystem** | IslandSystem, ResourceSystem, SpawnSystem |
| @Systems/SpawnSystem | **SpawnSystem** | **unitPool, unitSystem** | UnitPool, UnitSystem |
| | | islandSystem | IslandSystem (впрыскивается в юнитов — бэкстоп удержания на острове) |
| @Systems/UnitSystem | UnitSystem | — | (полей нет) |
| @Systems/UnitPool | UnitPool | — | (полей нет; юниты инстанцируются его детьми) |
| @Systems/PerkSystem | PerkSystem | catalogue | список PerkDef (можно пусто) |
| @Systems/PrestigeSystem | PrestigeSystem | runManager, saveSystem | RunManager, SaveSystem |
| @Systems/AgeSequencer | AgeSequencer | islandSystem, perkSystem | IslandSystem, PerkSystem |
| @Systems/TapSystem | **TapSystem** | **inputHandler** | InputHandler |
| @Systems/PlacementController | **PlacementController** | **inputHandler, structureSystem, resourceSystem, islandSystem, mainCamera, gridOverlay** | соответствующие компоненты |
| | | validColor, invalidColor, sellHoverColor, moveHoverColor | цвета госта/наведения: гост валид/невалид, красный при продаже, зелёный «можно схватить» в Move (есть дефолты) |
| | | territoryValidColor, territoryInvalidColor, territorySortingLayer, territorySortingOrder | ореол занимаемой территории госта: зелёный/красный α0.18, слой Ground/1001 (дефолты) |
| @Input | **InputHandler** | **mainCamera** | Main Camera |
| Island | **IslandSystem** | **tilemap, grassTile** | Tilemap-компонент, тайл травы (TileBase) |
| | | initialSize, cellSize | 10×10, 1 (дефолты) |
| GridOverlay | **GridOverlay** | **islandSystem** | IslandSystem |
| | | lineColor, lineWidth | белый α0.6, 0.04 (дефолты) |
| | | occupiedColor | заливка занятых клеток (территория структур), белый α0.12 (дефолт) |
| | | sortingLayerName, sortingOrder | **Ground**, 1000 (чтобы линии были поверх травы) |

### UI

| Объект | Компонент | Поле | Что назначить |
|--------|-----------|------|---------------|
| Canvas/BuildModeButton | BuildModeButton | button, iconImage, buildIcon, playIcon | Button, иконка-Image, спрайты Build/Play |
| Canvas/BuildPanel | **BuildPanelUI** | palette | BuildPaletteDef-ассет |
| | | placementController, resourceSystem | PlacementController, ResourceSystem |
| | | cardPrefab | префаб карточки (BuildCard) |
| | | cardContainer | дочерний CardContainer (с Horizontal Layout Group) |
| | | canvasGroup | CanvasGroup на самой панели |
| | | sellButton, sellHighlight | кнопка Sell и её подсветка (опц., но нужны для продажи) |
| Canvas/ResourceUI | ResourceUI | resourceType, label | тип ресурса, TMP_Text |
| Canvas/AgeUI | AgeUI | ageLabel, nextAgeButton | TMP_Text, Button |
| Canvas/PerkSelectionUI | PerkSelectionUI | cardSlots[], cardLabels[], cardButtons[] | 3 слота/лейбла/кнопки |

## Префабы

### BaseUnit (юнит)
```
Root        → Rigidbody2D (Dynamic, GravityScale 0, Continuous, Freeze Rotation Z) + Unit
├── Visual  → SpriteRenderer (Sort Point = Pivot, пивот у ног)
└── Physics → CircleCollider2D (+ PhysicsMaterial2D «Bouncy»: Bounciness 1, Friction 0)
```
Слой **Unit** на всём BaseUnit. Префаб назначается в `UnitDef.prefab`.

### BaseBuilding / постройки (Structure)
```
Root        → Rigidbody2D (Static) + Structure  [+ Spawner и/или ResourceSource]
├── Visual  → SpriteRenderer (Sort Point = Pivot, пивот у основания)
└── Physics → BoxCollider2D
```
Rigidbody2D всегда на ROOT, чтобы `OnCollisionEnter2D`/`OnTriggerEnter2D` доходили до скрипта.
Obstacle (отскок) vs Interactable (триггер) задаётся флагом `isTrigger` на коллайдере.

Компоненты на постройке (комбинируются — у структуры может быть один или оба):

| Компонент | Поле | Что назначить |
|-----------|------|---------------|
| **Spawner** (производит юнитов) | spawnSystem | SpawnSystem в сцене *(или впрыснется в рантайме при постройке)* |
| | unitDef | UnitDef (его `prefab` → BaseUnit) |
| | capacity | число слотов (≥1) |
| | restDuration, lockoutDuration | тайминги цикла (3, 2) |
| | launchSpeedMultiplier, launchBoostDuration, launchGap | параметры вылета (2.5, 1, 0.1) |
| **ResourceSource** (производит ресурсы) | def | ResourceSourceDef-ассет |
| | resourceSystem | ResourceSystem *(или впрыснется в рантайме)* |

> Природные источники (Tree/Wheat/Stone) — это `Structure` + `ResourceSource` **без** `Spawner`.
> Здания-источники (Forge/Church) — `Structure` + `ResourceSource` с `infinite`-дефом.
> Юниты появятся только если у `Spawner` заполнены `spawnSystem` + `unitDef` (цикл стартует в `Spawner.Start()`).

### BuildCard (префаб карточки, инстанцируется BuildPanelUI в рантайме)
```
Root → Button + BuildCardUI + CanvasGroup
├── Icon            → Image
├── CostText        → TMP_Text
└── SelectedHighlight                (подсветка выбранной карточки, выкл. по умолчанию)
```
Поля `BuildCardUI`: button, iconImage, costText, selectedHighlight, canvasGroup.

## ScriptableObject-ассеты

| Ассет | Меню создания | Главное содержимое |
|-------|---------------|--------------------|
| **StructureDef** | LittlePeeps/StructureDef | id, displayName, icon, prefab, size, cost[], allowedTerrain[] (пусто = любой биом), sellRefundPercent (0..1), border (расширяет занимаемую территорию в клетках: дом=1 → 2×2 занимает 4×4 сетки; дерево/поле=0) |
| **ResourceSourceDef** | LittlePeeps/ResourceSourceDef | resource, amountPerHit, allowedWorkers[] (пусто = любой), infinite, hitsBeforeDespawn, respawnTime |
| **UnitDef** | (см. ассет) | unitType, prefab (→ BaseUnit), скорость и т.д. |
| **StartingLayoutDef** | LittlePeeps/StartingLayout | entries: список { StructureDef def; Vector2Int cell } — стартовые постройки (cell = origin/нижний-левый, SIGNED) |
| **BuildPaletteDef** | LittlePeeps/BuildPalette | structures: список StructureDef для нижней панели |
| PerkDef | (см. ассет) | перки для PerkSystem.catalogue |

> Координаты сетки **знаковые** и не зависят от того, какие клетки существуют: центр клетки `c` = мир `(c+0.5)·cellSize`.
> Дом 2×2 на `cell=(-1,-1)` занимает `(-1,-1)…(0,0)` и центрируется в мировом нуле.

## Build mode — как связано

- **Вход/выход** по `BuildModeButton` (правый-нижний угол) → событие → `GameplayContainerState` переключает внутренний FSM `Playing↔BuildMode` и держит 5-сек кулдаун на повторный вход.
- **`BuildModeState.Enter`**: `Time.timeScale=0` + `SpawnSystem.DespawnAllAndResetSpawners` (юниты в пул). **`Exit`**: `SpawnSystem.WarmupAllSpawners` (юниты респавнятся из зданий) + `Time.timeScale=1`.
- **Инструмент = что выбрано в панели** (`PlacementController`):
  - карточка → **PLACE** (гост под курсором, клик ставит),
  - кнопка Sell → **SELL** (наведение красит постройку, клик продаёт с возвратом `sellRefundPercent×cost`),
  - ничего не выбрано → **MOVE** (клик берёт постройку, она тащится за курсором; клик ставит, ПКМ — отмена на исходное место).
- `GridOverlay` показывает/прячет сетку на вход/выход.
- **`EventSystem` обязателен** — `PlacementController` игнорирует клики над UI через `EventSystem.IsPointerOverGameObject`, а uGUI-кнопки без него не нажимаются.

## Порядок выполнения скриптов — НЕ трогаем

Script Execution Order настраивать **не нужно** (это костыль). Инициализация независима от порядка by design:

- Unity гарантирует, что **все `Awake` отработают раньше любого `Start`**.
- `GameBootstrap` делает всю проводку и старт рана в своём `Awake`. К моменту любого `Start` всё уже связано, а остров сгенерирован — какой объект инициализируется первым, не важно.
- **Правило для новых систем:** не читай «вколотое» состояние (контексты, другие системы) в своих `Awake`/`OnEnable` — только начиная со `Start`. Подписки на события и `GetComponent` в `Awake`/`OnEnable` — можно.

## Что должно произойти при Play

1. `GameBootstrap.Awake`: загрузка Meta, создание Session, проводка систем; `RunManager.StartNewRun()` → создаётся RunContext, инициализируются ресурсы, `IslandSystem.GenerateForRun()` рисует остров, раскладываются стартовые постройки; App FSM → `Boot → GameplayContainer → Playing`.
2. На экране: остров (трава), юниты появляются внутри зданий-спавнеров, отдыхают, вылетают и отскакивают; сбор ресурсов при ударах по источникам. Клик — буст юнитов в радиусе. Клик по Pier — событие `PrestigeTriggeredEvent` (обработчика пока нет).
3. Кнопка build mode → пауза + сетка + панель: ставим/продаём/двигаем постройки; выход → юниты респавнятся.

## Project settings — важное (не в сцене, но влияет)

- **Physics 2D → Layer Collision Matrix:** Unit×Unit **OFF** (юниты проходят сквозь друг друга), Unit×Default **ON**.
- **Physics 2D → Bounce Threshold = 0.1** (иначе скольжение на малых скоростях).
- **Y-сортировка:** на URP 2D Renderer-ассете (`Assets/Settings/Renderer2D.asset`) — Transparency Sort Mode = Custom Axis, Axis (0,1,0). Юниты и постройки — на одном Sorting Layer с равным Order, сортируются по Y динамически.

## Что ещё заглушка (чтобы не ждать большего)

Главное меню и HUD, реальный Save на диск, переходы эпох (`AgeSequencer`), формула престижа (`PrestigeSystem`), перки (`PerkSystem`), `ResourceUI/AgeUI/PerkSelectionUI` (Initialize/Show — `// TODO`) — пока не реализованы. Существа-источники (Boar/Fox/Alpaca) и их спавнеры, Windmill, HP/бой, генерация по биомам — в бэклоге.
