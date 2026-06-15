# Little Peeps — организация систем в сцене

Как устроен старт после правок бутстрапа и как разложить системы в `SampleScene`.

## Принцип

- **Один сцена-проект.** Системы — это синглтоны-объекты в сцене (подход A). Не создаём их кодом.
- **`GameBootstrap` — единственная точка регистрации.** Порядок инициализации задаёт он (раздаёт контексты в `Awake`/`Start`), а не случайный порядок `Awake` у объектов.
- **Зависимости между системами — ссылками в инспекторе.** Каждое поле `[SerializeField]` нужно прокинуть руками. Если забыть — будет `NullReferenceException` на старте.
- **Контейнеры с префиксом `@`** всплывают наверх иерархии и сразу читаются как «менеджеры», а не игровые объекты.
- **Пространственные системы живут в мире.** `IslandSystem` кладём к `Tilemap`, а не в `@Systems`.

## Рекомендуемая иерархия

```
SampleScene
├── @Bootstrap        → GameBootstrap, SaveSystem
├── @Systems          → каждый менеджер на своём дочернем объекте:
│   ├── RunManager
│   ├── ResourceSystem
│   ├── BuildingSystem
│   ├── SpawnSystem        (уже есть в сцене)
│   ├── UnitSystem
│   ├── UnitPool
│   ├── PerkSystem
│   ├── PrestigeSystem
│   ├── AgeSequencer
│   ├── DragController
│   └── TapSystem          (уже есть в сцене)
├── @Input            → InputHandler
├── Island            → IslandSystem  +  Grid → Tilemap (дочерний, рендер + коллайдер границы)
├── Main Camera
├── UI (Canvas)       → ResourceUI×N, AgeUI, PerkSelectionUI   (опционально на этом этапе)
├── Pier              → Pier + CircleCollider2D (isTrigger = true)
└── --Units           → пустой родитель для юнитов из пула (необязательно)
```

> Можно навесить все менеджеры компонентами на один объект `@Systems` — компактнее, но инспектор большой. Рекомендую по объекту на систему: видно в иерархии, можно отключать поштучно.

## Checklist проводки (критично)

Прокинь все ссылки. Жирным — то, без чего старт падает с `NullReferenceException`.

| Объект | Компонент | Поле | Что назначить |
|--------|-----------|------|---------------|
| @Bootstrap | **GameBootstrap** | resourceSystem, islandSystem, unitPool, unitSystem, spawnSystem, buildingSystem, dragController, tapSystem, ageSequencer, perkSystem, prestigeSystem, runManager, saveSystem | соответствующие компоненты сцены |
| | | perkSelectionUI | PerkSelectionUI (опц., можно пусто) |
| @Systems/RunManager | **RunManager** | **resourceSystem, islandSystem** | ResourceSystem, IslandSystem |
| @Systems/PrestigeSystem | **PrestigeSystem** | **runManager, saveSystem** | RunManager, SaveSystem |
| @Systems/DragController | **DragController** | buildingSystem, islandSystem | BuildingSystem, IslandSystem |
| @Systems/BuildingSystem | **BuildingSystem** | islandSystem, resourceSystem | IslandSystem, ResourceSystem |
| @Systems/SpawnSystem | **SpawnSystem** | **unitPool** | UnitPool |
| @Systems/TapSystem | **TapSystem** | **inputHandler** | InputHandler |
| @Systems/AgeSequencer | AgeSequencer | islandSystem, perkSystem | IslandSystem, PerkSystem |
| @Systems/PerkSystem | PerkSystem | catalogue | список PerkDef (можно пусто) |
| @Input | **InputHandler** | **mainCamera** | Main Camera |
| Island | **IslandSystem** | **tilemap, grassTile** | Tilemap-компонент, тайл травы (TileBase) |
| | | initialSize, cellSize | 10×10, 1 (дефолты) |

### Префабы-здания со спавнером
На префабе с компонентом **Spawner** (`BaseBuilding`):
- `spawnSystem` → ссылка на SpawnSystem в сцене,
- `unitDef` → нужный UnitDef (его `prefab` должен указывать на `BaseUnit`).

Без этого юниты не появятся — спавн-цикл стартует из `Spawner.Start()`.

## Порядок выполнения скриптов — НЕ трогаем

Script Execution Order настраивать **не нужно** (это костыль). Инициализация независима от порядка by design:

- Unity гарантирует, что **все `Awake` отработают раньше любого `Start`**.
- `GameBootstrap` делает всю проводку и старт рана в своём `Awake`. Значит, к моменту любого `Start` всё уже связано, а остров сгенерирован — какой объект инициализируется первым, не важно.
- **Правило для новых систем:** не читай «вколотое» состояние (контексты, другие системы) в своих `Awake`/`OnEnable` — только начиная со `Start`. Подписки на события и `GetComponent` в `Awake`/`OnEnable` — можно.

## Что должно произойти при Play

1. `GameBootstrap.Awake`: загрузка Meta, создание Session, регистрация систем, App FSM → `BootState`.
2. `GameBootstrap.Start`: `RunManager.StartNewRun()` → создаётся RunContext, инициализируются ресурсы, `IslandSystem.GenerateForRun()` рисует остров; затем автопереход `Boot → GameplayContainer → Playing`.
3. На экране: остров (трава), юниты появляются внутри зданий-спавнеров, отдыхают, вылетают и отскакивают. Клик — буст юнитов в радиусе. Клик по Pier — событие `PrestigeTriggeredEvent` (обработчика пока нет).

## Что ещё заглушка (чтобы не ждать большего)

Главное меню и HUD, реальный Save на диск, постройка зданий через UI, переходы эпох, формула престижа — пока `// TODO`. Этот этап = **загрузка + старт рана + остров + регистрация всех систем + летающие юниты**.
