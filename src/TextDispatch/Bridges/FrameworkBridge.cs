using System;
using System.Collections.Generic;
using System.Reflection;
using Rage;

namespace TextDispatch.Bridges
{
    /// <summary>
    /// What other plugins can do, reached by reflection and offered as commands.
    ///
    /// Three mods own features LSPDFR itself does not have - K9 units, spike strips, road blocks, a
    /// felony-stop routine, breathalysers, insurance and registration lookups - and each publishes a
    /// static API for them: StopThePed.API.Functions, UltimateBackup.API.Functions and
    /// PolicingRedefined.API.*. They are other people's menus, which is why typing could not reach them.
    ///
    /// Reached by reflection rather than referenced at build time, for the same reason LSPDFR is: there
    /// must be no dependency on them. If a mod is not installed, or its API has changed, the command
    /// says so rather than failing, and nothing else in the plugin is affected. The names and shapes
    /// below were read out of the installed assemblies, not guessed.
    /// </summary>
    internal sealed class FrameworkBridge
    {
        private Assembly _stopThePed;
        private Assembly _ultimateBackup;
        private Assembly _policingRedefined;

        // The API types, once found. Each is looked up by name inside its own assembly.
        private Type _stpFunctions;
        private Type _ubFunctions;
        private Type _prBackup;
        private Type _prTraffic;
        private Type _prPed;

        public bool HasStopThePed { get { return _stpFunctions != null; } }
        public bool HasUltimateBackup { get { return _ubFunctions != null; } }
        public bool HasPolicingRedefined { get { return _prBackup != null; } }

        /// <summary>What it found, for the log and for /plugins.</summary>
        public string Describe()
        {
            var parts = new List<string>();
            parts.Add("StopThePed " + (HasStopThePed ? "yes" : "no"));
            parts.Add("UltimateBackup " + (HasUltimateBackup ? "yes" : "no"));
            parts.Add("PolicingRedefined " + (HasPolicingRedefined ? "yes" : "no"));
            return string.Join(", ", parts.ToArray());
        }

        // ------------------------------------------------------------------ discovery

        public void Discover()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name;
                try { name = assembly.GetName().Name; }
                catch { continue; }
                if (string.IsNullOrEmpty(name)) continue;

                if (name.Equals("StopThePed", StringComparison.OrdinalIgnoreCase)) _stopThePed = assembly;
                else if (name.Equals("UltimateBackup", StringComparison.OrdinalIgnoreCase)) _ultimateBackup = assembly;
                else if (name.Equals("PolicingRedefined", StringComparison.OrdinalIgnoreCase)) _policingRedefined = assembly;
            }

            _stpFunctions = Type(_stopThePed, "StopThePed.API.Functions");
            _ubFunctions = Type(_ultimateBackup, "UltimateBackup.API.Functions");
            _prBackup = Type(_policingRedefined, "PolicingRedefined.API.BackupAPI");
            _prTraffic = Type(_policingRedefined, "PolicingRedefined.API.TrafficControlAPI");
            _prPed = Type(_policingRedefined, "PolicingRedefined.API.PedAPI");

