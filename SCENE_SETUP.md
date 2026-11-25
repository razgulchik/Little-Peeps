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
> подношения и `OfferingPanel` больше нет: **2026-10-20**, сверено с `Canvas.prefab`. Большой ромб ленты уезжает с
> руками, на стоянку приезжает маленький, раскладка ленты по UI03; кромка острова в два слоя (`TrimHorizontal` /
> `TrimVertical`): **2026-10-22**, сверено со сценой и `Canvas.prefab`; в тот же день — полоса наползает на точки со
> скоростью ленты, под стоянкой вторые точки `StandDots`, сверено с `Canvas.prefab`; и панель эпохи одной панелью по
> UI03 (рамка эпохи с бонусом или «NO BONUS», цены, кнопка Expand Island; кольцо вокруг иконки цены, которой не хватает;
> вариант ResourceUnit для цены эпохи), сверено с `Canvas.prefab`. Своё окно конца цикла по макету Gods Blessing
> (`Screens/OfferingWindow` вместо `ConfirmDialog`, кнопка кары удержанием, весь текст на целых пикселях —
> `PixelSnapText`) и тень текста на панели эпохи пресетами шрифтов: **2026-10-23**, сверено с `Canvas.prefab`. Отклик
> на удары (`Systems/BumpVfxSystem`, `CoastBump` на `Ground`, `BumpView` / `LeanView` на префабах, `FX_Bump*` и
> `FX_Coast_Ripple`): **2026-10-28**, сверено со сценой и префабами. Уровни перков и мета-перков внутри одного
> ассета, Flat / Percent выбором, цифра уровня в ячейке `PerkSlot`: **2026-10-29**, сверено с кодом и `PerkSlot.prefab`. Своя вода (`WaterSystem`, волны, тень острова, `WaterProjection`): **2026-11-04**, сверено со сценой и префабами. Забег, престиж и мета-перки (очки за время, бесплатные эпохи, тестовый режим): **2026-11-25**, сверено со сценой, ассетами и `Canvas.prefab`; в тот же день — гусятник, мост, бамбук, протяжка забора, горы, камень, рынок, пуф, налог, поля перков и эффекты подбора, окно зон, окно перков, карточка стройки, подъём острова и берег, сверено со сценой, ассетами и префабами. Не описана проводка звука (`SoundSystem` / `SoundDef`):
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
├── TestMode                    → TestMode           (пресеты для плей-теста; включается в меню, см. «Тестовый режим»)
├── Input                       → InputHandler, GameHotkeys
├── Global Light 2D             → Light2D
├── Bootstrap                   → GameBootstrap, SaveSystem
├── Systems
│   ├── AgeSystem               → AgeSystem          (каталог эпох + CanAdvance)
│   ├── AgeSequencer            → AgeSequencer
│   ├── BumpVfxSystem           → BumpVfxSystem      (волны ударов и рябь у берега; эмиттеры становятся его детьми)
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
│   └── WaterSystem             → WaterSystem        (квад моря `Sea` создаёт сам в рантайме — в сцену воду НЕ класть)
├── Island                      → IslandSystem
│   ├── Grid                    → Grid (компонент)   ← все тайлмапы под ним — на слое сортировки Island
│   │   ├── Ground              → Tilemap + TilemapRenderer + Rigidbody2D (Static) + TilemapCollider2D
│   │   │                          + CompositeCollider2D + CoastBump   ← суша и граница острова (CoastBump —
│   │   │                          рябь на воде, когда юнит отскакивает от берега)
│   │   ├── TrimHorizontal      → Tilemap + TilemapRenderer   ← горизонтальная кромка берега — полосы под сушей (Order 0);
│   │   │                          лежит на клетках ВОДЫ, поэтому без коллайдера
│   │   ├── TrimVertical        → Tilemap + TilemapRenderer   ← вертикальная кромка — столбики по бокам суши, поверх полос;
│   │   │                          без коллайдера; материал, слой и Order ставит код (как у TrimHorizontal, +1)
│   │   ├── Decor               → Tilemap + TilemapRenderer   ← декор поверх земли (Order 1), без коллайдера
│   │   ├── BordersForest       → Tilemap + TilemapRenderer   ← мягкая граница леса, без коллайдера
│   │   ├── BordersMeadow       → Tilemap + TilemapRenderer   ← мягкая граница луга, без коллайдера
│   │   ├── WaveHorizontal      → Tilemap + TilemapRenderer   ← волны под полосами, без коллайдера;
│   │   │                          материал, слой и Order ставит код (от TrimHorizontal, −2)
│   │   ├── WaveVertical        → Tilemap + TilemapRenderer   ← волны у столбиков, поверх горизонтальных, без коллайдера;
│   │   │                          материал, слой и Order ставит код (−1)
│   │   ├── CoastShadow         → Tilemap + TilemapRenderer   ← тень острова на воде, без коллайдера;
│   │   │                          материал, слой и Order ставит код (−5)
│   │   └── Water (выключен)    → Tilemap + TilemapRenderer
│   └── GridOverlay             → MeshFilter + MeshRenderer + GridOverlay   (Transform строго 0,0,0, без поворота/масштаба)
├── SceneGridGizmo (выключен)   → SceneGridGizmo     (сетка только для Scene view, в игре не участвует)
├── WaterGradient               → префаб `Prefabs/Elements/WaterGradient`: Canvas (Screen Space — Camera, слой Water, Order 1)
│                                  + CanvasScaler 640×360 — градиент поверх моря, под островом; Up, Side Left, Side Right,
│                                  Light — Image с выключенным Raycast. Render Camera — Main Camera (назначена в сцене)
├── ZonePreviewOverlay          → MeshFilter + MeshRenderer + ZonePreviewOverlay   (заливка зоны под карточкой при выборе)
├── IslandRise                  → IslandRisePlayer   (Transform 0,0,0; в рантайме его дети — дублёры тайлов,
│                                  излучатели эффектов, «Rise voices» и CinemachineImpulseSource)
├── Music (выключен)            → AudioSource: Audio Resource = трек флейты, Play On Awake, Loop, Priority 0
│                                  ← фоновая музыка без кода: играет с запуска всю сессию, timeScale её не трогает.
│                                    Сейчас объект выключен в сцене — музыки нет
└── Canvas                      → префаб `Prefabs/Elements/Canvas`: Canvas + CanvasScaler + GraphicRaycaster
    │                             + **UIRoot** + **UIVisibility**. Порядок детей = порядок отрисовки: ниже = поверх
    ├── Hud                     → CanvasGroup                    ← весь игровой HUD одной группой
    │   ├── AgePanel            → Image (panel_base_age_expand, Sliced, Raycast ВКЛ) + CanvasGroup + AgeAdvancePanel
    │   │   │                     + Vertical Layout Group + Content Size Fitter
    │   │   │                     ← левый верхний угол (8, −10), ширина 79, высоту считает Fitter. Один спрайт на
    │   │   │                       всю панель: нарезка сверху держит эпоху и разделитель, тянется низ. Padding
    │   │   │                       0/0/45/49 (сверху рамка эпохи, снизу место под кнопку), Upper Center, Control
    │   │   │                       Child Size W+H. Высота 93 + 14 × строк цены: 107 / 121 / 135 при 1–3 строках
    │   │   │                       (121 — как на макете); 94 на последней эре (Cost выключен) и в редакторе (Cost пуст);
    │   │   │                       107 — бесплатная эпоха (вместо Cost — Free)
    │   │   ├── Age             → Image (panel_age_actual, Sliced) + LayoutElement (Ignore Layout)
    │   │   │   │                 (рамка эпохи 73×35 в (3, −3), вне раскладки)
    │   │   │   ├── Title       → TMP_Text, m6x11plus 18, #C69F67     («AGE IV»; y −4, 73×22)
    │   │   │   ├── BonusValue  → TMP_Text, Micro5 11, #E3CC81       («+10%»; y −19, 73×10)
    │   │   │   └── BonusText   → TMP_Text, Micro5 11, #E3CC81       («ALL PRODUCTION» или «NO BONUS»; y −26, 73×10)
    │   │   │                     у всех трёх материал — пресет с тенью (см. «Шрифты и тень текста»)
    │   │   │                     все три Left + Baseline, без переноса, Raycast выкл.; x ставит скрипт — центрует
    │   │   │                     по краям букв на целых пикселях. Размеры шрифтов не трогать: m6x11plus чёткий
    │   │   │                     только в 18 (36), Micro5 — в 11 (22); в других размерах пиксель шрифта ложится
    │   │   │                     между пикселями экрана и текст мылится
    │   │   ├── Cost            → Image + Grid Layout Group + AgeCostPanel  (ценник, юниты в рантайме; 2 колонки,
    │   │   │                     ячейки 34×13, Spacing 3×1, Padding 0 — свою высоту сетка считает сама)
    │   │   ├── Free (выключен) → TMP_Text «FREE», m6x11plus 9, #BE9F85, по центру + PixelSnapText + LayoutElement
    │   │   │                     (Preferred Height 13) — стоит вместо Cost, пока следующая эпоха бесплатна; включает AgeCostPanel
    │   │   └── AdvanceButton   → Image (bttn_age_expand) + Button + LayoutElement (Ignore Layout)
    │   │       │                 (родные 69×43, по центру, 4 px от низа: прибита к низу AgePanel и едет с ним;
    │   │       │                  от последней строки цены 2 px)
    │   │       ├── Line1       → TMP_Text «EXPAND», m6x11plus 18 + пресет с тенью, #C69F67, Left + Baseline, (13, −7), 50×22
    │   │       └── Line2       → TMP_Text «ISLAND», то же, (14, −21) — обе строки по центру кнопки по краям букв
    │   ├── SellButton          → Image + Button + CanvasGroup + SellButtonUI   (левый нижний угол (2, 17), 34×42;
    │   │                         Button: Sprite Swap, Navigation None, Selected Sprite пуст — обычные спрайты sell / hover / press)
    │   ├── BuildTabs           → Vertical Layout Group + Content Size Fitter + CanvasGroup + BuildTabBar
    │   │                         ← слева от палитры (59, 7), ширина 79, растёт вверх; Spacing −3 — соседние
    │   │                           вкладки перекрываются на 3 px, как на макете; кнопки вкладок в рантайме
    │   ├── BuildPalette        → CanvasGroup + Image (card_panel, Sliced) + BuildPaletteUI   (справа внизу (−47, 4), 440×64)
    │   │   └── Viewport        → Image + RectMask2D + BuildPanelScroller   (тянется по ширине палитры)
    │   │       └── CardRow     → BuildCardRow   (Horizontal Layout Group, который ещё держит порядок отрисовки; карточки в рантайме)
    │   ├── BuildToggle         → Image + Button + BuildPanelToggle   (правый нижний угол (−9, 19), 32×40: прячет /
    │   │                         показывает SellButton, BuildTabs и BuildPalette — не режим, игра идёт и так и так)
    │   ├── ResourceBar         → Image + Grid Layout Group + ResourcePanel   (ResourceUnit на ресурс, в рантайме)
    │   │                         ← правый верхний угол, 124×36; ячейки 34×13, три в строку
    │   ├── TimePanel           → Image (time_panel, Sliced, **Raycast Target ВКЛ** — ловит наведение) + CanvasGroup
    │   │   │                     + TimePanel   ← лента времени до подношения, по центру сверху (0.5, −10), 281×29:
    │   │   │                     ширина нечётная, поэтому X = 0.5 — левый край панели на целом пикселе (от него
    │   │   │                     код считает всю ленту); своя строка видимости, у CanvasGroup включён Ignore Parent Groups
    │   │   ├── Now             → Image                 (кружок «сейчас», слева, 7..15)
    │   │   ├── Track           → RectMask2D            (маска слоя ромбов: от левого края кружка до правого края стоянки, 7..274)
    │   │   │   ├── Belt        → RectMask2D            (своя маска: от края кружка до точек, 18..260; линия, засечки
    │   │   │   │                 лет и края линии у засечек подношения — в рантайме. Правый край маски двигает код:
    │   │   │   │                 по точкам — за полосой, которая перед отъездом ромба наползает на них навстречу
    │   │   │   │                 ему, и до ромба, пока он проезжает над ними)
    │   │   │   └── Riders      → только RectTransform  (большие ромбы в пути и маленький на стоянке — в рантайме)
    │   │   ├── Years           → RectMask2D            (цифры лет под засечками: 18..260, строки 16..20)
    │   │   │   └── Year        → TMP_Text, выключен    (шаблон цифры: Micro5 11, #BE9F85; копии — в рантайме)
    │   │   ├── Dots            → Image                 («и так далее», 261..266; Filled справа ставит код — видны
    │   │   │                     только правее полосы, наползающей на них перед отъездом ромба, и правее
    │   │   │                     уезжающего ромба. Полоса идёт со скоростью ленты и доходит до ромба, когда он трогается)
    │   │   ├── StandDots       → Image                 (копия Dots под стоянкой, справа (−9, −11), 267..272 — через
    │   │   │                     пиксель после Dots, сплошной ряд; Filled ставит код — не видны, пока стоит большой
    │   │   │                     ромб, открываются за уезжающим, гаснут за пиксель до въезжающего маленького)
    │   │   ├── Offering        → Point + Icon (Image)  (стоянка, 266..275: большой ромб и руки под ним. Ромб уезжает
    │   │   │                     вместе с руками — код прячет Point, лента везёт его копию; на стоянку из-за края
    │   │   │                     приезжает маленький ромб, после подношения снова большой с руками)
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
    │   │   ├── Panel           → Image (затемнение — перехватывает ввод, это модальное окно)
    │   │   └── Window          → только RectTransform: по центру, Pivot (0, 1), (−126, 170), 252×346
    │   │       │                 ← окно по макету `Sprites/UI/God Blessings/Gods Blessings.png`
    │   │       ├── BaseTop / BaseBottom → Image (blessings_base, Sliced) 252×66 и 252×280 — фон двумя частями
    │   │       ├── Title       → TMP_Text «Gods Blessings», m6x11plus 18, #FFC35A + PixelSnapText
    │   │       ├── Subtitle    → TMP_Text «Choose wisely, …», 12, #887262 + PixelSnapText
    │   │       └── Cards       → только RectTransform (13, −71) 226×264 — место под три карточки; раскладывает код,
    │   │                         Layout Group сюда не вешать
    │   ├── ZoneScreen          → CanvasGroup + ZoneSelectionUI
    │   │   └── Panel           → Image (прозрачная, **Raycast Target ВЫКЛ** — иначе при выборе зоны остров не перетащить)
    │   │       └── CardRow     → Horizontal Layout Group       (низ экрана по центру, отступ 16, промежуток 4; карточки в рантайме)
    │   └── OfferingWindow      → Image (чёрный α 0.7 — затемнение, глотает клики) + CanvasGroup + OfferingWindow
    │       │                     ← окно конца цикла по макету `Sprites/UI/Offering Window/Gods Blessing.png`;
    │       │                     растянут на весь Canvas; весь текст — m6x11plus, на каждой надписи PixelSnapText
    │       └── Window          → только RectTransform: по центру, Pivot (0, 1), (−122, 155), 245×309 —
    │           │                 ширина нечётная, позиция целая, так края окна на целых пикселях
    │           ├── BaseLeft / BaseRight → Image (ui_window_demand_base_0 / _1, Sliced) (0, 0) 123×309 и (123, 0)
    │           │                 122×309 — фон двумя половинами: у спрайта орнамент по центру сверху и снизу,
    │           │                 тянуть можно только полосы по бокам от него, а у Sliced полоса одна
    │           ├── DecorLeft   → Image (ui_window_demand_decor_01) (13, −9) 29×38
    │           ├── DecorRight  → Image (ui_window_demand_decor_02) (205, −8) 24×39
    │           ├── Title       → TMP_Text «Gods Demand / the Offering», m6x11plus **36** + пресет с тенью, #FFCB2D,
    │           │                 Center + Top, Line Spacing −27.78 (шаг строки 26) — (0, −4), 245×64
    │           ├── TextPanelLeft / TextPanelRight → Image (ui_window_demand_text_panel_0 / _1, Sliced)
    │           │                 (4, −72) и (122, −72), по 118×109 — подложка под текст, тоже две половины
    │           ├── Body        → TMP_Text m6x11plus 18, #6E5439, цветные слова тегами <color>, Center + Top,
    │           │                 Line Spacing −16.67 (шаг 15), переносы строк как на макете — (4, −78), 236×90
    │           ├── DemandShadow → Image без спрайта, #23282C (47, −240) 150×3 — тень под плашкой, как на макете
    │           ├── Demand      → Image (offer_res_panel, Sliced) (47, −184) 150×57 — кремовая плашка квоты
    │           │   ├── Header  → TMP_Text «Demanded Offering», m6x11plus 18, #6E5439 — (0, −2), 150×18
    │           │   └── Costs   → только RectTransform (20, −22) 114×26 — ResourceUnit на ресурс квоты в рантайме,
    │           │                 раскладывает код: ряды по 3 (34×13, зазор 6), каждый ряд по центру
    │           ├── PayButton   → Image (bttn_pay_offering, Sliced, Raycast ВКЛ) + Button + CanvasGroup
    │           │   │             (12, −248) 105×47; Button: Color Tint только на нажатие, Navigation None;
    │           │   │             CanvasGroup гасит всю кнопку, пока запасов не хватает
    │           │   ├── HandsLeft / HandsRight → Image (Timeline/time_icon_offering) (10, −10) и (86, −10), 9×7
    │           │   └── Label   → TMP_Text «Pay / the Offering», m6x11plus 18 + тень, #DD8F2D, (0, −3) 105×34
    │           └── WrathButton → Image (bttn_facethewrath, Sliced, Raycast ВКЛ) + HoldButton   (128, −248) 105×47
    │               ├── Fill    → Image без спрайта, #721C26 α 0.6, (5, −5) 95×33 — заливка удержания (полная ширина;
    │               │             в покое код сжимает её в ноль)
    │               ├── SkullLeft / SkullRight → Image (icon_skulls) (10, −8) и (86, −8), 9×9
    │               └── Label   → TMP_Text «Face / the Wrath», m6x11plus 18 + тень, #A9231C, (0, −3) 105×34
    ├── HintView                → Image + CanvasGroup + HintView + Vertical Layout Group   ← окно подсказок, кликов не ловит
    │   ├── TitleText, BodyText → TMP_Text
    │   ├── CostContainer       → Vertical Layout Group         (строки цены в рантайме)
    │   └── FooterText          → TMP_Text
    ├── ConfirmDialog           → вложенный префаб `Prefabs/Elements/ConfirmDialog`: CanvasGroup + ConfirmDialog
    │                             (Backdrop, Panel с текстом и кнопками Yes / No) — сейчас его никто не зовёт:
    │                             конец цикла спрашивает через `OfferingWindow`; оставлен для простых да/нет
    ├── FPSmeter
    │   └── FPSText             → TMP_Text + FpsMeter                     ← счётчик кадров
    ├── MetaScreen              → Image + CanvasGroup + MetaUpgradesUI   ← окно Ancestral Wisdom после престижа (включено, как Screens)
    │   ├── Decor               → 12 плоских квадратов `window_wisdom_decor` за сеткой
    │   ├── Title               → TMP_Text + PixelSnapText
    │   ├── Points              → Image (`window_wisdom_points`)
    │   │   ├── PointsText      → TMP_Text + PixelSnapText   (свободные очки)
    │   │   └── Star            → Image (`wisdom_icon`)
    │   ├── Grid                → Grid Layout Group (7 колонок, ячейка 38×39, зазор 3)   (ячейки мета-перков в рантайме)
    │   ├── Footer              → TMP_Text + PixelSnapText
    │   ├── NextIslandButton    → Image + Button   (`bttn_pay_offering`)
    │   │   └── Label           → TMP_Text + PixelSnapText
    │   └── Tip                 → Image + CanvasGroup + MetaUpgradeTipUI   ← последний ребёнок, поверх ячеек
    │       └── Text            → TMP_Text + PixelSnapText
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
| | | testMode | `TestMode` — пресеты плей-теста. Пусто — при включённом тестовом режиме предупреждение, игра идёт на боевых |
| | | cameraController, zonePreview | `CameraController`, `ZonePreviewOverlay` — бутстрап сам их не использует, только передаёт в UI: выбор зоны двигает камеру и рисует зону на острове. Пусто — предупреждение `[UIServices] … is not assigned`, карточки зон без камеры и превью |
| Bootstrap | SaveSystem | — | (полей нет; запись на диск пока заглушка) |
| TestMode | TestMode | startConfig, offeringSchedule | `StartConfig/Test Start Config`, `OfferingDef/Test Offering Schedule Def` — чем заменить боевые в тестовом режиме. Пусто = боевой |
| Systems/RunManager | **RunManager** | **resourceSystem, islandSystem, structureSystem, spawnSystem** | ResourceSystem, Island, StructureSystem, SpawnSystem (spawnSystem обязателен — иначе NPE в StartNewRun) |
| | | **startConfig** | `ScriptableObjects/StartConfig/Start Config` — сид и правила острова, стартовый биом, дом, пирс, стартовые ресурсы, модификаторы и мета-перки. Пусто = случайный остров без дома и пирса, ноль ресурсов |
| Systems/ResourceSystem | ResourceSystem | logChanges | вкл/выкл лог ресурсов в консоль (дебаг) |
| Systems/StructureSystem | **StructureSystem** | **islandSystem, resourceSystem, spawnSystem** | Island, ResourceSystem, SpawnSystem (без SpawnSystem стройплощадка не видит юнитов на месте и ставит здание сразу) |
| | | siteLook → blueprintColor, groundColor | вид стройплощадки, пока у неё нет своего арта: множитель спрайтов чертежа (белый α0.45 — альфа = насколько прозрачен) и подложка по клеткам (песочный α0.25) — дефолты |
| | | siteLook → groundSortingLayer, groundSortingOrder | Ground, 1000 — подложка под ореолом госта (1001), чтобы гост над площадкой читался |
| | | siteLook → fenceThickness | в сцене 0.3 клетки (дефолт 0.25) — ширина полосы площадки забора вдоль ребра |
| Systems/SpawnSystem | **SpawnSystem** | **unitPool, unitSystem** | UnitPool, UnitSystem |
| | | islandSystem | Island (впрыскивается в юнитов — задел на будущее island-aware поведение) |
| | | soundSystem | SoundSystem (юниты берут его отсюда для своих звуков; пуф тоже звучит через него) |
| | | stuckSweepInterval | 0.5 с (дефолт): как часто ищутся застрявшие юниты — их отправляют в ближайший дом |
| | | puffFx, smallPuffFx | `Prefabs/FX/FX_Puff` и `FX_Puff_Small` — «пуф», с которым жители и звери исчезают с острова, и маленький для гусей (отдельный лист: уменьшенный большой сбил бы пиксели). Общие эмиттеры (World space, Looping, без своей эмиссии). Пусто — исчезают без эффекта |
| | | puffParticles | в сцене 1 (дефолт 8) — частиц на пуф любого размера: клуб нарисован флипбуком целиком |
| | | puffSound | SoundDef пуфа — в сцене `SoundDef/Puff_Air` (3 варианта звука, `Sound/Puff`) |
| Systems/UnitSystem | UnitSystem | — | (полей нет) |
| Systems/UnitPool | UnitPool | — | (полей нет; юниты инстанцируются его детьми) |
| Systems/PerkSystem | PerkSystem | catalogue | `ScriptableObjects/Perks/Perk Catalogue Def` (PerkCatalogueDef — список перков) |
| | | choicesOffered | 3 (дефолт): сколько карточек в выборе перка (особых бывает меньше — сколько осталось) |
| Systems/OfferingSystem | **OfferingSystem** | **schedule** | `ScriptableObjects/OfferingDef/Offering Schedule Def`. Пусто или без циклов — предупреждение, цикл выключен |
| | | **resourceSystem** | ResourceSystem — из неё платится квота |
| Systems/PrestigeSystem | **PrestigeSystem** | **saveSystem** | SaveSystem (на `Bootstrap`) |
| | | formula → agePoints | очки за вход в каждую эпоху по порядку: элемент 0 — Age II. Эпохи за концом списка платят последний элемент, пустой список — ничего. В сцене по 1 на каждую. Платится только то, что бьёт рекорд профиля |
| | | formula → minutesPerInterval, pointsPerInterval | 5 и 1 — очки за время: за каждые целые 5 минут живой игры, в каждом забеге, без рекорда. Остаток сгорает вместе с забегом; на любом экране поверх игры время стоит. 0 минут = время не платит |
| | | formula → weights, defaultWeight, coefficient, exponent | очки за собранное, тоже только сверх рекорда. **В сцене coefficient 0 — сбор очков не даёт** |
| | | — | выплата — при каре богов, в любой эпохе; забег, не побивший рекордов, получает только за время |
| | | upgrades | `Global Upgrade Catalogue Def` (GlobalUpgradeCatalogueDef) — мета-перки в порядке мета-экрана |
| Systems/AgeSystem | **AgeSystem** | **ages, resourceSystem** | список AgeDef-ассетов по порядку (начинается с Age II — у Age I дефа нет); ResourceSystem |
| Systems/AgeSequencer | AgeSequencer | islandSystem | Island |
| | | risePlayer | IslandRisePlayer на `IslandRise` (пусто = зона просто появляется, без подъёма) |
| | | cameraController | CameraController (камера едет к всплывающей зоне) |
| | | inputHandler | InputHandler на `Input` (тапы: ×3, пропуск, закрыть баннер; пусто = без тапов). Слушает `OnAnyClick` — клики и поверх UI, баннер сам UI |
| | | — (баннер) | своих полей нет: баннер (`Canvas/AgeTransition` + `BannerText`) назначен на `UIRoot`, бутстрап отдаёт его кодом (`AgeSequencer.Initialize`) |
| | | bannerFade, titleHold, bannerTapGrace | 0.5, 2, 0.4 (сек, дефолты): проявление/угасание баннера, сколько он висит, через сколько его можно закрыть тапом |
| IslandRise | **IslandRisePlayer** | **profile** | ассет `ScriptableObjects/Profiles/Island Rise Profile` — все ручки подъёма (тайминги, кривые, эффекты, звук, тряска). Слоты эффектов: bubblesFx `FX_Rise_Bubble_Glint` (1 → 3 пузыря в секунду к выныриванию), splashFx `FX_Rise_Spray`, touchDownFx `FX_Rise_Touch`, popFx `FX_Rise_Pop`, finaleFx `FX_Rise_Finale` (доля клеток 0.6). Остальные `FX_Rise_*` — запасные варианты |
| | | **islandSystem** | Island |
| Systems/TapSystem | **TapSystem** | **inputHandler** | InputHandler |
| | | radiusVisual | TapRadiusVisual на этом же объекте (ширина, материал и цвет кольца — на его LineRenderer). Кольцо видно, пока игра идёт и рука пуста: с карточкой или Sell в руке клик — стройки, и кольцо прячется |
| | | tapRadius, boostSpeedMultiplier, boostDuration | 0.5, ×2, 5 с (дефолты). Радиус и множитель — БАЗА: перки растят их статами `TapRadius` / `TapBoostMultiplier` |
| | | refreshStamina | выкл (дефолт): тап только ускоряет; вкл — ещё и восполняет выносливость |
| | | tapClip | звук тапа (`Sound/click`); пусто = тапы без звука. AudioSource не нужен — TapSystem создаёт его сам |
| | | tapVolume, tapPitchJitter | 0.6 и ±0.05 (дефолты): громкость щелчка и случайный разброс высоты на каждый тап, 0 = всегда одинаково |
| Systems/PlacementController | **PlacementController** | **inputHandler, structureSystem, resourceSystem, islandSystem, gridOverlay** | соответствующие компоненты (gridOverlay — на `Island/GridOverlay`). Камеры нет: курсор в мире берёт у `InputHandler` |
| | | visuals → validColor, invalidColor, sellHoverColor | цвета госта/наведения: гост валид/невалид, красный при продаже (есть дефолты) |
| | | visuals → glideDuration, glideEase, flightScale | 0.07 с, OutCubic, 0.9 — доезд призрака забора до нового ребра и его масштаб в пути (0 с = прыгает) |
| | | visuals → snapScale, snapDuration | кривая 1.12 → 0.97 → 1 за 0.12 с — щелчок призрака на ребро (последний ключ кривой держать на 1) |
| | | visuals → territoryValidColor, territoryInvalidColor, territorySortingLayer, territorySortingOrder | ореол занимаемой территории госта: зелёный/красный α0.18, слой Ground/1001 (дефолты) |
| Systems/HarvestVfxSystem | HarvestVfxSystem | viewCamera | Main Camera (опц. — пусто = `Camera.main` в `Awake`; нужна только чтобы не играть эффекты за экраном) |
| | | offscreenMargin | в сцене 0.033 (дефолт 0.1) — запас вокруг экрана в долях вьюпорта |
| | | resources | `ResourceIconSet/Resource Icon Set` — эффект подбора каждого ресурса, для источников без своего и для налога. Пусто = играют только эффекты дефов |
| | HarvestNumbers | **numberPrefab** | `Prefabs/FX/FX_HarvestNumber` (без него цифр нет, в консоли ошибка) |
| | | motion | `ScriptableObjects/HarvestNumberMotionDef/Harvest Number Motion` — вся подгонка ощущения: lifetime (0.9 с), travel (0, 0.8), кривые полёта/масштаба/альфы, spawnOffset, spawnJitter (0.15, 0.1) |
| | | viewCamera, offscreenMargin | как выше |
| | | maxConcurrent | 60 (дефолт) — потолок живых цифр; сверх него перерабатывается ближайшая к смерти |
| Systems/BumpVfxSystem | BumpVfxSystem | usefulWave | `Prefabs/FX/FX_Bump` — белые дуги удара, который сработал. Пусто = без волны |
| | | blockedWave | `Prefabs/FX/FX_Bump_Blocked` — тонкие янтарные молнии: объект для этого юнита, но мешает (устал, дом полон, кузница перегрета, рынок снаружи) |
| | | uselessWave | пусто (дефолт) — у бесполезного удара волны нет, хватает сквоша |
| | | coastRipple | `Prefabs/FX/FX_Coast_Ripple` — рябь на воде у берега. Пусто = без ряби |
| | | viewCamera, offscreenMargin | пусто (= `Camera.main` в `Awake`) и 0.1 — как у HarvestVfxSystem |
| | | pixelsPerUnit | 16 — центр волны снапится к пиксельной сетке арта, чтобы линии в 1 px ложились на целые пиксели; 0 = без снапа |
| Systems/WaterSystem | **WaterSystem** | **waterMaterial** | `Materials/M_Water` — цвет моря и штрихи. У плитки `Sprites/GamePlay/Water/water_flow` нужен Read/Write, иначе штрихов нет. Пусто = предупреждение в консоли и остров на фоне камеры |
| Input | **InputHandler** | **mainCamera** | Main Camera |
| Input | GameHotkeys | buildPanelKey, sellKey, exitToMenuKey | клавиши команд: B — спрятать / показать панель стройки, X — Sell, Esc (дефолты, правятся в инспекторе); цифры 1–9 — вкладки стройки (зашиты в коде) |
| CameraController | **CameraController** | **cameraTarget, vcam** | пустой `CameraTarget` (его и двигаем); CinemachineCamera — зум пишется в её `Lens > Orthographic Size`, значение в сцене перезаписывается стартовым видом |
| | | **islandSystem, viewCamera, inputHandler** | Island (кламп по острову); viewCamera = Main Camera (только для перевода пикселей перетаскивания в мир); inputHandler = InputHandler на `Input` — от него перетаскивание ПКМ. **Пусто — остров правой не двигается, молча** |
| | | zoomLevels, startZoomLevel | виды зума на `+` / `−`: Ortho Size от дальнего к ближнему — 16.875 / 11.25 / 8.4375 (×2 / ×3 / ×4 на 1080p); старт = 1 (классика) — дефолты. Любое значение безопасно: до целого масштаба пикселей его округляет `CinemachinePixelPerfect` |
| | | panSpeed, edgePanEnabled, edgeThickness | скорость WASD/стрелок (12), край-скролл (в сцене **выключен**, дефолт вкл), толщина края в px (12) |
| | | boundsMargin | насколько центр (= цель) может уйти за край острова (4) — дефолт |
| Main Camera | **CinemachineBrain** | Ignore Time Scale ✓ | пишет позицию из активной vcam — и при `timeScale = 0` |
| Main Camera | **PixelPerfectCamera** | Assets PPU 16, Reference Resolution 640×360, Upscale Render Texture ✗ | целый масштаб пикселей под экран. 640×360 = ширина Канваса → в классическом виде пиксель мира = пиксель UI. Upscale RT не включать — лесенкой станут наклоны, рябь, частицы |
| CinemachineCamera | **CinemachinePixelPerfect** | — | округляет Ortho Size vcam до ближайшего целого масштаба. Без него виды зума били бы пиксели |
| CinemachineCamera | **CinemachineCamera** (vcam) | Follow = CameraTarget; Body = CinemachineFollow (damping ~0.1–0.2) | плавно следует за целью |
| | CinemachineImpulseListener | Channel Mask 1 (дефолт) | без него толчки подъёма острова никто не почувствует; сам источник толчков IslandRise добавляет себе кодом |
| Island | **IslandSystem** | **tilemap, trimHorizontalTilemap** | `Grid/Ground` (суша с коллайдером), `Grid/TrimHorizontal` (горизонтальная кромка — полосы под сушей, без коллайдера) |
| | | trimVerticalTilemap | `Grid/TrimVertical` — вертикальная кромка: столбики по бокам суши, поверх полос. Внутренний угол залива — полоса плюс столбик, каждый в цвете своего биома, отдельных тайлов для него нет. Материал, слой сортировки и Order код берёт у TrimHorizontal (на один выше). Пусто = столбики ложатся на TrimHorizontal, у внутренних углов пропадает столбик, в консоли предупреждение. Тайлмап должен быть свой |
| | | decorTilemap | `Grid/Decor` — декор из списков Decor в тайлсетах; пусто = без декора. Тайлмап должен быть свой (не Ground / кромка / тайлмап границы) — иначе ошибка в консоли и декора нет. Слой сортировки Island, Order 1, материал `Materials/BendMaterial`, без коллайдера |
| | | coastWaves, waveHorizontalTilemap, waveVerticalTilemap | `TileSet/Coast Wave Set` (кадры пены художницы и их тайминг), `Grid/WaveHorizontal`, `Grid/WaveVertical`. Рисуются только в Play Mode. Пусто = без волн |
| | | coastShadowTile, coastShadowTilemap | `Tiles/Water/water_shadow_island`, `Grid/CoastShadow` — тень острова: один тайл под каждой полосой кромки. Пусто = без тени |
| | | projectionDark, projectionLight | спрайты `Water/water_shadow_bldngs` и `Water/water_reflect_bldings` — проекции построек на воде, общие для всех. Пивот сверху по центру: от него блок свисает с футпринта. Пусто = без проекций |
| | | **tileSet, biomes** | `TileSet/Tile Set Meadow` — тайлы для биома без своего тайлсета; биомы, из которых эпоха предлагает зоны (Meadow, Forest, Desert) |
| | | **borders** | мягкие границы между биомами, **СВЕРХУ ВНИЗ**: `Grass` → `TileSet/Brdr Set Meadow` → `Grid/BordersMeadow`; `Forest` → `TileSet/Brdr Set Autumn` → `Grid/BordersForest` (лес сейчас осенний). Порядок строк = кто кого накрывает (биом без строки — в самом низу, это пустыня). Строку луга держать даже без арта, иначе лес сочтёт луг нижним и наползёт на него. **Слой и Order тайлмапов границ ставит код по этому списку** (слой сортировки — как у `Ground`, то есть Island; над Ground / Decor / обеими кромками), введённое в инспекторе не важно. Повтор биома, общий тайлмап у двух строк или тайлмап земли / кромки — ошибка в консоли, строка не рисуется (общий с декором — не рисуется декор). Тайлмапы границ: материал `Materials/BendMaterial`, как у Ground (иначе не гнутся при подъёме острова), без коллайдера |
| | | **structureSystem** | StructureSystem — через неё зона получает природное содержимое (деревья, реки, стартовый дом) |
| | | cellSize, previewSeed | 1; сид для редакторского превью «Generate Island» (контекстное меню). Забег берёт сид из StartConfig |
| Island/Grid/Ground | CoastBump | southOutline, sideOutline, northOutline | 4 / 1 / 0 — на сколько арт-пикселей сдвинуть точку удара к воде: кромка берега нарисована на клетках воды (полоса под южным краем, столбик у восточного и западного, на севере ничего) |
| | | pixelsPerUnit | 16 |
| | | minDistance, minInterval | 0.75 и 0.25 с — рябь ближе этого к показанной меньше этого назад пропускается. Троттлинг по МЕСТУ, а не по объекту: берег — один объект на весь остров |
| | | bulgePixels, bulgeReach | 2 px и 2 клетки — насколько берег выгибается к воде под ударом и как далеко в стороны; 0 px = не выгибается. Гнутся тайлы с материалом `BendMaterial`, коллайдер стоит |
| | | bulgeFrequency, bulgeDamping | 8 Гц и 0.35 — пружина: чем выше частота, тем жёстче берег; 1 у затухания = без качания |
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
`ResourcePanel`, `ZoneSelectionUI`, `AgeAdvancePanel`, `AgeCostPanel`, `TimePanel`, `OfferingTooltip`,
`PerkPanel`, `OfferingWindow`. Поэтому у экземпляра Канваса в сцене нет ни
одного оверрайда со ссылкой на объект сцены — и появляться их не должно.

