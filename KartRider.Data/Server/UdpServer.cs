using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using KartRider.Common.Utilities;
using KartRider.IO.Packet;
using KartRider_PacketName;

namespace KartRider
{
    /// <summary>
    /// UDP服务端封装类
    /// </summary>
    public class UdpServer
    {
        // UDP核心通信对象
        private UdpClient _udpClient;
        // 监听端口
        private readonly int _listenPort;
        // 服务端名称（日志区分多实例）
        private readonly string _serverName;
        // 线程取消标识（替代CancellationToken）
        private volatile bool _isRunning;
        // 同步锁（防止重复启动/停止）
        private readonly object _lockObj = new object();

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="serverName">服务端名称（日志区分）</param>
        /// <param name="listenPort">监听端口（唯一）</param>
        public UdpServer(string serverName, int listenPort)
        {
            _serverName = serverName;
            _listenPort = listenPort;
            _isRunning = false;
        }

        /// <summary>
        /// 启动UDP服务端
        /// </summary>
        public void Start()
        {
            lock (_lockObj)
            {
                if (_isRunning)
                {
                    Console.WriteLine($"[{_serverName}] 服务端已启动，无需重复启动");
                    return;
                }

                try
                {
                    // 初始化UDP客户端并绑定端口
                    _udpClient = new UdpClient(_listenPort);
                    _isRunning = true;

                    Console.WriteLine($"[{_serverName}] 服务端启动成功，监听端口：{_listenPort}");
                    Console.WriteLine($"[{_serverName}] 等待客户端数据...\n");

                    // 开始异步接收数据（APM模式）
                    BeginReceive();
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"[{_serverName}] 启动失败：{ex.Message}（端口可能被占用）");
                    _isRunning = false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{_serverName}] 启动异常：{ex.Message}");
                    _isRunning = false;
                }
            }
        }

        /// <summary>
        /// 停止UDP服务端
        /// </summary>
        public void Stop()
        {
            lock (_lockObj)
            {
                if (!_isRunning)
                {
                    Console.WriteLine($"[{_serverName}] 服务端未启动，无需停止");
                    return;
                }

                _isRunning = false;

                try
                {
                    // 关闭UDP客户端（终止异步接收）
                    _udpClient?.Close();
                    Console.WriteLine($"[{_serverName}] 服务端停止成功");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{_serverName}] 停止异常：{ex.Message}");
                }
                finally
                {
                    // 释放资源
                    _udpClient?.Dispose();
                    _udpClient = null;
                }
            }
        }

        /// <summary>
        /// 异步接收数据（APM模式：BeginReceive）
        /// </summary>
        private void BeginReceive()
        {
            if (!_isRunning || _udpClient == null) return;

            try
            {
                // 启动异步接收，完成后回调EndReceive
                _udpClient.BeginReceive(EndReceive, null);
            }
            catch (Exception ex)
            {
                if (_isRunning) // 仅在运行中时打印异常（停止时的异常忽略）
                {
                    Console.WriteLine($"[{_serverName}] 接收数据异常：{ex.Message}");
                    // 延迟重试接收（避免异常循环）
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        Thread.Sleep(1000);
                        BeginReceive();
                    });
                }
            }
        }

        /// <summary>
        /// 异步接收完成回调（APM模式：EndReceive）
        /// </summary>
        /// <param name="ar">异步操作结果</param>
        private void EndReceive(IAsyncResult ar)
        {
            if (!_isRunning || _udpClient == null) return;

            IPEndPoint clientEP = null;
            byte[] receiveBuffer = null;

            try
            {
                // 结束异步接收，获取数据和客户端地址
                clientEP = new IPEndPoint(IPAddress.Any, 0);
                receiveBuffer = _udpClient.EndReceive(ar, ref clientEP);

                try
                {
                    // 解析数据
                    if (receiveBuffer.Length >= 16)
                    {
                        uint iv = BitConverter.ToUInt32(receiveBuffer, 0);
                        uint otherChecksum = BitConverter.ToUInt32(receiveBuffer, receiveBuffer.Length - 4);
                        byte[] packetData = new byte[receiveBuffer.Length - (4 + 4)];
                        Buffer.BlockCopy(receiveBuffer, 4, packetData, 0, packetData.Length);
                        Crypto.HashDecrypt(packetData, packetData.Length, iv);
                        InPacket p = new InPacket(packetData);
                        uint accountID = p.ReadUInt();
                        uint hash = p.ReadUInt();

                        string nickname = "";
                        if (!ClientManager.UserNOToNickname.TryGetValue(accountID, out nickname) || string.IsNullOrEmpty(nickname))
                        {
                            return;
                        }

                        uint packetName = p.ReadUInt();
                        var packetValue = (PacketName)packetName;

                        string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        if (_serverName == "UDP")
                        {
                            Console.WriteLine($"[UDP][{currentTime}][{nickname}] " + packetValue + ": " + BitConverter.ToString(p.ToArray()).Replace("-", " "));
                        }
                        else if (_serverName == "P2P")
                        {
                            Console.WriteLine($"[P2P][{currentTime}][{nickname}] " + packetValue + ": " + BitConverter.ToString(p.ToArray()).Replace("-", " "));
                        }

                        OutPacket outPacket = new OutPacket();
                        if (packetValue == PacketName.PqUdpEcho)
                        {
                            outPacket.WriteUInt(accountID);
                            outPacket.WriteUInt(hash);
                            outPacket.WriteInt((int)PacketName.PrUdpEcho);

                            outPacket.WriteInt(p.ReadInt());
                            outPacket.WriteInt(p.ReadInt());
                            BeginSend(outPacket, clientEP);
                        }
                        else if (packetValue == PacketName.PqUdpTimeSync)
                        {
                            outPacket.WriteUInt(accountID);
                            outPacket.WriteUInt(hash);
                            outPacket.WriteInt((int)PacketName.PrUdpTimeSync);

                            outPacket.WriteInt(p.ReadInt());
                            outPacket.WriteUInt(MultyPlayer.ConvertTick());
                            bool success = BeginSend(outPacket, clientEP);
                            if (MultyPlayer.Ready != null)
                            {
                                if (MultyPlayer.Ready.ContainsKey(nickname) && MultyPlayer.Ready[nickname] == false)
                                {
                                    MultyPlayer.Ready[nickname] = success;
                                }
                            }
                        }
                        else if (packetValue == PacketName.GameSlotPacket)
                        {
                            int roomId = RoomManager.TryGetRoomId(nickname);
                            var room = RoomManager.GetRoom(roomId);
                            if (room != null)
                            {
                                if (room.RelayType == 0) //UDP
                                {
                                    foreach (RoomMember Object in room._slots)
                                    {
                                        if (Object is Player player && player.Nickname != nickname)
                                        {
                                            OutPacket oPacket = new OutPacket();

                                            oPacket.WriteUInt(ClientManager.GetUserNO(player.Nickname));
                                            oPacket.WriteUInt(hash);
                                            oPacket.WriteInt((int)PacketName.GameSlotPacket);

                                            oPacket.WriteBytes(p.ReadBytes(p.Available));
                                            if (ClientManager.ClientUdpAddrs.TryGetValue(player.Nickname, out IPEndPoint UdpPoint))
                                            {
                                                bool success = BeginSend(oPacket, UdpPoint);
                                                if (success)
                                                    Console.WriteLine($"[UDP][{currentTime}][{nickname}] " + packetValue + ": " + BitConverter.ToString(oPacket.ToArray()).Replace("-", " "));
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    using (OutPacket oPacket = new OutPacket("GameSlotPacket"))
                                    {
                                        oPacket.WriteBytes(p.ReadBytes(p.Available));
                                        MultyPlayer.BroadCast(roomId, oPacket, nickname);
                                    }
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Unknown Packet on UDP : {packetValue}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{_serverName}] 处理数据异常：{ex.Message}");
                }
            }
            catch (ObjectDisposedException)
            {
                // 服务端停止时触发，忽略
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[{_serverName}] 处理数据异常：{ex.Message}，错误码：{ex.SocketErrorCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_serverName}] 处理数据异常：{ex.Message}");
            }
            finally
            {
                // 持续接收下一个数据包（核心：循环异步接收）
                if (_isRunning)
                {
                    BeginReceive();
                }
            }
        }

        public bool BeginSend(OutPacket outPacket, IPEndPoint endPoint)
        {
            byte[] buffer = outPacket.ToArray();
            try
            {
                if (buffer.Length >= 16)
                {
                    byte[] data = new byte[buffer.Length + 8];

                    uint siv = (uint)(new Random((int)DateTime.Now.Ticks).Next());
                    uint newHash = Crypto.HashEncrypt(buffer, buffer.Length, siv);
                    Buffer.BlockCopy(BitConverter.GetBytes(siv), 0, data, 0, 4);
                    Buffer.BlockCopy(BitConverter.GetBytes((uint)(siv ^ newHash ^ 1329075907U)), 0, data, data.Length - 4, 4);
                    Buffer.BlockCopy(buffer, 0, data, 4, buffer.Length);

                    int sentBytes = _udpClient.Send(data, data.Length, endPoint);
                    if (sentBytes == data.Length)
                    {
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"发送失败（部分发送）：{sentBytes} / {data.Length}");
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"发送失败（网络错误）：{ex.Message}，错误码：{ex.SocketErrorCode}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发送失败：{ex.Message}");
                return false;
            }
        }
    }
}