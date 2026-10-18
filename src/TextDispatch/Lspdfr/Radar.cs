using System;
using System.Collections.Generic;
using Rage;
using TextDispatch.Chat;

namespace TextDispatch.Lspdfr
{
    /// <summary>
    /// A traffic radar in text: the front and rear antennas, the patrol speed, a lock, and an alert.
    ///
    /// A radar on a dashboard is something to look at; in a chat box it is something that says. /radar
    /// reads both antennas once - the strongest target in each beam, its speed, its plate and whether it is
    /// coming or going - and /radar on keeps the antennas watching (both, or just the front or the rear)
    /// and speaks when something comes through either beam over the alert speed. A lock is what a speeding ticket rests on: /cite speeding takes the
    /// locked speed onto the citation and the report.
    /// </summary>
    internal sealed class Radar
    {
        public sealed class Reading
        {
            public Vehicle Vehicle;
            public string Plate;
            public string Model;
            public int Mph;
            public bool Front;
            public bool Closing;     // coming towards the patrol car
            public float Metres;
            public DateTime At;

            public string Describe()
            {
                return (Front ? "FRONT" : "REAR") + " " + Mph + " mph - " + Model + " " + Plate + ", " +
                       Metres.ToString("0") + "m, " + (Closing ? "closing" : "going away");
            }
        }

        private const float Range = 140f;
        private const float Beam = 0.966f;   // cos 15 degrees: the antenna's cone

        private readonly ChatBox _chat;
        private readonly Dictionary<uint, int> _alerted = new Dictionary<uint, int>();
        private int _nextScanAt;

        /// <summary>Which antennas are watching. Both off is the radar off.</summary>
        public bool FrontOn, RearOn;
        public bool On { get { return FrontOn || RearOn; } }
        public int AlertMph = 75;

        /// <summary>"front and rear", "front", "rear" - for saying what is watching.</summary>
        public string Watching
        {
            get { return FrontOn && RearOn ? "front and rear" : FrontOn ? "front" : RearOn ? "rear" : "off"; }
        }

        /// <summary>The last reading locked, by hand or by an alert.</summary>
        public Reading Locked;

        /// <summary>Whether the box's RADAR tab is showing: then it reads both antennas live, on or off.</summary>
        public Func<bool> Watched;

        // The latest scan, kept for the RADAR tab - drawn in the render callback, which may not scan itself.
        private readonly object _gate = new object();
        private Reading _front, _rear;
        private int _patrol;
        private bool _inCar;
        private readonly List<string> _history = new List<string>();

        /// <summary>
        /// The RADAR tab: the head unit as lines - patrol speed, both antennas, the lock, what is watching and
        /// the alerts so far. From the last scan, so it is safe to call while drawing.
        /// </summary>
        public List<ChatLine> Screen(int rows)
        {
            Reading front, rear, locked;
            int patrol;
            bool inCar;
            List<string> history;
            lock (_gate)
            {
                front = _front; rear = _rear; patrol = _patrol; inCar = _inCar;
                history = new List<string>(_history);
            }
            locked = Locked;

            var lines = new List<ChatLine>();
            if (!inCar)
            {
                lines.Add(new ChatLine { Channel = ChatChannel.Error, Text = "No patrol car - the radar is in the dash. Get in a car." });
            }
            else
            {
                lines.Add(new ChatLine { Channel = ChatChannel.Notice, Text = "PATROL  " + patrol.ToString().PadLeft(3) + " mph" });
                lines.Add(Antenna("FRONT", front));
                lines.Add(Antenna("REAR ", rear));
            }
            lines.Add(new ChatLine
            {
                Channel = locked != null ? ChatChannel.Error : ChatChannel.Do,
                Text = locked != null ? "LOCK    " + locked.Mph + " mph  " + locked.Model + " " + locked.Plate + "  (" + (locked.Front ? "front" : "rear") + ", " + locked.At.ToString("HH:mm:ss") + ")"
                                      : "LOCK    none - /radar lock [front|rear]"
            });
            lines.Add(new ChatLine { Channel = ChatChannel.Do, Text = "WATCH   " + Watching + ", alert at " + AlertMph + " mph - /radar on|off [front|rear], /radar alert <mph>" });
            lines.Add(new ChatLine { Channel = ChatChannel.Dispatch, Text = history.Count == 0 ? "No alerts yet this session." : "Alerts:" });
            var room = Math.Max(0, rows - lines.Count);
            for (var i = Math.Max(0, history.Count - room); i < history.Count; i++)
                lines.Add(new ChatLine { Channel = ChatChannel.Radio, Text = "  " + history[i] });
            return lines;
        }

