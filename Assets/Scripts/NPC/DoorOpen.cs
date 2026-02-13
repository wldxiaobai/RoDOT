using UnityEngine;
using System.Collections;

public class DoorOpen : MonoBehaviour
{
    [SerializeField] private SceneThingsOutPut scene;

    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
        scene.openDoor += DoorOpening;
    }

    void OnDestroy()
    {
        scene.openDoor -= DoorOpening;
    }

    private void DoorOpening()
    {
        animator.SetTrigger("open");
    }
}