using System.IO;
using UnityEngine;

// Serializes MetaContext to/from JSON in Application.persistentDataPath.
// Note: MetaContext.globalUpgrades (Dictionary) needs a custom serialization wrapper; implement alongside Save/Load.
public class SaveSystem : MonoBehaviour
{
    private const string SaveFileName = "meta.json";

    private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    // Write MetaContext as JSON to disk
    public void Save(MetaContext meta)
    {
        // TODO: string json = JsonUtility.ToJson(meta, prettyPrint: true); File.WriteAllText(SavePath, json)
        // TODO: also serialize meta.globalUpgrades separately (JsonUtility doesn't support Dictionary)
    }

    // Read JSON from disk; return fresh MetaContext if no save file exists
    public MetaContext Load()
    {
        // TODO: if !File.Exists(SavePath) return new MetaContext()
        // TODO: string json = File.ReadAllText(SavePath); MetaContext meta = JsonUtility.FromJson<MetaContext>(json)
        // TODO: deserialize globalUpgrades from companion JSON array back into meta.globalUpgrades Dictionary
        return new MetaContext();
    }

    // Remove save file (e.g. for debug reset)
    public void DeleteSave()
    {
        // TODO: if File.Exists(SavePath), File.Delete(SavePath)
    }
}
