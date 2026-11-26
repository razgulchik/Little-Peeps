using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittlePeeps.EditorTools
{
    // Warns in the console the moment a def with a broken id lands on disk: created, copied with Ctrl+D,
    // saved after an edit, brought in by a pull or a merge (the rules are DefIdAudit's). Only the defs that
    // just changed are reported: an older fault elsewhere in the same kind is DefIdTests' to show, so saving
    // one perk never repeats another perk's warning.
    //
    // Two hooks feed one queue because neither covers every case alone: an import is a file that changed on
    // disk (a new asset, a copy, a pull), a save is an edit made in the inspector. The queue is checked once,
    // after the editor is done with the batch, so a save that also reimports the asset warns once, and a pull
    // of fifty assets searches each kind once.
    public sealed class DefIdWatch : AssetPostprocessor
    {
        private static readonly HashSet<string> Pending = new();
        private static bool scheduled;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved,
                                                   string[] movedFrom)
        {
            Queue(imported);
            Queue(moved);
        }

        internal static void Queue(string[] paths)
        {
            foreach (string path in paths)
                if (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) Pending.Add(path);

            if (Pending.Count == 0 || scheduled) return;
            scheduled = true;
            EditorApplication.delayCall += Flush;
        }

        private static void Flush()
        {
            scheduled = false;
            var changed = new HashSet<Object>();
            var kinds = new HashSet<DefIdAudit.Kind>();
            foreach (string path in Pending)
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                foreach (var kind in DefIdAudit.KindsOf(asset))
                {
                    changed.Add(asset);
                    kinds.Add(kind);
                }
            }
            Pending.Clear();

            foreach (var kind in kinds)
                foreach (var (def, message) in DefIdAudit.Check(kind, DefIdAudit.FindAll(kind)))
                    if (changed.Contains(def)) Debug.LogWarning("[Ids] " + message, def);
        }
    }

    // The save half of DefIdWatch: an id typed in the inspector reaches the check when the asset is saved.
    public sealed class DefIdSaveWatch : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            DefIdWatch.Queue(paths);
            return paths;
        }
    }
}
