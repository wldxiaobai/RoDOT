using System.Collections;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class SpaceSequenceController : MonoBehaviour
{
    [SerializeField] float shortDistance = 2f;
    [SerializeField] float longDistance = 4f;
    [SerializeField] float phaseDuration = 0.5f;

    private readonly ActSeq actionSequence = new();

    void Awake()
    {
        var startCursor = actionSequence.Start;
        var endCursor = actionSequence.End;

        var leftMoveShort = actionSequence.CreateActionNode(
            () => this.MoveByStep(Vector2.left * shortDistance, phaseDuration, 0.8f)
            );
        startCursor.SetNext(leftMoveShort);

        var waitNode = actionSequence.CreateActionNode(
            () => this.Wait(phaseDuration)
            );
        leftMoveShort.SetNext(waitNode);

        var oscillationLoop = actionSequence.CreateRepeatNode(
            () => this.LerpToPoint((Vector2)transform.position + Vector2.up * longDistance, 0.1f),
            maxIterations: 3
            );
        waitNode.SetNext(oscillationLoop);

        var waitForLanding = actionSequence.CreateDoWhileNode(
            () => RotateCoroutine(180f, 0.5f),
            () => transform.position.y > -2f
            );
        oscillationLoop.SetNext(waitForLanding);

        var branchNode = actionSequence.CreateConditionalNode(
            () => transform.position.x > 0f
            );
        waitForLanding.SetNext(branchNode);

        var moveLeftBranch = actionSequence.CreateActionNode(
            () => this.MoveByStep(Vector2.left * longDistance, phaseDuration, 0.8f)
            );

        var moveRightBranch = actionSequence.CreateActionNode(
            () => this.MoveByStep(Vector2.right * longDistance, phaseDuration, 0.8f)
            );

        branchNode.SetBranches(moveLeftBranch, moveRightBranch);
        moveLeftBranch.SetNext(endCursor);
        moveRightBranch.SetNext(endCursor);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (actionSequence.IsPlaying)
            {
                actionSequence.Stop();
            }
            else
            {
                actionSequence.Play(this);
            }
        }
    }

    IEnumerator RotateCoroutine(float angle, float duration)
    {
        Quaternion initialRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, initialRotation.z + angle);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            transform.rotation = Quaternion.Slerp(initialRotation, targetRotation, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.rotation = targetRotation;
    }
}