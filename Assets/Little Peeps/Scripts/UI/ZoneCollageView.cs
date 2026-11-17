using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // A biome's collage seen through a window smaller than itself: the zone card's icon. At rest the
    // window shows the collage's top-left corner; while playing it travels the collage clockwise — along
    // the top, down the right, back along the bottom, up the left — so a player who keeps the cursor on
    // the card gets to see every part of it in turn.
    //
    // This object IS the window: its rect is what shows, a RectMask2D on it cuts the rest. The collage is
    // a child anchored and pivoted to the window's top-left corner, at its native size. How far the window
    // can travel is the difference of the two rects, so a collage of another size needs no setting here.
    //
    // Unscaled time: the zone choice runs with the game frozen (timeScale 0). Positions are whole canvas
    // pixels — a pixel-art collage drawn half a pixel off blurs.
    public class ZoneCollageView : MonoBehaviour
    {
        [Tooltip("The collage: a child of this window, anchored and pivoted to its top-left corner, at the " +
                 "sprite's native size. Larger than the window — the window shows a part of it.")]
        [SerializeField] private Image collage;

        [Tooltip("How fast the window travels over the collage while playing, in canvas pixels per second.")]
        [Min(0f)] [SerializeField] private float speed = 10f;

        private float travelled;   // distance along the loop from the top-left corner
        private bool playing;

        // A new collage starts at rest in its top-left corner. No sprite hides the image rather than
        // showing a white square.
        public void Show(Sprite sprite)
        {
            if (collage == null) return;

            collage.sprite = sprite;
            collage.enabled = sprite != null;

            travelled = 0f;
            playing = false;
            Place();
        }

        // Stopping keeps the window where it is: playing again carries on from there.
        public void SetPlaying(bool value)
        {
            playing = value;
        }

        private void Update()
        {
            if (!playing || collage == null) return;

            float loop = Loop(Travel());
            if (loop <= 0f) return;

            travelled = (travelled + speed * Time.unscaledDeltaTime) % loop;
            Place();
        }

        // How far the window can move across and down.
        private Vector2 Travel()
        {
            Vector2 travel = collage.rectTransform.rect.size - ((RectTransform)transform).rect.size;
            return Vector2.Max(travel, Vector2.zero);
        }

        private static float Loop(Vector2 travel) => 2f * (travel.x + travel.y);

        private void Place()
        {
            if (collage == null) return;

            Vector2 travel = Travel();
            Vector2 view = PointOnLoop(travel, travelled);

            // The window moving right and down is the collage moving left and up.
            collage.rectTransform.anchoredPosition = new Vector2(-Mathf.Round(view.x), Mathf.Round(view.y));
        }

        // The window's top-left corner over the collage, `distance` along the clockwise loop, measured
        // from the collage's top-left corner with y growing downwards.
        private static Vector2 PointOnLoop(Vector2 travel, float distance)
        {
            float d = distance;
            if (d < travel.x) return new Vector2(d, 0f);                     // along the top, rightwards

            d -= travel.x;
            if (d < travel.y) return new Vector2(travel.x, d);               // down the right side

            d -= travel.y;
            if (d < travel.x) return new Vector2(travel.x - d, travel.y);    // along the bottom, leftwards

            d -= travel.x;
            return new Vector2(0f, Mathf.Max(0f, travel.y - d));             // up the left side
        }
    }
}
