using UnityEngine;
using System.Collections;

public class DoorOpen : MonoBehaviour
{
    [SerializeField] private SceneThingsOutPut scene;

    private Animator animator;
    private Collider2D doorCollider;

    void Start()
    {
        animator = GetComponent<Animator>();
        doorCollider = GetComponent<Collider2D>();
        scene.openDoor += DoorOpening;
    }

    void OnDestroy()
    {
        scene.openDoor -= DoorOpening;
    }

    private void DoorOpening()
    {
        animator.SetTrigger("open");
        doorCollider.enabled = false;
    }
}