Сам Canvas несёт две таблицы, и они отвечают на разные вопросы:

| компонент | вопрос | что перечисляет |
|---|---|---|
| **UIRoot** | чем код **снаружи** UI управляет сам | 4 экрана (зона, перк, окно подношения, мета) + баннер эпохи (см. ниже) |
| **UIVisibility** | кто **когда** виден | CanvasGroup'ы + маска режимов |

Элемент, которому не нужно ни то ни другое, не попадает ни в одну (`BuildToggle`), а видимый
по режиму — только во вторую (`BuildPalette`, `SellButton`, `BuildTabs`, `TimePanel`). `ConfirmDialog` и `HintView` нет в таблице видимости:
свою CanvasGroup каждый ведёт сам.

| Объект | Компонент | Поле | Что назначить |
|--------|-----------|------|---------------|
| Canvas | **UIRoot** | **zoneScreen, perkScreen, offeringWindow, metaScreen** | `Screens/ZoneScreen`, `Screens/PerkScreen`, `Screens/OfferingWindow`, `MetaScreen` — их зовут гейплейные стейты. Без окна подношения конец цикла не может спросить, и забег не кончится никогда — ошибка на старте |
| | | ageBanner, ageTitle | `AgeTransition` (CanvasGroup), `AgeTransition/BannerText` (TMP_Text) — баннер эпохи; бутстрап отдаёт их `AgeSequencer`. Пусто — предупреждение, эпохи сменяются без баннера |
| Canvas | **UIVisibility** | groups | 9 строк, таблица ниже |
| Canvas/Hud/BuildToggle | BuildPanelToggle | button, label, hideLabel, showLabel | свой Button; label — TMP_Text надписи (опц., **в префабе пусто**); «B» / «B» — надпись, пока панель видна / спрятана. Хозяин «показана / спрятана»: клик и B переключают, X и 1–9 возвращают спрятанную панель, прятание кладёт инструмент из руки |
| Canvas/Hud/BuildTabs | **BuildTabBar** | palette | `ScriptableObjects/BuildMode/Build Palette Def` — без него нет ни вкладок, ни карточек |
| | | tabPrefab, container | префаб `Prefabs/Elements/BuildTabUi`; container пусто = сам `BuildTabs` (его Vertical Layout Group) |
| Canvas/Hud/BuildPalette | **BuildPaletteUI** | cardPrefab, cardContainer | префаб `Prefabs/Elements/BuildCard`, дочерний `Viewport/CardRow` |
| | | scroller | BuildPanelScroller на `Viewport` |
| Canvas/Hud/SellButton | SellButtonUI | button | свой Button. Его Sprite Swap — вид с пустой рукой (`bttn_buildmode_sell` / `_hover` / `_press`); **Navigation None и Selected Sprite пуст** — иначе фокус UI оставляет кнопку «включённой» после выключения режима |
| | | activeSprite, activeHoverSprite | `bttn_buildmode_sell_active` / `_active_hover`: вид и наведение, пока Sell в руке (нажатие — прежнее `_press`) |
| | | soldSprite, soldSeconds | `bttn_buildmode_sell_active_process` (с плюсиком) на `soldSeconds` (0.2 с, unscaled) после каждой продажи; протяжка забором — одна вспышка на всю протяжку |
| Canvas/Hud/ResourceBar | ResourcePanel | unitPrefab, container, iconSet | `Prefabs/Elements/ResourceUnit`, себя, ResourceIconSet |
| Canvas/Hud/PerkPanel | PerkPanel | slotPrefab | префаб `Prefabs/Elements/PerkSlot` (его PerkSlotUI). Сетка на том же объекте: Cell 20×20, Spacing 1, Fixed Column Count 2, **Start Corner и Child Alignment — Upper Right** (одного Start Corner мало: пока перков меньше двух, сетка сужается до одной колонки и прижимает её по Child Alignment — одиночный перк встал бы влево); Content Size Fitter — Vertical Fit Preferred |
| Canvas/Hud/TimePanel | **TimePanel** | now, belt, riders, hands | `Now`, `Track/Belt`, `Track/Riders`, `Offering/Icon`. Руки ждут там, где стоят в префабе, — по центру большого ромба: с этого столбца ромб уезжает, и руки уезжают с ним без рывка |
| | | bigDiamond, dots, standDots | `Offering/Point` — большой ромб на стоянке (пока подношение в пути, код его прячет, а лента везёт его копию с тем же спрайтом), `Dots`, `StandDots` (необязательно). Зазор перед маленьким ромбом код берёт из префаба — сколько `Dots` отстоят от `Offering/Point` |
| | | lineSprite, yearSprite, offeringSprite | `time_line`, `time_icon_year`, `time_icon_halfdecade`. Засечку подношения скрипт режет на три части при старте: края линии идут вокруг большого ромба на ленте, середина — маленький ромб на стоянке. Поэтому её спрайт нельзя паковать в атлас плотно (Tight), только прямоугольником |
| | | segmentWidth, diamondWidth | 58 — длина отрезка-«года» в пикселях (скорость ленты = 58 px за Segment Seconds расписания); 5 — ширина маленького ромба в середине засечки подношения |
| | | yearTemplate, tooltip | `Years/Year` (шаблон цифры, его якоря и выравнивание скрипт не читает — центрирует сам), `Tooltip` |
| Canvas/Hud/TimePanel/Tooltip | OfferingTooltip | header, costContainer, anchor | `Header`, `Costs` (её Grid Layout Group: Cell 34×13, Spacing 2×3, Fixed Column Count 2, Child Alignment Upper Center), `Offering/Icon` — куда смотрит хвостик |
| | | costPrefab, iconSet, shortColor, highlight | `Prefabs/Elements/ResourceUnit`, ResourceIconSet, #9B211E — ресурса не хватает, #FFD663 — слово Offering |
| | | offset | (−2, −8): правый верхний угол подсказки (с хвостиком) от правого нижнего угла рук. Vertical Layout Group на корне: Padding 5 / 6 / 8 / 3, Spacing 3, Control Child Size — ширина и высота, Force Expand — ширина; **Content Size Fitter не вешать** — размер считает скрипт |
| Canvas/Hud/AgePanel | AgeAdvancePanel | titleLabel, bonusValueLabel, bonusTextLabel | `Age/Title`, `Age/BonusValue`, `Age/BonusText`. Бонус из данных («+10% ALL PRODUCTION») делится по первому пробелу: число — в первой строке, остальное — во второй |
| | | noBonusText, nextAgeButton | «NO BONUS» — у эпохи без бонуса (Age I без AgeDef или пустой бонус в данных): одна строка посередине между строками бонуса, высоту скрипт берёт из их позиций в префабе; `AdvanceButton` |
| Canvas/Hud/AgePanel/Cost | AgeCostPanel | unitPrefab, container, iconSet | `Prefabs/Elements/ResourceUnit Variant For Age` (не сам ResourceUnit — см. его раздел), себя (Grid Layout Group), ResourceIconSet |
| | | unaffordableColor, visibilityRoot | #9B211E — сумма, которой не хватает (её иконку ещё и обводит кольцо); что гасить, когда цены нет: следующая эпоха бесплатна или покупать больше нечего (пусто = сам Cost) |
| | | freeLabel | `AgePanel/Free` — надпись вместо цены бесплатной эпохи. Должна лежать вне visibilityRoot, иначе погаснет вместе с ценой |
| Canvas/Screens/PerkScreen | PerkSelectionUI | cardPrefab, cardContainer, spacing | префаб `Prefabs/Elements/PerkCardUI`, `Window/Cards`, 3 px между карточками. Окно на три карточки, меньше — по центру |
| Canvas/Screens/ZoneScreen | ZoneSelectionUI | cardPrefab, cardContainer | префаб `Prefabs/Elements/ZoneCardUI`, `Panel/CardRow` |
| | | focusOffsetY | 0.15 (дефолт) — насколько выше центра экрана встаёт зона под карточкой |
| Canvas/Screens/OfferingWindow | **OfferingWindow** | **payButton, wrathButton** | `Window/PayButton` (его Button), `Window/WrathButton` (его HoldButton). Без любой из них окно не может спросить — `OfferingEndState` тогда не замораживает игру, а берёт что может |
| | | payGroup, payDisabledAlpha | CanvasGroup на `PayButton` (необязательно), 0.5 — прозрачность всей кнопки, пока запасов не хватает (кнопка при этом и не нажимается) |
| | | costArea, costPrefab, iconSet | `Window/Demand/Costs`, `Prefabs/Elements/ResourceUnit Variant For Offering` (светлая подложка, см. раздел ResourceUnit), ResourceIconSet |
| | | perRow, spacing, shortColor | 3, (6, 0) — по сколько в ряд и зазоры; #9B211E — ресурса не хватает. Ячейку код берёт из размера корня префаба юнита (34×13) |
| Canvas/Screens/OfferingWindow/Window/WrathButton | HoldButton | holdDuration, fill | 1 с удержания (0 = обычный клик на отпускании); `Fill` — его размер в префабе и есть полная заливка, якорь слева |
| надписи окна | PixelSnapText | — | полей нет: ставит каждую строку TMP на целый пиксель (центр по краям букв, базовую линию от верхнего края). Рамка надписи должна стоять на целых пикселях, шрифт — в чётком размере |
| Canvas/HintView | HintView | titleText, bodyText, costContainer, footerText | свои дети; секция с пустым содержимым прячется сама. Встаёт над элементом или слева от него — как попросит источник (`HintShowEvent.Side`; карточки стройки — сверху, ячейки перков — слева) |
| | | costPrefab, iconSet, unaffordableColor | ResourceUnit, ResourceIconSet, #9B211E — цена, которую не потянуть |
| | | maxWidth, gap, screenMargin, showDelay, chainWindow | 160, 10 (дефолт 4), 2, 0.25 с, 0.3 с: ширина до переноса, отступ от элемента и от края экрана, задержка появления, окно «перехода» между соседними элементами без задержки |
| Canvas/MetaScreen | **MetaUpgradesUI** | slotPrefab, slotContainer, pointsText, tip, playButton | префаб `Prefabs/Elements/MetaUpgradeSlot`, `Grid`, `Points/PointsText`, `Tip`, `NextIslandButton` |
| префаб MetaUpgradeSlot | MetaUpgradeSlotUI | icon, costLabel | `Icon`, `Cost` (цена в углу; у купленного скрыта) |
| | | takenIconColor, notTakenIconColor | белый и белый α 0.18 — иконка купленного и некупленного |
| | | affordableCostColor, unaffordableCostColor | #6B85D2 — свободных очков на цену хватает, #E04840 — нет |
| Canvas/MetaScreen/Tip | **MetaUpgradeTipUI** | text | `Tip/Text` |
| | | titleColor, maxWidth, offset, screenMargin | #A8B4D6 — цвет названия; 83 — ширина до переноса; (0, −2) — левый верхний угол подсказки от правого верхнего угла ячейки; 2 — отступ от края экрана |
| Canvas/ConfirmDialog | ConfirmDialog | messageText, yesButton, noButton (+ yesLabel, noLabel опц.) | разведены внутри префаба `ConfirmDialog`; текст кнопок в префабе — по умолчанию. Сейчас не вызывается: окно конца цикла — `Screens/OfferingWindow`. На `UIRoot` ссылки на него больше нет |

