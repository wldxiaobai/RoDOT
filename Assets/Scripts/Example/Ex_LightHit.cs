using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ex_LightHit : MonoBehaviour
{
    [SerializeField] float Distance = 3f;
    [SerializeField] float Duration = 2f;

    ActSeq actSeq = new();

    private void Awake()
    {
        var startCursor = actSeq.Start;
        var endCursor = actSeq.End;

        var rightMove = actSeq.CreateActionNode(
            () => this.MoveByStep(Vector2.right * Distance, Duration, 0.5f)
            );

        var leftMove = actSeq.CreateActionNode(
            () => this.MoveByStep(Vector2.left * Distance, Duration, 0.8f)
            );

        startCursor.SetNext(rightMove);
        rightMove.SetNext(leftMove);
        leftMove.SetNext(endCursor);
    }
    // Update is called once per frame
    void Update()
    {
        if (!actSeq.IsPlaying)
        {
            actSeq.Play(this);
        }
    }
}
