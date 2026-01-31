using System;
using System.Collections;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

public static class MoveTargetRegistry
{
    private sealed class MoveState
    {
        public Vector2 Target;
    }

    private static readonly ConditionalWeakTable<Transform, MoveState> Registry = new();

    public static Vector2 GetLastMoveTarget(this Transform transform)
    {
        if (transform == null)
        {
            return Vector2.zero;
        }

        if (Registry.TryGetValue(transform, out var state))
        {
            return state.Target;
        }

        return transform.position;
    }

    public static void RegisterMoveTarget(this Transform transform, Vector2 target)
    {
        if (transform == null)
        {
            return;
        }

        Registry.GetOrCreateValue(transform).Target = target;
    }

    public static void UnregisterMoveTarget(this Transform transform)
    {
        if (transform == null)
        {
            return;
        }

        Registry.Remove(transform);
    }
}

public static class MovLib
{
    public static Vector2 WorldMousePos => 
        Camera.main != null ? 
        (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition) : 
        Vector2.zero;

    public static void StopCoroutineChecked(this MonoBehaviour runner, IEnumerator routine)
    {
        if (runner == null || routine == null)
        {
            return;
        }
        runner.StopCoroutine(routine);
        runner.transform.UnregisterMoveTarget();
    }
    public static void StopCoroutineChecked(this MonoBehaviour runner, Coroutine routine)
    {
        if (runner == null || routine == null)
        {
            return;
        }
        runner.StopCoroutine(routine);
        runner.transform.UnregisterMoveTarget();
    }

    public static IEnumerator MoveToPoint(this MonoBehaviour runner,
                                   Vector2 target,
                                   float moveTime,
                                   float transTimeRate)
        => runner.MoveToPoint(runner?.transform, target, moveTime, transTimeRate);
    public static IEnumerator MoveToPoint(this MonoBehaviour runner,
                                   Transform mover,
                                   Vector2 target,
                                   float moveTime,
                                   float transTimeRate)
    {
        mover ??= runner?.transform;
        if (mover == null)
        {
            yield break;
        }

        transTimeRate = Mathf.Clamp01(transTimeRate);
        var start = mover.GetLastMoveTarget();
        mover.RegisterMoveTarget(target);
        try
        {
            yield return MoveRoutine(
                mover,
                start,
                target,
                moveTime,
                transTimeRate / 2f * moveTime
                );
        }
        finally
        {
            mover.UnregisterMoveTarget();
        }
    }

    public static IEnumerator MoveByStep(this MonoBehaviour runner,
                                             Vector2 step,
                                             float moveTime,
                                             float transTimeRate)
        => runner.MoveByStep(runner?.transform, step, moveTime, transTimeRate);
    public static IEnumerator MoveByStep(this MonoBehaviour runner,
                                             Transform mover,
                                             Vector2 step,
                                             float moveTime,
                                             float transTimeRate)
    {
        mover ??= runner?.transform;
        if (mover == null)
        {
            yield break;
        }

        transTimeRate = Mathf.Clamp01(transTimeRate);
        Vector2 start = mover.GetLastMoveTarget();
        Vector2 target = start + step;
        mover.RegisterMoveTarget(target);
        try
        {
            yield return MoveRoutine(
                mover,
                start,
                target,
                moveTime,
                transTimeRate / 2f * moveTime
                );
        }
        finally
        {
            mover.UnregisterMoveTarget();
        }
    }

    public static IEnumerator MoveOverDistance(this MonoBehaviour runner,
                                             Vector2 target,
                                             float distance,
                                             float moveTime,
                                             float transTimeRate)
        => runner.MoveOverDistance(runner?.transform, target, distance, moveTime, transTimeRate);
    public static IEnumerator MoveOverDistance(this MonoBehaviour runner,
                                             Transform mover,
                                             Vector2 target,
                                             float distance,
                                             float moveTime,
                                             float transTimeRate)
    {
        mover ??= runner?.transform;
        if (mover == null)
        {
            yield break;
        }

        transTimeRate = Mathf.Clamp01(transTimeRate);
        Vector2 start = mover.GetLastMoveTarget();
        Vector2 step = (target - start).normalized * distance;
        Vector2 finalTarget = start + step;
        mover.RegisterMoveTarget(finalTarget);
        try
        {
            yield return MoveRoutine(
                mover,
                start,
                finalTarget,
                moveTime,
                transTimeRate / 2f * moveTime
                );
        }
        finally
        {
            mover.UnregisterMoveTarget();
        }
    }