#### Новый элемент Канваса, которому нужна система

1. Компонент реализует `IUIElement` — `public void Initialize(UIServices ui)` — и берёт из `ui` только своё.
2. Кладём его куда угодно под `Canvas`. Ни поля на `UIRoot`, ни перетаскивания, ни оверрайда в сцене.
3. `Initialize` приходит из `GameBootstrap.Awake` и может опередить собственный `Awake` элемента: там только
   запомнить, а рисовать — начиная со `Start`.
4. Нужной системы в наборе нет — одно поле в `UIServices` (названное по типу), одна строка там, где его заполняет
   `GameBootstrap`, и одна в `UIServices.WarnMissing`. Если у бутстрапа нет и самой ссылки — ещё поле на `GameBootstrap`.

Заспавненное в рантайме (карточки, юниты ресурсов) `IUIElement` не реализует: нужное отдаёт тот, кто его создал.
Поле на `UIRoot` нужно в обратном случае — когда игра сама управляет элементом снаружи (экран стейта, баннер).

#### Шрифты и тень текста

Пиксельный шрифт чёткий только в своих размерах: **m6x11plus — 18 и 36**, **Micro5 — 11 и 22**. В других пиксель
шрифта ложится между пикселями экрана, и текст мылится. Даже в чётком размере строку TMP может сдвинуть на
полпикселя центрирование — поэтому центрованные надписи ставит на целые пиксели код: `PixelSnapText` на надписи
(окно подношения) или центровка в скрипте панели (`AgeAdvancePanel`, годы `TimePanel`).

