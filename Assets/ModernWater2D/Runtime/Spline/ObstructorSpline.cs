using UnityEngine;
using UnityEngine.U2D.Animation;

namespace Water2D
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(SpriteSkin))]
    public class ObstructorSpline : MonoBehaviour
    {
        [SerializeField][HideInInspector] ObstructorSO _data;
        [SerializeField][HideInInspector][Range(0f, 1f)] public float height;
        [SerializeField][HideInInspector] SpriteRenderer sr;

        SpriteSkin _sourceSpriteSkin;
        Sprite _lastSyncedSprite;

        [HideInInspector]
        public ObstructorSO data
        {
            get
            {
                if (_data == null || !IsValid()) CreateData();
                return _data;
            }
            set { _data = value; }
        }

        public void CreateData()
        {
            _sourceSpriteSkin = GetComponent<SpriteSkin>();
            if (_sourceSpriteSkin == null) return;

            Transform obstructor = (_data != null && _data.child != null) ? _data.child : new GameObject("obstructor_spline : " + name).transform;
            Transform source = transform;

#if UNITY_EDITOR
            if (!WaterLayers.LayerExists(ObstructorManager.rlayer)) WaterLayers.CreateLayer(ObstructorManager.rlayer);
#endif
            obstructor.gameObject.layer = Obstructor.GetLayerIdx(ObstructorManager.rlayer);

            obstructor.parent = source;
            obstructor.position = source.position;
            obstructor.rotation = source.rotation;
            obstructor.localScale = source.localScale;

#if UNITY_EDITOR
            if (ObstructorManager.GetInstance() != null && !ObstructorManager.GetInstance().obstructionObjectsVisible.value)
                UnityEditor.SceneVisibilityManager.instance.Hide(obstructor.gameObject, true);
#endif

            if (ObstructorManager.GetInstance() != null && !ObstructorManager.GetInstance().obstructionObjectsVisible.value)
                obstructor.gameObject.hideFlags = HideFlags.HideInHierarchy;

            SpriteRenderer sourceSr = GetComponent<SpriteRenderer>();

            SpriteRenderer obstructorSr = obstructor.GetComponent<SpriteRenderer>();
            if (obstructorSr == null) obstructorSr = obstructor.gameObject.AddComponent<SpriteRenderer>();

            obstructorSr.color = sourceSr.color;
            obstructorSr.sortingLayerName = sourceSr.sortingLayerName;
            obstructorSr.sortingOrder = sourceSr.sortingOrder;
            obstructorSr.flipX = sourceSr.flipX;
            obstructorSr.flipY = sourceSr.flipY;
            obstructorSr.sprite = sourceSr.sprite;
            obstructorSr.material = ObstructorManager.red;

            SetupChildSpriteSkin(obstructor);
            UpdateMaterialProperties(obstructorSr);

            _lastSyncedSprite = sourceSr.sprite;

            ObstructorSO SO = new ObstructorSO(source, obstructor, sourceSr, obstructorSr);
            data = SO;
        }

        void SetupChildSpriteSkin(Transform child)
        {
            SpriteSkin childSkin = child.GetComponent<SpriteSkin>();
            if (childSkin == null) childSkin = child.gameObject.AddComponent<SpriteSkin>();

            CopySpriteSkinBones(_sourceSpriteSkin, childSkin);
        }

        static void CopySpriteSkinBones(SpriteSkin source, SpriteSkin target)
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(SpriteSkin).GetField("m_RootBone", flags)?.SetValue(target, source.rootBone);
            typeof(SpriteSkin).GetField("m_BoneTransforms", flags)?.SetValue(target, source.boneTransforms);
            target.autoRebind = false;
        }

        void UpdateMaterialProperties(SpriteRenderer obstructorSr)
        {
            if (obstructorSr.sprite == null) return;

            MaterialPropertyBlock prop = new MaterialPropertyBlock();
            obstructorSr.GetPropertyBlock(prop);
            prop.SetFloat("_h", height);

            float texH = obstructorSr.sprite.texture.height;
            prop.SetFloat("_ss", (texH <= obstructorSr.sprite.rect.height ? 0 : 1));
            prop.SetFloat("_minY", obstructorSr.sprite.rect.yMin / texH);
            prop.SetFloat("_maxY", obstructorSr.sprite.rect.yMax / texH);

            obstructorSr.SetPropertyBlock(prop);
        }

        void LateUpdate()
        {
            if (_data == null || _data.sourceSr == null || _data.childSr == null) return;

            SyncTransform();

            if (_data.sourceSr.sprite != _lastSyncedSprite)
            {
                _data.childSr.sprite = _data.sourceSr.sprite;
                _data.childSr.flipX = _data.sourceSr.flipX;
                _data.childSr.flipY = _data.sourceSr.flipY;
                UpdateMaterialProperties(_data.childSr);
                _lastSyncedSprite = _data.sourceSr.sprite;
            }
        }

        void SyncTransform()
        {
            if (_data.child == null) return;
            _data.child.position = transform.position;
            _data.child.rotation = transform.rotation;
            _data.child.localScale = transform.localScale;
        }

        public void Destroy()
        {
            ObstructorManager.instance?.RemoveObstructor(transform);
            if (_data != null && _data.child != null) DestroyImmediate(_data.child.gameObject);
            DestroyImmediate(this);
        }

        bool IsValid()
        {
            if (_data.childSr == null) return false;
            if (_data.child == null) return false;
            if (_data.source == null) return false;
            if (_data.sourceSr == null) return false;
            if (_data.childSr.sprite == null) return false;
            return true;
        }

        protected void Awake()
        {
            ObstructorManager.GetInstance()?.AddObstructor(data);
        }

        void OnEnable()
        {
            data.child.gameObject.layer = Obstructor.GetLayerIdx(ObstructorManager.rlayer);
            CreateData();
            if (_data != null && _data.childSr != null) UpdateMaterialProperties(_data.childSr);
        }

        protected void OnDestroy()
        {
            if (!Application.isPlaying)
            {
                ObstructorManager.instance?.RemoveObstructor(transform);
                if (_data != null && _data.child != null) DestroyImmediate(_data.child.gameObject);
            }
        }

        void OnDrawGizmos()
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            Gizmos.color = Color.red;
            float x1 = sr.bounds.center.x - sr.bounds.extents.x;
            float x2 = sr.bounds.center.x + sr.bounds.extents.x;
            float y = sr.bounds.center.y - sr.bounds.extents.y + (2 * sr.bounds.extents.y * height);
            Gizmos.DrawLine(new Vector2(x1, y), new Vector2(x2, y));
        }
    }
}