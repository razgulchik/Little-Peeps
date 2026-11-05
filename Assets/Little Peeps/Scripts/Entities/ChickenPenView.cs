using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The birds standing in a coop's pen: one model per READY bird, cloned from a template child, each on
    // its own spot inside penArea, turning left and right now and then as if alive. A resting bird is not
    // shown at all, so what stands in the pen is exactly what the next release lets out. Presentation only:
    // the flock's books are ChickenFlock's, and ChickenCoop says what to show. Nothing happens until the
    // first Show, so a placement ghost (the same prefab with its behaviours off) stands with an empty pen.
    public class ChickenPenView : MonoBehaviour
    {
        [Tooltip("Inactive child cloned once per bird: the sprite, sorting and shadow are authored on it.")]
        [SerializeField] private SpriteRenderer modelTemplate;

        [Tooltip("Where a bird's feet may stand, in this object's local space (world units). The yellow " +
                 "frame in the Scene view. Shape it to the pen in the art.")]
        [SerializeField] private Rect penArea = new(0.125f, 0.3125f, 0.625f, 0.25f);

        [Tooltip("Seconds between a ready bird's turns: a random value from x to y.")]
        [SerializeField] private Vector2 turnInterval = new(0.6f, 2.5f);

        // Samples per new bird: the one furthest from its neighbours wins, so birds spread out.
        private const int SpotTries = 6;

        private sealed class Model
        {
            public SpriteRenderer renderer;
            public float timer;
        }

        private readonly List<Model> models = new();   // the ones in the pen come first
        private int shown;     // how many models are in the pen

        public void Show(int ready)
        {
            if (modelTemplate == null) return;

            while (models.Count < ready) models.Add(NewModel());

            for (int i = shown; i < ready; i++) Bring(i);
            for (int i = ready; i < shown; i++) models[i].renderer.gameObject.SetActive(false);

            shown = ready;
        }

        private Model NewModel()
        {
            var renderer = Instantiate(modelTemplate, modelTemplate.transform.parent);
            return new Model { renderer = renderer };
        }

        // A bird arrives: a spot of its own, on the pixel grid, facing either way.
        private void Bring(int index)
        {
            var model = models[index];
            model.renderer.transform.localPosition = PickSpot(index);
            model.renderer.flipX = Random.value < 0.5f;
            model.renderer.gameObject.SetActive(true);
            model.timer = NextTurn();
        }

        private Vector3 PickSpot(int index)
        {
            Vector2 best = Sample();
            float bestDistance = -1f;
            for (int t = 0; t < SpotTries; t++)
            {
                Vector2 candidate = Sample();
                float nearest = float.PositiveInfinity;
                for (int i = 0; i < index; i++)
                    nearest = Mathf.Min(nearest, (candidate - (Vector2)models[i].renderer.transform.localPosition).sqrMagnitude);
                if (nearest > bestDistance)
                {
                    bestDistance = nearest;
                    best = candidate;
                }
            }
            return best;
        }

        private Vector2 Sample()
        {
            float ppu = modelTemplate.sprite != null ? modelTemplate.sprite.pixelsPerUnit : 16f;
            return new Vector2(Mathf.Round(Random.Range(penArea.xMin, penArea.xMax) * ppu) / ppu,
                               Mathf.Round(Random.Range(penArea.yMin, penArea.yMax) * ppu) / ppu);
        }

        private float NextTurn() => Random.Range(turnInterval.x, turnInterval.y);

        private void Update()
        {
            for (int i = 0; i < shown; i++)
            {
                var model = models[i];
                model.timer -= Time.deltaTime;
                if (model.timer > 0f) continue;

                model.renderer.flipX = !model.renderer.flipX;
                model.timer = NextTurn();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(penArea.center, penArea.size);
        }
    }
}
