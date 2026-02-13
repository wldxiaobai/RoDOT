using UnityEngine;

public class CaremaMoved : MonoBehaviour
{
    [Header("移动设置")]
    public float moveSpeed = 5f; // 移动速度

    void Update()
    {
        // 获取水平和垂直输入 (默认对应WASD和上下箭头)
        float horizontal = Input.GetAxisRaw("Horizontal"); // GetAxisRaw用于即时响应，没有平滑过渡
        float vertical = Input.GetAxisRaw("Vertical");

        // 计算移动方向 (Vector2表示2D平面)
        Vector2 direction = new Vector2(horizontal, vertical).normalized;

        // 将移动方向应用到摄像头位置
        // 使用 Time.deltaTime 来确保移动速度与帧率无关
        transform.Translate(direction * moveSpeed * Time.deltaTime);
    }
}