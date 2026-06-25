# Little Peeps — организация систем в сцене

Как устроен старт и как разложить объекты в `SampleScene`, чтобы сцена была «по полочкам».
Документ держим синхронным с кодом — обновляем при добавлении/переименовании систем и полей.

> Последняя сверка с кодом: **2026-06-23** (после добавления управления камерой + горячих клавиш).

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
├── @Input                      → InputHandler, GameHotkeys
├── Island                      → IslandSystem
│   └── Grid                    → Grid (компонент)
│       └── Tilemap             → Tilemap + TilemapRenderer + TilemapCollider2D
│                                  + CompositeCollider2D + Rigidbody2D (Static)   ← граница острова
├── GridOverlay                 → GridOverlay        (Transform строго 0,0,0, без поворота/масштаба)
├── Main Camera                 → Camera (Orthographic) + CinemachineBrain
├── CameraTarget                → CameraController        (пустой GO — логическая цель камеры)
├── CinemachineCamera           → CinemachineCamera (vcam): Follow = CameraTarget, Body с damping
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
| | | islandSystem | IslandSystem (впрыскивается в юнитов — задел на будущее island-aware поведение) |
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
| @Input | GameHotkeys | buildModeKey, sellKey, exitToMenuKey, infoKey | клавиши команд: B / X / Esc / I (дефолты, правятся в инспекторе) |
| CameraTarget | **CameraController** | **islandSystem, viewCamera** | IslandSystem (кламп по острову); viewCamera = Main Camera (только для перевода drag-пикселей в мир) |
| | | panSpeed, edgePanEnabled, edgeThickness | скорость WASD/стрелок (12), вкл. край-скролл, толщина края в px (12) — дефолты |
| | | boundsMargin | насколько центр (= цель) может уйти за край острова (4) — дефолт |
| Main Camera | **CinemachineBrain** | — | пишет позицию из активной vcam |
| CinemachineCamera | **CinemachineCamera** (vcam) | Follow = CameraTarget, Body (damping ~0.1–0.2) | плавно следует за целью |
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
| | readyRoot | дочерний объект-визуал состояния Ready *(пусто для `infinite`)* |
| | harvestedRoot | дочерний объект-визуал состояния Harvested *(пусто для `infinite`)* |

> Природные источники (Tree/Wheat/Stone) — это `Structure` + `ResourceSource` **без** `Spawner`.
> Здания-источники (Forge/Church) — `Structure` + `ResourceSource` с `infinite`-дефом.
> Юниты появятся только если у `Spawner` заполнены `spawnSystem` + `unitDef` (цикл стартует в `Spawner.Start()`).

**Не-`infinite` источники имеют два визуальных рута** вместо одного `Visual` — `ResourceSource`
включает один и выключает другой при истощении/восстановлении (`SetActive`). Арт каждого состояния
(спрайт + Sorting Layer + пивот) живёт в префабе, не в дефе:
```
Root          → Rigidbody2D (Static) + Structure + ResourceSource
├── Physics       → BoxCollider2D            (на хосте; гасится в Harvested через SetColliderEnabled)
├── ReadyRoot     → SpriteRenderer (Sorting Layer Entities, пивот у основания — Y-сортируется с юнитами)
└── HarvestedRoot → SpriteRenderer (Sorting Layer Ground — юниты всегда проходят поверх)
```
> Коллайдер НЕ кладётся внутрь рутов: `CollisionTarget` кэширует его в `Awake` через
> `GetComponentInChildren`, который не видит выключенные объекты. В префабе: `ReadyRoot` активен,
> `HarvestedRoot` выключен (код всё равно выставит состояние в `Start`, но так нет мигания кадром).
> `infinite`-источники (Forge/Church) оставляют оба рута пустыми и используют единственный `Visual`.
> Y-сортировка включается глобально: **Project Settings → Graphics → Transparency Sort Mode = Custom Axis (0,1,0)**.

### BaseFence / заборы (Structure на РЕБРЕ грида)
Забор не занимает клетку — он стоит на **ребре** (границе между двумя клетками). Это один префаб с
**двумя позами-детьми**; нужная включается по ориентации ребра под курсором (`EdgeStructureVisual`).
```
Root            → Rigidbody2D (Static) + Structure + EdgeStructureVisual
├── HorizontalRoot  → SpriteRenderer (fence_hor) + BoxCollider2D (тонкий бокс вдоль линии: широкий X, тонкий Y)
└── VerticalRoot    → SpriteRenderer (fence_ver) + BoxCollider2D (тонкий бокс вдоль линии: тонкий X, широкий Y)
```
- Обе позы **отцентрованы в local (0,0)**: рут ставится в середину ребра (`IslandGrid.EdgeToWorld`), и
  спрайт+коллайдер ложатся ровно на линию решётки. H-ребро = горизонтальная линия, V-ребро = вертикальная.
- `EdgeStructureVisual`: поля **horizontalRoot**, **verticalRoot** → соответствующие дочерние объекты.
  `Apply(horizontal)` включает одну позу, гасит другую. Дёргается при постройке, у ghost'а и при drag.
  Коллайдер **неактивной** позы гаснет вместе с её GameObject — отдельного кода не нужно.
