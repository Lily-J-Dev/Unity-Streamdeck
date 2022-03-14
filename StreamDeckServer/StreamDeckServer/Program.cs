using Network;
using Network.Enums;
using StreamDeckSharp;
using System;
using System.Collections.Generic;
using PacketData;
using OpenMacroBoard.SDK;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;

namespace StreamDeckServer
{
    class Server
    {
        private ServerConnectionContainer server;
        Connection clientConnection = null;
        List<StreamDeckContainer> decks = new List<StreamDeckContainer>();

        private int buttonPixelWidth;
        private int buttonPixelHeight;
        private int buttonPixelCount;
                                     
        private int deckPixelWidth;
        private int deckPixelHeight;
        private int deckPixelCount;

        private byte[] deckData;

        private DirectBitmap deckImage;
        private DirectBitmap buttonImage;

        private Dictionary<int, SetDeckImage> deckRenderQueue = new Dictionary<int, SetDeckImage>();
        private Dictionary<(int,int), SetButtonImage> buttonRenderQueue = new Dictionary<(int,int), SetButtonImage>();

        static void Main(string[] args)
        {
            Server server = new Server();
            server.Run();
        }

        public void Run()
        {
            deckImage = new DirectBitmap(1152, 576);
            buttonImage = new DirectBitmap(144, 144);
            buttonPixelWidth = 144;
            buttonPixelHeight = 144;
            buttonPixelCount = buttonPixelWidth * buttonPixelHeight;

            deckPixelWidth = buttonPixelWidth * 8;
            deckPixelHeight = buttonPixelHeight * 4;
            deckPixelCount = deckPixelWidth * deckPixelHeight;

            int i = 0;
            foreach (var device in StreamDeck.EnumerateDevices())
            {
                decks.Add(new StreamDeckContainer(device.Open(), i));
                i++;
            }

            StartServer();

            while (server.IsTCPOnline)
            {
                CheckForButtonUpdates();
                CheckForRenderUpdates();
                Thread.Sleep(20);
            }

            foreach (var deck in decks)
                deck.deck.Dispose();

            deckImage.Dispose();
        }

        private void CheckForButtonUpdates()
        {
            foreach(var deck in decks)
            {
                foreach(var change in deck.GetButtonChanges())
                {
                    PacketData.SetButtonState sbs = new SetButtonState();
                    sbs.streamDeckIndex = deck.index;
                    sbs.buttonIndex = change.Item1;
                    sbs.buttonState = change.Item2;
                    clientConnection.Send(sbs);
                }
            }
        }

        private void CheckForRenderUpdates()
        {
            // Render the decks first
            List<SetDeckImage> deckImages = new List<SetDeckImage>();
            lock (deckRenderQueue)
            {
                foreach (var pair in deckRenderQueue)
                {
                    deckImages.Add(pair.Value);
                }
                deckRenderQueue.Clear();
            }

            foreach (SetDeckImage sdi in deckImages)
            {
                RenderToDeck(sdi);
            }

            // Then individual buttons
            List<SetButtonImage> buttons = new List<SetButtonImage>();
            lock (buttonRenderQueue)
            {
                foreach (var pair in buttonRenderQueue)
                {
                    buttons.Add(pair.Value);
                }
                buttonRenderQueue.Clear();
            }

            foreach(SetButtonImage sbi in buttons)
            {
                RenderToButton(sbi);
            }
        }

        private void StartServer()
        {
            Console.WriteLine("Starting Server...");
            server = ConnectionFactory.CreateServerConnectionContainer(1234, false);
            server.ConnectionLost += (a, b, c) => Console.WriteLine($"{server.Count} {b.ToString()} Connection lost {a.IPRemoteEndPoint.Port}. Reason {c.ToString()}");
            server.ConnectionEstablished += OnConnect;
            server.Start();
        }

        private void OnConnect(Connection connection, ConnectionType type)
        {
            Console.WriteLine($"{server.Count} {connection.GetType()} connected on port {connection.IPRemoteEndPoint.Port}");
            // We only care for a single connection

            clientConnection = connection;

            Console.WriteLine($"{server.Count} {connection.GetType()} connected on port {connection.IPRemoteEndPoint.Port}");
        
            connection.RegisterRawDataHandler("Test", (rawData, con) => Console.WriteLine("Test"));
            connection.SendRawData("Test", null);
        
            //3. Register packet listeners.
            connection.RegisterPacketHandler<SetButtonColour>(HSetButtonColour, this);
            connection.RegisterPacketHandler<SetDeckColour>(HSetDeckColour, this);
            connection.RegisterPacketHandler<SetButtonImage>(HSetButtonImage, this);
            connection.RegisterPacketHandler<SetDeckImage>(HSetDeckImage, this);
            connection.RegisterRawDataHandler("Exit", (rawData, con) => connection.Close(CloseReason.ClientClosed));

            foreach(var deck in decks)
            {
                var sdsc = new StreamDeckStateChange();
                sdsc.streamDeckConnected = true;
                sdsc.streamDeckIndex = deck.index;
                connection.Send(sdsc);
            }
        }