Тень — не отдельный объект, а материал-пресет шрифта с подложкой (Underlay) на 1 px вниз, цвет #2D2D2B:
`Fonts/m6x11plus SDF - Shadow` и `Fonts/Micro5-Regular SDF - Shadow` (выбираются в TMP_Text → Material Preset).
Сдвиг подогнан под 18 и 11 соответственно и растёт с размером шрифта: у заголовка окна подношения (36) тень
2 px — один пиксель самого шрифта. В пресетах включён `RATIOS_OFF` — без него TMP пересчитывает сдвиг, и тень
не доходит до целого пикселя. Пресет TMP находит по первому слову имени и тому же атласу — имена не менять.

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
| 7 | `Hud/TimePanel` | ✓ | — | — | — | — | ✓ | — |
| 8 | `Screens/OfferingWindow` | — | — | — | — | — | ✓ | — |

Строка 0 читается как «только живая игра»: на выборе зоны и перка, подъёме острова, мета-экране и в окне конца
цикла HUD скрыт целиком, панель эры и панель перков вместе с ним (своих строк у них нет). Поэтому смена цены и
высоты блока эры игроку не видна: HUD возвращается уже с новыми ценами. Строки 4–6 — три виджета стройки всю
живую игру, пока игрок их не спрятал. Строка 7 — лента времени: у её CanvasGroup включён **Ignore Parent Groups**,
поэтому она не зависит от `Hud` и видна ещё в окне конца цикла — ромб подношения стоит в кружке над окном (на
макете окна ленты нет — снять `OfferingEnd` в строке 7, если её там не нужно). Строка 8 — окно подношения, только
в своём режиме. **В окне конца цикла запасов не видно** — если их нужно видеть, отметить `OfferingEnd` у `Hud`.
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
Root          → Rigidbody2D (Static) + Structure  [+ компоненты поведения, таблица ниже] [+ BumpView или LeanView]
├── Visual    (origin у основания — от него сквош и наклон)
│   └── Model → SpriteRenderer (пивот у основания) + SpriteShadow   (+ декор детьми)
├── Fx_Anchor (пустышка: откуда вылетает отклик сбора — у источников, см. fxAnchor)
├── TaxFx_Anchor (пустышка: откуда вылетает налог, — у дома, см. taxFxAnchor)
└── Physics   → Collider2D (Box / Circle / Polygon — по форме)
```
- Rigidbody2D всегда на ROOT, чтобы `OnCollisionEnter2D`/`OnTriggerEnter2D` доходили до скрипта.
  Препятствие (отскок) или проходимая (триггер) — решает только флаг **Is Trigger** на коллайдере.
- **`Visual` — всё, что сквошится или наклоняется** (`BumpView` / `LeanView` двигают именно его, не корень — иначе
  поехали бы коллайдеры). Что должно стоять на месте — полоска нагрева кузницы (`HeatBar`), `Fx_Anchor` у того, что
  сквошится, — кладётся на корень, вне `Visual`.
- **SpriteShadow** — тень строится из спрайта в `Start`, руками ничего не рисуется: castShadow, offsetPixels (0, 1),
  alpha (30/255). У госта стройки тени нет by design.
- **WaterProjection** — на КОРЕНЬ префаба (не на Model): тёмный и светлый блоки на воде перед постройкой, суша закрывает
  их сама. Спрайты общие (`IslandSystem.projectionDark`, `projectionLight`); поле lightInsetPixels (2) — на сколько
  арт-пикселей светлый блок уже тёмного с каждой стороны. Стоит на House, Market, Mill, RicePaddy, Sawmill, Smithy, Stable, Tavern, GooseCoop
  и 5 стойках; у пирса, пшеницы, забора и природы его нет. У госта и чертежа стройки проекций нет by design.

Компоненты поведения (на корне, комбинируются):

| Компонент | Поле | Что назначить |
|-----------|------|---------------|
| **Structure** (у любой постройки) | def | её StructureDef |
| | taxFxAnchor | откуда вылетает налог — ресурс, который постройка доплачивает за удар по перку (`AdditionalResourcePerHit`, перк Taxes). Пусто = корень. Отдельно от fxAnchor источника; сейчас назначен у House (`TaxFx_Anchor`) |
| **Spawner** (дом: выпускает юнитов) | spawnSystem | SpawnSystem в сцене *(или впрыснется в рантайме при постройке)* |
| | unitDef | UnitDef — какой юнит живёт в доме |
| | capacity | число слотов (≥1) — база, перки растят статом `HouseCapacity` |
| | restDuration | 3 с — отдых внутри перед вылетом |
| | launchSpeedMultiplier, launchBoostDuration, launchGap, launchJitterDegrees | параметры вылета (2.5, 1, 0.1, 12°) |
| **ResourceSource** (платит ресурс за удар) | def | ResourceSourceDef |
| | resourceSystem | ResourceSystem *(или впрыснется в рантайме)* |
| | fxAnchor | пустышка-ребёнок, откуда вылетают колосок и цифра (пусто = корень). У того, что сквошится (`BumpView`), — `Fx_Anchor` на корне, не внутри `Visual`: источник, который не кончается (рынок, кузница, мельница), платит прямо во время сквоша. У пшеницы и куста (только наклон, а на сборе наклон гаснет) — сам `Visual` |
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
| **Sawmill** (лесопилка: торцы по очереди) | endReach | 0.5 клетки — насколько торец заходит на верхний и нижний край (0 = только сами торцы, больше половины скамьи не бывает); зоны видны гизмо, когда лесопилка выделена |
| **SawmillView** (вид лесопилки) | blade, bladeFrames | `Visual/Blade` (стоит у ЛЕВОГО торца — правый зеркальный) и 4 кадра диска `bldng_sawmill_blade_00…03` |
| | travelTime, spinFps, idleSpinFps | 0.6 с от торца до торца; 16 и 6 кадров/с — в пути и на стоянке |
| | logBack, logFront | `Visual/Log_Back`, `Visual/Log_Front` — бревно, между частями которого идёт диск |
| | newLogDelay, dropTime, dropPixels | 0.3 с после раскола, 0.2 с падения, с 4 px — новое бревно падает, а не проявляется |
| | halfBack, halfFront | `Visual/Half_Back`, `Visual/Half_Front` — половинки, рендереры ВЫКЛЮЧЕНЫ, стоят там, где ложится раскол |
| | splitTime, backRollPixels, frontRollPixels | 0.45 с; задняя откатывается на 5 px назад, передняя на 8 px вперёд, обе гаснут |
| **GooseCoop** (гусятник) | goosePrefab | `Prefabs/Nodes/Goose` |
| | flockSize, restTime | 3 птицы (база, перк Geese x3 — стат `DenCapacity` со скоупом Goose); 0 с — отдых пойманной птицы в загоне, 0 = готова сразу |
| | releasedBy | профессии, чей рабочий удар выпускает стаю; в префабе все, вместе с Unassigned. Пустой список = любой пип |
| | releaseGap, releaseJitterDegrees | 0.3 и 25° — выход через свободную сторону: разброс крутит только полёт, а не точку выхода |
| | releaseSound, pen | звук выпуска (пусто); `Visual` (его GoosePenView) |
| **GoosePenView** (птицы в загоне) | modelTemplate, penArea, turnInterval | `Visual/Pen Bird Template` (выключен), прямоугольник загона (0.125, 0.3125) 0.625×0.25 — жёлтая рамка гизмо, 0.6–2.5 с между поворотами |
| **WaterBed** (река) | wall | `Physics` (его BoxCollider2D) — гаснет, пока над клеткой стоит мост |
| **MountainVisual** (гора) | model, topLeft … bottomRight | `Visual/Model`, девять кусков `mountain_9tiles` в порядке чтения листа |
| | northWestCorner …, innerNorthWest … | рендереры `Visual/Corner NW / NE / SW / SE` и их спрайты внутренних углов. **Спрайты пока пусты** — углы не рисуются, ждут арт |
| **RandomVariant** (камень) | variants | `Model_Ready/Variant_1…3` — при спавне остаётся включён один; в префабе включён первый |
| **VisitZone** (проход рынка) | hitsPerVisit | 3 засчитанных удара за заход — база, стат `MarketVisitHits` сверху |
| **Pier** | — | маркер без полей (см. «Пирс») |
| **BumpView** (отклик на отскок: сквош + волна) | squashTarget | `Visual`. Пусто = без сквоша, только волна (так у Pier и RicePaddy) |
| | squashHeight, widthFollows, squashDuration | кривая высоты сквоша от удара (0) до покоя (1): 0.94 → 1.03 → 1; ширина следует за высотой (1 — площадь сохраняется); 0.2 с |
| | waves | вкл (дефолт) — волна из задетой грани через `BumpVfxSystem`. **Выкл у BaseTree и Rock**: у ресурсов только тряска, лес в дугах — шум |
| | blockedSquash, uselessSquash | 0.5 и 0.35 — доля сквоша у удара Blocked (для этого юнита, но мешает) и Useless (не для него) |
| | minInterval | 0.15 с — удары внутри интервала после показанного пропускаются, но более сильный вердикт проходит всегда |
| **LeanView** (наклон проходимого за юнитом: BaseWheat и его вариант Bamboo, Bush, Mushrooms, пять `Rack_*`) | leanTarget | `Visual` (origin на линии земли) |
| | maxTilt, maxStretch, squatOnly | 8°, 0.06 высоты, выкл — наклон вбок, вытягивание вверх по экрану / приседание вниз; squatOnly — только приседание |
| | frequency, damping | 3 Гц, 0.3 — пружина: качается и успокаивается |
| | blockedLean, uselessLean | 0.6 и 0.6 — доля наклона у прохода Blocked / Useless |

> Природные источники (дерево, пшеница, камень) — это `Structure` + `ResourceSource` (+ `ResourceSourceView`) **без** `Spawner`.
> Юниты появятся только если у `Spawner` заполнены `spawnSystem` + `unitDef` (цикл стартует в `Spawner.Start()`).

**Ноды, которые отрастают** (`depletion = Regrow` в дефе), имеют два визуальных рута — `ResourceSourceView`
показывает один из них по состоянию. Арт каждого состояния (спрайт + Sorting Layer + пивот) живёт в префабе, не в дефе:
```
Root                → Rigidbody2D (Static) + Structure + ResourceSource + ResourceSourceView
├── Visual
│   ├── Model_Ready     → SpriteRenderer (Sorting Layer Entities, пивот у основания) + SpriteShadow
│   └── Model_Harvested → SpriteRenderer (Sorting Layer Ground — юниты всегда проходят поверх)
└── Physics         → Collider2D            (гасится, пока нода собрана)
```
> Коллайдер НЕ кладётся внутрь рутов: `CollisionTarget` собирает коллайдеры в `Awake` через
> `GetComponentsInChildren`, который не видит выключенные объекты. Начальное состояние выставит код;
> выключенный в префабе `Model_Harvested` просто убирает мигание в первый кадр.
> `Never`-источники (не истощаются) и `Despawn` (исчезают) второй рут не используют.
> Источник, который теряет тело (дерево, берёза, камень), отрастает, только когда на клетках его постройки никого нет:
> ни жителя, ни гуся. Отладка в Play (правая кнопка по заголовку компонента): `ResourceSource` → Deplete Now / Regrow Now,
> `Structure` → Deplete Sources / Regrow Sources (вся клетка леса сразу).

**Камень** (`Prefabs/Nodes/Rock`): `Model_Ready` — `RandomVariant` с тремя раскладками (`Variant_1` включён, остальные
выключены), у каждого куска свой SpriteRenderer (Entities) + SpriteShadow; `Model_Harvested` — щебень на слое Ground,
`swapStateVisuals` вкл.

**Бамбук** (`Prefabs/Nodes/Bamboo`) — вариант префаба `BaseWheat`: другие def, спрайты и ResourceSourceDef, всё
остальное от пшеницы.

**Лес** (`BaseForest`) = `Structure` + `DualVisual` с двумя раскладками деревьев (`Visual Left` / `Visual Right`) —
какая включится, решает чётность ряда, чтобы соседние леса вставали «кирпичиком». Каждое дерево внутри —
вложенный префаб `BaseTree` со своими `ResourceSource` + `ResourceSourceView`.

**Река** (`Prefabs/Nodes/River`) — одна клетка русла, ставит только генератор (у дефа `isWater` вкл,
`canSell` выкл). Кусок и поворот выбирает код по соседним клеткам реки:
```
Root          → Rigidbody2D (Static) + Structure + RiverVisual + WaterBed (wall → Physics)
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
- Материал `Model` — `Materials/RiverMaterial`: блики в глубине шейдером, **временно**, до кадров художницы. Спрайт реки
  должен быть отдельной текстурой, не куском листа; цвета мелководья и глубины в материале — как в арте.

