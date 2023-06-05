using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Reflection;
using GTA;
using GTA.Math;
using System.Drawing;
using GTA.Native;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Diagnostics;
using NativeUI;


namespace TeFS
{
    public class TeFS : Script
    {
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public int type;
            public InputUnion inputUnion;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        public float baseline = 0.54f;  //stereo baseline
        public float posY = 0.6f;  // camera y position on the car
        public float posZ = 1.0f; // camera z postion on the car
        public int angle = 0; // -15 for 15 degree inwards
        public int vAngle = 0;  // -6 for 6 degree downwards
        public float camVfov = 59.0f;
        // relative hFov for Ffov = 59 is 90f
        public bool paused = false;
        public int camIndex = 2;
        public Vehicle egoCar = null;
        public Camera cam0 = null;
        public Camera cam1 = null;
        public float fClipConst = 600.0f;
        public float nClipConst = 0.01f;
        public float carSpeed = 5f;
        public float maxSpdConst = 4.16f;  //4.16 run 100 for test


        public bool reduceTraffic = false;
        public float trafficRatio = 0.0f;
        public int storeTimeParameter = 5;
        public int uniTickCount = 0;
        public int pauseStartTickCount = 0;
        public int tickCount = 0;
        public int currentTickcycle = 0;
        public bool tickstart = false;
        //public bool timeCheck = false;

        //public TimeSpan elapsedTime = DateTime.Now - DateTime.Now;
        //public Vector3 camPosInit = new Vector3(0, 0, 0);

        public int frameCaptured = 0;
        public int camCount0 = 0;
        public int camCount1 = 0;
        public bool manualLock = false;

        //parameter for screen capture
        public int screenWidth = 3840;
        public int screenHeight = 2160;
        public int captureWidth = 1920;
        public int captureHeight = 1080;
        public int offsetX = 0;
        public int offsetY = 0;
        public int captureX = 0;//(screenWidth - captureWidth) / 2;
        public int captureY = 0;//(screenHeight - captureHeight) / 2;

        public int currentDestIndex = 0;
        List<Vector3> routes = new List<Vector3>();
        public Vector3 startPoint = new Vector3(0, 0, 0);
        private Vector3 endPoint = new Vector3(0, 0, 0);
        private bool autodriveEnabled = false;
        public string outputDir = "";
        public MenuPool _menuPool;
        public UIMenu mainMenu;
        public UIMenu weatherMenu;

        public string initialWeather = "EXTRASUNNY";
        //public int initTime = 7;  //7 //22
        public bool debug = false;


        public TeFS()
        {
            readConfig();
            TeFSMenu();
            this.Tick += onTick;
            this.KeyUp += onKeyUp;
            this.KeyDown += onKeyDown;
        }
        private void onTick(object sender, EventArgs e)
        {
            _menuPool.ProcessMenus();
            if (weatherMenu.Visible)
            {
                Game.DisableAllControlsThisFrame(0);
            }
            if (mainMenu.Visible)
            {
                Game.DisableAllControlsThisFrame(0);
            }

            Function.Call(Hash.SET_WEATHER_TYPE_NOW, initialWeather);

            if (reduceTraffic)
            {
                Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, trafficRatio);
                Function.Call(Hash.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0.0f);
            }
            if (egoCar != null && cam0 != null)
            {
                Game.Player.WantedLevel = 0;
                if (debug)
                {
                    // UI.ShowSubtitle("car position: " + egoCar.Position + " cam0 pos: " + cam0.Position +  "cam0 rot: " +cam0.Rotation+ "cam0 dir: " + cam0.Direction);
                    UI.ShowSubtitle("car position: " + egoCar.Position + " tickCount: " + tickCount + " uniTickCount: " + uniTickCount);
                }
                egoCar.MaxSpeed = maxSpdConst;
                cam0.Rotation = egoCar.Rotation + new Vector3(vAngle, 0, angle);
                cam1.Rotation = egoCar.Rotation + new Vector3(vAngle, 0, -angle);
                /*if (tickCount < 3500)
                {
                    //    UI.ShowSubtitle("in Game time: " + Game.GameTime + " tick Count: " + tickCount + "UnitickCount: " + uniTickCount + " tick Start: " + tickstart + "FPS " + Game.FPS);
                }*/
                float distanceToEnd = Vector3.Distance(egoCar.Position, endPoint);
                if (distanceToEnd < 30f)
                {
                    //Vector3 newDest = destinationCoords[currentDestIndex];
                    Vector3 newDest = routes[currentDestIndex];
                    addNewDestination(newDest, carSpeed);
                    if (egoCar.Speed > 1)
                    {
                        float throttle = 0.5f;
                        Game.SetControlNormal(2, GTA.Control.VehicleBrake, throttle);

                    }
                    else
                    {
                        //egoCar.Speed = 0;
                        Game.SetControlNormal(2, GTA.Control.VehicleBrake, 1.0f);

                    } //ending needs more work
                }



            }

