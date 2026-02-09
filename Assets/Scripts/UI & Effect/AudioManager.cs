using UnityEngine;
using System.Collections.Generic;

public class AudioManager : Globalizer<AudioManager>
{
    private List<AudioSource> audioSourcePool = new List<AudioSource>();
    private int poolSize = 32; // 可根据需要调整

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

    public static void PlaySound(AudioClip clip, Vector3 position, float volume = 1f)
    {
        AudioSource availableSource = Instance.GetAvailableAudioSource();
        if (availableSource != null)
        {
            availableSource.transform.position = position;
            availableSource.clip = clip;
            availableSource.volume = volume;
            availableSource.Play();
        }
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