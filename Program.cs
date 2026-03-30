using LowLevelInput.Hooks;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Timers;

namespace RitualWalker
{
    public class Program
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private const string ActivePlayerEndpoint = @"https://127.0.0.1:2999/liveclientdata/activeplayer";
        private const string PlayerListEndpoint = @"https://127.0.0.1:2999/liveclientdata/playerlist";
        private const string ChampionStatsEndpoint = @"https://raw.communitydragon.org/latest/game/data/characters/";
        private const string SettingsFile = @"settings\settings.json";

        private static bool HasProcess = false;
        private static bool IsExiting = false;
        private static bool isInitializing = false;
        private static bool isUpdating = false;

        private static readonly Settings CurrentSettings = new Settings();
        private static readonly WebClient Client = new WebClient();
        private static readonly InputManager InputManager = new InputManager();
        private static Process LeagueProcess = null;

        private static readonly Timer OrbWalkTimer = new Timer(1000d / 60d);
        private static Timer attackSpeedCacheTimer;

        private static bool OrbWalkerTimerActive = false;
        private static bool showMenu = true;

        private static string ActivePlayerName = string.Empty;
        private static string ChampionName = string.Empty;
        private static string RawChampionName = string.Empty;

        private static double ClientAttackSpeed = 0.625;
        private static double ChampionAttackCastTime = 0.625;
        private static double ChampionAttackTotalTime = 0.625;
        private static double ChampionAttackSpeedRatio = 0.625;
        private static double ChampionAttackDelayPercent = 0.3;
        private static double ChampionAttackDelayScaling = 1.0;

        private static readonly double MinInputDelay = 1d / 60d;
#if DEBUG
        private static int TimerCallbackCounter = 0;
#endif

        public static double GetSecondsPerAttack() => 1 / ClientAttackSpeed;
        
        public static double GetWindupDuration() => (((GetSecondsPerAttack() * ChampionAttackDelayPercent) - ChampionAttackCastTime) * ChampionAttackDelayScaling) + ChampionAttackCastTime;
        
        public static double GetBufferedWindupDuration()
        {
            // dynamic buffer based on AS
            double buffer = ClientAttackSpeed > 2.5 ? 0.015 : 
                           ClientAttackSpeed > 2.0 ? 0.020 : 
                           ClientAttackSpeed > 1.5 ? 0.025 : 0.030;
            
            return GetWindupDuration() + buffer - CurrentSettings.PingCompensation;
        }

        public static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            if (!File.Exists(SettingsFile))
            {
                Directory.CreateDirectory("settings");
                CurrentSettings.CreateNew(SettingsFile);
            }
            else
            {
                CurrentSettings.Load(SettingsFile);
            }

            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            Client.Proxy = null;

            Console.Clear();
            Console.CursorVisible = true;

            InputManager.Initialize();
            InputManager.OnKeyboardEvent += InputManager_OnKeyboardEvent;
            InputManager.OnMouseEvent += InputManager_OnMouseEvent;

            OrbWalkTimer.Elapsed += OrbWalkTimer_Elapsed;
#if DEBUG
            Timer callbackTimer = new Timer(16.66);
            callbackTimer.Elapsed += Timer_CallbackLog;
#endif

            attackSpeedCacheTimer = new Timer(100);
            attackSpeedCacheTimer.Elapsed += AttackSpeedCacheTimer_Elapsed;

