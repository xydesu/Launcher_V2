using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using KartRider.Common.Utilities;
using KartRider.IO.Packet;
using KartRider_PacketName;
using Profile;

namespace KartRider
{
    /// <summary>
    /// TCP服务端封装类（用于消息）
    /// </summary>
    public class MsgrServer
    {
        // TCP核心通信对象
        private TcpListener _tcpListener;
        // 监听端口
        private readonly int _listenPort;
        // 服务端名称（日志区分多实例）
        private readonly string _serverName;
        // 线程取消标识
        private volatile bool _isRunning;
        // 同步锁（防止重复启动/停止）
        private readonly object _lockObj = new object();

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="serverName">服务端名称（日志区分）</param>
        /// <param name="listenPort">监听端口（唯一）</param>
        public MsgrServer(string serverName, int listenPort)
        {
            _serverName = serverName;
            _listenPort = listenPort;
            _isRunning = false;
        }

        /// <summary>
        /// 启动TCP服务端
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
                    // 初始化TCP监听器并绑定端口
                    _tcpListener = new TcpListener(IPAddress.Any, _listenPort);
                    _tcpListener.Start();
                    _isRunning = true;

                    Console.WriteLine($"[{_serverName}] 服务端启动成功，监听端口：{_listenPort}");
                    Console.WriteLine($"[{_serverName}] 等待客户端连接...\n");

