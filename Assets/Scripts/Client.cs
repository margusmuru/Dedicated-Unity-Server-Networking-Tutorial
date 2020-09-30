using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class Client
    {
        public static int DataBufferSize = 4096;

        public int Id;
        public Player Player;
        public TCP Tcp;
        public UDP Udp;

        public Client(int clientId)
        {
            Id = clientId;
            Tcp = new TCP(Id);
            Udp = new UDP(Id);
        }

        public class TCP
        {
            public TcpClient Socket;
            private readonly int _id;
            private NetworkStream _stream;
            private Packet _receivedData;
            private byte[] _recieveBuffer;

            public TCP(int id)
            {
                _id = id;
            }

            public void Connect(TcpClient socket)
            {
                Socket = socket;
                socket.ReceiveBufferSize = DataBufferSize;
                socket.SendBufferSize = DataBufferSize;
                _stream = socket.GetStream();
                _receivedData = new Packet();
                _recieveBuffer = new byte[DataBufferSize];

                _stream.BeginRead(_recieveBuffer, 0, DataBufferSize, RecieveCallback, null);

                ServerSend.Welcome(_id, "Welcome to the server!");
            }

            public void Disconnect()
            {
                Socket.Close();
                _stream = null;
                _receivedData = null;
                _recieveBuffer = null;
                Socket = null;
            }

            private void RecieveCallback(IAsyncResult result)
            {
                try
                {
                    int byteLength = _stream.EndRead(result);
                    if (byteLength <= 0)
                    {
                        Server.Clients[_id].Disconnect();
                        return;
                    }
                    byte[] data = new byte[byteLength];
                    Array.Copy(_recieveBuffer, data, byteLength);
                    _receivedData.Reset(HandleData(data));
                    _stream.BeginRead(_recieveBuffer, 0, DataBufferSize, RecieveCallback, null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error recieving TCP data: {ex}");
                    Server.Clients[_id].Disconnect();
                }
            }
            
            private bool HandleData(byte[] data)
            {
                int packetLength = 0;
                _receivedData.SetBytes(data);
                if (_receivedData.UnreadLength() >= 4)
                {
                    packetLength = _receivedData.ReadInt();
                    if (packetLength <= 0)
                    {
                        return true;
                    }
                }

                while (packetLength > 0 && packetLength <= _receivedData.UnreadLength())
                {
                    byte[] packetBytes = _receivedData.ReadBytes(packetLength);
                    ThreadManager.ExecuteOnMainThread(() =>
                    {
                        using (Packet packet = new Packet(packetBytes))
                        {
                            int packetId = packet.ReadInt();
                            Server.PacketHandlers[packetId](_id, packet);
                        }
                    });

                    packetLength = 0;
                    if (_receivedData.UnreadLength() >= 4)
                    {
                        packetLength = _receivedData.ReadInt();
                        if (packetLength <= 0)
                        {
                            return true;
                        }
                    }
                }

                if (packetLength <= 1)
                {
                    return true;
                }

                return false;
            }

            public void SendData(Packet packet)
            {
                try
                {
                    if (Socket != null)
                    {
                        _stream.BeginWrite(packet.ToArray(), 0, packet.Length(), null, null);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error sending data to player {_id} via TCP: {e}");
                }
            }
        }

        public class UDP
        {
            public IPEndPoint EndPoint;
            private int _id;

            public UDP(int id)
            {
                _id = id;
            }

            public void Connect(IPEndPoint endPoint)
            {
                EndPoint = endPoint;
            }

            public void Disconnect()
            {
                EndPoint = null;
            }

            public void SendData(Packet packet)
            {
                Server.SendUdpData(EndPoint, packet);
            }

            public void HandleData(Packet packetData)
            {
                int packetLength = packetData.ReadInt();
                byte[] packetBytes = packetData.ReadBytes(packetLength);
                
                ThreadManager.ExecuteOnMainThread(() =>
                {
                    using (Packet packet = new Packet(packetBytes))
                    {
                        int packetId = packet.ReadInt();
                        Server.PacketHandlers[packetId](_id, packet);
                    }
                });
            }
        }

        public void SendIntoGame(string playerName)
        {
            Player = NetworkManager.Instance.InstantiatePlayer();
            Player.Initialize(Id, playerName);
            foreach (Client client in Server.Clients.Values)
            {
                if (client.Player != null)
                {
                    if (client.Id != Id)
                    {
                        ServerSend.SpawnPlayer(Id, client.Player);
                    }
                }
            }

            foreach (Client client in Server.Clients.Values)
            {
                if (client.Player != null)
                {
                    ServerSend.SpawnPlayer(client.Id, Player);
                }
            }
        }

        private void Disconnect()
        {
            Console.WriteLine($"{Tcp.Socket.Client.RemoteEndPoint} has disconnected.");
            ThreadManager.ExecuteOnMainThread(() =>
            {
                UnityEngine.Object.Destroy(Player.gameObject);
                Player = null;
            });
            
            Tcp.Disconnect();
            Udp.Disconnect();
        }
    }