**Мост** (`Prefabs/Buildings/Bridge`, 1×1) — ставится только на клетку реки (у дефа `onWater` и `passable` вкл); река
остаётся под ним, гаснет только её стенка (`WaterBed`), и только когда мост достроен:
```
Root      → Structure   (ни Rigidbody2D, ни коллайдера: по мосту ходят, отскакивать не от чего)
└── Visual/Model → SpriteRenderer (Default, Order 1 — поверх реки; Y −0.0625)
```

**Гора** (`Prefabs/Nodes/Mountain`) — клетка массива, ставит только генератор; кусок и углы выбирает код по восьми
соседям:
```
Root          → Rigidbody2D (Static) + Structure + MountainVisual
├── Visual    (Y 0.5 — центр клетки)
│   ├── Model → SpriteRenderer (Default, Order 0)
│   └── Corner NW / NE / SW / SE → SpriteRenderer (Default, Order 1, компонент выключен) — внутренние углы поверх куска
└── Physics   → BoxCollider2D 1×1, Offset Y 0.5
```
- Спрайты с пивотом по центру, без тени. Угол рисуется только своими пикселями, без дыр внутри; `flipX` нельзя — свет слева.
- Горой клетку делает сам `MountainVisual` на корне, отдельной галочки в дефе нет.

**Рынок** (`Prefabs/Buildings/Market`, 3×3): пять спрайтов под `Visual` (`Tower_Up`, `Wall_Up`, `Flags`, `Tower_Bottom`,
`Wall_Bottom`), все на Entities с Order 0 — порядок даёт Y-сортировка, поэтому юнит в проходе идёт между стенами. Проход
горизонтальный: стены — два BoxCollider2D 3×1 на `Physics`, между ними `Passage` (триггер 2.73×1.22 + свой
Rigidbody2D Static + VisitZone).

**Лесопилка** (`Prefabs/Buildings/Sawmill`, 3×1) — диск идёт МЕЖДУ частями бревна и стола, поэтому порядок задаёт
`SortingGroup` на `Visual`, а не Y: лесопилка сортируется как одно здание по своему основанию, внутри — `sortingOrder`:
```
Root            → Rigidbody2D (Static) + Structure + ResourceSource (def Sawmill, fxAnchor → Fx_anchor)
│                 + Sawmill + SawmillView + BumpView (squashTarget → Visual) + WaterProjection
├── Visual      → SortingGroup (Entities, 0)
│   ├── Yard        order 0   — двор 48×16 = футпринт
│   ├── Table_Back  order 10  — задняя полоса столешницы с прорезью + SpriteShadow (0, 2 px)
│   ├── Half_Back   order 15  — половинка бревна, рендерер выключен
│   ├── Log_Back    order 20  — верхняя полоска коры
│   ├── Blade       order 30  — диск, у левого торца
│   ├── Log_Front   order 40  — остальное бревно
│   ├── Table_Front order 50  — передняя часть стола
│   └── Half_Front  order 55  — половинка бревна, рендерер выключен
├── Physics     → BoxCollider2D 3×1 (Y 0.5, Unit Physics Material 2D) — от него меряется endReach
└── Fx_anchor   (0, 1) — над бревном
```
- Порядки — с шагом 10: `SpriteShadow` рисует тень на order − 1, она не должна влезать между соседями.
- Тень только у `Table_Back`: тень вверх у остальных легла бы на столешницу или повисла в воздухе.
- Всё, что двигает `SawmillView`, в префабе стоит в покое (диск слева, бревно целое, половинки выключены) — призрак
  размещения показывает префаб как есть.
- Арт — заглушка (`Sprites/GamePlay/Buildings/Sawmill/`), иконка карточки склеена из тех же кусков.

