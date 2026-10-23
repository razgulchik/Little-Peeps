using TMPro;
using UnityEngine;

namespace LittlePeeps
{
    // Puts every line of a TMP label on whole canvas pixels. A pixel font is crisp only while each glyph
    // starts on a pixel, and TMP alone rarely manages that: its centring puts a line whose width differs in
    // parity from its box half a pixel off, and its first baseline hangs from the font's ascent, which is
    // seldom a whole number. This moves each line after TMP has laid it out, just before the mesh is
    // uploaded, so it holds for multi-line, rich-text and changing text alike:
    //  - across: a centred line so its INK is centred, Floor-rounded (a pixel font's advance carries the
    //    empty column after the last letter, which would sit every line a pixel left of the middle — the
    //    AgeAdvancePanel.SetCentred rule); any other line so its ink starts on the nearest pixel;
    //  - down: each baseline to the nearest whole pixel below the box's top.
    //
    // Measured from the box's left and top EDGES, never from its pivot — an odd-sized box with a centred
    // pivot has its pivot between pixels. So the box's edges must sit on whole pixels, as everything in
    // this Canvas does. The font must also be at a size where its own pixels are whole (see the font
    // notes): this snaps where a line starts, it cannot fix a glyph drawn at a fractional scale.
    //
    // ExecuteAlways, so the edit-mode Game view already shows the label as it will play.
    [ExecuteAlways]
    [RequireComponent(typeof(TMP_Text))]
    public class PixelSnapText : MonoBehaviour
    {
        private TMP_Text text;

        private void OnEnable()
        {
            text = GetComponent<TMP_Text>();
            text.OnPreRenderText += Snap;
            text.SetVerticesDirty();
        }

        private void OnDisable()
        {
            if (text == null) return;

            text.OnPreRenderText -= Snap;
            text.SetVerticesDirty();
        }

        private void Snap(TMP_TextInfo info)
        {
            Rect box = text.rectTransform.rect;
            bool centred = (text.horizontalAlignment & (HorizontalAlignmentOptions.Center |
                                                        HorizontalAlignmentOptions.Geometry)) != 0;

            for (int l = 0; l < info.lineCount; l++)
            {
                TMP_LineInfo line = info.lineInfo[l];

                float left = float.MaxValue, right = float.MinValue;
                for (int i = line.firstCharacterIndex; i <= line.lastCharacterIndex; i++)
                {
                    TMP_CharacterInfo c = info.characterInfo[i];
                    if (!c.isVisible) continue;

                    var glyph = c.textElement.glyph.metrics;
                    float start = c.origin + glyph.horizontalBearingX * c.scale;
                    left = Mathf.Min(left, start);
                    right = Mathf.Max(right, start + glyph.width * c.scale);
                }

                if (left > right) continue;   // nothing visible on this line — nothing to move

                float inkLeft = left - box.xMin;
                float targetLeft = centred
                    ? Mathf.Floor((box.width - Mathf.Round(right - left)) / 2f)
                    : Mathf.Round(inkLeft);
                float drop = box.yMax - line.baseline;
                var shift = new Vector3(targetLeft - inkLeft, drop - Mathf.Round(drop), 0f);

                for (int i = line.firstCharacterIndex; i <= line.lastCharacterIndex; i++)
                {
                    TMP_CharacterInfo c = info.characterInfo[i];
                    if (!c.isVisible) continue;

                    Vector3[] vertices = info.meshInfo[c.materialReferenceIndex].vertices;
                    for (int v = 0; v < 4; v++) vertices[c.vertexIndex + v] += shift;
                }
            }
        }
    }
}
