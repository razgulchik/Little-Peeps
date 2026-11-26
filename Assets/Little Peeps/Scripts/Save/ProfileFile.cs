using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The profile's file format: MetaContext as plain, readable JSON, with a version and a checksum.
    //
    //   { "version": 1, "prestigePoints": 12, "agePointsAwarded": 8, "harvestPointsAwarded": 0,
    //     "upgrades": [ { "id": "productivity", "points": 1 } ], "checksum": "…" }
    //
    // The checksum is a DAMAGE check, not a lock: a write cut short or a flipped byte reads as a different
    // file, so ProfileStore falls back to the backup instead of loading nonsense. Anyone set on editing
    // the file can recompute it — for a single-player game that only spoils their own game, and a real
    // lock is impossible anyway with the key shipped inside the game (agreed with the user, 2026-11-26).
    //
    // A shape for the profile alone, on JsonUtility: the run save, much bigger, picks its own format when it
    // comes, so nothing here was chosen for it.
    public static class ProfileFile
    {
        // Raise when the shape changes, and teach TryRead to read the older versions it replaces.
        public const int CurrentVersion = 1;

        [Serializable]
        public class Data
        {
            public int version;
            public int prestigePoints;
            public int agePointsAwarded;
            public int harvestPointsAwarded;
            public List<Upgrade> upgrades = new();
            public string checksum = "";
        }

        // One taken upgrade: its GlobalUpgradeDef.id and the points it holds (its price when it was taken).
        [Serializable]
        public class Upgrade
        {
            public string id;
            public int points;
        }

        public static string ToJson(MetaContext meta)
        {
            var data = new Data
            {
                version = CurrentVersion,
                prestigePoints = meta.prestigePoints,
                agePointsAwarded = meta.agePointsAwarded,
                harvestPointsAwarded = meta.harvestPointsAwarded,
            };

            // By id, so the same profile always writes the same file — a diff of two saves shows only what
            // changed, whatever order the upgrades were taken in.
            if (meta.globalUpgrades != null)
            {
                var ids = new List<string>(meta.globalUpgrades.Keys);
                ids.Sort(StringComparer.Ordinal);
                foreach (string id in ids)
                    data.upgrades.Add(new Upgrade { id = id, points = meta.globalUpgrades[id] });
            }

            data.checksum = Checksum(data);
            return JsonUtility.ToJson(data, prettyPrint: true);
        }

        // False with the reason when the text is not a profile this game can use: not JSON, a version it does
        // not know, or a checksum that does not match what the numbers say.
        public static bool TryRead(string json, out MetaContext meta, out string problem)
        {
            meta = null;
            problem = null;

            Data data;
            try
            {
                data = JsonUtility.FromJson<Data>(json);
            }
            catch (Exception e)
            {
                problem = $"not valid JSON ({e.Message})";
                return false;
            }

            if (data == null)
            {
                problem = "empty";
                return false;
            }

            // Before the checksum: a profile from a newer game is a known situation (a rolled-back update),
            // and the log should say that rather than "damaged".
            if (data.version < 1 || data.version > CurrentVersion)
            {
                problem = $"version {data.version}, this game reads 1 to {CurrentVersion}";
                return false;
            }

            if (string.IsNullOrEmpty(data.checksum) || data.checksum != Checksum(data))
            {
                problem = "the checksum does not match — the file is damaged or was edited";
                return false;
            }

            meta = new MetaContext
            {
                prestigePoints = Math.Max(0, data.prestigePoints),
                agePointsAwarded = Math.Max(0, data.agePointsAwarded),
                harvestPointsAwarded = Math.Max(0, data.harvestPointsAwarded),
            };
            if (data.upgrades != null)
                foreach (var upgrade in data.upgrades)
                    if (upgrade != null && !string.IsNullOrEmpty(upgrade.id))
                        meta.globalUpgrades[upgrade.id] = Math.Max(0, upgrade.points);

            return true;
        }

        // SHA-256 of everything but the checksum itself, in the serializer's own compact form — so how the
        // file is laid out (indents, line breaks) does not matter, only what it says.
        private static string Checksum(Data data)
        {
            string kept = data.checksum;
            data.checksum = "";
            string canonical = JsonUtility.ToJson(data);
            data.checksum = kept;
            return SafeFile.Checksum(canonical);
        }
    }
}
