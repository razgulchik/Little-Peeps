using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

namespace LittlePeeps
{
    // The strategy-camera "brain": drives the rig during gameplay AND the frozen screens without being part of the
    // rig itself. The rig is three dumb objects — a CameraTarget transform (the logical center), a rendering
    // Camera + CinemachineBrain, and a CinemachineCamera (vcam) that follows the target with damping (the
    // smooth motion lives in Cinemachine, not here). This controller lives on its OWN GameObject and reaches
    // into the rig through references, so each rig object keeps a single responsibility.
    //
    // Polls the keyboard and the cursor position itself and runs on UNSCALED time, so panning still works
    // while the game is frozen (Time.timeScale = 0). Movement sources:
    //   - WASD / arrow keys       → velocity pan
    //   - cursor at a screen edge  → pan toward that edge
    //   - right-mouse drag         → grab-and-drag the world under the cursor. The gesture is
    //                                InputHandler's (OnPan): the right button is also cancel, and only one
    //                                place may decide which of the two a press is. A drag starts only on
    //                                the world; keys and edge-pan aim at nothing and stay as they are.
    //
    // Zoom is a few fixed views, stepped with + and − (the main row and the numpad), toward the screen
    // centre, as a cut. Each view is a vcam ortho size; the CinemachinePixelPerfect extension snaps it to
    // the nearest size the Pixel Perfect Camera allows — a whole number of screen pixels per sprite pixel —
    // so no view can break the pixels on any screen, and a gliding zoom could not exist anyway: every
    // size in between is a broken one. The vcam's own Lens > Orthographic Size is overwritten by the
    // start view; the views are here.
    // The target is clamped to the island's world bounds (from IslandSystem) expanded by a configurable
    // margin, so the player can't pan off into empty space; the vcam follows the clamped target so the
    // camera stays in range too. (A Cinemachine Confiner2D is a later iteration.) While zones are on
    // offer the clamp is widened to take them in (SetExtraBounds) and the camera can be sent to look at
    // one (FocusOn) — the pan itself stays the player's.
    //
    // Every run starts from the same view (ResetView): the island in the middle of the screen at the start
    // zoom, as a cut.
    // A new run is a new island, and it should look like a fresh start — not inherit wherever the last
    // run left the camera.
    //
    // Place on a dedicated CameraController GameObject (NOT on the CameraTarget and NOT on the Camera) and wire:
    //   - cameraTarget  → the CameraTarget transform this drives (pan)
    //   - vcam          → the CinemachineCamera; reset to a cut on a new run, its ortho size read by FocusOn
    //   - viewCamera    → the rendering Camera, only to convert drag pixels → world units (its ortho size is
    //                     the one actually on screen, after the pixel-perfect snap).
    //   - islandSystem  → for the bounds clamp
    //   - inputHandler  → the right-drag pan (OnPan / IsPanning)
    // Reads the grid in Start (not Awake), per the bootstrap rule: the run + island are built in
    // GameBootstrap.Awake.
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private Transform cameraTarget;      // the logical center the vcam follows; we pan it
        [SerializeField] private CinemachineCamera vcam;      // follows the target; owns the authored ortho size
        [SerializeField] private Camera viewCamera;           // rendering camera (Cinemachine brain output); drag scale only
        [SerializeField] private IslandSystem islandSystem;
        [SerializeField] private InputHandler inputHandler;   // the right-drag pan

        [Header("Keyboard / edge pan")]
        [SerializeField] private float panSpeed = 12f;        // world units / second
        [SerializeField] private bool edgePanEnabled = true;
        [SerializeField] private float edgeThickness = 12f;   // px from the screen border that triggers edge-pan

        [Header("Zoom")]
        [Tooltip("The views + and − step between, as the vcam's ortho size, FAR to CLOSE. Any value is " +
                 "safe — the pixel-perfect extension snaps it to the nearest whole pixel scale of the screen " +
                 "at hand; these are exact on 1080p (×2, ×3, ×4) and keep about the same framing elsewhere.")]
        [SerializeField] private float[] zoomLevels = { 16.875f, 11.25f, 8.4375f };
        [Tooltip("Index into Zoom Levels every run starts at — the classic view.")]
        [SerializeField] private int startZoomLevel = 1;

        [Header("Bounds")]
        [SerializeField] private float boundsMargin = 4f;     // how far past the island edge the target may go

        private Bounds islandBounds;
        private bool hasBounds;
        private Bounds extraBounds;   // added to the clamp while set: the zone offers, which are not island yet
        private bool hasExtraBounds;
        private bool pointerActive;   // latched true on the first real mouse movement (see EdgeDir)
        private int zoomLevel;        // index into zoomLevels