### Гусятник и гуси (GooseCoop, Goose)
```
GooseCoop (2×1)  → Rigidbody2D (Static) + Structure + GooseCoop + BumpView (squashTarget → Visual) + WaterProjection
├── Visual       → SortingGroup (Entities, 0) + GoosePenView
│   ├── Model    → SpriteRenderer (order 0) + SpriteShadow (0, 2 px)
│   └── Pen Bird Template (выключен) → SpriteRenderer (order 2) + SpriteShadow   ← образец птицы в загоне, копии в рантайме
└── Physics      → BoxCollider2D 1.9×0.9 (Y 0.5)

Goose            → Rigidbody2D (DYNAMIC, Gravity 0, Continuous, Freeze Rotation Z) + CollisionTarget
│                  + ResourceSource (def Goose, fxAnchor → Fx_Anchor) + Goose
├── Model        → SpriteRenderer (Entities, арт смотрит ВПРАВО — код отражает по полёту) + SpriteShadow
├── Physics      → CircleCollider2D 0.125 + Unit Physics Material 2D, слой **Goose** — отскакивает от стен
├── Hitbox       → CircleCollider2D 0.1875, Is Trigger ВКЛ, слой **Default** — по нему гуся ловят юниты
└── Fx_Anchor
```
- **Слой Goose (7)** в матрице Physics 2D: Goose×Unit и Goose×Goose OFF — пипы летят сквозь птицу, её ловит только Hitbox.
- Поля `Goose`: speed (2), body → `Physics`, hitbox → `Hitbox`, model, releaseImmunity (1.5 с после выпуска не поймать),
  minBounceAngle (10°) и bounceJitterDegrees (3°) — как у юнита, stuckRadius (1) и stuckWindow (5 с) — застрявший летит домой.
- Кто и сколько получает за гуся — `ResourceSourceDef/Goose`: Unassigned 1, Farmer 1, Hunter 2 еды; `Despawn`, один удар.
- Продали гусятник — гуси на острове исчезают малым пуфом. Стройплощадка или закрывшаяся река под птицей — птица домой.

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

### FX_Pickup_* (эффект сбора)
Префаб с одной `ParticleSystem`: колосок из поля, бревно из дерева, монета с рынка. Обычный эффект ресурса — в
`ResourceIconSet` (поле `pickupFx` в строке ресурса); свой у источника — в `ResourceSourceDef.pickupFx`, он главнее.
**Не в сцену и не в префаб ноды** — систему инстанцирует `HarvestVfxSystem`, по одному общему эмиттеру на ПРЕФАБ
эффекта (пшеница и мельница с одним колоском делят один), и переиспользует до конца сессии.

Три требования, которые система проверяет и о нарушении пишет в консоль с именем ассета:

| Настройка | Значение | Почему |
|-----------|----------|--------|
| **Main → Simulation Space** | **World** | эмиттер один на все поля и переезжает в точку каждого сбора; в Local уже летящие колоски утащило бы за ним |
| **Main → Looping** | **ON** | система играется один раз и дальше кормится вручную; не-зацикленная сама остановится после Duration и молча проглотит все последующие сборы |
| **Emission → Rate over Time / Distance** | **0** | иначе эмиттер будет сыпать частицы в точке последнего сбора между ударами (это warning, не ошибка) |

Сколько частиц за сбор — не в префабе, а в `pickupFxCount` рядом с эффектом (в дефе или в `ResourceIconSet`; меньше 1 = 1).
Смещение относительно якоря ноды — **Shape → Position** внутри самого префаба: якорь отвечает
«откуда вообще идёт отклик у этого объекта», точная посадка эффекта — дело эффекта.
Слой сортировки — в модуле **Renderer**: `Overlay`, `Order in Layer` = 0 (цифра идёт поверх, см. ниже).

В плеймоде живые эмиттеры видны детьми объекта `Systems/HarvestVfxSystem` как `PickupFx_<имя префаба эффекта>` —
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

### FX_Bump, FX_Bump_Blocked, FX_Coast_Ripple (волны ударов и рябь у берега)
Префаб с одной `ParticleSystem`: одна частица на удар — пиксельный флипбук. Кладутся в поля
`Systems/BumpVfxSystem` (см. чек-лист), не в сцену и не в префабы построек: система делает по одному общему эмиттеру
на вердикт и на рябь (в плеймоде — дети `BumpVfxSystem`: `BumpWaveUseful`, `BumpWaveBlocked`, `CoastRipple`).
Три требования те же, что у `FX_Pickup_*` (World, Looping, Emission 0), система так же проверяет их и пишет в консоль.

| Префаб | Материал → лист | Кадры | Lifetime | Слой, Order |
|--------|-----------------|-------|----------|-------------|
| `FX_Bump` (Useful) | `M_Bump_Wave` → `fx_bump_wave` — белые дуги | 7 | 0.35 | Overlay, 0 |
| `FX_Bump_Blocked` | `M_Bump_Blocked` → `fx_bump_blocked` — три тонкие молнии, янтарь `E08C25` | 6 | 0.28 | Overlay, 0 |
| `FX_Coast_Ripple` | `M_Coast_Ripple` → `fx_coast_ripple` — кольцо | 6 | 0.42 | **Island, −10** |

- **Texture Sheet Animation:** Grid, Tiles = кадры × 1. Кадр 32×32 при PPU 16, поэтому **Start Size 2** — один
  арт-пиксель на пиксель. Листы — в `Sprites/GamePlay/Misc/Bump`, Filter Mode Point.
- **Цвет запечён в лист**, Start Color белый. Обводки нет: линии в 1 px.
- **Волна удара нарисована под ВЕРХНЮЮ грань**: дуги (молнии) начинаются в центре кадра и идут вверх. Для остальных
  граней систему поворачивает код четвертями оборота — арт рисовать только в этой ориентации и не масштабировать.
- **Рябь не поворачивается** — кольцо вокруг центра кадра. Слой Island с Order −10: над водой, но под тайлами
  острова, поэтому суша прячет половину кольца, которая легла бы на неё. В YAML рендерера ID слоя Island знаковый
  (`m_SortingLayerID: -661730883`, `m_SortingLayer: -2`).
- Листы — заглушки, сгенерированные скриптом; художник перерисует их в той же раскладке.

### FX_Puff, FX_Puff_Small (пуф исчезновения)
Одна `ParticleSystem`, одна частица на пуф — флипбук из 6 кадров, Lifetime 0.4, слой Overlay, Order 0. Кладутся в
`SpawnSystem.puffFx` / `smallPuffFx`; требования общих эмиттеров те же, что у `FX_Pickup_*`.

| Префаб | Материал → лист | Кадр, Start Size | Для кого |
|--------|-----------------|------------------|----------|
| `FX_Puff` | `M_Puff` → `Misc/Bump/fx_puff` | 32×32, 2 | жители и звери |
| `FX_Puff_Small` | `M_Puff_Small` → `Misc/Bump/fx_puff_small` | 16×16, 1 | гуси |

Листы — заглушки, перерисовывать в той же раскладке (кадр / 16 = Start Size, чтобы пиксель ложился на пиксель).

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
- Забор не встаёт **внутри** постройки (обе клетки ребра — одной и той же), а постройка — поверх забора.
- **Призрак** держит своё ребро, пока курсор не подойдёт к другой линии (0.15 клетки), и притягивается к концу стены
  (1.25 клетки): продолжает её прямо, а при уходе в сторону (0.3 клетки) предлагает поворот.
- **Протяжка:** зажать и вести по линиям сетки — призраки всей линии (зелёные встанут, красные нет: место, лимит, деньги
  по растущей цене), стройка и оплата на отпускании. Назад по линии — стирает, ПКМ — отмена, над UI замирает.
- **Sell:** выигрывает ближайший забор не дальше 0.35 клетки от его отрезка (иначе берётся структура в клетке под
  курсором). Нажал на забор — протяжка сноса: только заборы, продажа на отпускании; нажал на постройку — продана сразу.

### BuildCard (префаб карточки, инстанцирует BuildPaletteUI в рантайме — при первом открытии её вкладки)
```
Root → Image + Button + CanvasGroup + BuildCardUI
├── Shadow                → Image
└── AnimatedVisual
    ├── Artwork           → CanvasGroup
    │   ├── Thickness     → Image
    │   ├── SelectionFrame (выкл)       → Image
    │   ├── CardBody      → Image + RectMask2D
    │   │   └── Info      → CanvasGroup         (видна у раскрытой карточки — той, что в руке)
    │   │       ├── Title, Description, BuildsOn → TMP_Text
    │   │       └── Terrains → Horizontal Layout Group
    │   │           └── Grass / Forest / Desert → Image + Light, DarkTopRight, DarkBottomLeft (Image)
    │   ├── ItemContainer → Image
    │   │   ├── IconSelectionPlaceholder (выкл) → Image
    │   │   └── BuildingIcon            → Image
    │   ├── ResourceUnit  → вложенный префаб `ResourceUnit` — цена
    │   └── ShineViewport → RectMask2D
    │       └── Shine (выкл)            → Image
    └── LockedOverlay (выкл)            → Image
        └── LockedText                  → TMP_Text
```
Поля `BuildCardUI` названы по детям (cardBody → `CardBody`, iconImage → `BuildingIcon`, costUnit → `ResourceUnit`,
infoGroup → `Info`, titleText, descriptionText, lockedOverlay → `LockedOverlay` и т. д.); terrainChips — пары {земля,
объект из `Terrains`}; `countText` («Count: 1 of 2») — опционален, в префабе пуст. Размеры (в покое 54×70, наведение
66×82, раскрытая 138×90), наклон и блик — в ассете `ScriptableObjects/BuildMode/Build Card Motion Profile` (поле
motionProfile), иконки ресурсов — ResourceIconSet (iconSet). showHint выключен: подсказку над карточкой заменила
раскрытая карточка; hintAnchor — её точка, если включить.

Не хватает ресурсов — арт карточки (всё под `artwork`, кроме цены и рамки «в руке») становится серым материалом
`Materials/M_UI_Greyscale` (поле unaffordableMaterial профиля), число цены — #9B211E (unaffordableCostColor). Запертая
карточка цвета сохраняет: её закрывает `LockedOverlay`.

