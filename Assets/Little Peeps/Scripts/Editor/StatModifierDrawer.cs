using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LittlePeeps.EditorTools
{
    // Inspector for one StatModifier.
    //
    // The problem it solves: a modifier's six fields are never all meaningful at once. Which scope
    // dimensions a stat actually uses is declared in StatMeta.ScopeOf, and RunStats.MakeKey silently
    // zeroes every other one before the value is ever read. The default inspector drew all six
    // regardless, so an author could set a Resource on SourceRespawn and never learn it was thrown
    // away -- three of the ten modifiers authored so far carry exactly that kind of dead value.
    //
    // This drawer asks the SAME StatMeta.ScopeOf the runtime asks. It deliberately keeps no private
    // table of "which fields matter", so what is editable here is by construction what the game
    // reads: add a stat or change its scope mask and the inspector follows, with no second place to
    // update and nothing that can drift.
    [CustomPropertyDrawer(typeof(StatModifier))]
    public class StatModifierDrawer : PropertyDrawer
    {
        // One entry per line the drawer puts on screen. Height measurement and drawing walk this same
        // list, which is what stops a conditional row from desynchronising the two.
        private enum Row { Stat, Hint, Unit, Resource, ResourceFromSource, Source, Flat, Percent, NoEffect, Junk }

        private static readonly StatId[] AllIds = (StatId[])System.Enum.GetValues(typeof(StatId));
        private static readonly ResourceType[] AllRes =
            (ResourceType[])System.Enum.GetValues(typeof(ResourceType));

        private static float Line => EditorGUIUtility.singleLineHeight;
        private static float Gap => EditorGUIUtility.standardVerticalSpacing;
        private static float Box => EditorGUIUtility.singleLineHeight * 2f;

        // The struct's fields, resolved once per pass. Invalid if this drawer is ever pointed at
        // something that only looks like a StatModifier -- see the guard in OnGUI.
        private readonly struct Fields
        {
            public readonly SerializedProperty id, unit, res, src, flat, percent;

            public Fields(SerializedProperty p)
            {
                id = p.FindPropertyRelative("id");
                unit = p.FindPropertyRelative("unitScope");
                res = p.FindPropertyRelative("resourceScope");
                src = p.FindPropertyRelative("sourceScope");
                flat = p.FindPropertyRelative("flat");
                percent = p.FindPropertyRelative("percent");
            }

            public bool Valid => id != null && unit != null && res != null
                              && src != null && flat != null && percent != null;
        }

        // ---------------------------------------------------------------- layout

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return Line;

            var f = new Fields(property);
            if (!f.Valid) return EditorGUI.GetPropertyHeight(property, label, true);

            var rows = new List<Row>();
            BuildRows(f, rows);

            float h = Line;
            foreach (var row in rows) h += Gap + HeightOf(row);
            return h;
        }

        private static void BuildRows(Fields f, List<Row> rows)
        {
            var id = IdOf(f.id);
            var scope = StatMeta.ScopeOf(id);

            rows.Add(Row.Stat);
            if (!string.IsNullOrEmpty(HintLine(id, scope))) rows.Add(Row.Hint);

            if ((scope & StatScope.Unit) != 0) rows.Add(Row.Unit);

            // Resource stops being a choice the moment a source is filled in: a source produces exactly
            // one resource and RunStats.MakeKey derives it. An editable field there would offer a
            // decision that changes nothing and can only contradict itself.
            if ((scope & StatScope.Resource) != 0)
                rows.Add(DerivedSource(scope, f) != null ? Row.ResourceFromSource : Row.Resource);

            if ((scope & StatScope.Source) != 0) rows.Add(Row.Source);

            rows.Add(Row.Flat);
            rows.Add(Row.Percent);

            if (f.flat.floatValue == 0f && f.percent.floatValue == 0f) rows.Add(Row.NoEffect);
            if (JunkText(scope, f) != null) rows.Add(Row.Junk);
        }

        private static float HeightOf(Row row) => row switch
        {
            Row.NoEffect => Box,
            Row.Junk => Box + Gap + Line,
            _ => Line,
        };

        // ---------------------------------------------------------------- drawing

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var f = new Fields(property);
            if (!f.Valid)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            // The list element's own label ("Element 3") carries no information, so the foldout header
            // is the modifier read back as a sentence instead. Collapsed is the useful default: a whole
            // age's bonuses are then legible without expanding anything.
            var r = new Rect(position.x, position.y, position.width, Line);
            property.isExpanded = EditorGUI.Foldout(r, property.isExpanded, Summary(f), true);

            if (property.isExpanded)
            {
                var id = IdOf(f.id);
                var scope = StatMeta.ScopeOf(id);

                var rows = new List<Row>();
                BuildRows(f, rows);

                EditorGUI.indentLevel++;
                foreach (var row in rows)
                {
                    r.y += r.height + Gap;
                    r.height = HeightOf(row);
                    Draw(r, row, property, f, id, scope);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        private static void Draw(Rect r, Row row, SerializedProperty property,
                                 Fields f, StatId id, StatScope scope)
        {
            switch (row)
            {
                case Row.Stat:
                {
                    var field = EditorGUI.PrefixLabel(
                        r, new GUIContent("Stat", "What this modifier changes."));
                    var shown = new GUIContent(Category(scope) + " / " + Label(id));
                    if (EditorGUI.DropdownButton(field, shown, FocusType.Keyboard))
                        ShowStatMenu(property, id);
                    break;
                }

                case Row.Hint:
                    EditorGUI.LabelField(r, " ", HintLine(id, scope), EditorStyles.miniLabel);
                    break;

                case Row.Unit:
                    EditorGUI.PropertyField(r, f.unit,
                        new GUIContent("Unit", "Which unit type the bonus applies to."));
                    break;

                case Row.Resource:
                    EditorGUI.PropertyField(r, f.res,
                        new GUIContent("Resource", "Which resource the bonus applies to -- every source " +
                                                   "of it, as long as Source below stays empty."));
                    break;

                case Row.ResourceFromSource:
                {
                    // Guarded even though BuildRows only asks for this row when the source is there:
                    // rows are chosen once per pass, and a reordering that put Source above this one
                    // would let the field be cleared between the two.
                    var src = DerivedSource(scope, f);
                    if (src == null) break;

                    using (new EditorGUI.DisabledScope(true))
                        EditorGUI.LabelField(r,
                            new GUIContent("Resource", "Fixed by the source below: Tree is Wood, Wheat " +
                                                       "is Food. Clear the source to target a whole " +
                                                       "resource across every source instead."),
                            new GUIContent(src.resource + "  (from " + src.name + ")"));
                    break;
                }

                case Row.Source:
                    EditorGUI.PropertyField(r, f.src,
                        new GUIContent("Source", "Which source (Tree, Wheat, Alpaka, ...). Leave EMPTY " +
                                                 "to affect every source -- that is a real value, not a " +
                                                 "blank, and it is what makes a village-wide bonus one " +
                                                 "modifier instead of one per source."));
                    break;

                case Row.Flat:
                    EditorGUI.PropertyField(r, f.flat,
                        new GUIContent("Flat", "Added to the base value, before percents."));
                    break;

                case Row.Percent:
                    EditorGUI.PropertyField(r, f.percent,
                        new GUIContent("Percent", "0.1 = +10%. Percents from every source add up first, " +
                                                  "then multiply once: final = (base + flat) * (1 + percent)."));
                    break;

                case Row.NoEffect:
                    EditorGUI.HelpBox(EditorGUI.IndentedRect(r),
                        "Flat and Percent are both 0 -- this modifier does nothing.",
                        MessageType.Warning);
                    break;

                case Row.Junk:
                {
                    // The Source field is drawn earlier in this same pass, so clearing it there can
                    // resolve the complaint before this row gets to state it. Leave the space blank for
                    // the one frame rather than warn about something that is no longer true.
                    string text = JunkText(scope, f);
                    if (text == null) break;

                    var box = EditorGUI.IndentedRect(new Rect(r.x, r.y, r.width, Box));
                    EditorGUI.HelpBox(box, text, MessageType.Warning);

                    var btn = EditorGUI.IndentedRect(new Rect(r.x, r.y + Box + Gap, r.width, Line));
                    if (GUI.Button(btn, "Clean up these fields")) Normalise(scope, f);
                    break;
                }
            }
        }

        // Cleaning is a button and not an automatic reaction to changing the stat: switching the stat
        // by accident and back must not cost the author a source reference they had dragged in.
        private static void Normalise(StatScope scope, Fields f)
        {
            if ((scope & StatScope.Unit) == 0) f.unit.enumValueIndex = 0;
            if ((scope & StatScope.Source) == 0) f.src.objectReferenceValue = null;

            // After the source has been dropped, not before: DerivedSource must see the cleared state
            // and fall through to zeroing, rather than copy a resource off a reference on its way out.
            var derived = DerivedSource(scope, f);
            if (derived != null) f.res.enumValueIndex = System.Array.IndexOf(AllRes, derived.resource);
            else if ((scope & StatScope.Resource) == 0) f.res.enumValueIndex = 0;
        }

        // The source, on a stat that carries BOTH a resource and a source dimension -- the case where
        // the source fixes the resource and Resource stops being an independent choice. Null when there
        // is no such source, which is what keeps "a whole resource, from anywhere" authorable.
        //
        // Unity's == rather than ReferenceEquals: this reference is about to be DEREFERENCED, so a
        // destroyed or missing asset has to read as "no source" instead of throwing mid-repaint.
        private static ResourceSourceDef DerivedSource(StatScope scope, Fields f)
        {
            if ((scope & StatScope.Resource) == 0 || (scope & StatScope.Source) == 0) return null;
            var src = f.src.objectReferenceValue as ResourceSourceDef;
            return src == null ? null : src;
        }

        // A GenericMenu callback runs long after this OnGUI returned, and a SerializedProperty is only
        // valid for the pass that produced it. Capturing the SerializedObject plus the property PATH
        // and re-resolving inside the callback is what survives that gap.
        private static void ShowStatMenu(SerializedProperty property, StatId current)
        {
            var target = property.serializedObject;
            string path = property.propertyPath;

            var menu = new GenericMenu();
            foreach (var id in AllIds)
            {
                menu.AddItem(new GUIContent(Category(StatMeta.ScopeOf(id)) + "/" + Label(id)),
                             id == current,
                             () =>
                             {
                                 if (target == null || target.targetObject == null) return;
                                 target.Update();
                                 var idProp = target.FindProperty(path)?.FindPropertyRelative("id");
                                 if (idProp == null) return;
                                 idProp.enumValueIndex = System.Array.IndexOf(AllIds, id);
                                 target.ApplyModifiedProperties();
                             });
            }
            menu.ShowAsContext();
        }

        // ---------------------------------------------------------------- text

        // The modifier read back as a sentence, e.g. "Lumberjack - Move speed +50%" or
        // "any source - Respawn time -10%". Only dimensions the stat actually uses appear, so the line
        // never claims a scope the runtime will discard.
        private static string Summary(Fields f)
        {
            var id = IdOf(f.id);
            var scope = StatMeta.ScopeOf(id);

            var derived = DerivedSource(scope, f);

            var who = new List<string>();
            if ((scope & StatScope.Unit) != 0) who.Add(EnumName(f.unit));
            // The resource the RUNTIME will key on, which is the source's own whenever there is one --
            // reading back the raw field here would let the header state something MakeKey overrules.
            if ((scope & StatScope.Resource) != 0)
                who.Add(derived != null ? derived.resource.ToString() : EnumName(f.res));
            if ((scope & StatScope.Source) != 0)
            {
                var s = f.src.objectReferenceValue;
                who.Add(s == null ? "any source" : s.name);
            }

            var value = new List<string>();
            if (f.flat.floatValue != 0f)
                value.Add(f.flat.floatValue.ToString("+0.##;-0.##"));
            if (f.percent.floatValue != 0f)
                value.Add((f.percent.floatValue * 100f).ToString("+0.##;-0.##") + "%");

            return (who.Count > 0 ? string.Join(" / ", who) + " - " : "")
                 + Label(id) + " "
                 + (value.Count > 0 ? string.Join(", ", value) : "no effect");
        }

        // Derived from the scope mask rather than a hand-kept list, so a stat added later lands in a
        // sensible group whether or not anyone remembers this file.
        private static string Category(StatScope scope)
        {
            if ((scope & StatScope.Resource) != 0) return "Resources";
            if ((scope & StatScope.Unit) != 0) return "Units";
            if ((scope & StatScope.Source) != 0) return "Sources";
            return "Global";
        }

        private static string Label(StatId id) => id switch
        {
            StatId.ProductionGlobal => "All production",
            StatId.ResourceYield => "Yield per hit",
            StatId.UnitSpeed => "Move speed",
            StatId.SpawnerRecharge => "Spawner recharge",
            StatId.UnitFatigueDelay => "Fatigue delay",
            StatId.SourceRespawn => "Respawn time",
            _ => ObjectNames.NicifyVariableName(id.ToString()),
        };

        // One line of "what the number does". For the duration stats this is the whole point: they
        // scale SECONDS, so faster is a NEGATIVE percent, and that is exactly the sign nobody guesses
        // right from the field name alone.
        //
        // The "no scopes" tail is not decoration. A stat missing from StatMeta.ScopeOf falls through to
        // StatScope.None and silently becomes global; printing it here is what makes that visible to
        // the person authoring the asset instead of a mystery at runtime.
        private static string HintLine(StatId id, StatScope scope)
        {
            string hint = id switch
            {
                StatId.ProductionGlobal => "Multiplies every resource gained from a harvest.",
                StatId.ResourceYield => "How much one hit yields.",
                StatId.UnitSpeed => "Unit movement speed.",
                StatId.SpawnerRecharge => "Seconds a unit rests in a spawner. Negative percent = launches sooner.",
                StatId.UnitFatigueDelay => "Seconds a unit roams before entering a house. Negative percent = rests sooner.",
                StatId.SourceRespawn => "Seconds a depleted source takes to regrow. Negative percent = regrows faster.",
                _ => "",
            };

            if (scope != StatScope.None) return hint;

            const string global = "Applies globally - no scopes.";
            return string.IsNullOrEmpty(hint) ? global : hint + " " + global;
        }

        // Values left over from a previous stat choice. Returns null when there is nothing to report.
        //
        // Blind spot worth knowing: Farmer and Food are both enum value 0, so a leftover Farmer is
        // indistinguishable from an untouched field and goes unreported. Harmless -- MakeKey zeroes it
        // either way -- but it means a clean inspector is not a proof of a clean file.
        private static string JunkText(StatScope scope, Fields f)
        {
            List<string> dead = null;
            void Report(string s) => (dead ??= new List<string>()).Add(s);

            if ((scope & StatScope.Unit) == 0 && f.unit.enumValueIndex != 0)
                Report("Unit = " + EnumName(f.unit));
            if ((scope & StatScope.Resource) == 0 && f.res.enumValueIndex != 0)
                Report("Resource = " + EnumName(f.res));
            if ((scope & StatScope.Source) == 0 && f.src.objectReferenceValue != null)
                Report("Source = " + f.src.objectReferenceValue.name);

            // Not a dimension the stat ignores, but a value its source overrules: authorable only by
            // hand-edited YAML, by code, or by an asset written before the source axis existed.
            var derived = DerivedSource(scope, f);
            if (derived != null && ResOf(f.res) != derived.resource)
                Report("Resource = " + EnumName(f.res) + ", while " + derived.name
                       + " gives " + derived.resource);

            return dead == null
                ? null
                : "Values the key will not carry: " + string.Join(", ", dead)
                + ". MakeKey normalises them away, so they only mislead the next reader.";
        }

        private static StatId IdOf(SerializedProperty p)
        {
            int i = p.enumValueIndex;
            return i >= 0 && i < AllIds.Length ? AllIds[i] : default;
        }

        private static ResourceType ResOf(SerializedProperty p)
        {
            int i = p.enumValueIndex;
            return i >= 0 && i < AllRes.Length ? AllRes[i] : default;
        }

        private static string EnumName(SerializedProperty p)
        {
            var names = p.enumDisplayNames;
            int i = p.enumValueIndex;
            return i >= 0 && i < names.Length ? names[i] : "?";
        }
    }
}
