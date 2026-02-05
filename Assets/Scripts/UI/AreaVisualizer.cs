using UnityEngine;

public class AreaVisualizer : MonoBehaviour
{
    public AreaManager areaManager;
    public bool showGizmos = true;
    public Color activeAreaColor = new Color(0, 1, 0, 0.3f);
    public Color inactiveAreaColor = new Color(1, 0, 0, 0.3f);

    void OnDrawGizmos()
    {
        if (!showGizmos || areaManager == null) return;

        foreach (var area in areaManager.areas)
        {
            foreach (var collider in area.subAreas)
            {
                if (collider != null)
                {
                    // 根据玩家是否在区域内选择颜色
                    bool isPlayerInside = false;
                    if (areaManager.playerObject != null)
                    {
                        isPlayerInside = collider.bounds.Contains(areaManager.playerObject.transform.position);
                    }

                    Gizmos.color = isPlayerInside ? activeAreaColor : inactiveAreaColor;

                    // 根据碰撞器类型绘制不同的形状
                    if (collider is BoxCollider2D boxCollider)
                    {
                        Vector3 center = collider.transform.position + (Vector3)boxCollider.offset;
                        Vector3 size = new Vector3(
                            boxCollider.size.x * collider.transform.lossyScale.x,
                            boxCollider.size.y * collider.transform.lossyScale.y,
                            0.1f
                        );

                        Gizmos.DrawCube(center, size);
                    }
                    else if (collider is CircleCollider2D circleCollider)
                    {
                        Vector3 center = collider.transform.position + (Vector3)circleCollider.offset;
                        float radius = circleCollider.radius * Mathf.Max(
                            collider.transform.lossyScale.x,
                            collider.transform.lossyScale.y
                        );

                        Gizmos.DrawSphere(center, radius);
                    }
                }
            }
        }
    }
}