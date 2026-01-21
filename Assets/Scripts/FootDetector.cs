using UnityEngine;
using System.Collections.Generic;

public class FootDetector : MonoBehaviour
{
    [Header("Detection Zone (Meters)")]
    public float minDist = 0.5f;
    public float maxDist = 3.0f;

    [Header("Filtering Settings")]
    public int minPointsRequired = 30; // Ngưỡng tối thiểu để xác nhận là bàn chân
    public float lerpSpeed = 10f;      // Làm mượt chuyển động của VFX

    private Vector3 _smoothFootPos;
    private bool _hasDetected = false;

    // Thuộc tính để các script khác (như Manager) truy xuất vị trí
    public Vector3 FootPosition => _smoothFootPos;
    public bool IsDetected => _hasDetected;

    /// <summary>
    /// Xử lý danh sách điểm và trả về vị trí trung tâm bàn chân
    /// </summary>
    public void ProcessPoints(List<Vector3> validPoints)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;

        foreach (Vector3 p in validPoints)
        {
            // Kiểm tra khoảng cách thực tế (magnitude) từ LiDAR đến điểm
            float distance = p.magnitude;

            if (distance >= minDist && distance <= maxDist)
            {
                sum += p;
                count++;
            }
        }

        if (count >= minPointsRequired)
        {
            _hasDetected = true;
            Vector3 targetPos = sum / count; // Công thức Trọng tâm Centroid

            // Làm mượt tọa độ để tránh hiện tượng rung (jitter) do dữ liệu LiDAR quét
            _smoothFootPos = Vector3.Lerp(_smoothFootPos, targetPos, Time.deltaTime * lerpSpeed);
        }
        else
        {
            _hasDetected = false;
        }
    }
    [Header("Gizmos Settings")]
    public bool showGizmos = true;
    public Color zoneColor = Color.green;

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // Thiết lập màu sắc cho vùng quét
        Gizmos.color = zoneColor;

        // Lấy vị trí và hướng của GameObject chứa LiDAR
        Vector3 center = transform.position;

        // 1. VẼ VÙNG QUÉT THEO KHOẢNG CÁCH (Min/Max Distance)
        // Vẽ vòng tròn nhỏ (vùng mù sát máy)
        DrawWireArc(center, transform.up, transform.forward, 360f, minDist);

        // Vẽ vòng tròn lớn (tầm xa tối đa)
        Gizmos.color = Color.red; // Đổi màu cho vòng ngoài để dễ phân biệt
        DrawWireArc(center, transform.up, transform.forward, 360f, maxDist);

        // 2. VẼ CÁC VÙNG CHECKING (Nếu bạn dùng dạng ô vuông - Rect)
        // Giả sử bạn có một biến Rect detectionArea;
        // Gizmos.DrawWireCube(new Vector3(detectionArea.x, 0, detectionArea.y), new Vector3(detectionArea.width, 0.1f, detectionArea.height));
    }

    // Hàm phụ để vẽ hình tròn/hình quạt trên mặt phẳng XZ
    void DrawWireArc(Vector3 center, Vector3 normal, Vector3 from, float angle, float radius)
    {
        Vector3 lastPoint = center + from * radius;
        int segments = 30; // Độ mịn của đường cong
        for (int i = 1; i <= segments; i++)
        {
            float a = (i / (float)segments) * angle;
            Vector3 nextPoint = center + (Quaternion.AngleAxis(a, normal) * from) * radius;
            Gizmos.DrawLine(lastPoint, nextPoint);
            lastPoint = nextPoint;
        }
    }
}