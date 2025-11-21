using Microsoft.UI;                         // NuGet -> Microsoft.Windows.SDK.BuildTools  &  Microsoft.WindowsAppSDK
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SharpDX.DirectInput;                  // NuGet -> SharpDX.DirectInput  4.2.0
using Gma.System.MouseKeyHook;              // Nuget -> MouseKeyHook  5.7.1
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Devices.Enumeration;
using System.Diagnostics;
using System.Threading;
using WinRT;

namespace Control3
{
    public sealed partial class MainWindow : Window
    {
        public static TextBlock MessageControl { get; set; }                        // Object to be able to set a message from other windows

        private const int KeepAwakePeriod = 60000;
        private const System.Windows.Forms.Keys KeepAwakeKey = System.Windows.Forms.Keys.Scroll;
        private Remote remoteInstance = null;                                       // Create object for remote window so we can open/close it from the main window
        public static Mouse mouse;                                                  // Used for the mouse movement on system level (SharpDX)
        private IKeyboardMouseEvents globalHook;                                    // Used for all the mouse and keyboard event capture
        private CH9329 MyCH9329;                                                    // The object which sends the data to us CH9329
        List<VideoSourceInfo> videoSourceList = new List<VideoSourceInfo>();        // Used to fill the combobox and obtain the ID to open the remote window
        private Timer keepAwakeTimer;
        private Timer keepAwakeWhileInactiveTimer;
        private int screenWidth;
        private int screenHeight;
        public MainWindow()
        {
            // Initialize Keep Awake Timer
            keepAwakeTimer = new Timer(SendKeepAwakeSignal, null, Timeout.Infinite, Timeout.Infinite);
            keepAwakeWhileInactiveTimer = new Timer(SendKeepAwakeWhileInactiveSignal, null, Timeout.Infinite, Timeout.Infinite);

            // Initialize Window
            this.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(200, 100, 600, 350));
            this.AppWindow.SetIcon(@"Assets\control.ico");
            this.InitializeComponent();
            this.Closed += OnWindowClosed;
            PopulateVideoComboBox();
            MainWindow.MessageControl = message;

            System.Windows.Forms.Screen primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
            screenWidth = primaryScreen.Bounds.Width;
            screenHeight = primaryScreen.Bounds.Height; 

            // SharpDX Mouse initialization to get raw mouse movement 
            var directInput = new DirectInput();
            var firstMouseInstance = directInput.GetDevices(DeviceType.Mouse, DeviceEnumerationFlags.AllDevices).FirstOrDefault();
            mouse = new Mouse(directInput);
            mouse.Properties.AxisMode = DeviceAxisMode.Relative;
            mouse.Acquire();

            InitializePort();

            InitializeKeepAwakeTimers();

        }

        private bool InitializePort()
        {
            // Initialize CH9329 and Mouse hooks
            if (SetPort())
            {
                try
                {
                    MyCH9329 = new CH9329(App.Flag.COMPort);
                    if ((EnableEdgeSessionCheckBox?.IsChecked == true) || App.Flag.isRemote)
                    {
                        return RegisterGlobalHooks();
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    SetMessage($"Error initializing CH9329: '{ex.Message}'", Colors.Red);
                    MyCH9329 = null;
                }
            }

            return false;
        }

        private void ExitRemoteSession()
        {
            if (App.Flag.isFullScreen) { remoteInstance.SetFullScreen(false); }
            else
            {
                App.Flag.isRemote = false;
                MyCH9329.ReleaseAll();
                SetMessage("", Colors.Blue);

                if (EnableEdgeSessionCheckBox.IsChecked != true)
                {
                    UnregisterGlobalHooks();
                }
            }

        }

        private bool RegisterGlobalHooks()
        {
            if (globalHook != null) return true;
            try
            {
                globalHook = Hook.GlobalEvents();
                globalHook.KeyDown += GlobalHook_KeyDown;
                globalHook.KeyUp += GlobalHook_KeyUp;
                globalHook.MouseMove += GlobalHook_MouseMove;
                globalHook.MouseDown += GlobalHook_MouseDown;
                globalHook.MouseUp += GlobalHook_MouseUp;
                globalHook.MouseWheel += GlobalHook_MouseWheel;
                return true;
            }
            catch (Exception ex)
            {
                SetMessage($"Error registering global hook: '{ex.Message}'", Colors.Red);
                return false;
            }
        }

        private void UnregisterGlobalHooks()
        {
            if (globalHook != null)
            {
                globalHook.KeyDown -= GlobalHook_KeyDown;
                globalHook.KeyUp -= GlobalHook_KeyUp;
                globalHook.MouseMove -= GlobalHook_MouseMove;
                globalHook.MouseDown -= GlobalHook_MouseDown;
                globalHook.MouseUp -= GlobalHook_MouseUp;
                globalHook.MouseWheel -= GlobalHook_MouseWheel;
                globalHook.Dispose();
                globalHook = null;
            }
        }

        private void KeepAwakeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            InitializeKeepAwakeTimers();
        }

