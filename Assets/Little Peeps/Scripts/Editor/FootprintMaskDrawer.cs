using UnityEditor;
using UnityEngine;

namespace LittlePeeps.EditorTools
{
    // Inspector for a StructureDef's footprint mask: the box from the sibling `size` field, drawn as a
    // grid of toggles the author paints the shape on. Rows run top-down on screen and bottom-up in the
    // data, so the picture is the building as it stands on the island — y=0 is the bottom row here and
    // in Footprint alike.
    //
    // The cells array is left empty until the first click: an empty mask reads as the full box at
    // runtime (Footprint), so a def that was never painted needs no data and no migration. Resizing the
    // box after painting leaves an array of the wrong length, which the runtime also reads as the full
    // box; the drawer shows that state as "everything painted" and rebuilds the array on the next click.
    [CustomPropertyDrawer(typeof(FootprintMaskAttribute))]
    public class FootprintMaskDrawer : PropertyDrawer
    {
        private const float CellPx = 18f;
        private const int MaxShown = 12;   // a box bigger than this is a typo, not a building

        private static float Line => EditorGUIUtility.singleLineHeight;
        private static float Gap => EditorGUIUtility.standardVerticalSpacing;

        // The struct's array and the def's box, resolved once per pass. Not valid when the attribute
        // was put on something other than a FootprintMask next to a Vector2Int — see the guard in OnGUI.
        private readonly struct Fields
        {
            public readonly SerializedProperty cells;
            public readonly Vector2Int box;
            public readonly bool valid;

            public Fields(SerializedProperty property, string sizeField)
            {
                cells = property.FindPropertyRelative("cells");
                var size = property.serializedObject.FindProperty(sizeField);
                valid = cells != null && cells.isArray
                     && size != null && size.propertyType == SerializedPropertyType.Vector2Int;
                box = valid ? size.vector2IntValue : Vector2Int.zero;
                valid &= box.x > 0 && box.y > 0;
            }

            public bool Fits => cells.arraySize == box.x * box.y;
            public bool On(int x, int y) => !Fits || cells.GetArrayElementAtIndex(y * box.x + x).boolValue;
        }

        private Fields Resolve(SerializedProperty property)
            => new Fields(property, ((FootprintMaskAttribute)attribute).sizeField);

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var f = Resolve(property);
            if (!f.valid) return EditorGUI.GetPropertyHeight(property, label, true);

            float h = Line;                                     // header
            h += Gap + Mathf.Min(f.box.y, MaxShown) * CellPx;   // the grid
            if (Note(f) != null) h += Gap + Line * 2f;
            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var f = Resolve(property);
            if (!f.valid)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            var r = new Rect(position.x, position.y, position.width, Line);

            // Header: the label, and a way back to "no mask" once something has been painted.
            var field = EditorGUI.PrefixLabel(r, new GUIContent("Footprint",
                "Click cells to carve the shape out of the Size box. Unpainted = the full box."));
            if (f.cells.arraySize > 0 && GUI.Button(new Rect(field.x, field.y, 90f, Line), "Full box"))
                f.cells.ClearArray();

            r.y += Line + Gap;
            DrawGrid(new Vector2(EditorGUI.IndentedRect(r).x, r.y), f);
            r.y += Mathf.Min(f.box.y, MaxShown) * CellPx;

            string note = Note(f);
            if (note != null)
            {
                r.y += Gap;
                EditorGUI.HelpBox(EditorGUI.IndentedRect(new Rect(r.x, r.y, r.width, Line * 2f)), note, MessageType.Warning);
            }

            EditorGUI.EndProperty();
        }

        // The toggles. Reads are answered from the array when it fits the box and as "painted" otherwise;
        // the first write materialises the array at the box's size, all on, then flips the clicked cell.
        private static void DrawGrid(Vector2 at, Fields f)
        {
            int w = Mathf.Min(f.box.x, MaxShown), h = Mathf.Min(f.box.y, MaxShown);

            for (int row = 0; row < h; row++)
            {
                int y = f.box.y - 1 - row;   // top row on screen is the highest y
                for (int x = 0; x < w; x++)
                {
                    var cell = new Rect(at.x + x * CellPx, at.y + row * CellPx, CellPx, CellPx);
                    bool on = f.On(x, y);
                    bool now = GUI.Toggle(cell, on, GUIContent.none, "Button");
                    if (now == on) continue;

                    if (!f.Fits) Materialise(f);
                    f.cells.GetArrayElementAtIndex(y * f.box.x + x).boolValue = now;
                }
            }
        }

        private static void Materialise(Fields f)
        {
            f.cells.arraySize = f.box.x * f.box.y;
            for (int i = 0; i < f.cells.arraySize; i++) f.cells.GetArrayElementAtIndex(i).boolValue = true;
        }

        // What the runtime will make of the current state when it is not what the picture suggests.
        private static string Note(Fields f)
        {
            if (f.box.x > MaxShown || f.box.y > MaxShown)
                return $"Only the first {MaxShown}x{MaxShown} cells are shown.";
            if (f.cells.arraySize == 0) return null;
            if (!f.Fits)
                return "Size changed after painting: the shape is reset to the full box. Click a cell to paint again.";

            bool anyOn = false, anyOff = false;
            for (int i = 0; i < f.cells.arraySize; i++)
                if (f.cells.GetArrayElementAtIndex(i).boolValue) anyOn = true; else anyOff = true;
            if (!anyOn) return "Nothing painted: the game treats this as the full box.";
            if (!anyOff) return null;

            if (EmptyEdge(f))
                return "An outer row or column is empty: the box is bigger than the shape, so the building will stand off-centre. Shrink Size instead.";
            return null;
        }

        // An unpainted bottom/top row or left/right column — a box that should have been smaller.
        private static bool EmptyEdge(Fields f)
        {
            bool RowOn(int y) { for (int x = 0; x < f.box.x; x++) if (f.On(x, y)) return true; return false; }
            bool ColOn(int x) { for (int y = 0; y < f.box.y; y++) if (f.On(x, y)) return true; return false; }
            return !RowOn(0) || !RowOn(f.box.y - 1) || !ColOn(0) || !ColOn(f.box.x - 1);
        }
    }
}