- **Коллайдеры (отскок):** `isTrigger = 0`, **Material = тот же физматериал отскока, что у BaseBuilding**
  (guid `397134329f508124e95b17766bee3c17`). Юнит уже `Collision Detection = Continuous`, поэтому сквозь
  тонкий забор не туннелирует.
- В `StructureDef` забора выставить **placement = Edge** (`size`/`border` для рёбер не используются).
- Тот же префаб служит превью: ghost = его инстанс с выключенными коллайдерами и `Structure`, тинт
  green/red на обоих рендерерах.
- **Move/Sell:** забор перетаскивается и продаётся через тот же build-режим; клик «выигрывает» забор,
  когда курсор в пределах ~⅓ клетки от линии ребра (иначе берётся структура в клетке под курсором).

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
| **StructureDef** | LittlePeeps/StructureDef | id, displayName, icon, prefab, **placement (Cell=футпринт клеток / Edge=забор на ребре)**, size, cost[], allowedTerrain[] (пусто = любой биом), sellRefundPercent (0..1), border (расширяет занимаемую территорию в клетках: дом=1 → 2×2 занимает 4×4 сетки; дерево/поле=0; для Edge не используется) |
| **ResourceSourceDef** | LittlePeeps/ResourceSourceDef | resource, workerYields[] (кто и сколько добывает; пусто = никто), infinite, hitsBeforeDespawn, respawnTime *(визуалы состояний живут в префабе как ReadyRoot/HarvestedRoot, не в дефе)* |
| **UnitDef** | (см. ассет) | unitType, prefab (→ BaseUnit), скорость и т.д. |
| **StartingLayoutDef** | LittlePeeps/StartingLayout | entries: список { StructureDef def; Vector2Int cell } — стартовые постройки (cell = origin/нижний-левый, SIGNED) |
| **BuildPaletteDef** | LittlePeeps/BuildPalette | structures: список StructureDef для нижней панели |
| PerkDef | (см. ассет) | перки для PerkSystem.catalogue |

> Координаты сетки **знаковые** и не зависят от того, какие клетки существуют: центр клетки `c` = мир `(c+0.5)·cellSize`.
> Дом 2×2 на `cell=(-1,-1)` занимает `(-1,-1)…(0,0)` и центрируется в мировом нуле.

## Управление (камера + горячие клавиши) — как связано

Три слоя ввода, расцеплённые между собой:

- **`InputHandler`** — низкоуровневый роутер мыши: ЛКМ/ПКМ → мировые координаты → события `OnWorldClick`/`OnWorldRightClick`. Их слушают `TapSystem` (буст юнитов / Pier) и `PlacementController` (build mode). Камеру он не трогает.
- **`CameraController`** (на пустом **CameraTarget**, НЕ на камере) — двигает свой transform = логическую цель камеры; поллит ввод сам, считает на `unscaledDeltaTime` → работает и в build mode (`timeScale=0`). Источники: WASD + стрелки, курсор у края экрана, **drag средней кнопкой мыши** (мир тащится под курсором). Цель клампится по `IslandGrid.WorldBounds()` + `boundsMargin`; границы перечитываются на `AgeStartedEvent` (рост острова). Плавность даёт не он, а **Cinemachine vcam**, которая следует за целью с damping; `Main Camera` несёт `CinemachineBrain` и рендерит. `viewCamera` (= Main Camera) нужен скрипту только чтобы перевести drag-пиксели в мир (его `orthographicSize` крутит brain → отражает живой зум). Зум колесом + `Confiner2D` — отдельной итерацией позже.
- **`GameHotkeys`** (на @Input) — дискретные команды по нажатию, публикует события в `EventBus`, ни во что не лезет напрямую:
  - **B** → `BuildModeToggleRequestedEvent` (тот же путь, что кнопка build mode → `GameplayContainerState`, с 5-сек кулдауном);
  - **X** → `SellModeRequestedEvent` → `BuildPanelUI` тогглит инструмент Sell тем же путём, что кнопка (подсветка + контроллер синхронны); вне build mode — no-op;
  - **Esc** → `ExitToMenuRequestedEvent` → `GameBootstrap` переводит App FSM в `MainMenuState` (выход из контейнера восстанавливает `timeScale`); меню — пока заглушка;
  - **I** → `InfoToggleRequestedEvent` → подписчика пока нет (окно информации в бэклоге; событие уже публикуется).

> ⚠️ `CameraController` использует `viewCamera.orthographicSize` (drag) — **камера должна быть Orthographic** (2D-проект, так и есть). линзу vcam тоже держать Orthographic.

## Build mode — как связано

- **Вход/выход** по `BuildModeButton` (правый-нижний угол) **или клавише B** → событие `BuildModeToggleRequestedEvent` → `GameplayContainerState` переключает внутренний FSM `Playing↔BuildMode` и держит 5-сек кулдаун на повторный вход.
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
