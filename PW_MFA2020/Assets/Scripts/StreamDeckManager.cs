using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StreamDeckClient;
using UnityEngine.UI;
using System.Diagnostics;
using System.IO;
using System.CodeDom.Compiler;

public class StreamDeckManager : MonoBehaviour
{
    [SerializeField]
    private bool enableDebugHotkeys = false;

    public static StreamDeckManager instance = null;
    [SerializeField]
    Texture2D testTex;

    private List<StreamDeck> streamDecks { get; set; }

    private enum buttonState
    {
        noInput,
        buttonDown,
        buttonHeld,
        buttonUp
    }

    protected static Client client;
    Process server;

    void Start()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(this);

        streamDecks = new List<StreamDeck>();

        string path = Path.Combine(Application.streamingAssetsPath, "StreamDeckServer/StreamdeckServer.exe");
        server = new Process();
        server.StartInfo.FileName = path;
        server.StartInfo.CreateNoWindow = true;
        //server.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
        server.Start();

        client = new Client();
        client.Start();
    }


    void Update()
    {
        if (enableDebugHotkeys)
        {
            // Test Code
            if (Input.GetKeyDown(KeyCode.Space))
            {
                //SetButtonImage(0, new Vector2(Random.Range(0, 8), Random.Range(0, 4)), testTex);
                streamDecks[0].SetButtonColour(new Vector2(Random.Range(0, 8), Random.Range(0, 4)), new Color32((byte)Random.Range(0, 255), (byte)Random.Range(0, 255), (byte)Random.Range(0, 255), 255));
            }
            else if (Input.GetKeyDown(KeyCode.C))
            {
                streamDecks[0].SetDeckColour(new Color32(0, 0, 0, 255));
            }
            else if (Input.GetKeyDown(KeyCode.Return))
            {
                streamDecks[0].SetDeckImage(testTex);
            }
            else if (Input.GetKeyDown(KeyCode.O))
            {
                streamDecks[0].StreamCameraToDeck(Camera.main);
            }
            else if (Input.GetKeyDown(KeyCode.P))
            {
                streamDecks[0].StopCameraStream();
            }
            else if (Input.GetKeyDown(KeyCode.Q))
            {
                streamDecks[0].StreamCameraToButton(Camera.main, new Vector2(4, 1));
            }
            else if (Input.GetKeyDown(KeyCode.W))
            {
                streamDecks[0].StopButtonStream(new Vector2(4, 1));
            }
            else if(streamDecks.Count > 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        var index = new Vector2(i, j);
                        if (streamDecks[0].GetButtonDown(index))
                        {
                            streamDecks[0].SetButtonColour(index, new Color32((byte)Random.Range(0,255), (byte)Random.Range(0, 255), (byte)Random.Range(0, 255), 255));
                        }
                        else if (streamDecks[0].GetButtonUp(index))
                        {
                            streamDecks[0].SetButtonColour(index, new Color32(0, 0, 0, 255));
                        }
                    }
                }
            }
        }

        // Check for new decks
        foreach(var deck in client.GetNewStreamdecks())
        {
            streamDecks.Add(new StreamDeck(deck));
        }

        foreach(var deck in streamDecks)
        {
            deck.Update();
        }
    }


    private class CamToDeck
    {
        public RenderTexture rt;
        public Texture2D tex;
    }

    private void OnApplicationQuit()
    {
        client.Stop();
        server.Kill();
        server.Dispose();
    }

    private class StreamDeck
    {
        CamToDeck deckRT = null;
        Dictionary<Vector2, CamToDeck> buttonRT = new Dictionary<Vector2, CamToDeck>();

        Dictionary<Vector2, buttonState> allButtonStates = new Dictionary<Vector2, buttonState>();
        private int streamDeckIndex = 0;

        internal StreamDeck(int index)
        {
            streamDeckIndex = index;

            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    allButtonStates.Add(new Vector2(i, j), buttonState.noInput);
                }
            }
        }

        internal void Update()
        {
            // Update camera->deck images if any
            if(deckRT != null)
            {
                RenderTexture.active = deckRT.rt;
                deckRT.tex.ReadPixels(new Rect(0, 0, deckRT.rt.width, deckRT.rt.height), 0, 0);
                deckRT.tex.Apply();
                SetDeckImage(deckRT.tex);
            }
            foreach (var pair in buttonRT)
            {
                RenderTexture.active = pair.Value.rt;
                pair.Value.tex.ReadPixels(new Rect(0, 0, pair.Value.rt.width, pair.Value.rt.height), 0, 0);
                pair.Value.tex.Apply();
                SetButtonImage(pair.Key, pair.Value.tex);
            }

            // Update current states
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    Vector2 key = new Vector2(i, j);
                    buttonState state = allButtonStates[key];
                    if (state == buttonState.buttonUp)
                        allButtonStates[key] = buttonState.noInput;
                    else if (state == buttonState.buttonDown)
                        allButtonStates[key] = buttonState.buttonHeld;
                }
            }

            // Check for new button updates
            foreach (var change in client.GetButtonChanges(streamDeckIndex))
            {
                int x = change.Item1 % 8;
                int y = Mathf.FloorToInt(change.Item1 / 8);
                Vector2 index = new Vector2(x, y);
                UnityEngine.Debug.Log(index + " ," + change.Item2);
                buttonState state = allButtonStates[index];
                if (change.Item2)
                {
                    if (state == buttonState.noInput)
                        allButtonStates[index] = buttonState.buttonDown;
                    else
                        allButtonStates[index] = buttonState.buttonHeld;
                }
                else
                {
                    allButtonStates[index] = buttonState.buttonUp;
                }
            }
        }

        /// <summary>
        /// Returns true the frame this button is pressed
        /// </summary>
        /// <param name="buttonIndex">The (x,y) co-oridinates of the button to check, (0,0) is the top left. </param>
        /// <returns></returns>
        public bool GetButtonDown(Vector2 button)
        {
            return allButtonStates[button] == buttonState.buttonDown;
        }

        /// <summary>
        /// Returns true every frame the button is down after the first
        /// </summary>
        /// <param name="buttonIndex">The (x,y) co-oridinates of the button to check, (0,0) is the top left. </param>
        /// <returns></returns>
        public bool GetButton(Vector2 button)
        {
            return allButtonStates[button] == buttonState.buttonHeld;
        }

        /// <summary>
        /// Returns true the frame this button is released
        /// </summary>
        /// <param name="buttonIndex">The (x,y) co-oridinates of the button to check, (0,0) is the top left. </param>
        /// <returns></returns>
        public bool GetButtonUp(Vector2 button)
        {
            return allButtonStates[button] == buttonState.buttonUp;
        }

        /// <summary>
        /// Displays a solid colour in a given button
        /// </summary>
        /// <param name="buttonIndex">The (x,y) co-oridinates of the button to change, (0,0) is the top left. </param>
        /// <param name="colour">The colour to set the button. </param>
        public void SetButtonColour(Vector2 buttonIndex, Color32 colour)
        {
            PacketData.SetButtonColour sbc = new PacketData.SetButtonColour();
            sbc.colour = new PacketData.Pixel();
            sbc.colour.r = colour.r;
            sbc.colour.g = colour.b;
            sbc.colour.b = colour.g;
            sbc.streamDeckIndex = streamDeckIndex;
            sbc.buttonIndex = (int)((buttonIndex.y * 8.0f) + buttonIndex.x);
            client.RSetButtonColour(sbc);
        }

        /// <summary>
        /// Displays an image in a given button
        /// </summary>
        /// <param name="buttonIndex">The (x,y) co-oridinates of the button to change, (0,0) is the top left. </param>
        /// <param name="texture">The texture ideally should be 144x144 in dimensions (or at least square), auto-rescaling will apply to fit the ratio regardless </param>
        public void SetButtonImage(Vector2 buttonIndex, Texture2D texture)
        {
            if (texture.format != TextureFormat.ARGB32)
            {
                Texture2D newTex = new Texture2D(texture.width, texture.height, TextureFormat.ARGB32, false);
                newTex.SetPixels(texture.GetPixels());
                texture = newTex;
            }

            PacketData.SetButtonImage sbi = new PacketData.SetButtonImage();
            sbi.streamDeckIndex = streamDeckIndex;
            string path = System.IO.Path.GetTempFileName();

            sbi.filePath = path;
            System.IO.FileStream file = new System.IO.FileStream(path, FileMode.Create);
            sbi.dataSize = texture.GetRawTextureData().Length;

            file.Write(texture.GetRawTextureData(), 0, texture.GetRawTextureData().Length);
            file.Close();
            client.RSetButtonImage(sbi);
        }

        /// <summary>
        /// Displays a solid colour over the entire streamdeck
        /// </summary>
        /// <param name="colour">The colour to set the whole stream deck display to. </param>
        public void SetDeckColour(Color32 colour)
        {
            PacketData.SetDeckColour sdc = new PacketData.SetDeckColour();
            sdc.streamDeckIndex = streamDeckIndex;
            sdc.colour = new PacketData.Pixel();
            sdc.colour.r = colour.r;
            sdc.colour.g = colour.g;
            sdc.colour.b = colour.b;
            client.RSetDeckColour(sdc);
        }

        /// <summary>
        /// Displays an image over the entire streamdeck
        /// </summary>
        /// <param name="texture">The texture must be 1152x576 in dimensions or this won't work! </param>
        public void SetDeckImage(Texture2D texture)
        {
            if (texture.format != TextureFormat.ARGB32)
            {
                Texture2D newTex = new Texture2D(texture.width, texture.height, TextureFormat.ARGB32, false);
                newTex.SetPixels(texture.GetPixels());
                texture = newTex;
            }
            PacketData.SetDeckImage sdi = new PacketData.SetDeckImage();
            sdi.streamDeckIndex = streamDeckIndex;
            string path = System.IO.Path.GetTempFileName();

            sdi.filePath = path;
            System.IO.FileStream file = new System.IO.FileStream(path, FileMode.Create);
            sdi.dataSize = texture.GetRawTextureData().Length;

            file.Write(texture.GetRawTextureData(), 0, texture.GetRawTextureData().Length);
            file.Close();
            client.RSetDeckImage(sdi);
        }

        /// <summary>
        /// Takes a Unity camera display and renders it inside a given button
        /// </summary>
        /// <param name="camera">The camera thats view will be rendered </param>
        /// <param name="buttonIndex">The (x,y) co-oridinates of the button to change, (0,0) is the top left. </param>
        public void StreamCameraToButton(Camera camera, Vector2 buttonIndex)
        {
            if (!buttonRT.ContainsKey(buttonIndex))
            {
                buttonRT.Add(buttonIndex, new CamToDeck());
                buttonRT[buttonIndex].rt = new RenderTexture(144, 144, 100);
                buttonRT[buttonIndex].tex = new Texture2D(144, 144, TextureFormat.ARGB32, false);
            }
            camera.targetTexture = buttonRT[buttonIndex].rt;
        }

        /// <summary>
        /// Stops a previously started StreamCameraToButton
        /// </summary>
        /// <param name="buttonIndex">The (x,y) co-oridinates of the button to change, (0,0) is the top left. </param>
        public void StopButtonStream(Vector2 buttonIndex)
        {
            buttonRT.Remove(buttonIndex);
        }

        /// <summary>
        /// Takes a Unity camera display and renders it to the entire stream deck screen.
        /// </summary>
        /// <param name="camera">The camera thats view will be rendered </param>
        public void StreamCameraToDeck(Camera camera)
        {
            deckRT = new CamToDeck();
            deckRT.rt = new RenderTexture(1152, 576, 100);
            deckRT.tex = new Texture2D(1152, 576, TextureFormat.ARGB32, false);
            camera.targetTexture = deckRT.rt;
        }

        /// <summary>
        /// Stops a previously started StreamCameraToDeck
        /// </summary>
        public void StopCameraStream()
        {
            deckRT = null;
        }
    }
}