    public static IEnumerator LerpToPoint(this MonoBehaviour runner,
                                   Vector2 target,
                                   float lerpFactor,
                                   float maxSpeed = 100f)
        => runner.LerpToPoint(runner?.transform, target, lerpFactor, maxSpeed);
    public static IEnumerator LerpToPoint(this MonoBehaviour runner,
                                   Transform mover,
                                   Vector2 target,
                                   float lerpFactor,
                                   float maxSpeed = 100f)
    {
        mover ??= runner?.transform;
        if (mover == null)
        {
            yield break;
        }

        var start = mover.GetLastMoveTarget();
        mover.RegisterMoveTarget(target);
        try
        {
            yield return LerpRoutine(
                mover,
                start,
                target,
                lerpFactor,
                maxSpeed
                );
        }
        finally
        {
            mover.UnregisterMoveTarget();
        }
    }

    public static IEnumerator Wait(this MonoBehaviour actor, float waitSeconds)
    {
        yield return new WaitForSeconds(waitSeconds);
    }

    private static IEnumerator MoveRoutine(Transform mover, 
                                           Vector2 start, 
                                           Vector2 target, 
                                           float moveTime, 
                                           float acceTime)
    {
        Vector2 displacement = target - start;
        float totalDistance = displacement.magnitude;
        if (totalDistance <= Mathf.Epsilon || moveTime <= Mathf.Epsilon || mover == null)
        {
            yield break;
        }

        Vector2 direction = displacement / totalDistance;
        float constantTime = Mathf.Max(0f, moveTime - 2f * acceTime);
        float previousDistance = 0f;

        if (acceTime <= 0f)
        {
            float elapsed = 0f;
            while (elapsed < moveTime)
            {
                float progressRatio = Mathf.Clamp01(elapsed / moveTime);
                float currentDistance = totalDistance * progressRatio;
                float deltaDistance = currentDistance - previousDistance;
                if (deltaDistance > Mathf.Epsilon)
                {
                    mover.position += (Vector3)(direction * deltaDistance);
                }

                previousDistance = currentDistance;
                elapsed += Time.deltaTime;
                yield return null;
            }

            float remaining = totalDistance - previousDistance;
            if (remaining > Mathf.Epsilon)
            {
                mover.position += (Vector3)(direction * remaining);
            }

            yield break;
        }

        float phaseDuration = constantTime + acceTime;
        float maxSpeed = totalDistance / phaseDuration;
        float acceleration = maxSpeed / acceTime;
        float accDistanceTotal = 0.5f * maxSpeed * acceTime;
        float constantPhaseEnd = acceTime + constantTime;

        float timer = 0f;
        while (timer < moveTime)
        {
            float currentDistance;
            if (timer <= acceTime)
            {
                currentDistance = 0.5f * acceleration * timer * timer;
            }
            else if (timer <= constantPhaseEnd)
            {
                currentDistance = accDistanceTotal + maxSpeed * (timer - acceTime);
            }
            else
            {
                float decElapsed = timer - constantPhaseEnd;
                float decDistance = maxSpeed * decElapsed - 0.5f * acceleration * decElapsed * decElapsed;
                currentDistance = accDistanceTotal + maxSpeed * constantTime + decDistance;
            }

            float deltaDistance = currentDistance - previousDistance;
            if (deltaDistance > Mathf.Epsilon)
            {
                mover.position += (Vector3)(direction * deltaDistance);
            }

            previousDistance = currentDistance;
            timer += Time.deltaTime;
            yield return null;
        }

        float remainingDistance = totalDistance - previousDistance;
        if (remainingDistance > Mathf.Epsilon)
        {
            mover.position += (Vector3)(direction * remainingDistance);
        }
    }

    private static IEnumerator LerpRoutine(Transform mover,
                                           Vector2 start,
                                           Vector2 target,
                                           float t,
                                           float maxSpeed)
    {
        while (Vector2.Distance(start, target) > 1E-5f)
        {
            var newPos = (start + t * target) / (t + 1f);
            var offset = newPos - start;
            if (offset.magnitude > maxSpeed * Time.deltaTime)
            {
                offset = offset.normalized * maxSpeed * Time.deltaTime;
                newPos = start + offset;
            }
            mover.position += (Vector3)offset;
            start = newPos;
            yield return null;
        }
        mover.position += (Vector3)(target - start);
    }
}