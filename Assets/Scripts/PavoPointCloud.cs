using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

public class PavoFootstepManager : MonoBehaviour
{
    [Header("Cấu hình mạng")]
    public int port = 2368;

    [Header("Vùng nhận diện (Mét)")]
    public float minDistance = 0.3f; // Bỏ qua vật sát máy
    public float maxDistance = 3.0f; // Khoảng cách cầu thủ đứng

    [Header("Hiệu ứng")]
    public GameObject effectPrefab; // Kéo Prefab hiệu ứng vào đây
    public float spawnInterval = 0.3f; // Khoảng cách thời gian giữa 2 lần hiện hiệu ứng

    private UdpClient udpClient;
    private Thread receiveThread;
    private bool isRunning = true;
    private ConcurrentQueue<Vector3> footPositionQueue = new ConcurrentQueue<Vector3>();
    private float lastEffectTime;

    void Start()
    {
        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    // 1. NHẬN DỮ LIỆU (Chạy ngầm)
    private void ReceiveData()
    {
        try
        {
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            s.Bind(new IPEndPoint(IPAddress.Any, port));
            udpClient = new UdpClient { Client = s };
            IPEndPoint ip = new IPEndPoint(IPAddress.Any, port);

            while (isRunning)
            {
                byte[] data = udpClient.Receive(ref ip);
                if (data.Length == 126) ParseAndDetect(data);
            }
        }
        catch (Exception e) { Debug.Log(e.Message); }
    }

    // 2. GIẢI MÃ & TÌM BÀN CHÂN
    private void ParseAndDetect(byte[] data)
    {
        List<Vector3> detectedPoints = new List<Vector3>();

        for (int i = 0; i < 12; i++)
        {
            int offset = i * 10;
            ushort azRaw = BitConverter.ToUInt16(new byte[] { data[offset], data[offset + 1] }, 0);
            float angleRad = (azRaw / 100.0f) * Mathf.Deg2Rad;

            for (int p = 0; p < 2; p++)
            {
                int pOff = offset + 2 + (p * 3);
                ushort distRaw = BitConverter.ToUInt16(new byte[] { data[pOff], data[pOff + 1] }, 0);
                float dist = distRaw * 2.0f * 0.001f; // Sang mét

                if (dist >= minDistance && dist <= maxDistance)
                {
                    // Tính tọa độ X, Z trên mặt sàn
                    float x = Mathf.Sin(angleRad) * dist;
                    float z = Mathf.Cos(angleRad) * dist;
                    detectedPoints.Add(new Vector3(x, 0, z));
                }
            }
        }

        // Nếu có cụm điểm (ví dụ > 10 điểm), tính trung tâm của bàn chân
        if (detectedPoints.Count > 10)
        {
            Vector3 center = Vector3.zero;
            foreach (var p in detectedPoints) center += p;
            center /= detectedPoints.Count;
            footPositionQueue.Enqueue(center);
        }
    }

    // 3. HIỂN THỊ (Chạy luồng chính)
    void Update()
    {
        if (footPositionQueue.TryDequeue(out Vector3 footPos))
        {
            if (Time.time - lastEffectTime > spawnInterval)
            {
                // Tạo hiệu ứng tại vị trí bàn chân
                Instantiate(effectPrefab, footPos, Quaternion.identity);
                lastEffectTime = Time.time;
            }
        }
    }

    void OnDestroy() { isRunning = false; udpClient?.Close(); receiveThread?.Abort(); }


}