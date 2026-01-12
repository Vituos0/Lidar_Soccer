using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class PavoLidarReceiver : MonoBehaviour
{
    private UdpClient udpClient;
    private Thread receiveThread;
    private bool isRunning = true;

    private const int port = 2368; // Cổng mặc định của PAVO 
    private const int packetSize = 134; // Độ dài gói tin 134 byte [cite: 218]

    // Đảm bảo chỉ có 1 instance tồn tại (Singleton-ish)
    private static PavoLidarReceiver instance;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;
    }

    void Start()
    {
        isRunning = true;
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
        Debug.Log($"<color=green>Đang lắng nghe dữ liệu PAVO tại cổng {port}</color>");
    }

    private void ReceiveData()
    {
        try
        {
            // Kỹ thuật OOP: Khởi tạo Socket thủ công để thiết lập ReuseAddress
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            // Cho phép sử dụng lại địa chỉ/cổng ngay cả khi đang bị kẹt (Fix SocketException)
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Bind(new IPEndPoint(IPAddress.Any, port));

            udpClient = new UdpClient();
            udpClient.Client = socket;

            IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, port);

            while (isRunning)
            {
                byte[] data = udpClient.Receive(ref anyIP);
                if (data != null && data.Length == packetSize)
                {
                    ParsePacket(data);
                }
            }
        }
        catch (Exception e)
        {
            if (isRunning) Debug.LogError("Lỗi kết nối Socket: " + e.Message);
        }
    }

    private void ParsePacket(byte[] data)
    {
       // Duyệt qua 12 khối dữ liệu (mỗi khối 10 byte) [cite: 221]
        for (int i = 0; i < 12; i++)
        {
            int offset = 8 + (i * 10); // Bỏ qua 8 byte UDP Header [cite: 219]

            // 1. Giải mã Góc (Azimuth) - Đảo ngược byte [cite: 299]
            ushort azimuthRaw = (ushort)((data[offset + 3] << 8) | data[offset + 2]);
            float azimuth = azimuthRaw / 100.0f; // Chuyển sang thập phân [cite: 301, 302]

            // 2. Giải mã 2 điểm dữ liệu trong mỗi khối [cite: 222]
            for (int p = 0; p < 2; p++)
            {
                int pOffset = offset + 4 + (p * 3);

                // Giải mã khoảng cách - Đảo ngược byte và nhân đơn vị 2mm [cite: 314, 316]
                ushort distRaw = (ushort)((data[pOffset + 1] << 8) | data[pOffset]);
                float distance = distRaw * 2.0f; // Đơn vị thực tế: mm [cite: 305, 308]

              if (distance > 100) // Lọc điểm nhiễu hoặc điểm thuộc vùng mù (distance = 0) [cite: 347]
                {
                    // Đã nhận được dữ liệu sạch
                }
            }
        }
    }

    void OnDisable() { Cleanup(); }
    void OnApplicationQuit() { Cleanup(); }

    private void Cleanup()
    {
        isRunning = false;
        if (udpClient != null)
        {
            udpClient.Close();
            udpClient = null;
        }
        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Abort();
        }
    }
}