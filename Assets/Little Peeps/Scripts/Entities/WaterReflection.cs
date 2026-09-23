using UnityEngine;
using Water2D;

namespace LittlePeeps
{
    // This object's silhouette in the water (Modern 2D Water top-down reflections): at Start it hands the
    // package a Reflector, set up the one way that works for our art. Put THIS on prefabs — never the
    // package's Reflector itself, for three reasons found the hard way:
    //  - Reflector runs in edit mode. With no water outside Play Mode it throws in its Start, after it has
    //    already created its "reflection_pivot" / "reflection" objects, and Unity puts objects a script
    //    creates into the open scene even from Prefab Mode — opening such a prefab litters SampleScene.
    //    This class has no edit-mode code at all.
    //  - The package mirrors and places reflections only on the frame after a reflection SETTINGS change.
    //    Anything that appears later (every building, the pier, a new zone's content) keeps an unplaced,
    //    merely x-flipped copy beside the object. Here the flag that triggers that pass is raised once,
    //    right after this object's Reflector exists.
    //  - Its default pivot mode ("auto") finds the sprite's bottom by reading the texture's pixels and throws
    //    unless Read/Write is on. Our sprites already pivot at their base, so sprite_pivot is set instead.
    //    The flag is lowered around AddComponent: the new Reflector registers itself inside AddComponent,
    //    still in auto mode, and would take that path if another object had raised the flag this frame.
    //
    // Created at Start like SpriteShadow, so the build-mode ghost (every behaviour disabled) never gets one.
    // Without a water that has top-down reflections on, it does nothing.
    [RequireComponent(typeof(SpriteRenderer))]
    public class WaterReflection : MonoBehaviour
    {
        [Tooltip("Where the mirror line sits relative to the sprite's pivot, in pixels of THIS sprite (+y is up). " +
                 "0 = the pivot, which is the base of our sprites. Read once at Start.")]
        [SerializeField] private Vector2Int mirrorOffsetPixels;

        private void Start()
        {
            if (ReflectionsSystem.GetInstanceTopDown() == null) return;

            var sprite = GetComponent<SpriteRenderer>().sprite;
            float pixelsPerUnit = sprite != null ? sprite.pixelsPerUnit : 16f;

            ReflectionsSystem.update_extended = false;
            var reflector = gameObject.AddComponent<Reflector>();
            reflector.pivotSourceMode = ReflectionPivotSourceMode.sprite_pivot;
            reflector.displacement.value = (Vector2)mirrorOffsetPixels / pixelsPerUnit;
            reflector.UpdateData();                      // rebuild with the mode and offset set above
            ReflectionsSystem.update_extended = true;    // the package's next pass mirrors and places it
        }
    }
}
