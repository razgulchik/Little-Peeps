using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittlePeeps.EditorTools
{
    // The ids saves store defs by, checked across the whole project. A save writes "good_rest is taken" or
    // "house stands at (3,5)" and finds the def again by that id on load, so an id that is empty or repeated
    // within its kind breaks a save without a word: the thing is gone, or another one comes back in its
    // place. A copy made with Ctrl+D carries its original's id — the likeliest way to get a repeat.
    //
    // The rules: a–z, 0–9 and _ only; never empty; unique within its kind, while two kinds may share one
    // (the birch structure and the birch source are one thing in two roles); never changed after release,
    // whatever happens to the title. An id says what the thing is — usually its name, for a wisdom its
    // effect (start_tent, not vagabonds). Only the first three can be checked; the last two are on us.
    //
    // What gets checked is found, not listed: every ScriptableObject class of the game with a string field
    // called `id` — the save key's name by the same rules. So a new def with an id is checked from its first
    // compile, and there is no list to forget.
    //
    // DefIdWatch runs this on every def that is saved or imported, DefIdTests over the whole project.
    public static class DefIdAudit
    {
        // One kind of def a save keys by id: the class that declares the id. A subclass belongs to its
        // base's kind — every perk is one catalogue and one list in a save, whatever its effect.
        public sealed class Kind
        {
            public readonly Type type;
            private readonly FieldInfo[] path;

            public Kind(Type type, FieldInfo[] path)
            {
                this.type = type;
                this.path = path;
            }

            public string Label => type.Name;

            // Where the id sits in the def: "id", or "profile.id" for a biome, whose id is part of the
            // plain-data profile the generator reads.
            public string IdPath
            {
                get
                {
                    var names = new string[path.Length];
                    for (int i = 0; i < path.Length; i++) names[i] = path[i].Name;
                    return string.Join(".", names);
                }
            }

            public string IdOf(Object def)
            {
                object value = def;
                foreach (var field in path)
                {
                    if (value == null) return null;
                    value = field.GetValue(value);
                }
                return value as string;
            }
        }

        // Found once per domain, and every compile reloads the domain — so a class added or changed in code
        // is in the next check after it compiles.
        private static Kind[] kinds;

        public static IReadOnlyList<Kind> Kinds => kinds ??= Discover();

        // How deep into serializable blocks an id is looked for. One is what the biome needs; the margin is
        // for a block inside a block, and the limit keeps a block that holds its own type from looping.
        private const int MaxDepth = 3;

        // The game's own classes only: they all live in one runtime assembly, the one StructureDef is in.
        private static Kind[] Discover()
        {
            var found = new List<Kind>();
            foreach (var type in typeof(StructureDef).Assembly.GetTypes())
            {
                if (!typeof(ScriptableObject).IsAssignableFrom(type)) continue;
                var path = IdPathOf(type, declaredOnly: true, depth: 0);
                if (path != null) found.Add(new Kind(type, path));
            }
            found.Sort((a, b) => string.CompareOrdinal(a.Label, b.Label));
            return found.ToArray();
        }

        // The fields from an instance of `type` down to its id, or null when it has none. On a def, only the
        // fields it declares itself: an inherited id is its base's, and the def is in the base's kind.
        private static FieldInfo[] IdPathOf(Type type, bool declaredOnly, int depth)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            if (declaredOnly) flags |= BindingFlags.DeclaredOnly;
            var fields = type.GetFields(flags);

            foreach (var field in fields)
                if (field.Name == "id" && field.FieldType == typeof(string) && IsSerialized(field))
                    return new[] { field };

            if (depth >= MaxDepth) return null;

            foreach (var field in fields)
            {
                if (!IsSerialized(field) || !IsBlock(field.FieldType)) continue;
                var rest = IdPathOf(field.FieldType, declaredOnly: false, depth + 1);
                if (rest == null) continue;

                var path = new FieldInfo[rest.Length + 1];
                path[0] = field;
                rest.CopyTo(path, 1);
                return path;
            }
            return null;
        }

        // What Unity writes to the asset: a public field, or a private one marked [SerializeField].
        private static bool IsSerialized(FieldInfo field) =>
            !field.IsStatic && !field.IsInitOnly && !field.IsLiteral && !field.IsNotSerialized &&
            (field.IsPublic || field.IsDefined(typeof(SerializeField), false));

        // A plain-data block the asset stores inline, like the biome's profile. Not a reference to another
        // asset (its id is that asset's, checked there), not a list (ids of entries inside one asset are not
        // what a save looks defs up by).
        private static bool IsBlock(Type type) =>
            !typeof(Object).IsAssignableFrom(type) && !typeof(IEnumerable).IsAssignableFrom(type) &&
            !type.IsPrimitive && !type.IsEnum && type.IsDefined(typeof(SerializableAttribute), false);

        private static readonly Regex Format = new("^[a-z0-9_]+$");

        public static bool IsWellFormed(string id) => id != null && Format.IsMatch(id);

        // Every kind `asset` belongs to: none for anything that is not a def with an id, one for every def
        // so far — more only if a subclass added an id of its own.
        public static IEnumerable<Kind> KindsOf(Object asset)
        {
            if (asset == null) yield break;
            foreach (var kind in Kinds)
                if (kind.type.IsInstanceOfType(asset)) yield return kind;
        }

        // Every asset of `kind` under Assets/ — packages hold none of ours. The type filter takes the base
        // class and finds its subclasses with it; the check after the load keeps out anything that only
        // shares the class name.
        public static List<Object> FindAll(Kind kind)
        {
            var found = new List<Object>();
            var guids = new HashSet<string>(AssetDatabase.FindAssets("t:" + kind.type.Name, new[] { "Assets" }));
            foreach (string guid in guids)
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (kind.type.IsInstanceOfType(asset)) found.Add(asset);
            }
            return found;
        }

        // What is wrong with the ids of `defs`, all of one kind, as (def, line) — only the defs at fault.
        public static List<(Object def, string message)> Check(Kind kind, IReadOnlyList<Object> defs)
        {
            var entries = new (string name, string id)[defs.Count];
            for (int i = 0; i < defs.Count; i++) entries[i] = (defs[i].name, kind.IdOf(defs[i]));

            var result = new List<(Object, string)>();
            foreach (var (index, message) in Problems(kind.Label, entries))
                result.Add((defs[index], message));
            return result;
        }

        // The rules themselves, on plain names and ids so they are testable without assets. One line per
        // fault, keyed by the def's index in `defs`. A repeat is reported on EVERY def that holds the id,
        // each naming the others: the watch reports only the def that was just saved, and that may be
        // either one.
        public static List<(int index, string message)> Problems(string label, IReadOnlyList<(string name, string id)> defs)
        {
            var problems = new List<(int, string)>();
            var holders = new Dictionary<string, List<int>>();

            for (int i = 0; i < defs.Count; i++)
            {
                var (name, id) = defs[i];
                if (string.IsNullOrEmpty(id))
                {
                    problems.Add((i, $"{label} '{name}' has no id. A save stores it by id, so it would be lost on load."));
                    continue;
                }

                if (!IsWellFormed(id))
                    problems.Add((i, $"{label} '{name}': id '{id}' breaks the format — only a–z, 0–9 and _."));

                if (!holders.TryGetValue(id, out var list)) holders[id] = list = new List<int>();
                list.Add(i);
            }

            foreach (var pair in holders)
            {
                if (pair.Value.Count < 2) continue;
                foreach (int i in pair.Value)
                {
                    var others = new List<string>();
                    foreach (int j in pair.Value)
                        if (j != i) others.Add($"'{defs[j].name}'");
                    problems.Add((i, $"{label} '{defs[i].name}' repeats the id '{pair.Key}' of {string.Join(", ", others)}. " +
                                     "A save would load one in place of the other."));
                }
            }

            return problems;
        }
    }
}