        private ChatLine Antenna(string name, Reading reading)
        {
            if (reading == null) return new ChatLine { Channel = ChatChannel.Local, Text = name + "   --- no target" };
            var fast = reading.Mph >= AlertMph;
            return new ChatLine
            {
                Channel = fast ? ChatChannel.Error : ChatChannel.Local,
                Text = name + "  " + reading.Mph.ToString().PadLeft(3) + " mph  " + reading.Model + " " + reading.Plate + ", " +
                       reading.Metres.ToString("0") + "m, " + (reading.Closing ? "closing" : "going away") + (fast ? "  OVER" : "")
            };
        }

        public Radar(ChatBox chat)
        {
            _chat = chat;
        }

        /// <summary>Called on the plugin's fiber. While on, or while the RADAR tab is showing, it reads twice a second.</summary>
        public void Update()
        {
            var watched = Watched != null && Watched();
            if ((!On && !watched) || Environment.TickCount - _nextScanAt < 0) return;
            _nextScanAt = Environment.TickCount + (watched ? 300 : 500);

            try
            {
                Reading front, rear;
                int patrol;
                var inCar = Scan(out front, out rear, out patrol);
                lock (_gate) { _front = front; _rear = rear; _patrol = patrol; _inCar = inCar; }
                if (!inCar) return;
                if (FrontOn) Alert(front);
                if (RearOn) Alert(rear);
            }
            catch (Exception ex)
            {
                FrontOn = RearOn = false;
                Log.Error("radar (switched off)", ex);
                _chat.Error("The radar faulted and switched itself off - the log has why.");
            }
        }

        /// <summary>Speak, and fast-lock, when a target in a watching beam is over the alert speed - once a car per 30s.</summary>
        private void Alert(Reading reading)
        {
            if (reading == null || reading.Mph < AlertMph) return;

            var key = reading.Vehicle.Handle.Value;
            int last;
            if (_alerted.TryGetValue(key, out last) && Environment.TickCount - last < 30000) return;
            _alerted[key] = Environment.TickCount;
            if (_alerted.Count > 64) _alerted.Clear();

            // Over the alert speed is locked by itself, the way a radar with fast-lock does.
            Locked = reading;
            lock (_gate)
            {
                _history.Add(reading.At.ToString("HH:mm:ss") + "  " + reading.Describe());
                if (_history.Count > 30) _history.RemoveAt(0);
            }
            _chat.Write(ChatChannel.Radio, "[RADAR]", "*beep* " + reading.Describe() + " - LOCKED. /stop pulls it over, /cite speeding uses the lock.");
            Log.Line("radar: alert " + reading.Describe());
        }

        /// <summary>Both antennas, once. False when the player is not in a vehicle.</summary>
        public bool Scan(out Reading front, out Reading rear, out int patrolMph)
        {
            front = null;
            rear = null;
            patrolMph = 0;

            var player = Game.LocalPlayer.Character;
            if (player == null || !player.Exists() || !player.IsInAnyVehicle(false)) return false;
            var car = player.CurrentVehicle;
            if (car == null || !car.Exists()) return false;

            patrolMph = Mph(car.Speed);
            var origin = car.Position;
            var forward = car.ForwardVector;

            // Only what is within the antennas' range, not every car in the world: with /radar on this runs
            // twice a second for as long as the shift lasts.
            foreach (var entity in World.GetEntities(origin, Range, GetEntitiesFlags.ConsiderAllVehicles | GetEntitiesFlags.ExcludePlayerVehicle))
            {
                var vehicle = entity as Vehicle;
                if (vehicle == null || !vehicle.Exists() || vehicle == car) continue;
                var offset = vehicle.Position - origin;
                var metres = offset.Length();
                if (metres < 4f || metres > Range) continue;

                var direction = offset / metres;
                var along = Vector3.Dot(direction, forward);
                bool inFront = along >= Beam, behind = along <= -Beam;
                if (!inFront && !behind) continue;

                var mph = Mph(vehicle.Speed);
                if (mph < 3) continue;

                // Coming towards the patrol car when its movement points back along the line between them.
                var closing = Vector3.Dot(vehicle.Velocity, -direction) > 0f;
                var reading = new Reading
                {
                    Vehicle = vehicle,
                    Plate = Safe(() => vehicle.LicensePlate, "no plate"),
                    Model = Safe(() => vehicle.Model.Name, "vehicle"),
                    Mph = mph,
                    Front = inFront,
                    Closing = closing,
                    Metres = metres,
                    At = DateTime.Now
                };

                // The strongest return in each beam is the nearest; the fastest is reported if it is close.
                var current = inFront ? front : rear;
                if (current == null || metres < current.Metres || (mph > current.Mph + 10 && metres < current.Metres + 40f))
                {
                    if (inFront) front = reading; else rear = reading;
                }
            }
            return true;
        }

        private static int Mph(float metresPerSecond)
        {
            return (int)Math.Round(metresPerSecond * 2.23694f);
        }

        private static string Safe(Func<string> read, string fallback)
        {
            try { var value = read(); return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim(); }
            catch { return fallback; }
        }
    }
}