        private void HSetButtonColour(SetButtonColour packet, Connection connection)
        {
            decks[packet.streamDeckIndex].deck.SetKeyBitmap(packet.buttonIndex, OpenMacroBoard.SDK.KeyBitmap.Create.FromRgb(packet.colour.r, packet.colour.g, packet.colour.b));
        }
        
        private void HSetButtonImage(SetButtonImage packet, Connection connection)
        {
            var id = (packet.streamDeckIndex, packet.buttonIndex);
            lock (buttonRenderQueue)
            {
                if (buttonRenderQueue.ContainsKey(id))
                {
                    File.Delete(buttonRenderQueue[id].filePath);
                }
                buttonRenderQueue[id] = packet;
            }
        }

        private void HSetDeckColour(SetDeckColour packet, Connection connection)
        {
            System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(deckPixelWidth, deckPixelHeight);
            System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp);
            g.Clear(System.Drawing.Color.FromArgb(packet.colour.r, packet.colour.g, packet.colour.b));

            decks[packet.streamDeckIndex].deck.DrawFullScreenBitmap(bmp);
        }

        private void HSetDeckImage(SetDeckImage packet, Connection connection)
        {
            lock(deckRenderQueue)
            {
                if(deckRenderQueue.ContainsKey(packet.streamDeckIndex))
                {
                    File.Delete(deckRenderQueue[packet.streamDeckIndex].filePath);
                }
                deckRenderQueue[packet.streamDeckIndex] = packet;
            }
        }

        private void RenderToButton(SetButtonImage packet)
        {
            System.IO.FileStream file = new System.IO.FileStream(packet.filePath, FileMode.Open);
            byte[] data = new byte[packet.dataSize];
            file.Read(data, 0, packet.dataSize);

            int index = 0;

            for (int i = 0; i < buttonPixelHeight; i++)
            {
                for (int j = 0; j < buttonPixelWidth; j++)
                {
                    buttonImage.SetPixel(j, buttonPixelHeight - (i + 1), System.Drawing.Color.FromArgb(data[index+1], data[index + 2], data[index + 3]));
                    index += 4;
                }
            }
            decks[packet.streamDeckIndex].deck.SetKeyBitmap(packet.buttonIndex, OpenMacroBoard.SDK.KeyBitmap.Create.FromBitmap(buttonImage.Bitmap));
            file.Close();
            File.Delete(packet.filePath);
        }

        private void RenderToDeck(SetDeckImage packet)
        {
            System.IO.FileStream file = new System.IO.FileStream(packet.filePath, FileMode.Open);
            if (deckData == null)
                deckData = new byte[packet.dataSize];
            file.Read(deckData, 0, packet.dataSize);

            int index = 0;

            for (int i = 0; i < deckPixelHeight; i++)
            {
                for (int j = 0; j < deckPixelWidth; j++)
                {
                    deckImage.SetPixel(j, deckPixelHeight - (i + 1), System.Drawing.Color.FromArgb(deckData[index+1], deckData[index + 2], deckData[index + 3]));
                    index += 4;
                }
            }
            decks[packet.streamDeckIndex].deck.DrawFullScreenBitmap(deckImage.Bitmap);
            file.Close();
            File.Delete(packet.filePath);
        }
    }

    public class DirectBitmap : IDisposable
    {
        public Bitmap Bitmap { get; private set; }
        public Int32[] Bits { get; private set; }
        public bool Disposed { get; private set; }
        public int Height { get; private set; }
        public int Width { get; private set; }

        protected GCHandle BitsHandle { get; private set; }

        public DirectBitmap(int width, int height)
        {
            Width = width;
            Height = height;
            Bits = new Int32[width * height];
            BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
            Bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppArgb, BitsHandle.AddrOfPinnedObject());
        }

        public void SetPixel(int x, int y, Color colour)
        {
            int index = x + (y * Width);
            int col = colour.ToArgb();

            Bits[index] = col;
        }

        public Color GetPixel(int x, int y)
        {
            int index = x + (y * Width);
            int col = Bits[index];
            Color result = Color.FromArgb(col);

            return result;
        }

        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            Bitmap.Dispose();
            BitsHandle.Free();
        }
    }

    public class StreamDeckContainer
    {
        public StreamDeckContainer(IStreamDeckBoard d, int deckIndex)
        {
            index = deckIndex;
            deck = d;
            d.KeyStateChanged += KeyStateChanged;
        }

        public List<(int, bool)> GetButtonChanges()
        {
            lock (buttonChanges)
            {
                var returnList = new List<(int, bool)>(buttonChanges);
                buttonChanges.Clear();
                return returnList;
            }

        }

        private void KeyStateChanged(object sender, KeyEventArgs e)
        {
            Debug.Print(e.Key.ToString() + ", " + e.IsDown.ToString());
            lock (buttonChanges)
            {
                buttonChanges.Add((e.Key, e.IsDown));
            }
        }


        public IStreamDeckBoard deck { get; set; }
        public int index { get; }
        private List<(int, bool)> buttonChanges = new List<(int, bool)>();

    }
}