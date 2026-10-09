using System;
using Rage;
using TextDispatch.Dialogue;
using TextDispatch.Records;

namespace TextDispatch.Lspdfr
{
    /// <summary>
    /// A call that comes out of the records rather than out of a callout pack: somebody the player has
    /// already dealt with has a warrant now - they skipped a citation, or they were wanted when they were
    /// met - and dispatch sends the player to pick them up.
    ///
    /// It is not an LSPDFR callout, because LSPDFR's callouts are types registered at duty time and this
    /// one is a particular person decided at the moment it is raised. So it runs here: one person spawned
    /// near a street, a blip over the area, their record bound to them so the box and the terminal both
    /// know exactly who they are, and an end that is the arrest - however the player makes it.
    /// </summary>
    internal sealed class WarrantService
    {
        private static readonly string[] Models =
        {
            "a_m_y_downtown_01", "a_m_m_skater_01", "a_m_y_stwhi_01", "a_m_m_eastsa_01", "a_m_y_mexthug_01",
            "a_m_m_salton_01", "a_f_y_hipster_02", "a_f_m_downtown_01", "a_f_y_eastsa_01", "a_m_y_business_01"
        };

        private const int TimeoutMs = 20 * 60 * 1000;
        private const float ApproachMetres = 25f;

        private readonly LspdfrApi _api;
        private readonly RecordsLedger _records;
        private readonly PersonRecord _person;
        private readonly string _warrantFor;
        private readonly Random _rng = new Random();

        private Ped _ped;
        private Blip _blip;
        private Vector3 _where;
        private int _startedAt;
        private bool _approached;
        private bool _fled;

        public string Name { get; private set; }
        public string Location { get; private set; }
        public PersonRecord Person { get { return _person; } }
        public bool Active { get; private set; }

        public WarrantService(LspdfrApi api, RecordsLedger records, PersonRecord person)
        {
            _api = api;
            _records = records;
            _person = person;
            _warrantFor = person.WarrantFor ?? "an outstanding warrant";
            Name = "Warrant Service";
        }

        /// <summary>Put them somewhere a few streets away. False if the game would not have them.</summary>
        public bool Start()
        {
            try
            {
                var player = Game.LocalPlayer.Character;
                var angle = _rng.NextDouble() * Math.PI * 2;
                var distance = 250 + _rng.NextDouble() * 300;
                var around = new Vector3(player.Position.X + (float)(Math.Cos(angle) * distance),
                                         player.Position.Y + (float)(Math.Sin(angle) * distance),
                                         player.Position.Z);
                _where = World.GetNextPositionOnStreet(around);

                // Model is a struct in RPH, so "none found" is a name left null rather than a null model.
                string chosen = null;
                var start = _rng.Next(Models.Length);
                for (var i = 0; i < Models.Length && chosen == null; i++)
                {
                    var name = Models[(start + i) % Models.Length];
                    if (new Model(name).IsValid) chosen = name;
                }
                if (chosen == null) { Log.Line("warrant service: no ped model exists in this game"); return false; }

                _ped = new Ped(new Model(chosen), _where, _rng.Next(360));
                if (!_ped.Exists()) return false;
                _ped.IsPersistent = true;
                _ped.BlockPermanentEvents = true;
                try { _ped.Tasks.Wander(); } catch { }

                // They are the person on the warrant: the box calls them by that name, the terminal
                // finds that record when the player runs them.
                var handle = unchecked((int)_ped.Handle.Value);
                Identities.Name(handle, _person.Name);
                _records.Bind(handle, _person);

                _blip = new Blip(_where, 60f);
                _blip.Color = System.Drawing.Color.Yellow;
                _blip.Alpha = 0.5f;
                _blip.IsRouteEnabled = true;

                Location = Where(_where);
                _startedAt = Environment.TickCount;
                Active = true;
                Log.Line("warrant service: " + _person.Name + " (" + _warrantFor + ") placed at " + Location);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("starting a warrant service", ex);
                CleanUp(false);
                return false;
            }
        }

