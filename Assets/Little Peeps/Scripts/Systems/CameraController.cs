using UnityEngine;
using UnityEngine.InputSystem;

// Drives the CameraTarget transform during gameplay AND build mode — the strategy-camera rig pattern:
// this object is the logical target, and a Cinemachine vcam follows it with damping (the smooth motion
// lives in Cinemachine, not here). Self-contained: polls keyboard/mouse directly (no InputHandler
// dependency, since panning needs only a screen delta, not world coords) and runs on UNSCALED time so
// panning still works while build mode has frozen the game (Time.timeScale = 0). Movement sources:
//   - WASD / arrow keys       → velocity pan
//   - cursor at a screen edge  → pan toward that edge
//   - middle-mouse drag        → grab-and-drag the world under the cursor
// The target is clamped to the island's world bounds (from IslandSystem) expanded by a configurable
// margin, so the player can't pan off into empty space; the vcam follows the clamped target so the
// camera stays in range too. Zoom (and a Cinemachine Confiner2D) is a later iteration.
//
// Place on the CameraTarget GameObject (NOT the camera) and wire:
//   - islandSystem  → for the bounds clamp
//   - viewCamera    → the rendering Camera, only to convert drag pixels → world units (its ortho size
//                     is driven by the Cinemachine brain, so it reflects the live zoom).
// Reads the grid in Start (not Awake), per the bootstrap rule: the run + island are built in
// GameBootstrap.Awake.
public class CameraController : MonoBehaviour
{
    [SerializeField] private IslandSystem islandSystem;
    [SerializeField] private Camera viewCamera;           // rendering camera (Cinemachine brain output); drag scale only

    [Header("Keyboard / edge pan")]
    [SerializeField] private float panSpeed = 12f;        // world units / second
    [SerializeField] private bool edgePanEnabled = true;
    [SerializeField] private float edgeThickness = 12f;   // px from the screen border that triggers edge-pan

    [Header("Bounds")]
    [SerializeField] private float boundsMargin = 4f;     // how far past the island edge the target may go

    private bool dragging;
    private Vector2 lastDragScreenPos;
    private Bounds islandBounds;
    private bool hasBounds;
    private bool pointerActive;   // latched true on the first real mouse movement (see EdgeDir)

    private void Start() => RefreshBounds();

    private void OnEnable()  => EventBus<AgeStartedEvent>.Subscribe(OnAgeStarted);
    private void OnDisable() => EventBus<AgeStartedEvent>.Unsubscribe(OnAgeStarted);

    // The island grows on a new age → its bounds change, so re-cache them.
    private void OnAgeStarted(AgeStartedEvent _) => RefreshBounds();

    // Cache the island's world AABB; the camera center is clamped to it (+ margin). Called on Start
    // and whenever the island grows. Safe to call before the grid exists (clamp simply stays off).
    public void RefreshBounds()
    {
        var grid = islandSystem != null ? islandSystem.Grid : null;
        if (grid == null) { hasBounds = false; return; }
        islandBounds = grid.WorldBounds();
        hasBounds = true;
    }

    private void Update()
    {
        Vector3 pos = transform.position;

        // Middle-mouse drag takes over while held; keyboard/edge are skipped so they don't fight it.
        if (HandleDrag(ref pos))
        {
            ApplyPosition(pos);
            return;
        }

        Vector2 dir = KeyboardDir();
        if (edgePanEnabled) dir += EdgeDir();
        if (dir != Vector2.zero)
        {
            // Unscaled time → panning works in build mode (timeScale 0). Clamp so diagonals aren't faster.
            Vector2 step = Vector2.ClampMagnitude(dir, 1f) * (panSpeed * Time.unscaledDeltaTime);
            pos.x += step.x;
            pos.y += step.y;
        }

        ApplyPosition(pos);
    }

    // Middle-mouse drag-pan: while held, move the camera opposite the cursor's pixel delta so the
    // grabbed world point stays under the cursor. Returns true while a drag is active.
    private bool HandleDrag(ref Vector3 pos)
    {
        var mouse = Mouse.current;
        if (mouse == null || viewCamera == null) return false;

        if (mouse.middleButton.wasPressedThisFrame)
        {
            dragging = true;
            lastDragScreenPos = mouse.position.ReadValue();
        }
        else if (!mouse.middleButton.isPressed)
        {
            dragging = false;
        }

        if (!dragging) return false;

        Vector2 screen = mouse.position.ReadValue();
        Vector2 deltaPx = screen - lastDragScreenPos;
        lastDragScreenPos = screen;

        float worldPerPixel = (viewCamera.orthographicSize * 2f) / Screen.height;
        pos.x -= deltaPx.x * worldPerPixel;
        pos.y -= deltaPx.y * worldPerPixel;
        return true;
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

    // Clamp the camera center to the island bounds (+ margin) and keep its original depth.
    private void ApplyPosition(Vector3 pos)
    {
        if (hasBounds)
        {
            pos.x = Mathf.Clamp(pos.x, islandBounds.min.x - boundsMargin, islandBounds.max.x + boundsMargin);
            pos.y = Mathf.Clamp(pos.y, islandBounds.min.y - boundsMargin, islandBounds.max.y + boundsMargin);
        }
        pos.z = transform.position.z;
        transform.position = pos;
    }
}