        private void EnableEdgeSessionCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (EnableEdgeSessionCheckBox.IsChecked == true && globalHook == null)
            {
                RegisterGlobalHooks();
            }
        }

        private void EnableEdgeSessionCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!App.Flag.isRemote)
            {
                UnregisterGlobalHooks();
            }
        }

        /// <summary>
        /// Initializes the Keep Awake Timers
        /// </summary>
        private void InitializeKeepAwakeTimers()
        {
            // Check if the CH9329 is initialized
            if (MyCH9329 == null) return;
            // Get the selected option from the Keep Awake ComboBox
            var selectedOption = keepAwakeComboBox.SelectedItem as ComboBoxItem;
            if (selectedOption == null) return;
            switch (selectedOption.Content.ToString())
            {
                case "Off":
                    keepAwakeTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    keepAwakeWhileInactiveTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    break;
                case "While Inactive":
                    keepAwakeTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    keepAwakeWhileInactiveTimer.Change(0, KeepAwakePeriod);
                    break;
                case "Always":
                    keepAwakeWhileInactiveTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    keepAwakeTimer.Change(0, KeepAwakePeriod);
                    break;
            }
        }

        private void SendKeepAwakeSignal(object state = null)
        {
            //Wait a random amount of time to prevent the remote from detecting a pattern
            Thread.Sleep(new Random().Next(1000, 5000));

            byte value = Flags.KeyMap[(byte)KeepAwakeKey];
            MyCH9329.TapKey((CH9329.SpecialKeyCode)value);
        }

        private void SendKeepAwakeWhileInactiveSignal(object state = null)
        {
            if (!App.Flag.isRemote)
            {
                //Wait a random amount of time to prevent the remote from detecting a pattern
                Thread.Sleep(new Random().Next(1000, 5000));

                byte value = Flags.KeyMap[(byte)KeepAwakeKey];
                MyCH9329.TapKey((CH9329.SpecialKeyCode)value);
            }
        }

        private async void PopulateVideoComboBox()
        {
            string vSource = SettingsManager.GetSetting();
            Debug.WriteLine("From registry: "+vSource);
            var devices = await DeviceInformation.FindAllAsync(Windows.Devices.Enumeration.DeviceClass.VideoCapture);
            if (devices.Any())
            {
                Int32 i = 0;
                Int32 si = 0;
                foreach (var device in devices)
                {
                    string name = device.Name.ToString();
                    string id = device.Id;
                    VideoSourceInfo videoSourceInfo = new VideoSourceInfo { Name = name, Id = id };
                    videoSourceList.Add(videoSourceInfo);
                    if (id == vSource) si = i;
                    i++;
                }
                videoSource.ItemsSource = videoSourceList;
                videoSource.SelectedIndex = si;
            }
        }
        private void sourceSelected(object sender, SelectionChangedEventArgs e)
        {
            if (videoSource.SelectedItem != null)
            {
                VideoSourceInfo selectedVideoSource = (VideoSourceInfo)videoSource.SelectedItem;
                App.Flag.selectedSource = selectedVideoSource.Id;
                SettingsManager.SaveSetting(selectedVideoSource.Id);
            }
        }
        private void NoVideo_Click(object sender, RoutedEventArgs e)
        {
            if (MyCH9329 != null || InitializePort())
            {
                MouseUtility.SetMouseCursorAside();
                mouse.GetCurrentState();
                App.Flag.Decoration = 0;
                App.Flag.isRemote = true;
                SetMessage("Remote Session Active", Colors.Blue);
            }
        }
        private void Video_Click(object sender, RoutedEventArgs e)
        {
            if (App.Flag.selectedSource != null)
            {
                if (App.Flag.COMPort!= null)
                {
                    if (MyCH9329 == null) InitializePort();

                    App.Flag.Decoration = 0;
                    App.Flag.isRemote = true;
                    App.Flag.isFullScreen = (sender as FrameworkElement)?.Tag?.ToString() == "True" || false;  // Fullscreen button clicked?

                    if (remoteInstance == null || remoteInstance.IsClosed) { remoteInstance = new Remote(); }
                    remoteInstance.Activate();
                    remoteInstance.SetFullScreen(App.Flag.isFullScreen);

                    MouseUtility.SetMouseCursorAside();
                    mouse.GetCurrentState();
                    SetMessage("Remote Session Active", Colors.Blue);
                } else SetMessage("No KVM cable present", Colors.Red);
            } else SetMessage("No Videosource available", Colors.Red);
        }
        private void GlobalHook_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (!App.Flag.isRemote) return;

            // Modifier mapping helper
            void ModDown(CH9329.Modifier m) => MyCH9329.PressModifier(m);

            switch (e.KeyCode)
            {
                case System.Windows.Forms.Keys.LControlKey: ModDown(CH9329.Modifier.LEFT_CTRL); break;
                case System.Windows.Forms.Keys.RControlKey: ModDown(CH9329.Modifier.RIGHT_CTRL); break;
                case System.Windows.Forms.Keys.LShiftKey: ModDown(CH9329.Modifier.LEFT_SHIFT); break;
                case System.Windows.Forms.Keys.RShiftKey: ModDown(CH9329.Modifier.RIGHT_SHIFT); break;
                case System.Windows.Forms.Keys.LMenu: ModDown(CH9329.Modifier.LEFT_ALT); break;
                case System.Windows.Forms.Keys.RMenu: ModDown(CH9329.Modifier.RIGHT_ALT); break;
                case System.Windows.Forms.Keys.LWin: ModDown(CH9329.Modifier.LEFT_WIN); break;
                case System.Windows.Forms.Keys.RWin: ModDown(CH9329.Modifier.RIGHT_WIN); break;
            }

            // Skip Ctrl+Alt+E on keydown
            if (e.KeyCode == System.Windows.Forms.Keys.E &&
                MyCH9329 != null &&
                (App.Flag.Decoration & (1 << 0)) != 0 &&      // Ctrl
                (App.Flag.Decoration & (1 << 2)) != 0)        // Alt
            {
                e.Handled = true;
                return;
            }

            try
            {
                byte hid = Flags.KeyMap[(byte)e.KeyValue];
                MyCH9329.KeyDown((CH9329.SpecialKeyCode)hid);
            }
            catch { }

            e.Handled = true;
        }

        private void GlobalHook_KeyUp(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (!App.Flag.isRemote) return;

            // Modifier mapping helper
            void ModUp(CH9329.Modifier m) => MyCH9329.ReleaseModifier(m);

            switch (e.KeyCode)
            {
                case System.Windows.Forms.Keys.LControlKey: ModUp(CH9329.Modifier.LEFT_CTRL); break;
                case System.Windows.Forms.Keys.RControlKey: ModUp(CH9329.Modifier.RIGHT_CTRL); break;
                case System.Windows.Forms.Keys.LShiftKey: ModUp(CH9329.Modifier.LEFT_SHIFT); break;
                case System.Windows.Forms.Keys.RShiftKey: ModUp(CH9329.Modifier.RIGHT_SHIFT); break;
                case System.Windows.Forms.Keys.LMenu: ModUp(CH9329.Modifier.LEFT_ALT); break;
                case System.Windows.Forms.Keys.RMenu: ModUp(CH9329.Modifier.RIGHT_ALT); break;
                case System.Windows.Forms.Keys.LWin: ModUp(CH9329.Modifier.LEFT_WIN); break;
                case System.Windows.Forms.Keys.RWin: ModUp(CH9329.Modifier.RIGHT_WIN); break;
            }

            // Ctrl+Alt+E exits session
            if (e.KeyCode == System.Windows.Forms.Keys.E &&
                (App.Flag.Decoration & (1 << 0)) != 0 &&
                (App.Flag.Decoration & (1 << 2)) != 0)
            {
                ExitRemoteSession();
                e.Handled = true;
                return;
            }

            try
            {
                byte hid = Flags.KeyMap[(byte)e.KeyValue];
                MyCH9329.KeyUp();
            }
            catch { }

            e.Handled = true;
        }


        private void GlobalHook_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!App.Flag.isRemote) { return; }

            MyCH9329.MouseScroll(e.Delta < 0 ? -1 : 1);
            ((MouseEventExtArgs)e).Handled = true;
        }
        private void GlobalHook_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!App.Flag.isRemote)
            {
                // Check if the mouse is at the left edge of the screen
                if (EnableEdgeSessionCheckBox.IsChecked == true && e.X <= 0 && e.Y > 80 && e.Y < (screenHeight - 80))
                {
                    // Switch to remote mode
                    if (App.Flag.COMPort != null)
                    {
                        MouseUtility.SetMouseCursorAside();
                        mouse.GetCurrentState();
                        App.Flag.Decoration = 0;
                        App.Flag.isRemote = true;
                        SetMessage("Remote Session Active", Colors.Blue);
                    }
                    else
                    {
                        SetMessage("No KVM cable present", Colors.Red);
                    }
                }
                return;
            }

            MouseUtility.SetMouseCursorAside();
            if (!App.Flag.isMoving)
            {
                App.Flag.isMoving = true;  // In CH9329 the flag is set to false again after the package is sent to remote. Prevents queue of movement on remote
                var mouseState = mouse.GetCurrentState();
                int X = mouseState.X; int Y = mouseState.Y;
                MyCH9329.MouseMoveRel(X, Y);
            }
            ((MouseEventExtArgs)e).Handled = true;
        }
        private void GlobalHook_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!App.Flag.isRemote) { return; }

            if (e.Button == System.Windows.Forms.MouseButtons.Left) { MyCH9329.MouseButtonDown(CH9329.MouseButtonCode.LEFT); }
            else if (e.Button == System.Windows.Forms.MouseButtons.Right) { MyCH9329.MouseButtonDown(CH9329.MouseButtonCode.RIGHT); }
            else if (e.Button == System.Windows.Forms.MouseButtons.Middle) { MyCH9329.MouseButtonDown(CH9329.MouseButtonCode.MIDDLE); }
            else if (e.Button == System.Windows.Forms.MouseButtons.XButton1) { MyCH9329.MouseButtonDown(CH9329.MouseButtonCode.X1); }
            else if (e.Button == System.Windows.Forms.MouseButtons.XButton2)
            {
                // Skip Processing of XButton2 on mousedown
            }
            ((MouseEventExtArgs)e).Handled = true;
        }
        private void GlobalHook_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!App.Flag.isRemote) { return; }

            else if (e.Button == System.Windows.Forms.MouseButtons.XButton2)
            {
                // Exit remote session on XButton2
                if (App.Flag.isFullScreen) { remoteInstance.SetFullScreen(false); }
                else
                {
                    App.Flag.isRemote = false;
                    SetMessage("", Colors.Blue);
                    if (EnableEdgeSessionCheckBox.IsChecked != true)
                    {
                        UnregisterGlobalHooks();
                    }
                }
            }
            MyCH9329.MouseButtonUpAll();
            ((MouseEventExtArgs)e).Handled = true;
        }
        public void OnWindowClosed(object sender, WindowEventArgs e)
        {
            if (remoteInstance != null && !remoteInstance.IsClosed) { remoteInstance.Close(); }
        }
        public static void SetMessage(string line, object color)
        {
            if (MessageControl != null)
            {
                MessageControl.Text = line;
                MessageControl.Foreground = new SolidColorBrush((Windows.UI.Color)color);
            }
        }
        public bool SetPort()
        {
            Dictionary<string, string> serialPorts = SerialPortUtility.GetSerialPorts();
            foreach (var port in serialPorts)
            {
                if (port.Value.Contains("VID_1A86&PID_7523"))
                {
                    //Remove the default option
                    if (App.Flag.COMPort == null)
                        comPort.Items.RemoveAt(0);

                    //Add an option to the comPort combobox
                    ComboBoxItem portOption = new ComboBoxItem { Content = port.Key };
                    comPort.Items.Add(portOption);

                    //Select the new option
                    comPort.SelectedItem = portOption;

                    App.Flag.COMPort = port.Key;
                }
            }

            if ( App.Flag.COMPort == null)
            {
                comPort.SelectedIndex = 0;
                SetMessage("No KVM cable present", Colors.Red);

                return false;
            }

            return true;
        }
    }
}