        /// <summary>
        /// One tick. Returns null while it runs, or the line dispatch closes it with - "served" when the
        /// warrant was, anything else when it was not. `served` says which.
        /// </summary>
        public string Update(out bool served)
        {
            served = false;
            if (!Active) return null;

            try
            {
                if (_ped == null || !_ped.Exists()) return Finish("we have lost the subject - the warrant stays open.");
                if (!_ped.IsAlive) return Finish("the subject is down. Warrant closed; get EMS and a supervisor out there.");

                if (_api.PedArrested(_ped))
                {
                    served = true;

                    // /arrest in front of them has already booked it; LSPDFR's own arrest has not.
                    if (_person.Wanted)
                    {
                        _records.RecordArrest(_person, _warrantFor, Name);
                        _records.ScheduleFollowUp(_person, "arrest", _warrantFor, 0, Name);
                    }
                    return Finish("warrant served on " + _person.Name + ". Bring them in.");
                }

                if (Environment.TickCount - _startedAt > TimeoutMs)
                    return Finish("the warrant service on " + _person.Name + " has timed out - it stays on file.");

                var player = Game.LocalPlayer.Character;
                if (!_approached && player.Position.DistanceTo(_ped.Position) < ApproachMetres)
                {
                    _approached = true;
                    Approach();
                }
            }
            catch (Exception ex) { Log.Error("warrant service tick", ex); }

            return null;
        }

        /// <summary>What they do when the officer walks up: how they are, decides it.</summary>
        private void Approach()
        {
            try { if (_blip != null && _blip.Exists()) _blip.Delete(); } catch { }
            _blip = null;
            try { _ped.AttachBlip().Color = System.Drawing.Color.Red; } catch { }

            var mood = Identities.MoodFor(unchecked((int)_ped.Handle.Value));
            var runs = mood == Temperament.Hostile || (mood == Temperament.Nervous && _rng.Next(100) < 45) ||
                       (mood == Temperament.Defensive && _rng.Next(100) < 20);

            if (runs)
            {
                _fled = true;
                try
                {
                    var pursuit = _api.CreatePursuit();
                    if (pursuit != null)
                    {
                        _api.AddPedToPursuit(pursuit, _ped);
                        _api.SetPursuitActiveForPlayer(pursuit, true);
                    }
                    else _ped.Tasks.Flee(Game.LocalPlayer.Character, 150f, -1);
                }
                catch (Exception ex) { Log.Error("warrant service pursuit", ex); }
                Log.Line("warrant service: " + _person.Name + " ran (" + mood + ")");
            }
            else
            {
                try { _ped.Tasks.StandStill(-1); } catch { }
                _api.StopPed(_ped);
                Log.Line("warrant service: " + _person.Name + " stopped for the officer (" + mood + ")");
            }
        }

        public bool Fled { get { return _fled; } }

        public void Cancel()
        {
            if (!Active) return;
            Finish("cancelled");
        }

        private string Finish(string line)
        {
            Active = false;
            CleanUp(true);
            return line;
        }

        private void CleanUp(bool keepIfArrested)
        {
            try { if (_blip != null && _blip.Exists()) _blip.Delete(); } catch { }
            _blip = null;

            try
            {
                if (_ped != null && _ped.Exists())
                {
                    try { var attached = _ped.GetAttachedBlip(); if (attached != null && attached.Exists()) attached.Delete(); } catch { }

                    var arrested = false;
                    try { arrested = _api.PedArrested(_ped); } catch { }
                    if (!(keepIfArrested && arrested)) _ped.Dismiss();
                }
            }
            catch { }
        }

        private string Where(Vector3 position)
        {
            string street = null, zone = null;
            try { street = World.GetStreetName(position); } catch { }
            try { zone = _api.ZoneAt(position); } catch { }

            if (!string.IsNullOrEmpty(street) && !string.IsNullOrEmpty(zone)) return street + ", " + zone;
            return street ?? zone ?? "a street near you";
        }
    }
}
