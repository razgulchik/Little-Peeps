using System.Collections.Generic;

namespace LittlePeeps
{
    // A part of a structure whose state a saved run carries (StructureSave.states): a few numbers, written
    // when the run is snapshotted (RunManager.Capture) and put back the moment the structure is rebuilt
    // (RunManager.TryRestoreRun) — right after it is instantiated, before any Start of its, so its views
    // come up already showing it and nothing replays: a felled tree is a stump, not a tree being reaped.
    //
    // Numbers only, so a snapshot stays a copy of plain data. A structure's states are put back in the
    // order its components are found (GetComponentsInChildren — the same for the same prefab), each only
    // onto a component of the same kind; a prefab that changed since the save loses that state, nothing
    // more. LoadState must therefore accept any numbers and clamp them to what the component allows.
    public interface IRunState
    {
        // What kind of state this is — stable, since it goes to disk.
        string StateKey { get; }

        void SaveState(List<float> into);

        void LoadState(IReadOnlyList<float> from);
    }
}