            uniTickCount++;
            if (uniTickCount == pauseStartTickCount + 1 * storeTimeParameter && paused)
            {
                if (camIndex == 1)
                {
                    screenCapture(outputDir + "gta0/cam1/", camCount1);
                    simulateKeyOG("N");
                }
                if (camIndex == 0)
                {
                    screenCapture(outputDir + "gta0/cam0/", camCount0);
                    simulateKeyOG("B");
                    metadataOut(camCount0);

                }
            }

            if (uniTickCount == pauseStartTickCount + 3 * storeTimeParameter && paused)
            {
                if (camIndex == 1)
                {
                    camCount1++;
                    swapCam(cam0);
                }
                else if (camIndex == 0)
                {
                    camCount0++;
                    swapCam(cam1);
                }
            }

            if (uniTickCount == pauseStartTickCount + 3 * storeTimeParameter + 1 && paused)
            {
                if (camIndex == 1)
                {
                    Game.Pause(false);
                    paused = false;
                    tickstart = true;
                }
                else if (camIndex == 0)
                {
                    tickstart = true;
                    Game.TimeScale = 1;
                    Game.Pause(false);
                    paused = false;

                }
            }




            if (tickstart)
            {
                tickCount++;

            }
            if (manualLock && tickCount > 4000)
            {
                if ((tickCount + 1) % 10 == 0 && tickstart)
                {
                    Game.TimeScale = 0.001f;
                }

                if (tickCount % 10 == 0 && tickstart)
                {
                    currentTickcycle = tickCount;
                    camIndex = 0;
                    Game.Pause(true);
                    paused = true;

                    pauseStartTickCount = uniTickCount;
                    tickstart = false;
                }
                else if (tickCount == currentTickcycle + 2 && tickstart)
                {

                    Game.Pause(true);
                    paused = true;
                    camIndex = 1;
                    pauseStartTickCount = uniTickCount;

                    tickstart = false;

                }

            }
        }



