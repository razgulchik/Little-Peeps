using System;
using UnityEngine;

namespace LittlePeeps
{
    // The flow strokes' map: what the sea's shader ("Little Peeps/Water") needs to give every stroke of the tile a
    // life of its own — grow it from its middle, fade it, shade it towards its ends. A stroke is a run of
    // non-clear pixels along a row of the tile (the artist's art: white where a stroke is, clear elsewhere); the
    // map holds, for each of its pixels, where in its stroke it lies and how long the stroke is. The rest — when a
    // stroke lives, how far it has grown — the shader works out from that and from where the stroke starts in the
    // world, so every copy of the tile across the sea lives on a clock of its own.
    //
    // Per texel: R = offset from the stroke's first pixel, G = the stroke's length, both in pixels; B = the tile's
    // own alpha (a stroke may be drawn half-clear); A = 255 on a stroke, 0 elsewhere.
    //
    // Rows wrap, because the tile repeats: a stroke running off the right edge goes on at the left one and is one
    // stroke, starting where it starts on the right. A row that is stroke from end to end starts at its first
    // pixel. Offsets and lengths are bytes, so they stop at 255: a tile with longer strokes than that is not
    // supported.
    public static class WaterFlowStrokes
    {
        public const int MaxLength = 255;

        // The map as a texture for the shader, built from the material's tile, which must be readable (Read/Write
        // in its import settings). Linear: its bytes are numbers, not colours. The caller owns it.
        public static Texture2D CreateTexture(Texture2D tile)
        {
            var map = new Texture2D(tile.width, tile.height, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                name = tile.name + " strokes",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
            };
            map.SetPixels32(Build(tile.GetPixels32(), tile.width, tile.height));
            map.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return map;
        }

        // pixels: the tile row by row, as Texture2D.GetPixels32 hands it over.
        public static Color32[] Build(Color32[] pixels, int width, int height)
        {
            if (pixels.Length != width * height) throw new ArgumentException("The pixels are not width × height.", nameof(pixels));

            var map = new Color32[pixels.Length];
            for (int y = 0; y < height; y++) BuildRow(pixels, map, y * width, width);
            return map;
        }

        private static void BuildRow(Color32[] pixels, Color32[] map, int row, int width)
        {
            int clear = -1;
            for (int x = 0; x < width; x++)
                if (pixels[row + x].a == 0) { clear = x; break; }

            if (clear < 0)
            {
                Mark(pixels, map, row, width, 0, width);
                return;
            }

            // Once round the row from just past a clear pixel, ending on it: no run starts mid-stroke, and every
            // run is closed by a clear pixel.
            int first = clear + 1;
            int runStart = -1;
            for (int step = 0; step < width; step++)
            {
                if (pixels[row + (first + step) % width].a > 0)
                {
                    if (runStart < 0) runStart = step;
                    continue;
                }
                if (runStart < 0) continue;
                Mark(pixels, map, row, width, first + runStart, step - runStart);
                runStart = -1;
            }
        }

        private static void Mark(Color32[] pixels, Color32[] map, int row, int width, int start, int length)
        {
            byte stored = (byte)Math.Min(length, MaxLength);
            for (int i = 0; i < length; i++)
            {
                int x = (start + i) % width;
                map[row + x] = new Color32((byte)Math.Min(i, MaxLength), stored, pixels[row + x].a, 255);
            }
        }
    }
}
