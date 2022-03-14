using System;
using System.Threading;
using StreamDeckClient;

namespace TestClient
{
    class Program
    {
        static void Main(string[] args)
        {
            Client c = new Client();
            c.Start();
            PacketData.SetButtonColour sbc = new PacketData.SetButtonColour();
            sbc.buttonIndex = 0;
            sbc.streamDeckIndex = 0;
            sbc.colour = new PacketData.Pixel();
            sbc.colour.r = 200;
            sbc.colour.b = 0;
            sbc.colour.g = 0;
            c.RSetButtonColour(sbc);
            while (true)
            {
                Thread.Sleep(100);
            }
        }
    }
}
