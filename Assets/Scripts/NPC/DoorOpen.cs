using UnityEngine;
using System.Collections;

public class DoorOpen : MonoBehaviour
{
    public Sprite[] frames;
    public float frameRate = 0.5f;
    public SceneThingsOutPut targetSceneThing;

    private SpriteRenderer spriteRenderer;
    private bool hasPlayed = false;

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError("DoorOpen需要SpriteRenderer组件！");
            enabled = false;
            return;
        }
        if (frames != null && frames.Length > 0)
        {
            spriteRenderer.sprite = frames[0];
        }
        if (targetSceneThing != null)
        {
            StartCoroutine(CheckTargetAlive());
        }
    }

    private IEnumerator CheckTargetAlive()
    {
        while (targetSceneThing != null && targetSceneThing.isAlive)
        {
            yield return null;
        }
        if (!hasPlayed)
        {
            PlayAnimation();
        }
    }
    public void PlayAnimation()
    {
        if (hasPlayed) return;
        if (frames == null || frames.Length == 0) return;

        StartCoroutine(PlayFrames());
    }

    private IEnumerator PlayFrames()
    {
        hasPlayed = true;
        for (int i = 0; i < frames.Length; i++)
        {
            spriteRenderer.sprite = frames[i];
            yield return new WaitForSeconds(1f / frameRate);
        }
    }
}