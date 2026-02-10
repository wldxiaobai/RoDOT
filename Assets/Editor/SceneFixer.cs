#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class SceneBuildSettingsFixer : EditorWindow
{
    [MenuItem("Tools/Scene Build Settings Fixer")]
    public static void ShowWindow()
    {
        GetWindow<SceneBuildSettingsFixer>("Scene Fixer");
    }

    private Vector2 scrollPos;

    private void OnGUI()
    {
        GUILayout.Label("场景构建设置修复工具", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // 显示当前 Build Settings 中的场景
        GUILayout.Label("当前 Build Settings 中的场景:", EditorStyles.boldLabel);

        scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(150));
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

        for (int i = 0; i < scenes.Length; i++)
        {
            string sceneName = Path.GetFileNameWithoutExtension(scenes[i].path);
            string status = scenes[i].enabled ? "[启用]" : "[禁用]";

            if (string.IsNullOrEmpty(sceneName))
            {
                EditorGUILayout.HelpBox($"索引 {i}: (空名称) - 路径: {scenes[i].path}", MessageType.Error);
            }
            else
            {
                EditorGUILayout.LabelField($"{i}: {status} {sceneName}", new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = scenes[i].enabled ? Color.white : Color.gray }
                });
            }
        }

        GUILayout.EndScrollView();
        GUILayout.Space(10);

        // 按钮区域
        if (GUILayout.Button("第一步：强制重新保存所有场景", GUILayout.Height(35)))
        {
            ForceResaveAllScenes();
        }

        GUILayout.Space(5);

        if (GUILayout.Button("第二步：清空并重建 Build Settings", GUILayout.Height(35)))
        {
            RebuildBuildSettings();
        }

        GUILayout.Space(5);

        if (GUILayout.Button("第三步：验证场景名称", GUILayout.Height(35)))
        {
            ValidateSceneNames();
        }

        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "操作步骤：\n" +
            "1. 点击'第一步'强制重新保存所有场景\n" +
            "2. 点击'第二步'清空并重建 Build Settings\n" +
            "3. 点击'第三步'验证场景名称是否正确\n" +
            "4. 在代码中确保场景名称拼写正确",
            MessageType.Info
        );
    }

    private void ForceResaveAllScenes()
    {
        // 保存当前打开的场景
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            int processedCount = 0;

            foreach (string guid in sceneGuids)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(guid);

                // 跳过 Packages 和 Settings 文件夹
                if (scenePath.StartsWith("Packages/") || scenePath.Contains("/Settings/"))
                    continue;

                Debug.Log($"重新保存场景: {scenePath}");

                // 打开场景
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                // 标记为脏并保存
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                processedCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("完成",
                $"已重新保存 {processedCount} 个场景文件。\n" +
                $"请继续点击'第二步'按钮。",
                "确定");
        }
    }

    private void RebuildBuildSettings()
    {
        // 清空现有设置
        EditorBuildSettings.scenes = new EditorBuildSettingsScene[0];

        List<EditorBuildSettingsScene> sceneList = new List<EditorBuildSettingsScene>();

        // 定义场景优先级顺序
        string[] priorityScenes = { "StartMenu", "SampleScene", "Level2" };

        // 首先添加优先场景
        foreach (string sceneName in priorityScenes)
        {
            string[] matchingScenes = AssetDatabase.FindAssets(sceneName + " t:Scene", new[] { "Assets/Scenes" });

            foreach (string guid in matchingScenes)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(scenePath);

                if (fileName == sceneName)
                {
                    sceneList.Add(new EditorBuildSettingsScene(scenePath, true));
                    Debug.Log($"添加场景 [{sceneList.Count - 1}]: {sceneName} - {scenePath}");
                    break;
                }
            }
        }

        // 添加其他场景
        string[] allSceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });

        foreach (string guid in allSceneGuids)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(scenePath);

            // 跳过已添加的场景
            bool alreadyAdded = false;
            foreach (var existingScene in sceneList)
            {
                if (existingScene.path == scenePath)
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
            {
                sceneList.Add(new EditorBuildSettingsScene(scenePath, true));
                Debug.Log($"添加额外场景 [{sceneList.Count - 1}]: {fileName} - {scenePath}");
            }
        }

        EditorBuildSettings.scenes = sceneList.ToArray();

        EditorUtility.DisplayDialog("完成",
            $"已重建 Build Settings。\n" +
            $"共添加 {sceneList.Count} 个场景。\n" +
            $"请继续点击'第三步'验证。",
            "确定");

        // 刷新窗口
        Repaint();
    }

    private void ValidateSceneNames()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        bool hasIssues = false;
        string report = "场景验证报告:\n\n";

        for (int i = 0; i < scenes.Length; i++)
        {
            string sceneName = Path.GetFileNameWithoutExtension(scenes[i].path);
            string scenePath = scenes[i].path;

            if (string.IsNullOrEmpty(sceneName))
            {
                report += $"❌ 索引 {i}: 场景名称为空！路径: {scenePath}\n";
                hasIssues = true;
            }
            else
            {
                report += $"✓ 索引 {i}: {sceneName}\n";
                report += $"   路径: {scenePath}\n";
                report += $"   状态: {(scenes[i].enabled ? "启用" : "禁用")}\n\n";
            }
        }

        if (hasIssues)
        {
            EditorUtility.DisplayDialog("发现问题", report, "确定");
        }
        else
        {
            EditorUtility.DisplayDialog("验证通过",
                report + "\n所有场景名称都正常！",
                "确定");
        }

        Debug.Log(report);
    }
}
#endif