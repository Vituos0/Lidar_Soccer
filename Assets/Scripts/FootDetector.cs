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
}