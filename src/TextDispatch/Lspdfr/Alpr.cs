using System;
using System.Collections.Generic;
using Rage;
using TextDispatch.Chat;
using TextDispatch.Records;

namespace TextDispatch.Lspdfr
{
    /// <summary>
    /// The automatic plate reader: while you drive, the plates of the cars around you are read and checked
    /// against TextDispatch's own records - stolen, uninsured, an expired registration, a wanted owner or a
    /// suspended licence, a BOLO, a flag - and a hit is called out on the RADAR tab.
    ///
    /// Checked against the same records /plate and /checks read, so a hit here is what the stop will find.
    /// Every car is read; only a hit is put on file, so the records do not fill with every car passed.
    /// </summary>
    internal sealed class Alpr
    {
        public sealed class Hit
        {
            public string Plate;
            public string Model;
            public string What;
            public bool Urgent;
            public DateTime At;
            public float Metres;
        }

        private const float Range = 45f;

        private readonly ChatBox _chat;
        private readonly RecordsLedger _records;
        private readonly LspdfrApi _api;
        private readonly Dictionary<string, int> _read = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly object _gate = new object();
        private readonly List<Hit> _hits = new List<Hit>();
        private int _nextScanAt;
        private int _lastCallout = Environment.TickCount - 60000;
        private bool _failed;

        public bool On = true;
        public int PlatesRead;

        public Alpr(ChatBox chat, RecordsLedger records, LspdfrApi api)
        {
            _chat = chat;
            _records = records;
            _api = api;
        }

        public List<Hit> Hits
        {
            get { lock (_gate) return new List<Hit>(_hits); }
        }

        /// <summary>Called on the plugin's fiber: a sweep a second while in a car.</summary>
        public void Update()
        {
            if (!On || _failed || Environment.TickCount - _nextScanAt < 0) return;
            _nextScanAt = Environment.TickCount + 1000;

            try
            {
                var player = Game.LocalPlayer.Character;
                if (player == null || !player.Exists() || !player.IsInAnyVehicle(false)) return;
                var car = player.CurrentVehicle;
                var origin = car.Position;

                foreach (var entity in World.GetEntities(origin, Range, GetEntitiesFlags.ConsiderAllVehicles | GetEntitiesFlags.ExcludePlayerVehicle))
                {
                    var vehicle = entity as Vehicle;
                    if (vehicle == null || !vehicle.Exists() || vehicle == car) continue;

                    string plate = null;
                    try { plate = vehicle.LicensePlate; } catch { }
                    plate = RecordsLedger.PlateKey(plate);
                    if (plate.Length == 0) continue;

                    // Each plate once in two minutes: a car sat beside you at the lights is not a new read.
                    int last;
                    if (_read.TryGetValue(plate, out last) && Environment.TickCount - last < 120000) continue;
                    _read[plate] = Environment.TickCount;
                    if (_read.Count > 400) _read.Clear();

                    // Police cars are not read.
                    try { var driver = vehicle.Driver; if (driver != null && driver.Exists() && _api.PedIsCop(driver)) continue; } catch { }

                    PlatesRead++;
                    string model = null;
                    try { model = vehicle.Model.Name; } catch { }
                    Check(plate, model, origin.DistanceTo(vehicle.Position));
                }
            }
            catch (Exception ex)
            {
                _failed = true;
                Log.Error("plate reader (stopped)", ex);
                _chat.Error("The plate reader faulted and stopped - the log has why.");
            }
        }

        private void Check(string plate, string model, float metres)
        {
            var record = _records.PeekVehicle(plate, model);
            if (record == null) return;

            var flags = new List<string>();
            var urgent = false;
            if (record.ReportedStolen) { flags.Add("STOLEN"); urgent = true; }

            foreach (var bolo in _records.Bolos)
                if (RecordsLedger.PlateKey(bolo.Subject) == plate) { flags.Add("BOLO: " + bolo.Reason); urgent = true; }
            foreach (var flag in _records.FlagsOn(plate))
                if (flag.Kind == "flag") { flags.Add("FLAG: " + flag.Text); urgent = true; }

            var owner = _records.FindPerson(record.OwnerName);
            if (owner != null && owner.Wanted) { flags.Add("owner " + owner.Name + " WANTED: " + owner.WarrantFor); urgent = true; }
            if (owner != null && owner.Licence == "suspended") flags.Add("owner's licence SUSPENDED");

            if (!record.Insured) flags.Add("no insurance");
            var registration = RecordsLedger.RegistrationStatus(record);
            if (registration == "EXPIRED" || registration == "NOT REGISTERED") flags.Add("registration " + registration.ToLowerInvariant());

            if (flags.Count == 0) return;

            // A serious hit is filed, so the plate - and its owner - come back the same on /plate and on the stop.
            // A minor one is not: insurance and registration come out the same from the plate either way, and
            // filing every uninsured car passed would fill the records.
            if (urgent) _records.EnsureVehicle(plate, model);

            var hit = new Hit { Plate = plate, Model = record.Model, What = string.Join("; ", flags.ToArray()), Urgent = urgent, At = DateTime.Now, Metres = metres };
            lock (_gate)
            {
                _hits.Add(hit);
                if (_hits.Count > 30) _hits.RemoveAt(0);
            }

            Log.Line("alpr: " + plate + " " + hit.What);

            // Busy traffic reads a plate every few seconds, and a lot of cars are uninsured. Only what matters is
            // said - stolen, a BOLO, a flag, a wanted owner - and not more than once in 45 seconds; everything is
            // on the RADAR tab and in /alpr.
            if (!urgent || Environment.TickCount - _lastCallout < 45000) return;
            _lastCallout = Environment.TickCount;
            _chat.Write(ChatChannel.Radio, "[ALPR]", "*ding* " + plate + " " + record.Model + ", " + metres.ToString("0") + " m - " + hit.What +
                                                      ". /plate " + plate + ", or light it up.");
        }
    }
}
