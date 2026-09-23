using UnityEngine;
using UnityEditor;
using System;

namespace Modern2DWater
{
    [InitializeOnLoad]
    public class AssetReviewPrompt
    {
        private const string ASSET_URL = "https://assetstore.unity.com/packages/tools/particles-effects/modern-2d-water-2-2-5d-simulated-urp-water-255068";
        private const string PREF_FIRST_USE = "Modern2DWater_FirstUseTime";
        private const string PREF_LAST_PROMPT = "Modern2DWater_LastPromptTime";
        private const string PREF_PROMPT_SHOWN_AT = "Modern2DWater_PromptShownAt";
        private const string PREF_REVIEW_DISMISSED = "Modern2DWater_ReviewDismissed";
        private const float PROMPT_DISPLAY_DURATION = 120f; 

        private static DateTime firstUseTime;
        private static DateTime lastPromptTime;
        private static DateTime promptShownAt;
        private static bool reviewDismissed;
        
   
        private static readonly float[] promptIntervals = { 2f, 4f, 8f, 16f, 32f, 64f, 128f };
        
        static AssetReviewPrompt()
        {
            LoadPreferences();
            EditorApplication.update += OnEditorUpdate;
        }

        private static void LoadPreferences()
        {
           
            if (!EditorPrefs.HasKey(PREF_FIRST_USE))
            {
                firstUseTime = DateTime.Now;
                EditorPrefs.SetString(PREF_FIRST_USE, firstUseTime.ToBinary().ToString());
            }
            else
            {
                long binary = long.Parse(EditorPrefs.GetString(PREF_FIRST_USE));
                firstUseTime = DateTime.FromBinary(binary);
            }

          
            if (EditorPrefs.HasKey(PREF_LAST_PROMPT))
            {
                long binary = long.Parse(EditorPrefs.GetString(PREF_LAST_PROMPT));
                lastPromptTime = DateTime.FromBinary(binary);
            }
            else
            {
                lastPromptTime = DateTime.MinValue;
            }

         
            if (EditorPrefs.HasKey(PREF_PROMPT_SHOWN_AT))
            {
                long binary = long.Parse(EditorPrefs.GetString(PREF_PROMPT_SHOWN_AT));
                promptShownAt = DateTime.FromBinary(binary);
            }
            else
            {
                promptShownAt = DateTime.MinValue;
            }

            reviewDismissed = EditorPrefs.GetBool(PREF_REVIEW_DISMISSED, false);
        }

        private static void OnEditorUpdate()
        {
        
            CheckForPrompt();
        }

        private static void CheckForPrompt()
        {
            if (reviewDismissed) return;

            TimeSpan timeSinceFirstUse = DateTime.Now - firstUseTime;
            float hoursSinceFirstUse = (float)timeSinceFirstUse.TotalHours;

            foreach (float interval in promptIntervals)
            {
             
                if (hoursSinceFirstUse >= interval)
                {
                    TimeSpan timeSinceLastPrompt = DateTime.Now - lastPromptTime;
                    float hoursSinceLastPrompt = (float)timeSinceLastPrompt.TotalHours;

    
                    if (hoursSinceLastPrompt >= interval || lastPromptTime == DateTime.MinValue)
                    {
                        
                        break;
                    }
                }
            }
        }

        public static bool ShouldShowPrompt()
        {
            if (reviewDismissed) return false;

            TimeSpan timeSinceFirstUse = DateTime.Now - firstUseTime;
            float hoursSinceFirstUse = (float)timeSinceFirstUse.TotalHours;


            foreach (float interval in promptIntervals)
            {
                if (hoursSinceFirstUse >= interval)
                {
                    TimeSpan timeSinceLastPrompt = DateTime.Now - lastPromptTime;
                    float hoursSinceLastPrompt = (float)timeSinceLastPrompt.TotalHours;

                    if (hoursSinceLastPrompt >= interval || lastPromptTime == DateTime.MinValue)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static void OnInspectorOpened()
        {
            if (ShouldShowPrompt() && promptShownAt == DateTime.MinValue)
            {
                promptShownAt = DateTime.Now;
                EditorPrefs.SetString(PREF_PROMPT_SHOWN_AT, promptShownAt.ToBinary().ToString());
            }
        }

        public static bool IsPromptActive()
        {
            if (!ShouldShowPrompt()) return false;
            if (promptShownAt == DateTime.MinValue) return false;

            TimeSpan timeSinceShown = DateTime.Now - promptShownAt;
            return timeSinceShown.TotalSeconds < PROMPT_DISPLAY_DURATION;
        }

        public static void OnReviewClicked()
        {
            Application.OpenURL(ASSET_URL);
            MarkPromptCompleted();
        }

        public static void OnDismissed()
        {
            MarkPromptCompleted();
        }

        public static void OnNeverAskAgain()
        {
            reviewDismissed = true;
            EditorPrefs.SetBool(PREF_REVIEW_DISMISSED, true);
            MarkPromptCompleted();
        }

        private static void MarkPromptCompleted()
        {
            lastPromptTime = DateTime.Now;
            EditorPrefs.SetString(PREF_LAST_PROMPT, lastPromptTime.ToBinary().ToString());
            
            promptShownAt = DateTime.MinValue;
            EditorPrefs.DeleteKey(PREF_PROMPT_SHOWN_AT);
        }

        public static float GetUsageHours()
        {
            TimeSpan timeSinceFirstUse = DateTime.Now - firstUseTime;
            return (float)timeSinceFirstUse.TotalHours;
        }

   
        [MenuItem("Tools/Modern 2D Water/Reset Review Prompt")]
        private static void ResetPrompt()
        {
            EditorPrefs.DeleteKey(PREF_FIRST_USE);
            EditorPrefs.DeleteKey(PREF_LAST_PROMPT);
            EditorPrefs.DeleteKey(PREF_PROMPT_SHOWN_AT);
            EditorPrefs.DeleteKey(PREF_REVIEW_DISMISSED);
            LoadPreferences();
            Debug.Log("Review prompt reset!");
        }
    }
}
