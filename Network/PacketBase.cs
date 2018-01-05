﻿using System;
using System.Net;
using System.Threading.Tasks;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using NetworkCommsDotNet.Connections.UDP;
using ProtoBuf;

namespace norsu.ass.Network
{
    [ProtoContract]
    abstract class Packet<T> where T : Packet<T>
    {
        public static string Header => typeof(T).Name;

        public override string ToString()
        {
            return Header;
        }

        public async Task Send(string ip, int port)
        {
            await Packet.Send(this, ip, port);
        }

        public async Task Send(IPEndPoint ep)
        {
            await Packet.Send(this, ep);
        }
    }

    class Packet
    {
        public static async Task Send(object message, string ip, int port)
        {
            await Send(message, new IPEndPoint(IPAddress.Parse(ip), port));
        }

        public static async Task Send(object message, IPEndPoint ip)
        {
            var sent = false;
            while (!sent)
            {
                try
                {
                    UDPConnection.SendObject(message.ToString(), message, ip, NetworkComms.DefaultSendReceiveOptions,
                        ApplicationLayerProtocolStatus.Enabled);
                    sent = true;
                    break;
                }
                catch (Exception)
                {
#if __ANDROID__
                    await Task.Delay(100);
#else
                    await TaskEx.Delay(100);
#endif
                }
            }
        }
}
}
