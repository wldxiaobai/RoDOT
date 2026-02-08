using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SaveManeger : Globalizer<SaveManeger>
{
    public void SaveGame(SaveData data)
    {
        string json = JsonUtility.ToJson(data);
        System.IO.File.WriteAllText(Application.persistentDataPath + "/save.json", json);
    }

    public SaveData LoadGame()
    {
        string path = Application.persistentDataPath + "/save.json";
        if (System.IO.File.Exists(path))
        {
            string json = System.IO.File.ReadAllText(path);
            return JsonUtility.FromJson<SaveData>(json);
        }
        return null;
    }
}

[System.Serializable]
public class SaveData
{
    public int score;
    public float playerX;
    public float playerY;
    // 你需要保存的数据
}