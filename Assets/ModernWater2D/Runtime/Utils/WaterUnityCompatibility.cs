using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Water2D
{
    internal static class WaterUnityCompatibility
    {
        public static Vector2 GetRigidbody2DLinearVelocity(Rigidbody2D rb)
        {
            if (rb == null) return Vector2.zero;

#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        public static T FindFirstObjectByTypeCompat<T>(bool includeInactive = false) where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindAnyObjectByType<T>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
        }

        public static T[] FindObjectsByTypeCompat<T>(bool includeInactive = false) where T : UnityEngine.Object
        {
            FindObjectsInactive inactiveMode = includeInactive
                ? FindObjectsInactive.Include
                : FindObjectsInactive.Exclude;

#if UNITY_6000_5_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<T>(inactiveMode);
#else
            return UnityEngine.Object.FindObjectsByType<T>(inactiveMode, FindObjectsSortMode.None);
#endif
        }

        public static int GetObjectStableId(UnityEngine.Object obj)
        {
            if (obj == null) return 0;

#if UNITY_6000_4_OR_NEWER
            return obj.GetEntityId().GetHashCode();
#else
            return obj.GetInstanceID();
#endif
        }

        public static void SetRenderTextureDepthStencil(RenderTexture renderTexture, GraphicsFormat graphicsFormat)
        {
            if (renderTexture == null) return;
            renderTexture.depthStencilFormat = graphicsFormat;
        }

        public static bool TryGetPixelPerfectRenderSize(Camera sourceCamera, out int baseWidth, out int baseHeight)
        {
            baseWidth = Mathf.Max(1, sourceCamera != null ? sourceCamera.scaledPixelWidth : Screen.width);
            baseHeight = Mathf.Max(1, sourceCamera != null ? sourceCamera.scaledPixelHeight : Screen.height);

            if (sourceCamera == null) return false;

            MonoBehaviour[] behaviours = sourceCamera.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null) continue;

                Type type = behaviour.GetType();
                if (type.Name != "PixelPerfectCamera") continue;

                int refX;
                int refY;
                if (!TryGetIntProperty(behaviour, type, "refResolutionX", out refX)) continue;
                if (!TryGetIntProperty(behaviour, type, "refResolutionY", out refY)) continue;
                if (refX <= 0 || refY <= 0) continue;

                bool stretchFill = false;
                TryGetBoolProperty(behaviour, type, "stretchFill", out stretchFill);

                float screenWidth = Mathf.Max(1, Screen.width);
                float screenHeight = Mathf.Max(1, Screen.height);
                float scaleX = screenWidth / refX;
                float scaleY = screenHeight / refY;
                float pixelScale = stretchFill ? Mathf.Max(scaleX, scaleY) : Mathf.Min(scaleX, scaleY);

                baseWidth = Mathf.Max(1, Mathf.RoundToInt(refX * pixelScale));
                baseHeight = Mathf.Max(1, Mathf.RoundToInt(refY * pixelScale));
                return true;
            }

            return false;
        }

        private static bool TryGetIntProperty(object target, Type type, string propertyName, out int value)
        {
            value = 0;
            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null) return false;

            object raw = property.GetValue(target, null);
            if (raw is int)
            {
                value = (int)raw;
                return true;
            }

            return false;
        }

        private static bool TryGetBoolProperty(object target, Type type, string propertyName, out bool value)
        {
            value = false;
            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null) return false;

            object raw = property.GetValue(target, null);
            if (raw is bool)
            {
                value = (bool)raw;
                return true;
            }

            return false;
        }
    }
}
