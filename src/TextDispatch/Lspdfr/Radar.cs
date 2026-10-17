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
    /// coming or going - and /radar on keeps the front antenna watching and speaks when something comes
    /// through over the alert speed. A lock is what a speeding ticket rests on: /cite speeding takes the
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

        public bool On;
        public int AlertMph = 75;

        /// <summary>The last reading locked, by hand or by an alert.</summary>
        public Reading Locked;

        public Radar(ChatBox chat)
        {
            _chat = chat;
        }

        /// <summary>Called on the plugin's fiber. While on, the front antenna watches twice a second.</summary>
        public void Update()
        {
            if (!On || Environment.TickCount - _nextScanAt < 0) return;
            _nextScanAt = Environment.TickCount + 500;

            try
            {
                Reading front, rear;
                int patrol;
                if (!Scan(out front, out rear, out patrol) || front == null || front.Mph < AlertMph) return;

                var key = front.Vehicle.Handle.Value;
                int last;
                if (_alerted.TryGetValue(key, out last) && Environment.TickCount - last < 30000) return;
                _alerted[key] = Environment.TickCount;
                if (_alerted.Count > 64) _alerted.Clear();

                // Over the alert speed is locked by itself, the way a radar with fast-lock does.
                Locked = front;
                _chat.Write(ChatChannel.Radio, "[RADAR]", "*beep* " + front.Describe() + " - LOCKED. /stop pulls it over, /cite speeding uses the lock.");
                Log.Line("radar: alert " + front.Describe());
            }
            catch (Exception ex)
            {
                On = false;
                Log.Error("radar (switched off)", ex);
                _chat.Error("The radar faulted and switched itself off - the log has why.");
            }
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

            foreach (var vehicle in World.GetAllVehicles())
            {
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
