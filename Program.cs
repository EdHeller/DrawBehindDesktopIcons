using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using CefSharp;
using CefSharp.WinForms;
using RestartManager;

namespace DrawBehindDesktopIcons
{
    class Program
    {
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("User32.dll")]
        static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("gdi32.dll")]
        static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        //for later LL_Mouse handling events like window dragging.
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        // https://msdn.microsoft.com/fr-fr/library/windows/desktop/ms686016.aspx
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(SetConsoleCtrlEventHandler handler, bool add);

        // https://msdn.microsoft.com/fr-fr/library/windows/desktop/ms683242.aspx
        private delegate bool SetConsoleCtrlEventHandler(CtrlType sig);


        public static NotifyIcon trayIcon;

        public static ChromiumWebBrowser chromeBrowser;

        private enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }
        // ***also dllimport of that function***
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        public static void Main(string[] args)
        {
            //DPI settings (if the text is scailed in later windows versions eg. 125% then the window won't take up the full space)
            if (Environment.OSVersion.Version.Major >= 6)
                SetProcessDPIAware();



#if DEBUG
           //some special debug  function here...
#elif !DEBUG
            //release version
            //hide our output window. And the access errors
            IntPtr Selfhandle = Process.GetCurrentProcess().MainWindowHandle;
            ShowWindow(Selfhandle, 0);
#endif

            
            trayIcon = new NotifyIcon();
            trayIcon.Text = "Desktop Drawing Application";
            trayIcon.Icon = new Icon(SystemIcons.Question, 40, 40);

            MenuItem configMenuItem = new MenuItem("Debug", new EventHandler(ShowConfig));
            MenuItem exitMenuItem = new MenuItem("Exit", new EventHandler(Exit));

            trayIcon.ContextMenu = new ContextMenu(new MenuItem[]
                { configMenuItem, exitMenuItem });
            trayIcon.Visible = true;

            trayIcon.MouseClick += new MouseEventHandler((s, e) =>
            { 

            });
            //Console.WriteLine("OPENED");
            // Register the handler
            SetConsoleCtrlHandler(Handler, true);

            //PrintVisibleWindowHandles(2);
            // The output will look something like this. 
            // .....
            // 0x00010190 "" WorkerW
            //   ...
            //   0x000100EE "" SHELLDLL_DefView
            //     0x000100F0 "FolderView" SysListView32
            // 0x000100EC "Program Manager" Progman



            // Fetch the Progman window
            IntPtr progman = W32.FindWindow("Progman", null);

            IntPtr result = IntPtr.Zero;

            // Send 0x052C to Progman. This message directs Progman to spawn a 
            // WorkerW behind the desktop icons. If it is already there, nothing 
            // happens.
            W32.SendMessageTimeout(progman,
                                   0x052C,
                                   new IntPtr(0),
                                   IntPtr.Zero,
                                   W32.SendMessageTimeoutFlags.SMTO_NORMAL,
                                   1000,
                                   out result);


            //PrintVisibleWindowHandles(2);
            // The output will look something like this
            // .....
            // 0x00010190 "" WorkerW
            //   ...
            //   0x000100EE "" SHELLDLL_DefView
            //     0x000100F0 "FolderView" SysListView32
            // 0x00100B8A "" WorkerW                                   <--- This is the WorkerW instance we are after!
            // 0x000100EC "Program Manager" Progman

            IntPtr workerw = IntPtr.Zero;

            // We enumerate all Windows, until we find one, that has the SHELLDLL_DefView 
            // as a child. 
            // If we found that window, we take its next sibling and assign it to workerw.
            W32.EnumWindows(new W32.EnumWindowsProc((tophandle, topparamhandle) =>
            {
                IntPtr p = W32.FindWindowEx(tophandle,
                                            IntPtr.Zero,
                                            "SHELLDLL_DefView",
                                            IntPtr.Zero);

                if (p != IntPtr.Zero)
                {
                    // Gets the WorkerW Window after the current one.
                    workerw = W32.FindWindowEx(IntPtr.Zero,
                                               tophandle,
                                               "WorkerW",
                                               IntPtr.Zero);
                }

                return true;
            }), IntPtr.Zero);

            // We now have the handle of the WorkerW behind the desktop icons.
            // We can use it to create a directx device to render 3d output to it, 
            // we can use the System.Drawing classes to directly draw onto it, 
            // and of course we can set it as the parent of a windows form.
            //
            // There is only one restriction. The window behind the desktop icons does
            // NOT receive any user input. So if you want to capture mouse movement, 
            // it has to be done the LowLevel way (WH_MOUSE_LL, WH_KEYBOARD_LL).

           


            // Demo 1: Draw graphics between icons and wallpaper

            /* if (dc != IntPtr.Zero)
             {
                 // Create a Graphics instance from the Device Context
                 using (Graphics g = Graphics.FromHdc(dc))
                 {


                     // Use the Graphics instance to draw a white rectangle in the upper 
                     // left corner. In case you have more than one monitor think of the 
                     // drawing area as a rectangle that spans across all monitors, and 
                     // the 0,0 coordinate beeing in the upper left corner.
                     g.FillRectangle(new SolidBrush(Color.White), 0, 0, actualPixelsX, actualPixelsY);

                 }
                 // make sure to release the device context after use.
                 W32.ReleaseDC(workerw, dc);
             }*/

            // Demo 2: Demo 2: Put a Windows Form behind desktop icons

            Form form = new Form();
            form.Text = "Test Window";
            form.FormBorderStyle = FormBorderStyle.None;
            form.AutoScaleMode = AutoScaleMode.Inherit;

            /*
            form.MouseDown += new MouseEventHandler((s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(form.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                }
            });
            */



            form.FormClosing += new FormClosingEventHandler((s, e) =>
            {

                Console.WriteLine("Chrome Closing");

                Cef.Shutdown();
            });

            form.Load += new EventHandler((s, e) =>
            {
                String page = string.Format(@"{0}\html-resources\html\index.html", Application.StartupPath);
                //String page = @"C:\Users\SDkCarlos\Desktop\artyom-HOMEPAGE\index.html";

                if (!File.Exists(page))
                {
                    MessageBox.Show("Error The html file doesn't exists : " + page);
                }

                CefSettings settings = new CefSettings();
                // Allow the use of local resources in the browser
                BrowserSettings browserSettings = new BrowserSettings();
                browserSettings.FileAccessFromFileUrls = CefState.Enabled;
                browserSettings.UniversalAccessFromFileUrls = CefState.Enabled;


                // Initialize cef with the provided settings
                Cef.Initialize(settings);
                // Create a browser component
                chromeBrowser = new ChromiumWebBrowser(page);

                //change the settings before making the browser
                chromeBrowser.BrowserSettings = browserSettings;

                // Add it to the form and fill it to the form window.
                form.Controls.Add(chromeBrowser);
                chromeBrowser.Dock = DockStyle.Fill;



                int actualPixelsX = Screen.PrimaryScreen.Bounds.Width;
                int actualPixelsY = Screen.PrimaryScreen.Bounds.Height;
                IntPtr dc = W32.GetDCEx(workerw, IntPtr.Zero, (W32.DeviceContextValues)0x403);
                if (dc != IntPtr.Zero)
                {
                    int DESKTOPVERTRES = 117;
                    int DESKTOPHORZRES = 118;
                    actualPixelsX = GetDeviceCaps(dc, DESKTOPHORZRES);
                    actualPixelsY = GetDeviceCaps(dc, DESKTOPVERTRES);
                    Console.WriteLine("Size X:" + actualPixelsX);

                    Console.WriteLine("Size Y:" + actualPixelsY);
                }
                W32.ReleaseDC(workerw, dc);





                // adjust for DPI scaling...
                form.Width = actualPixelsX;
                form.Height = actualPixelsY;
                form.Left = 0;
                form.Top = 0;
                form.BackColor = Color.Black;
                // Add a randomly moving button to the form
                /*Button button = new Button() { Text = "Catch Me" };
                form.Controls.Add(button);
                Random rnd = new Random();
                System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
                timer.Interval = 100;
                timer.Tick += new EventHandler((sender, eventArgs) =>
                {
                    button.Left = rnd.Next(0, form.Width - button.Width);
                    button.Top = rnd.Next(0, form.Height - button.Height);
                });
                timer.Start();*/

                // Those two lines make the form a child of the WorkerW, 
                // thus putting it behind the desktop icons and out of reach 
                // for any user intput. The form will just be rendered, no 
                // keyboard or mouse input will reach it. You would have to use 
                // WH_KEYBOARD_LL and WH_MOUSE_LL hooks to capture mouse and 
                // keyboard input and redirect it to the windows form manually, 
                // but thats another story, to be told at a later time.


                W32.SetParent(form.Handle, workerw);
            });

            // Start the Application Loop for the Form.
            Application.Run(form);
        }

        static void ShowConfig(object sender, EventArgs e)
        {
            //doesn't work on win 10...
            Console.WriteLine("Debug event!");
            IntPtr Selfhandle = Process.GetCurrentProcess().MainWindowHandle;
            ShowWindow(Selfhandle, 1);
        }

        static void Exit(object sender, EventArgs e)
        {
            Console.WriteLine("Exit event!");
            //unregister the handler...
            SetConsoleCtrlHandler(Handler, false);
            //find a better way to refresh the explorer process / swap out the current wall paper with itself.
            foreach (Process p in Process.GetProcesses())
            {
                // In case we get Access Denied
                try
                {
                    if (p.MainModule.FileName.ToLower().EndsWith(":\\windows\\explorer.exe"))
                    {
                        p.Kill();
                        break;
                    }
                }
                catch
                {
                    //access deined. 
                    Console.WriteLine("Exception caught.");
                }
            }
            Process.Start("explorer.exe");
            Console.WriteLine("exiting");
            Environment.Exit(0);
        }

        static bool Handler(CtrlType signal)
        {
            switch (signal)
            {
                case CtrlType.CTRL_BREAK_EVENT:
                case CtrlType.CTRL_C_EVENT:
                case CtrlType.CTRL_LOGOFF_EVENT:
                case CtrlType.CTRL_SHUTDOWN_EVENT:
                case CtrlType.CTRL_CLOSE_EVENT:
                    //unregister the handler...
                    SetConsoleCtrlHandler(Handler, false);


                    //find a better way to refresh the explorer process / swap out the current wall paper with itself.
                    foreach (Process p in Process.GetProcesses())
                    {
                        // In case we get Access Denied
                        try
                        {
                            if (p.MainModule.FileName.ToLower().EndsWith(":\\windows\\explorer.exe"))
                            {
                                p.Kill();
                                break;
                            }
                        }
                        catch (Exception e)
                        {
                            //access deined. 
                            Console.WriteLine("{0} Exception caught.", e);
                        }
                    }

                    //errors out 32x process can't shut down a 64x process... 
                    //attempt to rebuild on 32x...
                    //Cef.Shutdown();
                    Process.Start("explorer.exe");
                    Console.WriteLine("exiting");
                    //spawned app continues to run...
                    //new is declared somewhere that should be instanced...
                    Environment.Exit(0);
                    return false;

                default:
                    return false;
            }
        }

        static void PrintVisibleWindowHandles(IntPtr hwnd, int maxLevel = -1, int level = 0)
        {
            bool isVisible = W32.IsWindowVisible(hwnd);

            if (isVisible && (maxLevel == -1 || level <= maxLevel))
            {
                StringBuilder className = new StringBuilder(256);
                W32.GetClassName(hwnd, className, className.Capacity);

                StringBuilder windowTitle = new StringBuilder(256);
                W32.GetWindowText(hwnd, windowTitle, className.Capacity);

                Console.WriteLine("".PadLeft(level * 2) + "0x{0:X8} \"{1}\" {2}", hwnd.ToInt64(), windowTitle, className);

                level++;

                // Enumerates all child windows of the current window
                W32.EnumChildWindows(hwnd, new W32.EnumWindowsProc((childhandle, childparamhandle) =>
                {
                    PrintVisibleWindowHandles(childhandle, maxLevel, level);
                    return true;
                }), IntPtr.Zero);
            }
        }
        static void PrintVisibleWindowHandles(int maxLevel = -1)
        {
            // Enumerates all existing top window handles. This includes open and visible windows, as well as invisible windows.
            W32.EnumWindows(new W32.EnumWindowsProc((tophandle, topparamhandle) =>
            {
                PrintVisibleWindowHandles(tophandle, maxLevel);
                return true;
            }), IntPtr.Zero);
        }
    }
}
