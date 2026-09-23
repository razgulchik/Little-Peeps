using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace Water2D
{
    [ExecuteInEditMode]
    [Serializable]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(SpriteSkin))]
    public class ReflectorSpline : MonoBehaviour
    {
        [SerializeField][HideInInspector] public WaterCryo<bool> flipX = new WaterCryo<bool>(false);
        [SerializeField][HideInInspector] public WaterCryo<Vector2> displacement = new WaterCryo<Vector2>(new Vector2(0, 0));

        [HideInInspector][SerializeField] Transform _reflection;
        [HideInInspector][SerializeField] SpriteRenderer _reflectionSr;

        SpriteSkin _sourceSpriteSkin;
        Transform[] _sourceBones;
        Transform[] _clonedBones;
        Transform _bonesRoot;
        Sprite _lastSyncedSprite;

        void OnEnable()
        {
            flipX.onValueChanged = CreateData;
            displacement.onValueChanged = CreateData;
          
        }

        void Start()
        {
            if (_reflection != null)
                _reflection.gameObject.layer = Obstructor.GetLayerIdx(ReflectionsSystem.rlayer);
            CreateData();
        }

        public void CreateData()
        {
            _sourceSpriteSkin = GetComponent<SpriteSkin>();
            if (_sourceSpriteSkin == null || _sourceSpriteSkin.rootBone == null) return;

            bool reuseData = _reflection != null;
            Transform reflection = reuseData ? _reflection : new GameObject("reflection_spline : " + name).transform;

            CleanOldBones();

#if UNITY_EDITOR
            if (!WaterLayers.LayerExists(ReflectionsSystem.rlayer)) WaterLayers.CreateLayer(ReflectionsSystem.rlayer);
#endif
            reflection.gameObject.layer = Obstructor.GetLayerIdx(ReflectionsSystem.rlayer);

#if UNITY_EDITOR
            if (ReflectionsSystem.GetInstanceTopDown() != null && !ReflectionsSystem.GetInstanceTopDown().reflectionObjectsVisible.value)
                UnityEditor.SceneVisibilityManager.instance.Hide(reflection.gameObject, true);
#endif

            if (ReflectionsSystem.GetInstanceTopDown() != null && !ReflectionsSystem.GetInstanceTopDown().reflectionObjectsVisible.value)
                reflection.gameObject.hideFlags = HideFlags.HideInHierarchy;

            if (!reuseData) reflection.parent = transform;
            reflection.localPosition = Vector3.zero;
            reflection.localScale = Vector3.one;
            reflection.localRotation = Quaternion.identity;

            SpriteRenderer sourceSr = GetComponent<SpriteRenderer>();
            SpriteRenderer reflectionSr = reuseData ? reflection.GetComponent<SpriteRenderer>() : reflection.gameObject.AddComponent<SpriteRenderer>();
            reflectionSr.color = sourceSr.color;
            reflectionSr.material = ReflectionsSystem.instanceTopDown.reflectorMat;
            reflectionSr.sharedMaterial = ReflectionsSystem.instanceTopDown.reflectorMat;
            reflectionSr.sortingLayerName = sourceSr.sortingLayerName;
            reflectionSr.sortingOrder = sourceSr.sortingOrder;
            reflectionSr.flipX = flipX.value ? !sourceSr.flipX : sourceSr.flipX;
            reflectionSr.flipY = sourceSr.flipY;
            reflectionSr.sprite = sourceSr.sprite;

            SetupClonedBones(reflection);

            _reflection = reflection;
            _reflectionSr = reflectionSr;
            _sourceBones = _sourceSpriteSkin.boneTransforms;
            _lastSyncedSprite = sourceSr.sprite;
        }

        void SetupClonedBones(Transform child)
        {
            SpriteSkin childSkin = child.GetComponent<SpriteSkin>();
            if (childSkin == null) childSkin = child.gameObject.AddComponent<SpriteSkin>();

            Transform clonedRoot;
            Dictionary<Transform, Transform> boneMap;
            _clonedBones = SplineBoneCloner.CloneBoneHierarchy(_sourceSpriteSkin, child, out clonedRoot, out boneMap);
            _bonesRoot = clonedRoot;

            SplineBoneCloner.AssignBonesToSkin(childSkin, clonedRoot, _clonedBones);
        }

        void CleanOldBones()
        {
            if (_bonesRoot != null) DestroyImmediate(_bonesRoot.gameObject);
            _bonesRoot = null;
            _clonedBones = null;
        }

        float GetMirrorY()
        {
            SpriteRenderer sourceSr = GetComponent<SpriteRenderer>();
            return sourceSr.bounds.min.y + displacement.value.y;
        }

        void LateUpdate()
        {
            if (_sourceBones == null || _clonedBones == null) return;
            if (_reflectionSr == null) return;

            // 1. Calculate where the mirror line is
            float mirrorY = GetMirrorY();
            Vector3 sourcePos = transform.position;

            // 2. Move the entire reflection GameObject to the mirrored world position
            _reflection.position = new Vector3(sourcePos.x, 2f * mirrorY - sourcePos.y, sourcePos.z);

            // 3. Invert the Y-scale to automatically flip the mesh and bone structure upside down
            _reflection.localScale = new Vector3(1f, -1f, 1f);

            // 4. Sync the bones LOCALLY
            SplineBoneCloner.SyncBonesLocal(_sourceBones, _clonedBones);

            // 5. Sync Sprite changes
            SpriteRenderer sourceSr = GetComponent<SpriteRenderer>();
            if (sourceSr.sprite != _lastSyncedSprite)
            {
                _reflectionSr.sprite = sourceSr.sprite;
                _reflectionSr.flipX = flipX.value ? !sourceSr.flipX : sourceSr.flipX;
                _reflectionSr.flipY = sourceSr.flipY;
                _lastSyncedSprite = sourceSr.sprite;
            }
        }

        public void DeleteData()
        {
            CleanOldBones();
            if (_reflection != null) DestroyImmediate(_reflection.gameObject);
            _reflection = null;
            _reflectionSr = null;
        }

        public void UpdateData()
        {
            CreateData();
        }

        protected void OnDestroy()
        {
            if (!gameObject.scene.isLoaded) return;
            if (!Application.isPlaying) DeleteData();
        }
    }
}