            ShowMainMenu();
        }

        private static void ShowMainMenu()
        {
            while (showMenu)
            {
                Console.Clear();
                try
                {
                    Console.SetWindowSize(Math.Min(120, Console.LargestWindowWidth), Math.Min(30, Console.LargestWindowHeight));
                }
                catch { }

                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("        ╔═══════════════════════════════════════════════════════════════════════╗");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("        ║                                                                       ║");
                Console.WriteLine("        ║                    RITUAL WALKER - ORBWALKER                          ║");
                Console.WriteLine("        ║                                                                       ║");
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("        ╚═══════════════════════════════════════════════════════════════════════╝");
                Console.ResetColor();
                Console.WriteLine();
                
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.WriteLine("        ╔═══════════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("        ║                      CURRENT CONFIGURATION                            ║");
                Console.WriteLine("        ╠═══════════════════════════════════════════════════════════════════════╣");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write("        ║  ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Hotkey: ");
                Console.ForegroundColor = ConsoleColor.White;
                string hotkeyText = $"{(VirtualKeyCode)CurrentSettings.ActivationKey}";
                Console.Write(hotkeyText);
                Console.ForegroundColor = ConsoleColor.Gray;
                int hotkeyPadding = 71 - 8 - hotkeyText.Length - 2;
                Console.WriteLine(new string(' ', hotkeyPadding) + "║");
                
                Console.Write("        ║  ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("Ping: ");
                Console.ForegroundColor = ConsoleColor.White;
                string pingText = $"{CurrentSettings.PingCompensation * 1000}ms";
                Console.Write(pingText);
                Console.ForegroundColor = ConsoleColor.Gray;
                int pingPadding = 71 - 6 - pingText.Length - 2;
                Console.WriteLine(new string(' ', pingPadding) + "║");
                
                Console.Write("        ║  ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Windup: ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("Auto-adjusted (15-30ms)");
                Console.ForegroundColor = ConsoleColor.Gray;
                int windupPadding = 71 - 8 - 23 - 2;
                Console.WriteLine(new string(' ', windupPadding) + "║");
                
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.WriteLine("        ╚═══════════════════════════════════════════════════════════════════════╝");
                Console.ResetColor();
                Console.WriteLine();
                
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("        ╔═══════════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("        ║                        RITUAL COMMANDS                                ║");
                Console.WriteLine("        ╠═══════════════════════════════════════════════════════════════════════╣");
                
                Console.Write("        ║  ");
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write("[1] ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Change Hotkey");
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("                                                    ║");
                
                Console.Write("        ║  ");
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.Write("[2] ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("Adjust Ping");
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("                                                      ║");
                
                Console.Write("        ║  ");
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.Write("[3] ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("BEGIN RITUAL");
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("                                                     ║");
                
                Console.Write("        ║  ");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("[4] ");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write("Exit");
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("                                                             ║");
                
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("        ╚═══════════════════════════════════════════════════════════════════════╝");
                Console.ResetColor();
                Console.WriteLine();
                
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.Write("        >> ");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("Enter your choice: ");
                Console.ForegroundColor = ConsoleColor.White;

                var choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        ChangeHotkey();
                        break;
                    case "2":
                        ChangePing();
                        break;
                    case "3":
                        StartOrbWalker();
                        showMenu = false;
                        break;
                    case "4":
                        Environment.Exit(0);
                        break;
                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("\n        >> Invalid choice! Press any key to continue...");
                        Console.ResetColor();
                        Console.ReadKey(true);
                        break;
                }
            }
        }

        private static void ChangeHotkey()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine("=== CHANGE HOTKEY ===");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Press any key for new hotkey...");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("(Recommended: C, X, V, Space)");
            Console.ResetColor();
            Console.WriteLine();
            
            var keyPressed = false;
            VirtualKeyCode newKey = VirtualKeyCode.C;

            void TempKeyHandler(VirtualKeyCode key, KeyState state)
            {
                if (state == KeyState.Down && !keyPressed)
                {
                    newKey = key;
                    keyPressed = true;
                }
            }

            InputManager.OnKeyboardEvent += TempKeyHandler;
            
            while (!keyPressed)
            {
                System.Threading.Thread.Sleep(100);
            }

            InputManager.OnKeyboardEvent -= TempKeyHandler;

            CurrentSettings.ActivationKey = (int)newKey;
            CurrentSettings.Save(SettingsFile);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n>> Hotkey set to '{newKey}'!");
            Console.ResetColor();
            System.Threading.Thread.Sleep(1500);
        }

        private static void ChangePing()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine("=== ADJUST PING ===");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Enter your ping in milliseconds:");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Example: 30 (for 30ms ping)");
            Console.WriteLine("Recommended: Your in-game ping value");
            Console.ResetColor();
            Console.Write("\n>> Ping (ms): ");
            
            if (double.TryParse(Console.ReadLine(), out double pingMs) && pingMs >= 0 && pingMs <= 500)
            {
                CurrentSettings.PingCompensation = pingMs / 1000.0;
                CurrentSettings.Save(SettingsFile);
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n>> Ping set to {pingMs}ms!");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n>> Invalid value! Please enter a number between 0-500");
                Console.ResetColor();
            }
            System.Threading.Thread.Sleep(1500);
        }

        private static void StartOrbWalker()
        {
            Console.Clear();
            Console.CursorVisible = false;
            
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine("        ╔═══════════════════════════════════════════════════════════════════════╗");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("        ║                                                                       ║");
            Console.WriteLine("        ║                    RITUAL WALKER INITIATED                            ║");
            Console.WriteLine("        ║                                                                       ║");
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine("        ╚═══════════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
            
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine("        ╔═══════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("        ║                          SYSTEM STATUS                                ║");
            Console.WriteLine("        ╠═══════════════════════════════════════════════════════════════════════╣");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write("        ║  ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Hotkey: ");
            Console.ForegroundColor = ConsoleColor.White;
            string hotkeyText = $"{(VirtualKeyCode)CurrentSettings.ActivationKey}";
            Console.Write(hotkeyText);
            Console.ForegroundColor = ConsoleColor.Gray;
            int hotkeyPadding = 71 - 8 - hotkeyText.Length - 2;
            Console.WriteLine(new string(' ', hotkeyPadding) + "║");
            
            Console.Write("        ║  ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("Ping: ");
            Console.ForegroundColor = ConsoleColor.White;
            string pingText = $"{CurrentSettings.PingCompensation * 1000}ms";
            Console.Write(pingText);
            Console.ForegroundColor = ConsoleColor.Gray;
            int pingPadding = 71 - 6 - pingText.Length - 2;
            Console.WriteLine(new string(' ', pingPadding) + "║");
            
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine("        ╚═══════════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
            
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"        >> Hold '{(VirtualKeyCode)CurrentSettings.ActivationKey}' to activate orbwalker");
            Console.ResetColor();
            Console.WriteLine();
            
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("        >> Searching for League of Legends process...");
            Console.ResetColor();
            Console.WriteLine();

            CheckLeagueProcess();
            
            Console.SetCursorPosition(0, 16);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("        >> League process found!                                                  ");
            Console.ResetColor();
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("        >> Connecting to League API...");
            Console.ResetColor();

            attackSpeedCacheTimer.Start();

            Console.ReadLine();
        }

#if DEBUG
        private static void Timer_CallbackLog(object sender, ElapsedEventArgs e)
        {
            if (TimerCallbackCounter > 1 || TimerCallbackCounter < 0)
            {
                Console.Clear();
                Console.WriteLine("Timer Error Detected");
                throw new Exception("Timers must not run simultaneously");
            }
        }
#endif

        private static void InputManager_OnMouseEvent(VirtualKeyCode key, KeyState state, int x, int y)
        {
        }

        private static void InputManager_OnKeyboardEvent(VirtualKeyCode key, KeyState state)
        {
            if (key == (VirtualKeyCode)CurrentSettings.ActivationKey)
            {
                switch (state)
                {
                    case KeyState.Down when !OrbWalkerTimerActive:
                        OrbWalkerTimerActive = true;
                        OrbWalkTimer.Start();
                        break;

                    case KeyState.Up when OrbWalkerTimerActive:
                        OrbWalkerTimerActive = false;
                        OrbWalkTimer.Stop();
                        break;
                }
            }
        }

        private static DateTime nextInput = DateTime.MinValue;
        private static DateTime nextMove = DateTime.MinValue;
        private static DateTime nextAttack = DateTime.MinValue;

        private static readonly Stopwatch owStopWatch = new Stopwatch();

        private static void OrbWalkTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
#if DEBUG
            owStopWatch.Start();
            TimerCallbackCounter++;
#endif
            if (!HasProcess || IsExiting || GetForegroundWindow() != LeagueProcess.MainWindowHandle)
            {
#if DEBUG
                TimerCallbackCounter--;
#endif
                return;
            }

            var time = e.SignalTime;

            if (nextInput < time)
            {
                if (nextAttack < time)
                {
                    nextInput = time.AddSeconds(MinInputDelay);

                    InputSimulator.Keyboard.KeyDown((ushort)DirectInputKeys.DIK_A);
                    InputSimulator.Mouse.MouseClick(InputSimulator.Mouse.Buttons.Left);
                    InputSimulator.Keyboard.KeyUp((ushort)DirectInputKeys.DIK_A);

                    var attackTime = DateTime.Now;

                    nextMove = attackTime.AddSeconds(GetBufferedWindupDuration());
                    nextAttack = attackTime.AddSeconds(GetSecondsPerAttack());
                }
                else if (nextMove < time)
                {
                    nextInput = time.AddSeconds(MinInputDelay);
                    InputSimulator.Mouse.MouseClick(InputSimulator.Mouse.Buttons.Right);
                }
            }
#if DEBUG
            TimerCallbackCounter--;
            owStopWatch.Reset();
#endif
        }

        private static void CheckLeagueProcess()
        {
            while (LeagueProcess is null || !HasProcess)
            {
                LeagueProcess = Process.GetProcessesByName("League of Legends").FirstOrDefault();
                if (LeagueProcess is null || LeagueProcess.HasExited)
                {
                    continue;
                }
                HasProcess = true;
                LeagueProcess.EnableRaisingEvents = true;
                LeagueProcess.Exited += LeagueProcess_Exited;
            }
        }

        private static void LeagueProcess_Exited(object sender, EventArgs e)
        {
            HasProcess = false;
            LeagueProcess = null;
            Console.WriteLine("League Process Exited");
            CheckLeagueProcess();
        }

        private static void AttackSpeedCacheTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (HasProcess && !IsExiting && !isInitializing && !isUpdating)
            {
                isUpdating = true;

                JToken activePlayerToken = null;
                try
                {
                    activePlayerToken = JToken.Parse(Client.DownloadString(ActivePlayerEndpoint));
                }
                catch
                {
                    try
                    {
                        Console.SetCursorPosition(0, 16);
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("        >> Cannot connect to League API                                           ");
                        Console.WriteLine("        >> Make sure you are in-game!                                             ");
                        Console.ResetColor();
                    }
                    catch { }
                    isUpdating = false;
                    return;
                }

                if (string.IsNullOrEmpty(ChampionName))
                {
                    try
                    {
                        ActivePlayerName = activePlayerToken?["summonerName"].ToString();
                        isInitializing = true;
                        JToken playerListToken = JToken.Parse(Client.DownloadString(PlayerListEndpoint));
                        foreach (JToken token in playerListToken)
                        {
                            if (token["summonerName"].ToString().Equals(ActivePlayerName))
                            {
                                ChampionName = token["championName"].ToString();
                                string[] rawNameArray = token["rawChampionName"].ToString().Split('_', StringSplitOptions.RemoveEmptyEntries);
                                RawChampionName = rawNameArray[^1];
                            }
                        }

                        if (!GetChampionBaseValues(RawChampionName))
                        {
                            Console.SetCursorPosition(0, 16);
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"        >> Cannot get champion stats for {RawChampionName}                       ");
                            Console.ResetColor();
                            isInitializing = false;
                            isUpdating = false;
                            return;
                        }

                        Console.SetCursorPosition(0, 16);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"        >> Connected! Champion: {ChampionName}                                    ");
                        Console.ResetColor();

                        isInitializing = false;
                    }
                    catch
                    {
                        try
                        {
                            Console.SetCursorPosition(0, 16);
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"        >> Initializing... (waiting for game data)                               ");
                            Console.ResetColor();
                        }
                        catch { }
                        isInitializing = false;
                        isUpdating = false;
                        return;
                    }
                }

                try
                {
                    double newAttackSpeed = activePlayerToken["championStats"]["attackSpeed"].Value<double>();
                    if (Math.Abs(newAttackSpeed - ClientAttackSpeed) > 0.01)
                    {
                        ClientAttackSpeed = newAttackSpeed;
                        
                        Console.SetCursorPosition(0, 19);
                        Console.ForegroundColor = ConsoleColor.DarkMagenta;
                        Console.WriteLine("        ╔═══════════════════════════════════════════════════════════════════════╗");
                        Console.WriteLine("        ║                        LIVE STATISTICS                                ║");
                        Console.WriteLine("        ╠═══════════════════════════════════════════════════════════════════════╣");
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.Write("        ║  ");
                        Console.ForegroundColor = ConsoleColor.Green;
                        string asText = $"Attack Speed: {ClientAttackSpeed:0.00}";
                        Console.Write(asText);
                        Console.ForegroundColor = ConsoleColor.Gray;
                        int asPadding = 71 - asText.Length - 2;
                        Console.WriteLine(new string(' ', asPadding) + "║");
                        
                        Console.Write("        ║  ");
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        string windupText = $"Windup: {GetWindupDuration() * 1000:0.0}ms";
                        Console.Write(windupText);
                        Console.ForegroundColor = ConsoleColor.Gray;
                        int windupPadding = 71 - windupText.Length - 2;
                        Console.WriteLine(new string(' ', windupPadding) + "║");
                        
                        Console.Write("        ║  ");
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        string intervalText = $"Attack Interval: {GetSecondsPerAttack() * 1000:0.0}ms";
                        Console.Write(intervalText);
                        Console.ForegroundColor = ConsoleColor.Gray;
                        int intervalPadding = 71 - intervalText.Length - 2;
                        Console.WriteLine(new string(' ', intervalPadding) + "║");
                        
                        Console.Write("        ║  ");
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        string pingCompText = $"Ping Compensation: {CurrentSettings.PingCompensation * 1000:0.0}ms";
                        Console.Write(pingCompText);
                        Console.ForegroundColor = ConsoleColor.Gray;
                        int pingCompPadding = 71 - pingCompText.Length - 2;
                        Console.WriteLine(new string(' ', pingCompPadding) + "║");
                        
                        Console.ForegroundColor = ConsoleColor.DarkMagenta;
                        Console.WriteLine("        ╚═══════════════════════════════════════════════════════════════════════╝");
                        Console.ResetColor();
                    }
                }
                catch
                {
                    Console.SetCursorPosition(0, 19);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"        >> Error reading attack speed                                             ");
                    Console.ResetColor();
                }

                isUpdating = false;
            }
        }

        private static bool GetChampionBaseValues(string championName)
        {
            string lowerChampionName = championName.ToLower();
            JToken championBinToken = null;
            try
            {
                championBinToken = JToken.Parse(Client.DownloadString($"{ChampionStatsEndpoint}{lowerChampionName}/{lowerChampionName}.bin.json"));
            }
            catch
            {
                return false;
            }
            JToken championRootStats = championBinToken[$"Characters/{championName}/CharacterRecords/Root"];
            ChampionAttackSpeedRatio = championRootStats["attackSpeedRatio"].Value<double>();

            JToken championBasicAttackInfoToken = championRootStats["basicAttack"];
            JToken championAttackDelayOffsetToken = championBasicAttackInfoToken["mAttackDelayCastOffsetPercent"];
            JToken championAttackDelayOffsetSpeedRatioToken = championBasicAttackInfoToken["mAttackDelayCastOffsetPercentAttackSpeedRatio"];

            if (championAttackDelayOffsetSpeedRatioToken?.Value<double?>() != null)
            {
                ChampionAttackDelayScaling = championAttackDelayOffsetSpeedRatioToken.Value<double>();
            }

            if (championAttackDelayOffsetToken?.Value<double?>() == null)
            {
                JToken attackTotalTimeToken = championBasicAttackInfoToken["mAttackTotalTime"];
                JToken attackCastTimeToken = championBasicAttackInfoToken["mAttackCastTime"];

                if (attackTotalTimeToken?.Value<double?>() == null && attackCastTimeToken?.Value<double?>() == null)
                {
                    string attackName = championBasicAttackInfoToken["mAttackName"].ToString();
                    string attackSpell = $"Characters/{attackName.Split(new[] { "BasicAttack" }, StringSplitOptions.RemoveEmptyEntries)[0]}/Spells/{attackName}";
                    ChampionAttackDelayPercent += championBinToken[attackSpell]["mSpell"]["delayCastOffsetPercent"].Value<double>();
                }
                else
                {
                    ChampionAttackTotalTime = attackTotalTimeToken.Value<double>();
                    ChampionAttackCastTime = attackCastTimeToken.Value<double>();

                    ChampionAttackDelayPercent = ChampionAttackCastTime / ChampionAttackTotalTime;
                }
            }
            else
            {
                ChampionAttackDelayPercent += championAttackDelayOffsetToken.Value<double>();
            }

            return true;
        }
    }
}