        // The menus over the game: the island stands still behind them, and the pause is a pause of the
        // player's hands too. On every other frozen screen the pan stays — the zone pick needs it.
        private const UIMode HandsOffModes = UIMode.MainMenu | UIMode.Pause;

        // Starts from the boot mode rather than waiting for the first event, for UIVisibility's reason:
        // GameBootstrap.Awake may announce the menu before this OnEnable has subscribed.
        private bool handsOff = (UIModeChangedEvent.BootMode & HandsOffModes) != 0;

        private void Start() => ResetView();

        private void OnEnable()
        {
            EventBus<AgeStartedEvent>.Subscribe(OnAgeStarted);
            EventBus<RunStartedEvent>.Subscribe(OnRunStarted);
            EventBus<UIModeChangedEvent>.Subscribe(OnModeChanged);
            if (inputHandler != null) inputHandler.OnPan += OnPan;
        }

        private void OnDisable()
        {
            EventBus<AgeStartedEvent>.Unsubscribe(OnAgeStarted);
            EventBus<RunStartedEvent>.Unsubscribe(OnRunStarted);
            EventBus<UIModeChangedEvent>.Unsubscribe(OnModeChanged);
            if (inputHandler != null) inputHandler.OnPan -= OnPan;
        }

        private void OnModeChanged(UIModeChangedEvent e) => handsOff = (e.Mode & HandsOffModes) != 0;

        // The island grows on a new age → its bounds change, so re-cache them.
        private void OnAgeStarted(AgeStartedEvent _) => RefreshBounds();

        // A new run stands on a new island — already grown by any ages a meta perk skipped — so the clamp
        // must not keep the finished run's bounds, and the view starts over. The first run is covered by Start.
        private void OnRunStarted(RunStartedEvent _) => ResetView();

        // The view a run starts from: the whole island centred on screen at the start zoom. A cut, not a
        // glide — PreviousStateIsValid = false makes the vcam skip its damping for one update.
        private void ResetView()
        {
            RefreshBounds();
            SetZoomLevel(startZoomLevel);
            if (cameraTarget != null && hasBounds)
                ApplyPosition(new Vector3(islandBounds.center.x, islandBounds.center.y, cameraTarget.position.z));
            if (vcam != null) vcam.PreviousStateIsValid = false;
        }

        // Cache the island's world AABB; the camera center is clamped to it (+ margin). Called on Start
        // and whenever the island grows. Safe to call before the grid exists (clamp simply stays off).
        public void RefreshBounds()
        {
            var grid = islandSystem != null ? islandSystem.Grid : null;
            if (grid == null) { hasBounds = false; return; }
            islandBounds = grid.WorldBounds();
            hasBounds = true;
        }

        // Let the clamp reach a world box beyond the island until ClearExtraBounds: the zone offers are
        // drawn on water the player could otherwise not pan to. Kept apart from the island bounds so a
        // RefreshBounds in between (the island does not grow while offers are up, but nothing here
        // should depend on that) cannot drop it.
        public void SetExtraBounds(Bounds bounds)
        {
            extraBounds = bounds;
            hasExtraBounds = true;
        }

        // Back to the island alone. A target left outside is clamped on the next Update; the vcam's
        // damping turns that into a glide rather than a cut.
        public void ClearExtraBounds()
        {
            hasExtraBounds = false;
        }

        // Send the camera to look at a world point (within the clamp). Sets the TARGET — the vcam
        // follows it with its own damping, so this is the same smooth motion a pan gives, and the
        // player can pan away from it at any time.
        //
        // `viewportOffsetY` lifts the point that far up the screen, as a fraction of the visible
        // height: 0.15 puts it 15% above the middle, which is how a caller with UI along the bottom
        // keeps what it is showing out from under it. A fraction rather than world units because the
        // UI covers the same share of the screen at every zoom — the conversion needs the live ortho
        // size, which is why it happens here and not in the caller.
        public void FocusOn(Vector2 worldPoint, float viewportOffsetY = 0f)
        {
            if (cameraTarget == null) return;

            // Camera goes DOWN by the offset so the point ends up above the middle.
            float viewHeight = 2f * (vcam != null ? vcam.Lens.OrthographicSize
                                  : viewCamera != null ? viewCamera.orthographicSize : 0f);
            float y = worldPoint.y - viewportOffsetY * viewHeight;

            ApplyPosition(new Vector3(worldPoint.x, y, cameraTarget.position.z));
        }

        private void Update()
        {
            if (cameraTarget == null) return;

            if (!handsOff) HandleZoomKeys();

            Vector3 pos = cameraTarget.position;

            // A right-drag moves the target itself (OnPan); keys and the edge sit it out so they don't fight it.
            bool panning = inputHandler != null && inputHandler.IsPanning;

            Vector2 dir = Vector2.zero;
            if (!panning && !handsOff)
            {
                dir = KeyboardDir();
                if (edgePanEnabled) dir += EdgeDir();
            }
            if (dir != Vector2.zero)
            {
                // Unscaled time → panning works on the frozen screens too (timeScale 0). Clamp so diagonals aren't faster.
                Vector2 step = Vector2.ClampMagnitude(dir, 1f) * (panSpeed * Time.unscaledDeltaTime);
                pos.x += step.x;
                pos.y += step.y;
            }

            ApplyPosition(pos);
        }