        private void onKeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5)
            {
                mainMenu.Visible = !mainMenu.Visible;
            }


            if (e.KeyCode == Keys.U)
            {
                if (Game.TimeScale == 1)
                {
                    Game.TimeScale = 0.001f;
                }
                else
                {
                    Game.TimeScale = 1;
                }
            }
            if (e.KeyCode == Keys.K)
            {
                screenCapture(outputDir + "gta0/test/", 0);
                UI.ShowSubtitle(outputDir + "gta0/cam0/  " + routes[0].ToString() + screenHeight.ToString() + screenHeight.ToString() + " reduceT" + reduceTraffic.ToString() + "store time" + storeTimeParameter);
            }



        }

        private void onKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.NumPad7)
            {
                Game.Pause(true);
            }

            if (e.KeyCode == Keys.NumPad9)
            {
                Game.Pause(false);
                tickstart = true;
            }
            if (e.KeyCode == Keys.NumPad1)
            {
                if (egoCar == null)
                {
                    Create();
                }
                //cam.IsActive = true;
                World.RenderingCamera = cam0;
                tickstart = true;
                UI.ShowSubtitle("Cam:1", 3000);


            }
            if (e.KeyCode == Keys.NumPad3)
            {
                if (World.RenderingCamera == cam0)
                {
                    World.RenderingCamera = cam1;
                    UI.ShowSubtitle("Cam: 1", 3000);
                }
                else if (World.RenderingCamera == cam1)
                {
                    World.RenderingCamera = cam0;
                    UI.ShowSubtitle("Cam: 0", 3000);
                }
            }
            if (e.KeyCode == Keys.Add)
            {
                World.RenderingCamera = null;
                UI.ShowSubtitle("Cam: gameplay Cam", 3000);

            }
            if (e.KeyCode == Keys.Divide)
            {
                stereoInit();
            }
            if (e.KeyCode == Keys.Multiply)
            {
                // tickCount = 0;
                // manualLock = false;
                debug = !debug;

            }
            autoDriveControl(e);


        }


        //create ego car, camera, and attach player to car
        public void Create()
        {

            egoCar = World.CreateVehicle("Blista", Game.Player.Character.GetOffsetInWorldCoords(new Vector3(0, 5, 0)));
            Game.Player.Character.SetIntoVehicle(egoCar, VehicleSeat.Driver);

            cam0 = World.CreateCamera(egoCar.Position, new Vector3(0, 0, 0), camVfov);
            cam0.FarClip = fClipConst;
            cam0.NearClip = nClipConst;

            cam0.AttachTo(egoCar, new Vector3(-baseline / 2, posY, posZ));
            cam0.Direction = new Vector3(0, 0, 0);
            cam0.Rotation = egoCar.Rotation + new Vector3(vAngle, 0, angle);
            cam1 = World.CreateCamera(egoCar.Position, new Vector3(0, 0, 0), camVfov);
            cam1.FarClip = fClipConst;
            cam1.NearClip = nClipConst;

            cam1.AttachTo(egoCar, new Vector3(baseline / 2, posY, posZ));
            cam1.Direction = new Vector3(0, 0, 0);
            cam1.Rotation = egoCar.Rotation + new Vector3(vAngle, 0, -angle);
            egoCar.Position = startPoint;
        }

        public void screenCapture(String folderName, int nameCount)
        {
            Bitmap bmp = new Bitmap(captureWidth, captureHeight);
            Graphics graphics = Graphics.FromImage(bmp);
            graphics.CopyFromScreen(captureX + offsetX, captureY + offsetY, 0, 0, bmp.Size);
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

            string folder = folderName;

            string dataSubfolder = folder + "data/";
            string depthSubfolder = folder + "depth/";

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            if (!Directory.Exists(dataSubfolder))
            {
                Directory.CreateDirectory(dataSubfolder);
            }

            if (!Directory.Exists(depthSubfolder))
            {
                Directory.CreateDirectory(depthSubfolder);
            }

            bmp.Save(folder + "data/" + nameCount.ToString("D6") + ".png", System.Drawing.Imaging.ImageFormat.Png);
            //UI.Notify("Screenshot saved to " + folder + "screenshot_" + timestamp + ".png");
            frameCaptured = tickCount;

        }

        public void metadataOut(int camCountIndex)
        {
            string data = "fov：" + cam0.FieldOfView + " car position: " + egoCar.Position.X + " " + egoCar.Position.Y + " " + egoCar.Position.Z + " cam0 position: " + cam0.Position.X + " " + cam0.Position.Y + " " + cam0.Position.Z + " cam0 rotation: " + cam0.Rotation.X + " " + cam0.Rotation.Y + " " + cam0.Rotation.Z + " cam0 direction: " + cam0.Direction.X + " " + cam0.Direction.Y + " " + cam0.Direction.Z + " cam1 position: " + cam1.Position.X + " " + cam1.Position.Y + " " + cam1.Position.Z + " cam1 rotation: " + cam1.Rotation.X + " " + cam1.Rotation.Y + " " + cam1.Rotation.Z + " cam1 direction: " + cam1.Direction.X + " " + cam1.Direction.Y + " " + cam1.Direction.Z + " world Time: " + World.CurrentDayTime.Hours + " " + World.CurrentDayTime.Minutes + " " + World.CurrentDayTime.Seconds + " " + World.Weather;

            // Save the camera's position and rotation to a text file
            //File.WriteAllText($"metadata_{camCountIndex}.txt", data);
            string folder = outputDir + "metadata";
            //"C:/Users/luoye/Desktop/stereo output/ScreenShot/";

            // Check if the folder exists
            if (!Directory.Exists(folder))
            {
                // Create the folder if it doesn't exist
                Directory.CreateDirectory(folder);
            }

            //File.WriteAllText(Path.Combine(folder, "metadata_" + camCountIndex.ToString("D6") + ".txt"), data);
            File.WriteAllText(Path.Combine(folder, camCountIndex.ToString("D6") + ".txt"), data);
        }

        public void swapCam(Camera camName)
        {
            World.RenderingCamera = camName;
        }

        public void simulateKeyOG(string keyName)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = 1; // Keyboard input type
            inputs[0].inputUnion.ki = new KEYBDINPUT();
            if (keyName == "B")
            {
                inputs[0].inputUnion.ki.wVk = 0x42; // F10 key 0x79 virtual key code  B key: 0x42 N key: 0x4E
            }
            else if (keyName == "N")
            {
                inputs[0].inputUnion.ki.wVk = 0x4E;
            }
            else if (keyName == "Num1")
            {
                inputs[0].inputUnion.ki.wVk = 0x61;
            }
            else if (keyName == "O")
            {
                inputs[0].inputUnion.ki.wVk = 0x4F;
            }
            else
            {
                //UI.ShowSubtitle("F10 pressed");

                inputs[0].inputUnion.ki.wVk = 0x79;

            }
            inputs[0].inputUnion.ki.wScan = 0;
            inputs[0].inputUnion.ki.dwFlags = 0;
            inputs[0].inputUnion.ki.time = 0;
            inputs[0].inputUnion.ki.dwExtraInfo = IntPtr.Zero;

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public void autoDriveControl(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.O)
            {
                autodriveEnabled = !autodriveEnabled;
                if (autodriveEnabled)
                {
                    Ped player = Game.Player.Character;
                    Vehicle vehicle = player.CurrentVehicle;
                    if (vehicle != null && vehicle.GetPedOnSeat(VehicleSeat.Driver) == player)
                    {
                        //vehicle.Position = startPoint; // Teleport the vehicle to the start point
                        if (World.GetWaypointPosition().X != 0)
                        {
                            endPoint = World.GetWaypointPosition();
                        }

                        DrivingStyle drivingStyle = DrivingStyle.AvoidTrafficExtremely;
                        Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD, player.Handle, vehicle, endPoint.X, endPoint.Y, endPoint.Z, carSpeed, (int)drivingStyle, 1.0f);
                    }
                }
                else
                {
                    stopAutoDrive();
                }
                //still needs work

            }

        }

        public void stopAutoDrive()
        {

            Function.Call(Hash.CLEAR_PED_TASKS, Game.Player.Character);

        }


        public void addNewDestination(Vector3 newPos, float maxSpd)
        {
            if (endPoint != newPos)
            {

                Function.Call(Hash.CLEAR_PED_TASKS, Game.Player.Character);
                endPoint = newPos;
                if (routes.Count > currentDestIndex + 1)
                {
                    currentDestIndex++;
                }
                Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD, Game.Player.Character.Handle, egoCar, newPos.X, newPos.Y, newPos.Z, maxSpd, (int)DrivingStyle.Normal, 1.0f);
            }
        }


        public void TeFSMenu()
        {
            _menuPool = new MenuPool();
            mainMenu = new UIMenu("TeFS Stereo", "MAIN MENU");
            _menuPool.Add(mainMenu);

            List<string> mainNames = new List<string>
            {
                "Teleport to Ego Car",
                "Change Weather",
                "Drive to WayPoint",
                "Auto Drive",
                "Drive Custom Route",
                "Set Time",
                //"Teleport to Marker",
                "Stop Auto Drive",
                "Start Stereo Collection"

            };

            List<Action> mainActions = new List<Action>
            {
                () => TeleToCar(),
                () => { },
                () => driveToMaker(),
                () => AutoDrive(),
                () => driveCustomRoute(),
                () => { },
                //() => TeleportToMarker(), //Needs more work.
                () => stopAutoDrive(),
                () => stereoInit()

            };


            UIMenu setTimeMenu = new UIMenu("Set Time", "SELECT AN OPTION TO SET");
            _menuPool.Add(setTimeMenu);
            UIMenuItem currentTimeItem = new UIMenuItem("Current Time", "This is the current in-game time.");
            setTimeMenu.AddItem(currentTimeItem);

            setTimeMenu.OnMenuOpen += (sender) =>
            {
                currentTimeItem.Text = "Current Time: " + World.CurrentDayTime.ToString(@"hh\:mm\:ss");
            };

            List<string> setTimeOptions = new List<string> { "Hour", "Minute", "Second" };

            foreach (string option in setTimeOptions)
            {
                UIMenuItem item = new UIMenuItem(option);
                setTimeMenu.AddItem(item);

                setTimeMenu.OnItemSelect += (sender, selectedItem, index) =>
                {
                    if (selectedItem == item)
                    {
                        string input = Game.GetUserInput(2);

                        if (int.TryParse(input, out int value))
                        {
                            switch (selectedItem.Text)
                            {
                                case "Hour":
                                    World.CurrentDayTime = new TimeSpan(value, World.CurrentDayTime.Minutes, World.CurrentDayTime.Seconds);
                                    break;
                                case "Minute":
                                    World.CurrentDayTime = new TimeSpan(World.CurrentDayTime.Hours, value, World.CurrentDayTime.Seconds);
                                    break;
                                case "Second":
                                    World.CurrentDayTime = new TimeSpan(World.CurrentDayTime.Hours, World.CurrentDayTime.Minutes, value);
                                    break;
                            }
                            currentTimeItem.Text = "Current Time: " + World.CurrentDayTime.ToString(@"hh\:mm\:ss");

                        }
                    }
                };
            }


            List<string> weatherNames = new List<string>
            {
                "EXTRASUNNY",
                "CLEAR",
                "CLOUDS",
                "SMOG",
                "FOGGY",
                "OVERCAST",
                "RAIN",
                "THUNDER",
                "NEUTRAL",
                "SNOW",
                "BLIZZARD",
                "SNOWLIGHT",
            };

            weatherMenu = new UIMenu("Weathers", "SELECT A WEATHER TYPE");
            _menuPool.Add(weatherMenu);

            for (int i = 0; i < weatherNames.Count; i++)
            {
                UIMenuItem item = new UIMenuItem(weatherNames[i]);
                weatherMenu.AddItem(item);
            }


            weatherMenu.OnItemSelect += (sender, selectedItem, index) =>
            {
                if (weatherNames.Contains(selectedItem.Text))
                {
                    ChangeWeather(selectedItem.Text);
                }
            };

            for (int i = 0; i < mainNames.Count; i++)
            {
                if (mainNames[i] == "Change Weather")
                {
                    UIMenuItem changeWeatherItem = new UIMenuItem(mainNames[i]);
                    mainMenu.AddItem(changeWeatherItem);
                    mainMenu.BindMenuToItem(weatherMenu, changeWeatherItem);
                }
                else if (mainNames[i] == "Set Time")
                {
                    UIMenuItem setTimeItem = new UIMenuItem(mainNames[i]);
                    mainMenu.AddItem(setTimeItem);
                    mainMenu.BindMenuToItem(setTimeMenu, setTimeItem);
                }
                else
                {
                    UIMenuItem item = new UIMenuItem(mainNames[i]);
                    mainMenu.AddItem(item);
                }
            }
            mainMenu.OnItemSelect += (sender, selectedItem, index) =>
            {
                int actionIndex = mainNames.IndexOf(selectedItem.Text);

                if (actionIndex >= 0 && actionIndex < mainActions.Count)
                {
                    mainActions[actionIndex]();
                }
            };

            mainMenu.RefreshIndex();
            _menuPool.RefreshIndex();
        }


        public void stereoInit()
        {
            tickCount = 3800;
            manualLock = true;
            Function.Call(Hash.SET_WEATHER_TYPE_NOW, initialWeather);
            // Set time to 12:00 (noon)
            //Function.Call(Hash.SET_CLOCK_TIME, initTime, 0, 0);
            UI.ShowSubtitle("Stereo Collection will start soon", 1000);
        }


        public void TeleToCar()
        {
            UI.ShowSubtitle("TeleToCar");
            if (egoCar == null)
            {
                Create();
            }
            //cam.IsActive = true;
            World.RenderingCamera = cam0;
            tickstart = true;
            UI.ShowSubtitle("Cam:1", 3000);
        }

        public void ChangeWeather(string weatherName)
        {
            UI.ShowSubtitle("Change Weather " + weatherName);
            initialWeather = weatherName;
        }

        public void driveToMaker()
        {
            UI.ShowSubtitle("Drive to WayPoint");
            //Function.Call(Hash.SET_NEW_WAYPOINT, 844.8807f, 1770.133f);
            //simulateKeyOG("O");
            Ped player = Game.Player.Character;
            Vehicle playerVehicle = player.CurrentVehicle;

            if (playerVehicle != null)
            {
                Vector3 waypoint = World.GetWaypointPosition();

                if (waypoint != Vector3.Zero)
                {
                    player.Task.DriveTo(playerVehicle, waypoint, 10f, 100f);
                }
                else
                {
                    UI.ShowSubtitle("No waypoint set");
                }
            }
            else
            {
                UI.ShowSubtitle("You are not in a vehicle!");
            }


        }
        public void AutoDrive()
        {
            UI.ShowSubtitle("Auto Drive");
            Ped player = Game.Player.Character;
            Vehicle playerVehicle = player.CurrentVehicle;

            if (playerVehicle != null)
            {
                player.Task.CruiseWithVehicle(playerVehicle, carSpeed, (int)DrivingStyle.Normal);
            }
            else
            {
                UI.ShowSubtitle("You are not in a vehicle!");
            }

        }

        public void driveCustomRoute()
        {
            UI.ShowSubtitle("Drive Custom Route");
            Ped player = Game.Player.Character;
            Vehicle playerVehicle = player.CurrentVehicle;

            if (playerVehicle != null)
            {
                //  Function.Call(Hash.SET_NEW_WAYPOINT, destinationCoords[0].X, destinationCoords[0].Y);
                Function.Call(Hash.SET_NEW_WAYPOINT, routes[0].X, routes[0].Y);
                simulateKeyOG("O");
            }
            else
            {
                UI.ShowSubtitle("You are not in a vehicle!");
            }

        }
        public void TeleportToMarker()
        {
            Vector3 waypointPosition = World.GetWaypointPosition();
            float groundZ = World.GetGroundHeight(waypointPosition);
            //            waypointPosition.Z = groundZ + 1.5f; 
            if (waypointPosition == Vector3.Zero)
            {
                UI.ShowSubtitle("No waypoint set.");
            }
            else
            {
                if (Game.Player.Character.IsInVehicle())
                {
                    Game.Player.Character.CurrentVehicle.Position = waypointPosition;
                    UI.ShowSubtitle("Teleported vehicle to waypoint.");
                }
                else
                {
                    Game.Player.Character.Position = waypointPosition;
                    UI.ShowSubtitle("Teleported to waypoint.");
                }
            }
        }

        public void readConfig()
        {
            string directory = AppDomain.CurrentDomain.BaseDirectory;
            string configPath = Path.Combine(directory, "config.txt");

            string[] lines = File.ReadAllLines(configPath);

            string currentSection = "";

            foreach (string line in lines)
            {
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2);
                }
                else
                {
                    switch (currentSection)
                    {
                        case "Output":
                            if (line.StartsWith("Directory="))
                            {
                                outputDir = line.Substring("Directory=".Length);
                            }
                            break;
                        case "Start Position":
                            string[] startCoord = line.Split(',');
                            if (startCoord.Length == 3)
                            {
                                float x = float.Parse(startCoord[0]);
                                float y = float.Parse(startCoord[1]);
                                float z = float.Parse(startCoord[2]);
                                startPoint = new Vector3(x, y, z);
                            }
                            break;
                        case "Custom Routes":
                            string[] coords = line.Split(',');
                            if (coords.Length == 3)
                            {
                                float x = float.Parse(coords[0]);
                                float y = float.Parse(coords[1]);
                                float z = float.Parse(coords[2]);
                                routes.Add(new Vector3(x, y, z));
                            }
                            break;
                        case "Reduce Traffic":
                            string[] traffic = line.Split(',');
                            if (traffic.Length == 2)
                            {
                                if (int.Parse(traffic[0]) == 1)
                                {
                                    reduceTraffic = true;
                                }
                                else
                                {
                                    reduceTraffic = false;
                                }
                                trafficRatio = float.Parse(traffic[1]);
                            }
                            break;
                        case "Speed":
                            string[] speedConst = line.Split(',');
                            if (speedConst.Length == 2)
                            {
                                carSpeed = float.Parse(speedConst[0]);
                                maxSpdConst = float.Parse(speedConst[1]);
                            }
                            break;
                        case "Screen Resolution":
                            string[] SRes = line.Split(',');
                            if (SRes.Length == 2)
                            {
                                screenWidth = int.Parse(SRes[0]);
                                screenHeight = int.Parse(SRes[1]);
                            }
                            break;
                        case "Game Resolution":
                            string[] GRes = line.Split(',');
                            if (GRes.Length == 2)
                            {
                                captureWidth = int.Parse(GRes[0]);
                                captureHeight = int.Parse(GRes[1]);
                            }
                            break;
                        case "Capture Offset":
                            string[] offsets = line.Split(',');
                            if (offsets.Length == 2)
                            {
                                offsetX = int.Parse(offsets[0]);
                                offsetY = int.Parse(offsets[1]);
                            }
                            break;
                        case "Camera Setup":
                            string[] CamParams = line.Split(',');
                            if (CamParams.Length == 8)
                            {
                                baseline = float.Parse(CamParams[0]);
                                angle = int.Parse(CamParams[1]);
                                vAngle = int.Parse(CamParams[2]);
                                camVfov = float.Parse(CamParams[3]);
                                fClipConst = float.Parse(CamParams[4]);
                                nClipConst = float.Parse(CamParams[5]);
                                posY = float.Parse(CamParams[6]);
                                posZ = float.Parse(CamParams[7]);
                            }
                            break;
                        case "Store Time":
                            string[] StoreTime = line.Split(',');
                            storeTimeParameter = int.Parse(StoreTime[0]);
                            
                            break;
                    }
                }
            }


        }





    }
}
