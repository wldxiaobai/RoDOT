using UnityEngine;

public class SpriteAlphaCycle : MonoBehaviour
{
    [Header("透明度循环设置")]
    [Range(0f, 1f)]
    public float minAlpha = 0f;
    [Range(0f, 1f)]
    public float maxAlpha = 1f;
    [Range(0.1f, 10f)]
    public float cycleDuration = 2f;

    [Header("循环模式")]
    public bool pingPongMode = true;
    public bool playOnStart = true;
    public bool randomStartOffset = false;

    [Header("当前状态")]
    [Range(0f, 1f)]
    public float currentAlpha = 0f;
    public bool isPlaying = false;

    private SpriteRenderer spriteRenderer;
    private float timeElapsed = 0f;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            Debug.LogError($"SpriteAlphaCycle: {gameObject.name} 没有SpriteRenderer组件！");
            enabled = false;
            return;
        }

        if (randomStartOffset)
        {
            timeElapsed = Random.Range(0f, cycleDuration);
        }

        if (playOnStart)
        {
            Play();
        }

        Debug.Log($"贴图循环脚本启动: {gameObject.name}，初始Alpha: {spriteRenderer.color.a}");
    }

    void Update()
    {
        if (!isPlaying || spriteRenderer == null) return;

        timeElapsed += Time.deltaTime;

        if (pingPongMode)
        {
            // PingPong模式：0->1->0
            float t = Mathf.PingPong(timeElapsed / (cycleDuration / 2), 1f);
            currentAlpha = Mathf.Lerp(minAlpha, maxAlpha, t);
        }
        else
        {
            // 循环模式：0->1，然后重置到0
            float t = (timeElapsed % cycleDuration) / cycleDuration;
            currentAlpha = Mathf.Lerp(minAlpha, maxAlpha, t);
        }

        // 应用透明度
        Color color = spriteRenderer.color;
        color.a = currentAlpha;
        spriteRenderer.color = color;
    }

    public void Play()
    {
        isPlaying = true;
        Debug.Log($"开始透明度循环: {gameObject.name}");
    }

    public void Stop()
    {
        isPlaying = false;
        Debug.Log($"停止透明度循环: {gameObject.name}");
    }

    public void SetAlpha(float alpha)
    {
        currentAlpha = Mathf.Clamp01(alpha);

        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = currentAlpha;
            spriteRenderer.color = color;
        }
    }

    public void TogglePlay()
    {
        isPlaying = !isPlaying;
        Debug.Log($"透明度循环 {(isPlaying ? "开始" : "停止")}: {gameObject.name}");
    }

    [ContextMenu("测试透明度0")]
    public void TestAlpha0()
    {
        SetAlpha(0f);
        Debug.Log($"设置透明度为0: {gameObject.name}");
    }

    [ContextMenu("测试透明度0.5")]
    public void TestAlpha50()
    {
        SetAlpha(0.5f);
        Debug.Log($"设置透明度为0.5: {gameObject.name}");
    }

    [ContextMenu("测试透明度1")]
    public void TestAlpha100()
    {
        SetAlpha(1f);
        Debug.Log($"设置透明度为1: {gameObject.name}");
    }

    void OnGUI()
    {
        if (!isPlaying) return;

        // 在Scene视图中显示当前透明度
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white;
        style.fontSize = 10;
        style.alignment = TextAnchor.MiddleCenter;

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 1f,
            $"Alpha: {currentAlpha:F2}\n{gameObject.name}",
            style
        );
#endif
    }
}