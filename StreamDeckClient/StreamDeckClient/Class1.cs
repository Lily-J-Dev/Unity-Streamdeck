using Network;
using Network.Enums;
using PacketData;
using System;
using System.Collections.Generic;

namespace StreamDeckClient
{
    public class Client
    {
        Connection clientTCP;
        private Dictionary<int, List<(int, bool)>> buttonChanges = new Dictionary<int, List<(int, bool)>>();
        private List<int> newStreamDecks = new List<int>();

        public void Start()
        {
            ConnectionResult cr;
            clientTCP = ConnectionFactory.CreateTcpConnection("127.0.0.1", 1234, out cr);
            clientTCP.RegisterPacketHandler<PacketData.SetButtonState>(HSetButtonState, this);
            clientTCP.RegisterPacketHandler<PacketData.StreamDeckStateChange>(HStreamDeckStateChanged, this);
        }

        public List<(int, bool)> GetButtonChanges(int streamDeckIndex)
        {
            lock (buttonChanges)
            {
                var returnList = new List<(int, bool)>(buttonChanges[streamDeckIndex]);
                buttonChanges[streamDeckIndex].Clear();
                return returnList;
            }
        }

        public List<int> GetNewStreamdecks()
        {
            lock(newStreamDecks)
            {
                var returnList = new List<int>(newStreamDecks);
                newStreamDecks.Clear();
                return returnList;
            }
        }

        private void HSetButtonState(SetButtonState packet, Connection connection)
        {
            lock(buttonChanges)
            {
                if (!buttonChanges.ContainsKey(packet.streamDeckIndex))
                    buttonChanges.Add(packet.streamDeckIndex, new List<(int, bool)>());
                buttonChanges[packet.streamDeckIndex].Add((packet.buttonIndex, packet.buttonState));
            }
        }

        private void connectionEstablished(Connection connection, ConnectionType type)
        {
            Console.WriteLine($"{type.ToString()} Connection established");
        }

        public void Stop()
        {
            clientTCP.Close(CloseReason.ClientClosed);
        }
        
        public void RSetButtonColour(SetButtonColour setButtonColourRequest)
        {
            clientTCP.Send(setButtonColourRequest, this);
        }
        
        public void RSetButtonImage(SetButtonImage setButtonImageRequest)
        {
            clientTCP.Send(setButtonImageRequest, this);
        }
        
        public void RSetDeckColour(SetDeckColour setDeckColourRequest)
        {
            clientTCP.Send(setDeckColourRequest, this);
        }
        
        public void RSetDeckImage(SetDeckImage setDeckImageRequest)
        {
            clientTCP.Send(setDeckImageRequest, this);
        }

        public void HStreamDeckStateChanged(StreamDeckStateChange packet, Connection connection)
        {
            lock (newStreamDecks)
            {
                if (packet.streamDeckConnected)
                    newStreamDecks.Add(packet.streamDeckIndex);
                lock(buttonChanges)
                {
                    buttonChanges.Add(packet.streamDeckIndex, new List<(int, bool)>());
                }
            }
        }
    }
}