            Log.Line("bridges: " + Describe());
        }

        private static Assembly Load(string simpleName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { if (string.Equals(assembly.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase)) return assembly; }
                catch { }
            }
            return null;
        }

        private static Type Type(Assembly assembly, string fullName)
        {
            if (assembly == null) return null;
            try { return assembly.GetType(fullName, false); }
            catch { return null; }
        }

        // ------------------------------------------------------------------ calling

        /// <summary>
        /// Call the first overload of a method whose runtime parameter types accept these arguments -
        /// the same shape-matching the LSPDFR bridge uses, and for the same reason: the signatures
        /// cannot be read out of another plugin's assembly at build time.
        /// </summary>
        private bool Call(Type type, string method, object[] arguments, out object result)
        {
            result = null;
            if (type == null) return false;

            try
            {
                foreach (var candidate in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (candidate.Name != method) continue;

                    var parameters = candidate.GetParameters();
                    if (parameters.Length != (arguments == null ? 0 : arguments.Length)) continue;

                    var usable = true;
                    for (var i = 0; i < parameters.Length; i++)
                    {
                        if (arguments[i] == null)
                        {
                            if (parameters[i].ParameterType.IsValueType) { usable = false; break; }
                            continue;
                        }
                        if (!parameters[i].ParameterType.IsInstanceOfType(arguments[i])) { usable = false; break; }
                    }
                    if (!usable) continue;

                    result = candidate.Invoke(null, arguments);
                    return true;
                }
            }
            catch (Exception ex) { Log.Error("bridge " + type.Name + "." + method, ex); }

            return false;
        }

        private bool Call(Type type, string method, params object[] arguments)
        {
            object ignored;
            return Call(type, method, arguments, out ignored);
        }

        /// <summary>Ask a framework for one of its units or services, newest first: Policing Redefined,
        /// then Ultimate Backup, then StopThePed. Returns what answered, or null.</summary>
        public string Backup(string kind)
        {
            var wanted = (kind ?? "").Trim().ToLowerInvariant();

            if (HasPolicingRedefined)
            {
                var unit = UnitFor(wanted);
                if (unit != null)
                {
                    var code = wanted == "code2" ? "Code2" : "Code3";
                    if (CallPolicingRedefined(unit, code)) return "Policing Redefined";
                }
            }

            if (HasUltimateBackup)
            {
                var method = UltimateBackupMethod(wanted);
                if (method != null && Call(_ubFunctions, method, true, false)) return "Ultimate Backup";
            }

            if (HasStopThePed)
            {
                if (wanted == "pit" && Call(_stpFunctions, "requestPIT")) return "StopThePed";
                if (wanted == "coroner" && Call(_stpFunctions, "callCoroner")) return "StopThePed";
                if (wanted == "animal" && Call(_stpFunctions, "callAnimalControl")) return "StopThePed";
            }

            return null;
        }

        /// <summary>Policing Redefined's RequestBackup takes its own enums, so they are built by name.</summary>
        private bool CallPolicingRedefined(string unitName, string codeName)
        {
            try
            {
                var unitType = _prBackup.Assembly.GetType("PolicingRedefined.API.EBackupUnit", false);
                var codeType = _policingRedefined.GetType("PolicingRedefined.Backup.Entities.EBackupResponseCode", false);
                if (unitType == null || codeType == null)
                {
                    Log.Line("bridge: Policing Redefined's backup enums were not where the docs say");
                    return false;
                }

                var unit = Enum.Parse(unitType, unitName, true);
                var code = Enum.Parse(codeType, codeName, true);

                // RequestBackup(EBackupUnit, EBackupResponseCode, Boolean dispatchNotif, Boolean
                // dispatchAnim, Boolean dispatchAudio) - the three flags mean "let dispatch say it",
                // which is the point of asking in text, so all three are true.
                return Call(_prBackup, "RequestBackup", new object[] { unit, code, true, true, true }) ||
                       Call(_prBackup, "RequestBackup", new object[] { unit, code, Game.LocalPlayer.Character.Position, true, true, true });
            }
            catch (Exception ex) { Log.Error("bridge policing redefined backup", ex); return false; }
        }

        private static string UnitFor(string kind)
        {
            switch (kind)
            {
                case "code2": case "local": case "unit": return "LocalPatrol";
                case "code3": return "LocalPatrol";
                case "state": return "StatePatrol";
                case "female": return "LocalFemalePatrol";
                case "swat": case "noose": return "NooseSWAT";
                case "localswat": return "LocalSWAT";
                case "k9": case "dog": return "LocalK9Patrol";
                case "statek9": return "StateK9Patrol";
                case "air": case "helicopter": return "LocalAir";
                case "nooseair": return "NooseAir";
                case "transport": return "PoliceTransport";
                case "ems": case "ambulance": case "medic": return "Ambulance";
                case "fire": case "firetruck": return "FireDepartment";
                case "coroner": return "Coroner";
                case "tow": return "FlatbedTowTruck";
                case "animal": return "AnimalControl";
                default: return null;
            }
        }

        private static string UltimateBackupMethod(string kind)
        {
            switch (kind)
            {
                case "code2": case "local": case "unit": return "callCode2Backup";
                case "code3": return "callCode3Backup";
                case "state": return "callCode3Backup";
                case "female": return "callFemaleBackup";
                case "swat": case "noose": return "callCode3SwatBackup";
                case "k9": case "dog": case "statek9": return "callK9Backup";
                case "felony": return "callFelonyStopBackup";
                case "group": return "callGroupBackup";
                case "pursuit": return "callPursuitBackup";
                case "traffic": case "stop": return "callTrafficStopBackup";
                case "spikes": case "spikestrips": return "callSpikeStripsBackup";
                case "roadblock": case "block": return "callRoadBlockBackup";
                case "panic": case "10-13": return "callPanicButtonBackup";
                case "ems": case "ambulance": case "medic": return "callAmbulance";
                case "fire": case "firetruck": return "callFireDepartment";
                default: return null;
            }
        }

        /// <summary>Who sent the units home, or null if nobody was there to ask.</summary>
        public string DismissAll()
        {
            if (HasPolicingRedefined && Call(_prBackup, "DismissAllBackupUnits", new object[] { true })) return "Policing Redefined";
            if (HasUltimateBackup && Call(_ubFunctions, "dismissAllBackupUnits")) return "Ultimate Backup";
            return null;
        }

        /// <summary>
        /// Ask again if a framework was not there last time. Ultimate Backup sorts after TextDispatch
        /// in the folder, so it can genuinely still be loading when this plugin starts - which is why
        /// discovery cannot be a one-off at startup.
        /// </summary>
        public void Ensure()
        {
            if (HasStopThePed && HasUltimateBackup && HasPolicingRedefined) return;
            Discover();
        }

        // ------------------------------------------------------------------ the checks

        /// <summary>Insurance and registration, straight out of StopThePed's own records.</summary>
        public string VehicleStatus(Vehicle vehicle, bool insurance)
        {
            if (vehicle == null) { return null; }

            object status;
            var called = insurance
                ? Call(_stpFunctions, "getVehicleInsuranceStatus", new object[] { vehicle }, out status)
                : Call(_stpFunctions, "getVehicleRegistrationStatus", new object[] { vehicle }, out status);

            return called && status != null ? status.ToString() : null;
        }

        public string Alcohol(Ped ped)
        {
            object over;
            if (!Call(_stpFunctions, "isPedAlcoholOverLimit", new object[] { ped }, out over)) return null;
            return over is bool && (bool)over ? "over the limit" : "under the limit";
        }

        public string Drugs(Ped ped)
        {
            object under;
            if (!Call(_stpFunctions, "isPedUnderDrugsInfluence", new object[] { ped }, out under)) return null;
            return under is bool && (bool)under ? "showing signs of drug use" : "nothing obvious";
        }

        /// <summary>Ask dispatch to run the check on the radio - StopThePed's own procedure.</summary>
        public string RadioPlateCheck()
        {
            return Call(_stpFunctions, "requestDispatchVehiclePlateCheck", new object[] { true }) ? "StopThePed" : null;
        }

        public string RadioPedCheck()
        {
            return Call(_stpFunctions, "requestDispatchPedCheck", new object[] { true }) ? "StopThePed" : null;
        }

        public string Pit() { return Call(_stpFunctions, "requestPIT") ? "StopThePed" : null; }

        public string FelonyStop()
        {
            if (HasPolicingRedefined && Call(_prBackup, "InitiateFelonyStop")) return "Policing Redefined";
            if (HasUltimateBackup && Call(_ubFunctions, "callFelonyStopBackup", true, false)) return "Ultimate Backup";
            return null;
        }
    }
}
