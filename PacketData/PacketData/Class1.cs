using Network.Attributes;
using Network.Packets;
using System;
using System.Collections.Generic;


namespace PacketData
{
    public class SetButtonColour : Packet
    {
        public int streamDeckIndex { get; set; }
        public int buttonIndex { get; set; }
        public Pixel colour { get; set; }
    }

    public class Pixel
    {
        public byte r { get; set; }
        public byte g { get; set; }
        public byte b { get; set; }
    }


    public class SetButtonImage : Packet
    {
        public int streamDeckIndex { get; set; }
        public int buttonIndex { get; set; }
        public string filePath { get; set; }
        public int dataSize { get; set; }
        //public List<Pixel> pixels { get; set; } // Should be 144*144
    }

    public class SetDeckColour : Packet
    {
        public int streamDeckIndex { get; set; }
        public Pixel colour { get; set; }
    }

    public class SetDeckImage : Packet
    {
        public int streamDeckIndex { get; set; }
        public string filePath { get; set; }
        public int dataSize { get; set; }
        //public List<Pixel> pixels { get; set; } // Should be 144*144 *32
    }

    public class SetButtonState : Packet
    {
        public int streamDeckIndex { get; set; }
        public int buttonIndex { get; set; }
        public bool buttonState { get; set; }
    }

    public class StreamDeckStateChange : Packet
    {
        public bool streamDeckConnected { get; set; }
        public int streamDeckIndex { get; set; }
    }
        
}