using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace Water2D
{
    public static class SplineBoneCloner
    {
        public static Transform[] CloneBoneHierarchy(SpriteSkin source, Transform cloneParent, out Transform clonedRoot, out Dictionary<Transform, Transform> boneMap)
        {
            boneMap = new Dictionary<Transform, Transform>();
            Transform sourceRoot = source.rootBone;

            CloneRecursive(sourceRoot, cloneParent, boneMap);
            clonedRoot = boneMap[sourceRoot];

            Transform[] sourceBones = source.boneTransforms;
            Transform[] clonedBones = new Transform[sourceBones.Length];
            for (int i = 0; i < sourceBones.Length; i++)
                clonedBones[i] = boneMap[sourceBones[i]];

            return clonedBones;
        }

        static void CloneRecursive(Transform source, Transform cloneParent, Dictionary<Transform, Transform> map)
        {
            Transform clone = new GameObject(source.name).transform;
            clone.parent = cloneParent;
            clone.localPosition = source.localPosition;
            clone.localRotation = source.localRotation;
            clone.localScale = source.localScale;
            map[source] = clone;

            for (int i = 0; i < source.childCount; i++)
                CloneRecursive(source.GetChild(i), clone, map);
        }

        public static void AssignBonesToSkin(SpriteSkin target, Transform clonedRoot, Transform[] clonedBones)
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(SpriteSkin).GetField("m_RootBone", flags)?.SetValue(target, clonedRoot);
            typeof(SpriteSkin).GetField("m_BoneTransforms", flags)?.SetValue(target, clonedBones);
            target.autoRebind = false;
        }

        public static void SyncBones(Transform[] sourceBones, Transform[] clonedBones)
        {
            for (int i = 0; i < sourceBones.Length; i++)
            {
                clonedBones[i].position = sourceBones[i].position;
                clonedBones[i].rotation = sourceBones[i].rotation;
                clonedBones[i].localScale = sourceBones[i].localScale;
            }
        }

        // NEW: Syncs bones locally so they perfectly match when the parent object is inverted/scaled
        public static void SyncBonesLocal(Transform[] sourceBones, Transform[] clonedBones)
        {
            for (int i = 0; i < sourceBones.Length; i++)
            {
                clonedBones[i].localPosition = sourceBones[i].localPosition;
                clonedBones[i].localRotation = sourceBones[i].localRotation;
                clonedBones[i].localScale = sourceBones[i].localScale;
            }
        }

        // Kept for backward compatibility just in case you use it elsewhere
        public static void SyncBonesMirrored(Transform[] sourceBones, Transform[] clonedBones, float mirrorY)
        {
            for (int i = 0; i < sourceBones.Length; i++)
            {
                Vector3 pos = sourceBones[i].position;
                clonedBones[i].position = new Vector3(pos.x, 2f * mirrorY - pos.y, pos.z);

                clonedBones[i].rotation = Quaternion.Euler(sourceBones[i].rotation.eulerAngles);
                clonedBones[i].localScale = sourceBones[i].localScale;
            }
        }
    }
}