        // + steps closer, − farther, stopping at the ends. Keys aim at nothing, so they work over UI and on
        // the frozen screens like the pan keys. `equalsKey` is the + key of the main row (+ without Shift).
        private void HandleZoomKeys()
        {
            var k = Keyboard.current;
            if (k == null) return;

            int step = 0;
            if (k.equalsKey.wasPressedThisFrame || k.numpadPlusKey.wasPressedThisFrame)   step += 1;
            if (k.minusKey.wasPressedThisFrame  || k.numpadMinusKey.wasPressedThisFrame)  step -= 1;
            if (step != 0) SetZoomLevel(zoomLevel + step);
        }

        // Puts the vcam on one of the views; the pixel-perfect extension does the snapping.
        private void SetZoomLevel(int level)
        {
            if (vcam == null || zoomLevels == null || zoomLevels.Length == 0) return;

            zoomLevel = Mathf.Clamp(level, 0, zoomLevels.Length - 1);
            vcam.Lens.OrthographicSize = zoomLevels[zoomLevel];
        }

        // Right-drag pan: move the camera opposite the cursor's pixel move so the grabbed world point stays
        // under the cursor. The rendering camera's ortho size is the one on screen, after the pixel-perfect
        // snap, so the grab holds on any resolution.
        private void OnPan(Vector2 deltaPx)
        {
            // A drag that began on the world just before the menu came up does not carry on under it.
            if (cameraTarget == null || viewCamera == null || handsOff) return;

            float worldPerPixel = (viewCamera.orthographicSize * 2f) / Screen.height;
            Vector3 pos = cameraTarget.position;
            pos.x -= deltaPx.x * worldPerPixel;
            pos.y -= deltaPx.y * worldPerPixel;
            ApplyPosition(pos);
        }

        private static Vector2 KeyboardDir()
        {
            var k = Keyboard.current;
            if (k == null) return Vector2.zero;

            Vector2 d = Vector2.zero;
            if (k.wKey.isPressed || k.upArrowKey.isPressed)    d.y += 1f;
            if (k.sKey.isPressed || k.downArrowKey.isPressed)  d.y -= 1f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) d.x += 1f;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed)  d.x -= 1f;
            return d;
        }

        // Pan toward whichever screen border the cursor is hugging. Guarded so the camera never drifts on
        // its own:
        //   - skipped while the window isn't focused (we run in background, so without this the camera would
        //     edge-pan off-screen while alt-tabbed);
        //   - skipped until the pointer has actually moved once. Before any real input the Input System
        //     reports the cursor at (0,0) — Unity's bottom-left corner — which would otherwise read as a
        //     constant push into that corner at startup (the bug this guards against);
        //   - skipped while the cursor is outside the game view.
        private Vector2 EdgeDir()
        {
            var mouse = Mouse.current;
            if (mouse == null || !Application.isFocused) return Vector2.zero;

            // Latch on the first real movement; (0,0) before that is the synthetic startup position.
            if (!pointerActive)
            {
                if (mouse.delta.ReadValue() == Vector2.zero) return Vector2.zero;
                pointerActive = true;
            }

            Vector2 m = mouse.position.ReadValue();
            if (m.x < 0f || m.y < 0f || m.x > Screen.width || m.y > Screen.height) return Vector2.zero;

            Vector2 d = Vector2.zero;
            if (m.x <= edgeThickness)                 d.x -= 1f;
            if (m.x >= Screen.width - edgeThickness)  d.x += 1f;
            if (m.y <= edgeThickness)                 d.y -= 1f;
            if (m.y >= Screen.height - edgeThickness) d.y += 1f;
            return d;
        }

        // Clamp the camera center to the island bounds (+ the extra box while set, + margin) and keep its
        // original depth.
        private void ApplyPosition(Vector3 pos)
        {
            if (hasBounds || hasExtraBounds)
            {
                var clamp = hasBounds ? islandBounds : extraBounds;
                if (hasBounds && hasExtraBounds) clamp.Encapsulate(extraBounds);

                pos.x = Mathf.Clamp(pos.x, clamp.min.x - boundsMargin, clamp.max.x + boundsMargin);
                pos.y = Mathf.Clamp(pos.y, clamp.min.y - boundsMargin, clamp.max.y + boundsMargin);
            }
            pos.z = cameraTarget.position.z;
            cameraTarget.position = pos;
        }
    }
}
