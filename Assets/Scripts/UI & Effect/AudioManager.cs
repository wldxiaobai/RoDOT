using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public struct SoundHandle
{
    public int Id;
    public bool IsValid => Id > 0;
    public bool IsPlaying => IsValid && AudioManager.IsSoundPlaying(this);
    public float RemainingTime => IsValid ? AudioManager.GetRemainingTime(this) : 0f;
    public static readonly SoundHandle Invalid = new SoundHandle { Id = 0 };
}

public class AudioManager : Globalizer<AudioManager>
{
    private List<AudioSource> audioSourcePool = new List<AudioSource>();
    private int poolSize = 32; // 可根据需要调整
    private int nextHandleId = 1;
    private Dictionary<int, AudioSource> activeHandles = new Dictionary<int, AudioSource>();

    protected override void GlobeInit()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = new GameObject("PooledAudioSource");
            obj.transform.SetParent(transform);
            AudioSource source = obj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            audioSourcePool.Add(source);
        }
    }

    public static SoundHandle PlaySound(AudioClip clip, Vector3 position, float volume = 1f)
    {
        AudioSource availableSource = Instance.GetAvailableAudioSource();
        if (availableSource != null)
        {
            availableSource.transform.position = position;
            availableSource.clip = clip;
            availableSource.volume = volume;
            availableSource.Play();

            int id = Instance.nextHandleId++;
            Instance.activeHandles[id] = availableSource;
            return new SoundHandle { Id = id };
        }
        return SoundHandle.Invalid;
    }

    public static bool IsSoundPlaying(SoundHandle handle)
    {
        if (!handle.IsValid) return false;
        if (!Instance.activeHandles.TryGetValue(handle.Id, out AudioSource source)) return false;
        return source.isPlaying;
    }

    public static float GetRemainingTime(SoundHandle handle)
    {
        if (!handle.IsValid) return 0f;
        if (!Instance.activeHandles.TryGetValue(handle.Id, out AudioSource source)) return 0f;
        if (!source.isPlaying || source.clip == null) return 0f;
        return source.clip.length - source.time;
    }

    public static SoundHandle PlaySoundWithFadeIn(AudioClip clip, Vector3 position, float volume, float fadeInTime)
    {
        AudioSource availableSource = Instance.GetAvailableAudioSource();
        if (availableSource != null)
        {
            availableSource.transform.position = position;
            availableSource.clip = clip;
            availableSource.volume = 0f;
            availableSource.Play();

            int id = Instance.nextHandleId++;
            Instance.activeHandles[id] = availableSource;

            Instance.StartCoroutine(Instance.FadeInCoroutine(availableSource, volume, fadeInTime));
            return new SoundHandle { Id = id };
        }
        return SoundHandle.Invalid;
    }

    public static void StopSound(SoundHandle handle, float fadeOutTime = 0f)
    {
        if (!handle.IsValid) return;
        if (!Instance.activeHandles.TryGetValue(handle.Id, out AudioSource source)) return;

        Instance.activeHandles.Remove(handle.Id);

        if (!source.isPlaying) return;

        if (fadeOutTime <= 0f)
        {
            source.Stop();
        }
        else
        {
            Instance.StartCoroutine(Instance.FadeOutCoroutine(source, fadeOutTime));
        }
    }

    private IEnumerator FadeOutCoroutine(AudioSource source, float fadeOutTime)
    {
        float startVolume = source.volume;
        float elapsed = 0f;

        while (elapsed < fadeOutTime)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeOutTime);
            yield return null;
        }

        source.Stop();
        source.volume = startVolume;
    }

    private IEnumerator FadeInCoroutine(AudioSource source, float targetVolume, float fadeInTime)
    {
        float elapsed = 0f;

        while (elapsed < fadeInTime)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(0f, targetVolume, elapsed / fadeInTime);
            yield return null;
        }

        source.volume = targetVolume;
    }

    private AudioSource GetAvailableAudioSource()
    {
        // 查找未在播放的 AudioSource
        foreach (var source in audioSourcePool)
        {
            if (!source.isPlaying)
                return source;
        }

        // 如果池中都在使用，动态扩展池
        GameObject obj = new GameObject("PooledAudioSource");
        obj.transform.SetParent(transform);
        AudioSource newSource = obj.AddComponent<AudioSource>();
        newSource.playOnAwake = false;
        audioSourcePool.Add(newSource);
        return newSource;
    }
}