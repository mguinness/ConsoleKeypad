using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleKeypad
{
    class Program
    {
        [Flags]
        enum Indicator
        {
            AlarmActive = 1, //ALARM (System is in Alarm)
            AlarmCancel = 2, //ALARM IN MEMORY
            ArmedAway = 4, //ARMED AWAY
            ACPower = 8, //AC PRESENT
            ZonesBypass = 16, //BYPASS (Zones are bypassed)
            ChimeEnabled = 32, //CHIME
            ProgramMode = 64, //Not Used
            ArmedInstant = 128, //ARMED (ZERO ENTRY DELAY)
            FireActive = 256, //ALARM (FIRE ZONE)
            SystemTrouble = 512, //CHECK ICON – SYSTEM TROUBLE
            //Unknown1 = 1024, //Not Used
            //Unknown2 = 2048, //Not Used
            SystemReady = 4096, //READY
            FireCancel = 8192, //FIRE
            LowBattery = 16384, //LOW BATTERY
            ArmedStay = 32768 //ARMED STAY
        }

        static TelnetClient _telnetClient;

        static async Task Main(string[] args)
        {
            Console.Title = "Ademco Virtual Keypad";

            if (args.Length == 0)
            {
                Console.WriteLine("Usage: ConsoleKeypad <pwd> [host]");
                return;
            }

            _telnetClient = new TelnetClient(args.Length > 1 ? args[1] : "envisalink", 4025, TimeSpan.Zero, CancellationToken.None);
            _telnetClient.MessageReceived += new EventHandler<string>(HandleMessage);
            _telnetClient.ConnectionClosed += new EventHandler((sender, e) => {
                Console.WriteLine("\nConnection closed");
                Environment.Exit(0);
            });

            try
            {
                Console.Write("Waiting to connect...");
                await _telnetClient.Connect();
                await _telnetClient.Send(args[0]);
                Thread.Sleep(250); //Wait for disconnect
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
            }

            if (_telnetClient.IsConnected)
                Console.WriteLine("\rConnected.  Enter a number, *, # or q to quit.");
            else
                return;

            char key;
            while ((key = Console.ReadKey(true).KeyChar) != 'q')
            {
                if (_telnetClient.IsConnected && (Char.IsNumber(key) || key == '#' || key == '*'))
                {
                    if (OperatingSystem.IsWindows())
                        Console.Beep(1600, 200);
                    await _telnetClient.Send(key.ToString());
                }
            }

            _telnetClient.Disconnect();
        }

        static void HandleMessage(object sender, string e)
        {
            if (e == "FAILED")
                Console.Write("\nIncorrect password");
            else if (e.StartsWith("%"))
            {
                var items = e.TrimStart('%').TrimEnd('$').Split(',');
                if (items[0] == "00")
                {
                    //var part = Int32.Parse(items[1], NumberStyles.HexNumber);
                    var icon = Int32.Parse(items[2], NumberStyles.HexNumber);
                    //var zone = Int32.Parse(items[3], NumberStyles.HexNumber);
                    var beep = Int32.Parse(items[4], NumberStyles.HexNumber);
                    var text = items[5];

                    for (int i = 0; i < beep; i++)
                    {
                        if (OperatingSystem.IsWindows())
                            Console.Beep(3200, 200);
                        else
                            Console.Beep();
                    }

                    var msg = String.Format("{0} | {1}", text.Substring(0, 16), text.Substring(16));
                    var lbls = GetLabels((Indicator)icon);

                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write(msg.PadRight(Console.WindowWidth - String.Join(" ", lbls.Keys).Length - 2));

                    foreach (var item in lbls)
                    {
                        Console.ForegroundColor = item.Value;
                        Console.Write(" " + item.Key);
                    }

                    Console.ResetColor();
                    Console.Write("\r");
                }
            }
        }

        static Dictionary<string, ConsoleColor> GetLabels(Indicator flags)
        {
            var lbls = new Dictionary<string, ConsoleColor>();

            if (flags.HasFlag(Indicator.AlarmActive))
                lbls.Add("ALARM", ConsoleColor.Red);
            else if (flags.HasFlag(Indicator.AlarmCancel))
                lbls.Add("ALARM", ConsoleColor.Yellow);

            if (flags.HasFlag(Indicator.ACPower))
                lbls.Add("AC", ConsoleColor.Green);
            else if (!flags.HasFlag(Indicator.ProgramMode))
                lbls.Add("AC", ConsoleColor.Red);

            if (flags.HasFlag(Indicator.ChimeEnabled))
                lbls.Add("CHIME", ConsoleColor.Green);

            if (flags.HasFlag(Indicator.ProgramMode))
                lbls.Add("PRGRM", ConsoleColor.Yellow);

            if (flags.HasFlag(Indicator.FireActive))
                lbls.Add("FIRE", ConsoleColor.Red);
            else if (flags.HasFlag(Indicator.FireCancel))
                lbls.Add("FIRE", ConsoleColor.Yellow);

            if (flags.HasFlag(Indicator.SystemTrouble))
                lbls.Add("TRBL", ConsoleColor.Yellow);

            if (flags.HasFlag(Indicator.SystemReady))
                lbls.Add("READY", ConsoleColor.Green);

            if ((flags & (Indicator.ArmedAway | Indicator.ArmedInstant | Indicator.ArmedStay)) != 0 )
                lbls.Add("ARMED", ConsoleColor.Red);

            return lbls;
        }
    }
}
