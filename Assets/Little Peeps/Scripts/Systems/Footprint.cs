using UnityEngine;

namespace LittlePeeps
{
    // The cells a structure stands on, as a SHAPE inside its bounding box. `Size` is the box; a cell
    // (x, y) of it — x to the right, y UP, (0,0) the bottom-left — is a footprint cell when the mask
    // says so. A plain rectangle carries no mask (null): every cell of the box is a footprint cell,
    // which is how every def authored before shapes existed keeps working untouched.
    //
    // Shape and box are read separately on purpose. What a structure CLAIMS follows the shape:
    // CanPlace / Place / Remove, the spawner's exits, the territory halo, an animal's roaming ring.
    // Where it STANDS follows the box: IslandGrid.OriginToWorld* place the root at the box's bottom
    // centre and map the cursor to an origin by the box, because the art is drawn for the whole box
    // with the missing cells left transparent.
    //
    // A struct around one array reference, so a def may hand it out on every read for free; the array
    // is never written through it.
    public readonly struct Footprint
    {
        public readonly Vector2Int Size;
        private readonly bool[] mask;   // row-major, index = y * Size.x + x; null = the full box

        public Footprint(Vector2Int size, bool[] mask)
        {
            Size = size;
            // The mask only counts when it fits the box AND actually carves something out of it. A
            // stale one (the box was resized after painting), an all-on one and an all-off one (nothing
            // painted yet) all read as the full box — never as garbage, and never as "no cells".
            this.mask = Carves(size, mask) ? mask : null;
        }

        public static Footprint Rect(Vector2Int size) => new Footprint(size, null);
        public static Footprint Rect(int width, int height) => Rect(new Vector2Int(width, height));

        // A shape spelled out as rows of text, TOP row first, '#' for a footprint cell and anything
        // else for a hole — the readable way to write an L in a test:  Parse("#.", "##").
        public static Footprint Parse(params string[] rows)
        {
            int h = rows.Length, w = h > 0 ? rows[0].Length : 0;
            var mask = new bool[w * h];
            for (int i = 0; i < h; i++)
            {
                int y = h - 1 - i;
                for (int x = 0; x < w; x++) mask[y * w + x] = rows[i][x] == '#';
            }
            return new Footprint(new Vector2Int(w, h), mask);
        }

        public bool IsRect => mask == null;

        public int CellCount
        {
            get
            {
                if (mask == null) return Size.x * Size.y;
                int n = 0;
                foreach (bool b in mask) if (b) n++;
                return n;
            }
        }

        // Is local cell (x, y) of the box a footprint cell? Outside the box never is.
        public bool Contains(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Size.x || y >= Size.y) return false;
            return mask == null || mask[y * Size.x + x];
        }

        public bool Contains(Vector2Int local) => Contains(local.x, local.y);

        // Is local cell (x, y) claimed by the shape grown by `border` on every side, diagonals included
        // — a footprint cell, or within `border` king's moves of one? For a rectangle that is the box
        // expanded by `border`, exactly the territory a bordered house has always claimed; for a shape
        // the claim hugs it, and a notch narrower than 2*border+1 is swallowed. Callers sweep the box
        // expanded by `border` and ask this per cell.
        public bool Claims(int x, int y, int border)
        {
            if (border <= 0) return Contains(x, y);
            if (mask == null)
                return x >= -border && x < Size.x + border && y >= -border && y < Size.y + border;

            int x0 = Mathf.Max(0, x - border), x1 = Mathf.Min(Size.x - 1, x + border);
            int y0 = Mathf.Max(0, y - border), y1 = Mathf.Min(Size.y - 1, y + border);
            for (int cx = x0; cx <= x1; cx++)
                for (int cy = y0; cy <= y1; cy++)
                    if (mask[cy * Size.x + cx]) return true;
            return false;
        }

        // The lowest painted cell, leftmost within its row — the one cell a placement can be keyed on
        // whatever the shape (its box corner need not be painted). Zero for an empty box.
        public Vector2Int Anchor
        {
            get
            {
                for (int y = 0; y < Size.y; y++)
                    for (int x = 0; x < Size.x; x++)
                        if (Contains(x, y)) return new Vector2Int(x, y);
                return Vector2Int.zero;
            }
        }

        private static bool Carves(Vector2Int size, bool[] mask)
        {
            if (mask == null || mask.Length != size.x * size.y) return false;
            bool anyOn = false, anyOff = false;
            foreach (bool b in mask)
            {
                if (b) anyOn = true; else anyOff = true;
                if (anyOn && anyOff) return true;
            }
            return false;
        }

        public override string ToString()
        {
            if (mask == null) return $"{Size.x}x{Size.y}";
            var sb = new System.Text.StringBuilder();
            for (int y = Size.y - 1; y >= 0; y--)
            {
                for (int x = 0; x < Size.x; x++) sb.Append(mask[y * Size.x + x] ? '#' : '.');
                if (y > 0) sb.Append('/');
            }
            return sb.ToString();
        }
    }

    // The serialized form of a painted shape: the mask alone, boxed in a struct so an inspector attribute
    // can sit on the FIELD (Unity applies a field attribute on a bare array to each element instead).
    // Row-major, y=0 the bottom row; null or empty = the full box. Read through Footprint, never directly.
    [System.Serializable]
    public struct FootprintMask
    {
        public bool[] cells;
    }

    // Marks a def's FootprintMask as the paint grid of the footprint whose box is the sibling Vector2Int
    // field `sizeField`. The Editor assembly draws it (FootprintMaskDrawer); the runtime only reads the
    // cells through Footprint.
    public sealed class FootprintMaskAttribute : PropertyAttribute
    {
        public readonly string sizeField;
        public FootprintMaskAttribute(string sizeField) { this.sizeField = sizeField; }
    }
}