                    // 开始异步接受连接
                    BeginAcceptClient();
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
        /// 停止TCP服务端
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
                    // 停止TCP监听器
                    _tcpListener?.Stop();
                    Console.WriteLine($"[{_serverName}] 服务端停止成功");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{_serverName}] 停止异常：{ex.Message}");
                }
                finally
                {
                    // 释放资源
                    _tcpListener = null;
                }
            }
        }

        /// <summary>
        /// 异步接受客户端连接
        /// </summary>
        private void BeginAcceptClient()
        {
            if (!_isRunning || _tcpListener == null)
                return;

            try
            {
                // 启动异步接受连接，完成后回调EndAcceptClient
                _tcpListener.BeginAcceptTcpClient(EndAcceptClient, null);
            }
            catch (Exception ex)
            {
                if (_isRunning) // 仅在运行中时打印异常（停止时的异常忽略）
                {
                    Console.WriteLine($"[{_serverName}] 接受连接异常：{ex.Message}");
                    // 延迟重试接受连接（避免异常循环）
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        Thread.Sleep(1000);
                        BeginAcceptClient();
                    });
                }
            }
        }

        /// <summary>
        /// 异步接受连接完成回调
        /// </summary>
        /// <param name="ar">异步操作结果</param>
        private void EndAcceptClient(IAsyncResult ar)
        {
            if (!_isRunning || _tcpListener == null)
                return;

            TcpClient client = null;

            try
            {
                // 结束异步接受连接，获取客户端
                client = _tcpListener.EndAcceptTcpClient(ar);
                Console.WriteLine($"[{_serverName}] 客户端连接成功：{client.Client.RemoteEndPoint}");

                // 为每个客户端创建一个缓冲区并开始异步接收数据
                var clientState = new ClientState { Client = client, Buffer = new byte[4096] };
                BeginReceive(clientState);
            }
            catch (ObjectDisposedException)
            {
                // 服务端停止时触发，忽略
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[{_serverName}] 接受连接异常：{ex.Message}，错误码：{ex.SocketErrorCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_serverName}] 接受连接异常：{ex.Message}");
                client?.Close();
            }
            finally
            {
                // 持续接受下一个客户端连接
                if (_isRunning)
                {
                    BeginAcceptClient();
                }
            }
        }

        /// <summary>
        /// 异步接收数据
        /// </summary>
        /// <param name="clientState">客户端状态</param>
        private void BeginReceive(ClientState clientState)
        {
            if (!_isRunning || clientState.Client == null || !clientState.Client.Connected)
                return;

            try
            {
                // 启动异步接收，完成后回调EndReceive
                clientState.Client.GetStream().BeginRead(clientState.Buffer, 0, clientState.Buffer.Length, EndReceive, clientState);
            }
            catch (Exception ex)
            {
                if (_isRunning)
                {
                    Console.WriteLine($"[{_serverName}] 接收数据异常：{ex.Message}");
                    CloseClient(clientState);
                }
            }
        }

        /// <summary>
        /// 异步接收完成回调
        /// </summary>
        /// <param name="ar">异步操作结果</param>
        private void EndReceive(IAsyncResult ar)
        {
            var clientState = ar.AsyncState as ClientState;
            if (clientState == null || !_isRunning || clientState.Client == null || !clientState.Client.Connected)
                return;

            NetworkStream stream = null;
            int bytesRead = 0;

            try
            {
                stream = clientState.Client.GetStream();
                bytesRead = stream.EndRead(ar);

                if (bytesRead > 0)
                {
                    // 处理接收到的数据
                    byte[] receiveBuffer = new byte[bytesRead];
                    Buffer.BlockCopy(clientState.Buffer, 0, receiveBuffer, 0, bytesRead);

                    try
                    {
                        string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        Console.WriteLine($"[MsgrServer][{currentTime}]: " + BitConverter.ToString(receiveBuffer).Replace("-", " "));

                        BeginSend(clientState, new OutPacket());
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{_serverName}] 处理数据异常：{ex.Message}");
                    }

                    // 继续接收下一个数据包
                    BeginReceive(clientState);
                }
                else
                {
                    // 客户端关闭连接
                    Console.WriteLine($"[{_serverName}] 客户端断开连接：{clientState.Client.Client.RemoteEndPoint}");
                    CloseClient(clientState);
                }
            }
            catch (ObjectDisposedException)
            {
                // 服务端停止时触发，忽略
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[{_serverName}] 处理数据异常：{ex.Message}，错误码：{ex.SocketErrorCode}");
                CloseClient(clientState);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_serverName}] 处理数据异常：{ex.Message}");
                CloseClient(clientState);
            }
        }

        /// <summary>
        /// 异步发送数据
        /// </summary>
        /// <param name="clientState">客户端状态</param>
        /// <param name="outPacket">要发送的数据包</param>
        private bool BeginSend(ClientState clientState, OutPacket outPacket)
        {
            if (!_isRunning || clientState.Client == null || !clientState.Client.Connected)
                return false;

            byte[] buffer = outPacket.ToArray();
            try
            {
                NetworkStream stream = clientState.Client.GetStream();
                stream.BeginWrite(buffer, 0, buffer.Length, EndSend, clientState);
                return true;
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"发送失败（网络错误）：{ex.Message}，错误码：{ex.SocketErrorCode}");
                CloseClient(clientState);
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发送失败：{ex.Message}");
                CloseClient(clientState);
                return false;
            }
        }

        /// <summary>
        /// 异步发送完成回调
        /// </summary>
        /// <param name="ar">异步操作结果</param>
        private void EndSend(IAsyncResult ar)
        {
            var clientState = ar.AsyncState as ClientState;
            if (clientState == null || !_isRunning || clientState.Client == null || !clientState.Client.Connected)
                return;

            try
            {
                clientState.Client.GetStream().EndWrite(ar);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_serverName}] 发送完成异常：{ex.Message}");
                CloseClient(clientState);
            }
        }

        /// <summary>
        /// 关闭客户端连接
        /// </summary>
        /// <param name="clientState">客户端状态</param>
        private void CloseClient(ClientState clientState)
        {
            try
            {
                clientState.Client?.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_serverName}] 关闭客户端异常：{ex.Message}");
            }
        }

        /// <summary>
        /// 客户端状态类
        /// </summary>
        private class ClientState
        {
            public TcpClient Client { get; set; }
            public byte[] Buffer { get; set; }
        }
    }
}