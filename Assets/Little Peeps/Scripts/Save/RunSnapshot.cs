using System;
using System.Collections.Generic;

namespace LittlePeeps
{
    // A run as it is written to disk: plain numbers and ids only, so it can be made on the main thread in a
    // moment, kept in memory and written later (SaveSystem), and read back with nothing of the scene around.
    //
    // What it holds is the run's own state; everything derived is rebuilt on load and NOT stored: the stat
    // sheet (from the start config, the meta perks, the ages and the perks, in that order), the standing
    // counts and the unlocked structures (from the perks), the units (fresh in their houses), the drawing.
    // Ids are the defs' stable `id` fields; a def that is gone is skipped on load with a warning.
    //
    // Format and decisions: RunFile. Version: RunFile.CurrentVersion.
    [Serializable]
    public class RunSnapshot
    {
        public int version;

        // The island: the seed it was generated from (the drawing's decor and the future zones read it), and
        // every section as it was committed — shape and generation plan (IslandSnapshot).
        public int seed;
        public List<SectionSave> sections = new();

        // The perk rolls made so far: the next one is derived from this number (PerkSystem.RollPerks), so a
        // continued run is offered the cards the left one would have been.
        public int perkRolls;

        // Everything that stands, natural features included. Ground first, then what stands over water (a
        // bridge needs its river under it), then the fences — the order they are put back in.
        public List<StructureSave> structures = new();
        public List<StructureSave> bridges = new();
        public List<FenceSave> fences = new();

        public List<AmountSave> resources = new();
        public List<AmountSave> harvested = new();

        public int currentAge;

        // RunContext.perksChosen by id, in the order taken: a perk at level 3 stands here three times.
        public List<string> perks = new();

        // RunContext.upgrades by id: the meta perks this run was built with — from the profile and the start
        // config as they were then, not as they are now.
        public List<string> upgrades = new();

        public int offeringCycle;
        public double offeringElapsed;
        public double playSeconds;
        public int perkPicksOwed;
        public int freeAgesLeft;
        public bool tiredGoHome;

        // RunContext.chosenZoom. A file from before it reads as NotChosen: the view follows the age, as it did.
        public int chosenZoom = RunContext.NotChosen;

        public string checksum = "";
    }

    [Serializable]
    public class StructureSave
    {
        public string id;
        public int x;
        public int y;

        // Still a construction site: it goes back up as one and finishes as usual.
        public bool site;

        // What its parts were in the middle of (IRunState): a tree's regrow, the forge's heat, a den's
        // animals. Empty for a site.
        public List<StateSave> states = new();
    }

    [Serializable]
    public class StateSave
    {
        public string key;   // IRunState.StateKey
        public List<float> values = new();
    }

    [Serializable]
    public class FenceSave
    {
        public string id;
        public int x;
        public int y;
        public bool horizontal;
        public bool site;
    }

    [Serializable]
    public class AmountSave
    {
        public string type;    // ResourceType by NAME, so a reordered enum cannot shift what was stored
        public float amount;
    }

    // One committed section of the island: its cells, and the generation plan of its zone (IslandSectionContent)
    // — the future zones are planned around it, so it must come back exactly, not just the land. Cell sets
    // are flat x, y pairs, sorted, so the same island always writes the same file.
    [Serializable]
    public class SectionSave
    {
        public List<int> cells = new();

        // False for a zone committed bare (its content pass failed): the rest below is empty then.
        public bool hasContent;
        public string biome;   // IslandBiome.id

        public FootprintSave houseFootprint = new();
        public FootprintSave pierFootprint = new();
        public List<FootprintSave> extraFootprints = new();

        public List<ObjectSave> objects = new();
        public List<int> mountains = new();
        public List<int> river = new();          // in path order, not sorted
        public List<int> house = new();
        public int houseOriginX, houseOriginY;
        public List<int> pier = new();
        public int pierOriginX, pierOriginY;
        public List<int> extras = new();
        public List<int> extraOrigins = new();   // in extraFootprints' order
        public List<int> clearing = new();
        public List<int> reserved = new();
        public List<int> bridges = new();        // six per bridge: the river cell, then its two banks
        public List<int> mineAccess = new();
        public List<int> millAccess = new();
    }

    [Serializable]
    public class FootprintSave
    {
        public int width;
        public int height;
        public List<bool> mask = new();   // row-major; empty = the full box
    }

    [Serializable]
    public class ObjectSave
    {
        public int x;
        public int y;
        public string def;   // the rule's StructureDef id, to find the rule in its biome again
    }
}
