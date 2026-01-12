using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using UnityEngine;

// Đóng gói dữ liệu điểm theo phong cách OOP
public struct LidarPoint
{
    public Vector3 Position;
    public Color Color;
}

public class PavoPointCloud : MonoBehaviour
{
    [Header("Network Settings")]
    public int port = 2368; // Cổng mặc định của PAVO [cite: 220]

    [Header("Visualization Settings")]
    public float scaleFactor = 0.001f; // Chuyển mm sang m (đơn vị Unity)
    public Color pointColor = Color.cyan;

    private UdpClient udpClient;
    private Thread receiveThread;
    private bool isRunning = true;

    // Hàng đợi an toàn để truyền dữ liệu giữa các Thread
    private ConcurrentQueue<LidarPoint[]> dataQueue = new ConcurrentQueue<LidarPoint[]>();
    private ParticleSystem pSystem;

    void Start()
    {
        pSystem = GetComponentInChildren<ParticleSystem>();

        // Khởi tạo Thread nhận dữ liệu UDP
        receiveThread = new Thread(new ThreadStart(ReceiveUDPData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    private void ReceiveUDPData()
    {
        try
        {
            // SỬA LỖI SOCKET: Ép buộc hệ thống cho phép dùng chung cổng 2368
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Bind(new IPEndPoint(IPAddress.Any, port));

            udpClient = new UdpClient();
            udpClient.Client = socket;
            IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, port);

            while (isRunning)
            {
                byte[] data = udpClient.Receive(ref anyIP);
                // THÊM DÒNG NÀY ĐỂ KIỂM TRA:
                Debug.Log("Nhận được gói tin độ dài: " + data.Length);
                if (data.Length == 126)
                { 
                    Debug.Log("Đã nhận gói tin!");
                    LidarPoint[] points = ParsePacket(data);
                    if (points != null) dataQueue.Enqueue(points);
                }
            }
        }
        catch (Exception e) { Debug.LogError("Lỗi Socket: " + e.Message); }
    }

    private LidarPoint[] ParsePacket(byte[] data)
    {
        LidarPoint[] framePoints = new LidarPoint[24]; // Mỗi gói chứa 24 điểm [cite: 222]
        int pointIndex = 0;

          for (int i = 0; i < 12; i++)
        { // 12 nhóm dữ liệu [cite: 221]
            int offset =  (i * 10); // Bỏ qua 8 byte Header [cite: 219]

             // Giải mã Góc (Azimuth) - Đảo byte [cite: 299]
            ushort azRaw = BitConverter.ToUInt16(new byte[] { data[offset + 2], data[offset + 3] }, 0);
            float angleDeg = azRaw / 100.0f; // [cite: 301, 302]
            float angleRad = (angleDeg + 90f) * Mathf.Deg2Rad; // Khớp hướng Unity

              for (int p = 0; p < 2; p++)
            { // Mỗi khối có 2 điểm [cite: 222]
                int pOff = offset + 4 + (p * 3);

                 // Giải mã Khoảng cách - Nhân đơn vị 2mm [cite: 314, 316]
                ushort distRaw = BitConverter.ToUInt16(new byte[] { data[pOff], data[pOff + 1] }, 0);
                float distance = distRaw * 2.0f;

                  if (distance > 100f)
                { // Lọc điểm thuộc vùng mù (0) hoặc nhiễu [cite: 347]
                    float d = distance * scaleFactor;
                    // Chuyển tọa độ Cực sang Descartes
                    framePoints[pointIndex].Position = new Vector3(Mathf.Cos(angleRad) * d, 0, Mathf.Sin(angleRad) * d);
                    framePoints[pointIndex].Color = pointColor;
                }
                pointIndex++;
            }
        }
        return framePoints;
    }

    void Update()
    {
        // Vẽ các điểm lên Particle System từ Main Thread
        while (dataQueue.TryDequeue(out LidarPoint[] newPoints))
        {
            Debug.Log("Đang vẽ: " + newPoints.Length + " điểm");
            foreach (var pt in newPoints)
            {
                if (pt.Position != Vector3.zero)
                {
                    var emitParams = new ParticleSystem.EmitParams();
                    emitParams.position = pt.Position;
                    emitParams.startColor = pt.Color;
                    pSystem.Emit(emitParams, 1);
                }
            }
        }
    }

    void OnDestroy()
    {
        isRunning = false;
        udpClient?.Close();
        receiveThread?.Abort();
    }
}