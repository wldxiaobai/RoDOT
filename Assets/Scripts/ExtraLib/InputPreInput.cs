using System.Collections.Generic;
using UnityEngine;

public static class InputPreInput
{
    private sealed class KeyState
    {
        public float Timestamp;
    }

    private static readonly Dictionary<KeyCode, KeyState> States = new();

    /// <summary>
    /// 查询目标键是否在本帧或前 <paramref name="bufferDuration"/> 秒内被按下。
    /// 只有在返回 true 并且动作真正处理了输入时才调用 <see cref="ConsumeBufferedKeyDown"/> 来清除缓存，避免重复触发。
    /// </summary>
    public static bool GetKeyDown(KeyCode key, float bufferDuration)
    {
        bufferDuration = Mathf.Max(0f, bufferDuration);
        var now = Time.unscaledTime;

        if (Input.GetKeyDown(key))
        {
            States[key] = new KeyState { Timestamp = now };
            return true;
        }

        if (States.TryGetValue(key, out var state))
        {
            if (now - state.Timestamp <= bufferDuration)
            {
                return true;
            }

            States.Remove(key);
        }

        return false;
    }

    /// <summary>
    /// 在处理完按键后调用以清除缓存，确保同一次按键不会被多次消费。
    /// </summary>
    public static bool ConsumeBufferedKeyDown(KeyCode key)
        => States.Remove(key);
}

public class Timer
{
    public float Duration;
    private float _elapsed;
    private bool _running;

    public Timer()
    {
        Duration = 0f;
        _elapsed = 0f;
        _running = false;
    }

    public void StartTimer(float duration)
    {
        Duration = duration;
        _elapsed = 0f;
        _running = true;
    }
    public void StopTimer()
    {
        _running = false;
    }
    public bool IsRunning => _running && _elapsed < Duration;
    public void Update()
    {
        if (_running)
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= Duration)
            {
                _running = false;
            }
        }
    }
}