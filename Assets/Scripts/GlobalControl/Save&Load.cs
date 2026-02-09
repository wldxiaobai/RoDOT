using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SaveManeger : Globalizer<SaveManeger>
{
    [Header("默认存档数据(第一关)")]
    [Tooltip("默认玩家位置")]
    [SerializeField] private Vector2 defaultPlayerPosition = new Vector2(-7.56f, -1.9f);
    [Tooltip("默认场景名称")]
    [SerializeField] protected string defaultSceneName = "SampleScene";

    [Header("在以下场景中会生成玩家")]
    [SerializeField] private List<string> spawnPlayerScenes = new List<string>();

    public SaveData Data { get; set; }

    static private SaveData defaultData = new SaveData();

    protected override void GlobeInit()
    {
        defaultData.PlayerPosition = defaultPlayerPosition;
        defaultData.SceneName = defaultSceneName;

        // 订阅场景加载事件，在场景加载完成后设置玩家位置
        SceneLoader.Instance.OnSceneLoad += SetPlayerPositionOnSceneLoad;
    }

    public void NewGame()
    {
        Debug.Log("设置新存档...");
        LevelInfoDict.ResetStates();
        Data = defaultData;
        DataSave();
        GameLoad();
    }

    public void GameLoad()
    {
        Debug.Log("加载游戏...");
        DataLoad();
        if(SceneLoader.Instance == null)
        {
            Debug.LogError("SceneLoader实例未找到，无法加载场景");
        }
        StartCoroutine(SceneLoader.Instance.LoadSceneAsync(Data.SceneName));
    }

    void SetPlayerPositionOnSceneLoad(string scene)
    {
        // 在加载场景时设置玩家位置
        if (spawnPlayerScenes.Contains(scene))
        {
            GlobalPlayer.Instance.SpawnPlayer(Data.PlayerPosition, Quaternion.identity);
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        // 确保在对象销毁时取消订阅事件，避免潜在的内存泄漏或错误调用
        SceneLoader.Instance.OnSceneLoad -= SetPlayerPositionOnSceneLoad;

    }

    public void DataSave()
    {
        Debug.Log("保存玩家信息...");
        Data.SaveLevelInfo(); // 保存LevelInfoDict到LevelInfo和LevelStates
        string json = JsonUtility.ToJson(Data);
        string savePath = Application.persistentDataPath + "/save.json";
        System.IO.File.WriteAllText(savePath, json);
        Debug.Log($"已将玩家信息保存到 {savePath}");
    }

    public void DataLoad()
    {
        string path = Application.persistentDataPath + "/save.json";
        Debug.Log($"尝试从以下路径加载玩家存档：{path}");
        if (System.IO.File.Exists(path))
        {
            string json = System.IO.File.ReadAllText(path);
            var data = JsonUtility.FromJson<SaveData>(json);
            data.LoadLevelInfo(); // 加载LevelInfoDict
            Data = data;
            Debug.Log("玩家信息加载成功.");
        }
        else
        {
            Debug.LogWarning("未找到玩家信息.");
        }
        return;
    }
}

[System.Serializable]
public class SaveData
{
    [SerializeField] private float playerX;
    [SerializeField] private float playerY;
    [SerializeField] private string sceneName;
    [SerializeField] private List<string> LevelInfo = new List<string>();
    [SerializeField] private List<int> LevelStates = new List<int>();
    // 你需要保存的数据

    public Vector2 PlayerPosition
    {
        get
        { return new Vector2(playerX, playerY); }
        set
        {
            playerX = value.x;
            playerY = value.y;
        }
    }

    public string SceneName
    {
        get { return sceneName; }
        set { sceneName = value; }
    }

    public void SetLevelInfo(string info, int state)
    {
        LevelInfoDict.SetState(info, state);
    }

    public void RemoveLevelInfo(string info)
    {
        LevelInfoDict.Remove(info);
    }

    public int GetLevelInfo(string info)
    {
        return LevelInfoDict.GetState(info);
    }

    public void LoadLevelInfo()
    {
        LevelInfoDict.LoadFromLists(LevelInfo, LevelStates);
    }

    public void SaveLevelInfo()
    {
        LevelInfoDict.PopulateLists(LevelInfo, LevelStates);
    }
}

public static class LevelInfoDict
{
    private static readonly Dictionary<string, int> _levelStates = new Dictionary<string, int>();

    public static IReadOnlyDictionary<string, int> Entries => _levelStates;

    public static void SetState(string levelName, int state)
    {
        if (string.IsNullOrWhiteSpace(levelName))
            return;

        _levelStates[levelName] = state;
    }

    public static void Remove(string levelName)
    {
        if (string.IsNullOrWhiteSpace(levelName))
            return;

        _levelStates.Remove(levelName);
    }

    public static int GetState(string levelName, int defaultState = -1)
    {
        if (string.IsNullOrWhiteSpace(levelName))
            return defaultState;

        return _levelStates.TryGetValue(levelName, out int state) ? state : defaultState;
    }

    public static void ResetStates(int defaultState = 0)
    {
        var keys = new List<string>(_levelStates.Keys);
        foreach (var key in keys)
        {
            _levelStates[key] = defaultState;
        }
    }

    public static void LoadFromLists(IList<string> levelNames, IList<int> levelStates)
    {
        _levelStates.Clear();

        if (levelNames == null || levelStates == null)
            return;

        int count = Mathf.Min(levelNames.Count, levelStates.Count);
        for (int i = 0; i < count; i++)
        {
            var key = levelNames[i];
            if (string.IsNullOrWhiteSpace(key))
                continue;

            _levelStates[key] = levelStates[i];
        }
    }

    public static void PopulateLists(IList<string> levelNames, IList<int> levelStates)
    {
        if (levelNames == null || levelStates == null)
            return;

        levelNames.Clear();
        levelStates.Clear();

        foreach (var kvp in _levelStates)
        {
            levelNames.Add(kvp.Key);
            levelStates.Add(kvp.Value);
        }
    }
}