### PerkCardUI (карточка перка, инстанцирует PerkSelectionUI в рантайме)
```
Root (226×86)     → Image (прозрачная, Raycast ВКЛ — вся карточка и есть кнопка) + PerkCardUI
└── Visual        (сжимается при нажатии — scaleTarget)
    ├── Shadow, Panel, PanelLit (выкл) → Image (Sliced)   (PanelLit — вид под курсором, hoverHighlight)
    ├── IconFrame   → Image 67×66       (рамка места под иконку)
    ├── RarityFrame → Image 31×31       (цветная рамка: спрайт по редкости перка, таблица rarityFrames)
    ├── Icon        → Image 29×29
    ├── Level       → TMP_Text + PixelSnapText   (римская цифра уровня)
    ├── Title, Description → TMP_Text + PixelSnapText
    ├── Resources   → Grid Layout Group (ячейка 13×13, 3 в ряд)   (иконки ресурсов перка — в рантайме)
    └── Claim       → Image (hold_to_claim, Sliced) + RectMask2D
        ├── Fill    → Image             (заливка удержания; её размер в префабе — полная, якорь слева)
        └── Label   → TMP_Text «Hold to Claim»
```
Числа в описании красит код (numberColor, #68D500) — теги цвета в текст не писать. holdDuration 0.5 с.

### ZoneCardUI (карточка зоны, инстанцирует ZoneSelectionUI в рантайме)
```
Root (72×116)     → ZoneCardUI          (место в ряду — за Layout Group, двигать нельзя)
└── Body          (всё, что нарисовано; выбранная карточка поднимается на focusLift = 5 px)
    ├── Focus Frame (выкл) → Image (frame_focus, Sliced, на 1 px шире со всех сторон)
    ├── Background  → Image (card_biome_choose, Sliced, Raycast ВКЛ)
    ├── Title       → TMP_Text 18, #E3CC81 + PixelSnapText
    ├── Collage Window (32×32) → RectMask2D + ZoneCollageView   (окно на коллаж; speed 10 px/с)
    │   └── Collage → Image 64×64       (якорь и пивот — левый верхний угол, спрайт родного размера)
    ├── Collage Frame → Image (icon_biome_frame, Sliced) 36×36
    └── Description → TMP_Text 11, #BEA085 + PixelSnapText
```
Окно едет по коллажу по часовой, пока курсор на карточке; сколько — разница размеров коллажа и окна, отдельной
настройки нет. Коллаж биома — `BiomeDef.collage`, текст — `BiomeDef.description`.

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

### ResourceUnit (иконка + число; инстанцируют ResourcePanel, AgeCostPanel, HintView и OfferingTooltip в рантайме)
```
Root          → ResourceUnit + LayoutElement (Preferred 34×13)   (своей раскладки нет — дети на якорях)
├── Backing   → Image (base_numbers, Sliced)   (подложка под числом: от x=6 до правого края, высота 8,
│   │                                           по центру иконки; левый край уходит ПОД иконку)
│   └── Label → TMP_Text, #BE9F85              (справа от иконки до конца подложки, по центру, без переноса)
├── Ring      → Image (материал M_UI_IconRing, #9B211E, Raycast выкл., **компонент выключен**)
│                                              (15×15 в (−1, 1) — на пиксель шире иконки со всех сторон)
└── Icon      → Image 13×13                    (последним: рисуется поверх подложки и кольца)
```
Поля `ResourceUnit`: icon → `Icon`, label → `Label`, ring → `Ring` (необязательно). Порядок детей значим: подложка
рисуется раньше иконки, кольцо — над подложкой. Кольцо — обводка в пиксель по силуэту иконки (шейдер
`Little Peeps/UI Icon Ring` красит прозрачные пиксели, которые касаются иконки сбоку или углом), цвет — у самого
Image. Спрайт кольцо берёт иконкин (`Bind`), включает его только тот, кто попросит (`ShowRing`): сейчас это цена
эпохи — в подсказке подношения и в хинте нехватку показывает только красное число. Шейдер ждёт, что спрайт иконки
занимает свою текстуру целиком (одиночный спрайт, не в атласе) — так у иконок ресурсов и есть.

**ResourceUnit Variant For Age** — вариант для цены эпохи (его берёт только `AgeCostPanel`): у `Label` Left 9 вместо 8,
у `Backing` Right −1 вместо 0. Цифры начинаются и подложка кончается на пиксель правее — как в панели эпохи на
макете: между кольцом и первой цифрой остаётся пустой столбец. Остальное — от ResourceUnit.
**ResourceUnit Variant For Offering** — вариант для квоты в окне подношения (его берёт только `OfferingWindow`):
светлая подложка на кремовой плашке, как на макете — у `Backing` спрайта нет (Image без спрайта рисует ровный
прямоугольник цвета), цвет #B4B29E; у `Label` цвет #292D2E. Красный нехватки (#9B211E) кладёт поверх код.
Размер ячейки задаёт родитель: Grid Layout Group у ResourceBar и Cost (Cell Size 34×13) берёт свой Cell Size, а
Layout Group, которая управляет размером детей (CostContainer в HintView), берёт Preferred из LayoutElement —
без него ячейка там схлопнется в 0×0. Один префаб и для запасов, и для цены: цифры читаются одинаково.

### PerkSlot (ячейка взятого перка; инстанцирует PerkPanel в рантайме)
```
Root      → Image (perk_place, Simple, 20×20, Raycast ВКЛ — ловит наведение) + PerkSlotUI
├── Icon  → Image 20×20 по центру, Preserve Aspect   (компонент выключен: Image без спрайта рисует белый квадрат,
│                                                      включает PerkSlotUI, когда у перка есть иконка)
└── Label → TMP_Text, Micro5 11, Right + Baseline    (цифра в правом нижнем углу, базовая линия на рамке;
                                                      уровень перка — пишет PerkSlotUI)
```
Поля `PerkSlotUI`: icon → `Icon`, levelLabel → `Label`. Иконка — `PerkDef.smallIcon` (`icon_blessing_xs_*`, 20×20) —
отдельный рисунок, не большая `icon` с карточки. Один перк — одна ячейка: следующий уровень меняет цифру, а не добавляет ячейку.
Micro5 размера 11 — пиксель шрифта на пиксель Канваса: цифры 3×5, как на макете; цвет — как у чисел ресурсов.
Label стоит после Icon — цифра рисуется поверх картинки. Подсказка (название + описание взятого уровня) — слева от ячейки.

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
| **StartConfig** | LittlePeeps/StartConfig | islandSeed (0 = случайный), islandRules (правила генерации), startBiome, house, pier, startingResources, startingModifiers, **startingUpgrades** (мета-перки, которые забег получает без очков, как купленные: перки, которым они нужны, тоже выпадают; то, что есть и здесь, и в профиле, считается один раз). Боевой — `Start Config` в `RunManager.startConfig`, `Test Start Config` — для тестового режима |
| **StructureDef** | LittlePeeps/StructureDef | id, displayName, description, icon, prefab, **placement (Cell = футпринт клеток / Edge = забор на ребре)**, size + **footprintMask** (форма внутри size — рисуется клетками в инспекторе), cost[], requiredAge, lockedUntilPerk (карточка заперта, пока её не откроет перк), allowedTerrain[] (пусто = любой биом), sellRefundPercent (доля возврата при продаже, 0.5), canSell, passable / animalsAvoid, isWater (река: по нему клетки реки стыкуются, а мельница ищет берег), needsWaterSide (мельница), **onWater** (мост: каждая клетка футпринта — клетка реки, река остаётся под ним; включать вместе с passable), border (расширяет занимаемую территорию: дом=1 → 2×2 занимает 4×4), **maxCount** (0 = без лимита; растёт статом `StructureLimit`), costStepFlat / costStepPercent / exponentialCostGrowth (рост цены за каждую стоящую) |
| **ResourceSourceDef** | LittlePeeps/ResourceSourceDef | resource, workerYields[] (кто и сколько добывает; пусто = никто; «любой рабочий» = перечислить всех), **depletion** (Never / Despawn / Regrow), hitsToDeplete, regrowTime, keepBodyWhileDepleted, **pickupFx** (префаб FX_Pickup_*; пусто = эффект ресурса из `ResourceIconSet`) + **pickupFxCount**. `pickupFx` — единственный визуал в дефе, и намеренно: он отвечает на вопрос «что именно добыли», когда тип ресурса этого не скажет. Один тип дефа для статичных нод и зверей |
| **UnitDef** | LittlePeeps/UnitDef | id, prefab (→ BaseUnit), speed, stamina (секунды работы за вылет), tiredSpeedMultiplier (скорость уставшего), profession (с какой профессией рождается — `Unassigned`, настоящую даёт стойка) |
| **ProfessionDef** | LittlePeeps/ProfessionDef | type (UnitType), frames[] — кадры ходьбы этой профессии |
| **BiomeDef** | LittlePeeps/BiomeDef | profile (параметры генерации зоны), tileSet, displayName (пусто = имя ассета), **description** (строка на карточке зоны), **collage** (кусок биома из игровых тайлов, 64×64 — больше окна карточки), previewColor (заливка зоны при выборе). Стартовому биому (`Meadow Start`) карточка не нужна — эти поля у него пусты |
| **IslandTileSet** | LittlePeeps/IslandTileSet | тайлы земли (светлые/тёмные клетки), углы суши, кромка — полосы под сушей (горизонтальная) и столбики по бокам (вертикальная); слотов внутренних углов нет: угол залива собирается из полосы и столбика; **decorLight / decorDark** (декор для светлых / тёмных клеток) и decorChance (доля клеток с декором, 0.3). В проекте: `TileSet/Tile Set Meadow`, `Tile Set Desert`, `Tile Set Autumn` (сейчас у биома Forest) и `Tile Set Forest` (запасной) |
| **TerrainBorderSet** | LittlePeeps/TerrainBorderSet | граница одного биома, кладётся тайлмапом из строки `IslandSystem.borders`. **edgeAllRound** — край по всему краю биома (и к биомам выше, и к воде), а не только к тем, что он накрывает; **overSea** — кайма и на клетки воды вдоль берега. На биом выше кайма не ложится никогда — это его граница. 8 слотов **Over** — кайма на клетке нижнего биома (4 стороны + 4 вогнутых угла); 8 слотов **Edge** — край на своей клетке. В подсказке слота — имя файла художника. Спрайты: **Pivot = Center**, иначе граница съезжает (с Bottom Center — на полклетки вверх). В проекте: `TileSet/Brdr Set Meadow`, `Brdr Set Autumn` (сейчас у леса) и `Brdr Set Forest` (запасной), у всех обе галочки включены; тайлы в `Tiles/BordersMeadow`, `BordersAutumn`, `BordersForest` |
| **BuildPaletteDef** | LittlePeeps/BuildPalette | tabs: вкладки нижней панели, у каждой name, icon, structures (список StructureDef). Порядок вкладок = порядок кнопок и цифр 1–9, порядок structures = порядок карточек; пустая вкладка кнопку не получает. Группировка живёт здесь, на StructureDef категории нет |
| **AgeDef** | LittlePeeps/AgeDef | title (только в лог — на баннере номер римскими цифрами), resourceCost[] (цена), modifiers[] (StatModifier — бонусы эпохи: стат, скоупы, Kind Flat / Percent и одно значение), bonusOverride (текст вместо сгенерированного описания). Порядок задаётся списком `AgeSystem.ages`, начиная с Age II |
| **Перки** | LittlePeeps/Perks/Stat Perk, …/Unlock Structure Perk | общее: id, title, description, icon (карточка, 29×29), **smallIcon** (ячейка в HUD, 20×20 — отдельный рисунок), **rarity** (цвет рамки на карточке, только вид: на шанс не влияет), **grade** (Common — обычные подношения и стартовый выбор; Special — только подношения особых циклов; наборы не смешиваются, а если особых не осталось, особый цикл даёт обычные), weight (шанс выпасть), minAge (с какой эпохи предлагается), requires (перк, который должен быть взят хоть на уровне 1, — один перк открывает другой; не для уровней), **requiresUpgrade** (мета-перк, который должен быть у забега, — для перка, имеющего смысл только поверх него; сейчас у Handful, ему нужен Peep Magnetism). Stat Perk — **levels** (сколько раз берётся за забег; взял — тот же перк выпадает следующим уровнем, пока не взят последний) и modifiers[]: стат, скоупы, Kind (Flat / Percent) и строка **Level N** на каждый уровень — сколько добавляет ИМЕННО этот уровень, не итог (0 — на нём не растёт; число строк следует за levels, новый уровень повторяет последний шаг). Карточка пишет шаг и итог: `+20% TAP RADIUS [TOTAL +40%]`; ручное description у перка с уровнями не ставить — оно одно на все уровни (инспектор предупредит и покажет тексты уровней II+). Unlock Structure Perk — structure (снимает `lockedUntilPerk`), один уровень. Особых сейчас нет: все перки Common |
| **Offering Schedule** | LittlePeeps/Offering Schedule | цикл подношений, для `OfferingSystem.schedule`: segmentSeconds (длина одного отрезка-«года» в секундах игры, одна на весь забег — лента всегда едет с одной скоростью; пауза не идёт), cycles[] (по порядку, первый элемент = цикл 1: segments — сколько отрезков длится цикл, не меньше одного; resources — квота; ресурс эпохой не проверяется — не просить его раньше, чем обычный игрок до неё дойдёт), growthPastEnd (после списка повторяется последний цикл: квота ×рост за каждый лишний цикл, округление вверх, длина — как есть), specialEvery (каждый N-й цикл особый, 0 = нет). В проекте `OfferingDef/Offering Schedule Def` — боевое: отрезок 10 с, 10 циклов (первые три 90 / 120 / 150 с, дальше по 120), рост ×1.3, **specialEvery 0 — особых циклов нет**; квоты и баланс — в ARCHITECTURE (*Balance baseline*), числа — за Даниилом. `OfferingDef/Test Offering Schedule Def` — для тестового режима |
| **PerkCatalogueDef** | LittlePeeps/Perk Catalogue | perks — список перков для `PerkSystem.catalogue` |
| **Мета-перки** | LittlePeeps/Meta/Stat Upgrade, …/Start Resources, …/Start Perk Pick, …/Free Age | общее: id, title, icon (картинка ячейки), description (пусто — текст хинта строится из данных), **cost** (цена в очках, число в углу ячейки); уровней нет, одна иконка — один апгрейд. Stat Upgrade — modifiers[] (обычные StatModifier); Start Resources — resources; Free Age — freeAges (сколько первых эпох забега бесплатны, несколько апгрейдов складываются; кнопку жмёт и зону выбирает игрок, бонус и престиж — как у купленной). Peep Magnetism — обычный Stat Upgrade со статом `CarryCapacity` (сколько пипов унесёт рука) |
| **GlobalUpgradeCatalogueDef** | LittlePeeps/Meta/Upgrade Catalogue | upgrades — мета-перки в порядке мета-экрана, для `PrestigeSystem.upgrades` |
| Island Rise Profile | LittlePeeps/Island Rise Profile | все ручки подъёма острова: тайминги, кривые прыжка и сплющивания, эффекты, звуки, тряска |
| Harvest Number Motion | LittlePeeps/Harvest Number Motion | полёт всплывающей цифры: lifetime, travel, кривые, разброс |
| Build Card Motion Profile | LittlePeeps/UI/Build Card Motion Profile | размеры и анимации карточки стройки; unaffordableMaterial и unaffordableCostColor — вид карточки, на которую не хватает |
| ResourceIconSet | LittlePeeps/ResourceIconSet | icons — строка на каждый ресурс: icon (HUD, ценники, подсказки, карточки перков), **pickupFx** + **pickupFxCount** (эффект подбора ресурса по умолчанию, см. FX_Pickup_*) |

> Координаты сетки **знаковые** и не зависят от того, какие клетки существуют: центр клетки `c` = мир `(c+0.5)·cellSize`.
> Дом 2×2 на `cell=(-1,-1)` занимает `(-1,-1)…(0,0)` и центрируется в мировом нуле.

## Управление (камера + горячие клавиши) — как связано

Три слоя ввода, расцеплённые между собой:

- **`InputHandler`** — низкоуровневый роутер мыши. Мышь у игры в две кнопки: колеса и средней нет. ЛКМ/ПКМ → мировые координаты → события:
  - `OnWorldClick` — ЛКМ, **только если курсор НЕ над UI**. Слушают оба, а забирает один: с карточкой или Sell в руке — `PlacementController` (ставит / продаёт), с пустой рукой — `TapSystem` (буст юнитов + щелчок; он сам спрашивает `PlacementController.HoldsTool`);
  - `OnAnyClick` — любая ЛКМ, и над UI тоже. Слушает `AgeSequencer`: тап ускоряет подъём острова и закрывает баннер эпохи, а баннер сам UI;
  - ПКМ универсальная, клик от перетаскивания отличает порог **Drag Threshold** у `EventSystem` (10 px — то же правило, что у полосы стройки):
    - `OnRightClick` — **клик**: ПКМ отпущена, не уйдя дальше порога. Положить инструмент из руки, над панелью тоже. Срабатывает на **отпускании** — при нажатии ещё неизвестно, клик это или перетаскивание;
    - `OnPan` / `IsPanning` — **перетаскивание**: ПКМ ушла дальше порога и была нажата на острове (нажатая над UI — ничего). Каждый кадр — сдвиг мыши в пикселях экрана, считая от точки нажатия. Слушает `CameraController`. Перетаскивание никогда не отмена.
  - `WorldMousePosition` — точка мира под курсором для всех, кто за ним следует (призрак постройки, протяжка забора, сетка у курсора, кольцо тапа). Пока ПКМ зажата на острове, стоит на точке нажатия: камера догоняет цель с damping, и без этого всё, что за курсором, ползло бы по острову. Считается при чтении, так что порядок `Update` не важен. Своих `ScreenToWorldPoint` в системах не заводить.

  Камеру он не двигает — только сообщает о перетаскивании.
- **`CameraController`** (на своём GO, НЕ на CameraTarget и НЕ на камере) — двигает transform пустого **CameraTarget** = логическую цель камеры; клавиатуру поллит сам, считает на `unscaledDeltaTime` → работает и на паузах (`timeScale=0`: выбор зоны и перка, подъём острова). Источники: WASD + стрелки, курсор у края экрана, **перетаскивание ПКМ** (от `InputHandler.OnPan`; мир тащится под курсором). Жест ПКМ сам не разбирает: та же кнопка — отмена, и решать «клик или перетаскивание» должно одно место. Цель клампится по `IslandGrid.WorldBounds()` + `boundsMargin`; границы перечитываются на `AgeStartedEvent` (рост острова) и `RunStartedEvent`. Плавность даёт не он, а **Cinemachine vcam**, которая следует за целью с damping; `Main Camera` несёт `CinemachineBrain` и рендерит. `viewCamera` (= Main Camera) нужен скрипту только чтобы перевести пиксели перетаскивания в мир (его `orthographicSize` — то, что на экране после пиксельного округления). `Confiner2D` — отдельной итерацией позже.
  - **Зум — `+` / `−`** (основной ряд — клавиша `=` без Shift — и нампад): три вида из `zoomLevels` (далеко / классика / близко), мгновенно, к центру экрана, у краёв упирается. Плавного зума нет и быть не может: все промежуточные размеры бьют пиксели. Целый масштаб пикселей обеспечивает не код, а связка `PixelPerfectCamera` + `CinemachinePixelPerfect` (см. таблицу выше).
  - **Старт забега** (`Start` и каждый `RunStartedEvent`): камера мгновенно, без damping, встаёт в центр острова на виде `startZoomLevel` — новый забег всегда начинается с одного вида, что бы ни делали до этого.
- **`GameHotkeys`** (на `Input`) — дискретные команды по нажатию, публикует события в `EventBus`, ни во что не лезет напрямую. UI его не фильтрует: клавиатура не целится в то, что под курсором, поэтому подписчики сами проверяют состояние:
  - **B** → `BuildPanelToggleRequestedEvent` → `BuildPanelToggle` (тот же переключатель, что клик по `BuildToggle`) прячет или показывает `SellButton`, `BuildTabs` и `BuildPalette`. Не режим: игра идёт и так и так; прятание кладёт инструмент из руки;
  - **X** → `SellModeRequestedEvent` → `SellButtonUI` берёт Sell в руку или кладёт — тем же путём, что клик (вид кнопки рисует по факту, из `PlacementController.ToolChanged`; вспышку продажи — из `PlacementController.Sold`); `BuildPanelToggle` показывает спрятанную панель. Поверх другого экрана (выбор перка, подъём острова…) — no-op: вне живой игры контроллер инструмент не даёт, а панель не показывается;
  - **1–9** (верхний ряд и нампад) → `BuildTabRequestedEvent` → `BuildTabBar` открывает N-ю вкладку, `BuildPanelToggle` показывает спрятанную панель; поверх другого экрана — no-op. Клавиши зашиты в коде, не в инспекторе: цифра = позиция вкладки;
  - **Esc** → `ExitToMenuRequestedEvent` → `GameBootstrap` пока **отклоняет** переход (warning в консоли): у `MainMenuState` нет экрана, и выход в него оставил бы игрока без пути назад. Вернуть — одна строка, когда меню появится.

**Мышь над UI принадлежит UI.** Начало перетаскивания ПКМ, как и `OnWorldClick`, ничего не делает, пока курсор над UI (`InputHandler.IsPointerOverUI()` — ответ EventSystem). Начатое на острове перетаскивание может пройти и над UI. Клик ПКМ (отмена) не фильтруется. WASD, край экрана и `+` / `−` тоже: они ни во что под курсором не целятся. Колесо над миром ничего не делает; над полосой стройки оно её прокручивает — это UI.

> ⚠️ «Над UI» = над **любой** графикой с включённым **Raycast Target**, даже прозрачной. Невидимая подложка на весь экран отрезает от игры тапы и перетаскивание. Если окно должно оставлять остров доступным, у его подложки Raycast Target выключен — так сделано у `Screens/ZoneScreen/Panel`, иначе при выборе зоны остров не перетащить. У модальных окон (затемнение `PerkScreen/Panel`, `MetaScreen`) подложка перехватывает ввод намеренно.

> ⚠️ `CameraController` использует `viewCamera.orthographicSize` (перетаскивание) — **камера должна быть Orthographic** (2D-проект, так и есть). линзу vcam тоже держать Orthographic.

## Стройка — как связано

**Режима стройки нет** (с 2026-10-15): игра не встаёт на паузу, юниты не исчезают, переноса построек нет.
Панель стройки открыта всю живую игру, остров работает под ней.

- **Когда работает:** `PlayingState.Enter` → `PlacementController.Begin`, `Exit` → `End`. Любой другой экран (выбор
  зоны или перка, подъём острова, окно конца цикла, мета-экран) начинается с того, что инструмент из руки кладётся.
- **Что в руке — знает только `PlacementController`.** Виджеты панели лишь просят (`Select` / `SelectSell` /
  `PutDown`) и рисуют подсветку по его событию `ToolChanged`, своих копий не держат; вне живой игры просьбы отклоняются:
  - карточка → **PLACE** (гост под курсором, клик ставит, забор ещё и протягивается; инструмент остаётся в руке, можно ставить дальше),
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
    гусь над площадкой улетает домой;
  - вид: полупрозрачный чертёж здания (его спрайты, умноженные на `siteLook.blueprintColor`) над подложкой
    `siteLook.groundColor` — настраивается на `StructureSystem` (см. checklist);
  - как только внутри никого — на том же месте встаёт настоящий префаб.

  Площадка уже занимает клетки, считается в лимите и цене, а продаётся как готовое здание. Генерируемое
  (содержимое зон, стартовый дом, пирс) встаёт сразу, без площадки. **Отложено:** стройка ударами юнитов снаружи,
  арт лесов, звук стука.
- **Продажа идёт в живой игре:** таверна выпускает гостей пьяными там, где стояла; звери проданной норы и гуси проданного
  гусятника исчезают с пуфом; проданный мост выпускает тех, кто на нём, и никого больше не впускает; лишние жители проданного дома (сколько не помещается в оставшиеся дома) исчезают с пуфом, ближайшие к нему
  первыми. Эффект и звук пуфа — поля `SpawnSystem` (`puffFx`, `smallPuffFx`, `puffSound`).
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
   включён тестовый режим — до первого забега подставляются его пресеты (предупреждение `[TEST MODE]` в консоли);
   `RunManager.StartNewRun()` → RunContext, стартовые модификаторы, ресурсы и мета-перки из StartConfig плюс купленные
   мета-перки, `IslandSystem.GenerateForRun` → стартовая зона с домом и пирсом. App FSM → `Boot → GameplayContainer → Playing`. Камера — в центре острова, музыка — с первого кадра.
2. На экране: юниты вылетают из домов и отскакивают; `Unassigned` берут инструмент на стойке и работают профессией;
   удар по ноде платит по её `workerYields`; вокруг нор гуляют звери; уставшие возвращаются в дом. Тап — буст юнитов в
   радиусе со щелчком.
3. Если мета-перк дал стартовый выбор перка — он открывается сразу.
4. Панель стройки внизу открыта всё время, игра под ней идёт: карточка в руке → сетка пятном у курсора и гост,
   клик ставит стройплощадку, которая становится зданием, когда с места все уйдут; Sell продаёт; ПКМ — пустая рука,
   клик снова тап. B прячет / показывает панель.
5. **Expand Island** (`AgeAdvancePanel`) активна, когда хватает ресурсов или эпоха бесплатна (мета-перк Free Age — вместо
   цены «FREE»): клик → выбор зоны → зона поднимается из моря →
   баннер «Age N» → снова игра (перка за эпоху нет). Тап ускоряет подъём, второй — к финалу, тап по баннеру закрывает его.
6. **Цикл подношений** идёт с первой секунды: лента сверху ползёт к кружку, под засечками годы с начала забега (1, 2,
   3, 4 …); на любом экране поверх игры часы и лента стоят. Перед отъездом большого ромба полоса наползает на точки
   ему навстречу со скоростью ленты. Когда до подношения остаётся ≈4,5 года, большой ромб справа уезжает вместе
   с руками, а на его место из-за края приезжает маленький. Курсор на ленте → подсказка «N years until Offering»
   и квота (красным — чего не хватает).
7. Ромб дошёл до кружка → пауза, затемнение и окно «Gods Demand the Offering» с квотой (красным — чего не хватает):
   **Pay the Offering** (бледная и не жмётся, если не хватает) → выбор перка (в особом цикле — особые перки) и
   следующий цикл, на стоянке снова большой ромб с руками; **Face the Wrath** — держать секунду, заливка тёмно-красным
   растёт слева направо (отпустил или увёл курсор — сброс) → очки престижа (новые эпохи сверх рекорда и
   время игры) → мета-экран (тратим очки на мета-перки; цена синяя — хватает, красная — нет, у купленного скрыта) →
   **Next Island** → новый забег с цикла 1 и года 1; камера снова в центре острова.

## Тестовый режим

Пресеты для плей-теста без перестановки боевых ссылок и правки боевых ассетов.

- **Включается** галочкой в меню **Little Peeps → Test Mode**. Она хранится на машине (EditorPrefs): сцену не пачкает, к
  остальным не уезжает, в билде режим всегда выключен. Переключённая во время Play — действует со следующего Play.
- **Что подменяет** — поля объекта `TestMode` в сцене: `startConfig` (что получает забег) и `offeringSchedule` (расписание
  подношений). Пустое поле = боевой ассет. Держится все забеги сессии Play; в консоли — предупреждение `[TEST MODE]` со
  списком подмен.
- Проверить мета-перк без очков — положить его в `startingUpgrades` у `Test Start Config`.

## Инструменты редактора (меню Little Peeps)

- **Little Peeps → Footprint Grid In Prefabs** — разметка в Prefab Mode (на машину, в EditorPrefs): клетки постройки по её
  StructureDef, пиксельная сетка, у выделенного спрайта — рамка холста (зелёная, если лежит ровно по клеткам) и линия, по
  которой он сортируется. Всё считается от КОРНЯ: его игра и ставит, двигать нужно `Visual` / `Model`. Префаб без дефа и
  забор разметки не получают.
- **Little Peeps → Test Mode** — см. «Тестовый режим».
- **Window → Little Peeps → Harvest Feedback / Island Rise** — просмотр отклика сбора и подъёма острова без Play.
- Инспектор StructureDef внизу показывает, как растёт цена (первые восемь, в обоих режимах роста).

## Project settings — важное (не в сцене, но влияет)

- **Physics 2D → Layer Collision Matrix:** Unit×Unit **OFF** (юниты проходят сквозь друг друга), Unit×Default **ON**; слой 7 **Goose**: Goose×Unit и Goose×Goose **OFF**, Goose×Default **ON**.
- **Physics 2D → Bounce Threshold = 0.1** (иначе скольжение на малых скоростях).
- **Y-сортировка:** на URP 2D Renderer-ассете (`Assets/Settings/Renderer2D.asset`) — Transparency Sort Mode = Custom Axis, Axis (0,1,0). Юниты и постройки — на одном Sorting Layer (`Entities`) с равным Order, сортируются по Y динамически.
- **Sorting Layers** снизу вверх: Water, Island, Ground, Default, Entities, Overlay, Mesh. **Island** — все тайлмапы острова (земля, кайма, декор, границы, волны, тень острова; на нём же проекции построек); **Ground** — то, что лежит на земле (рисовое поле, пни, стерня, щебень, пустые кусты), а ещё превью зоны, подсветка территории госта (Order 1001) и подложка стройплощадки (Order 1000). Сетка стройки — на Mesh, поверх всего. Так лежащее на земле всегда над тайлами, какой бы Order ни стоял у тех и других.
- **Global Light 2D → Target Sorting Layers:** должны быть отмечены все слои, на которых что-то рисуется, Island в том числе. Новый слой сортировки сам туда может не попасть — тогда всё на нём чёрное.

## Бонусы (RunStats) — коротко

Прибавки к параметрам (`ProductionGlobal`, `ResourceYield`, `UnitSpeed`, `HouseCapacity`, `TapRadius`, …) задаются списками
`StatModifier` (Kind Flat / Percent + значение): в `AgeDef.modifiers`, `StartConfig.startingModifiers` и Stat-мета-перках; в Stat-перках —
`LevelledModifier`, те же поля с шагом на каждый уровень. Слой живёт в
`RunContext.stats` и сбрасывается на престиже. База — в конфигах и компонентах (`UnitDef.speed`, `ResourceSourceDef.workerYields`,
`Spawner.capacity` …), бонус — отдельным слоем. Подробности для дизайнера: **BONUS_SYSTEM_GUIDE.md**.

## Что ещё заглушка

- **Главное меню:** `MainMenuState` без экрана, Esc переход отклоняет.
- **Сохранение на диск:** `SaveSystem.Save/Load` — TODO. Очки престижа и мета-перки живут до закрытия игры.
