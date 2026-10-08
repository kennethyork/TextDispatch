using System;
using System.Reflection;
using System.Text;
using Rage;

namespace TextDispatch.Lspdfr
{
    /// <summary>
    /// Everything this plugin knows about LSPDFR, behind one reflection bridge.
    ///
    /// Reflection is deliberate, not laziness:
    ///
    ///   1. LSPDFR's licence forbids bundling or shipping it, so there must be no build-time
    ///      reference to "LSPD First Response.dll".
    ///   2. It may not be installed at all, and a working game without it is a supported state.
    ///   3. It loads after us. A lookup that fails has to degrade to "LSPDFR absent", not to a
    ///      plugin that refuses to start.
    ///
    /// Overloads are resolved at call time against the *runtime* parameter types, so the bridge does
    /// not need the signatures of methods that take Rage entities (Ped, Vehicle, Vector3) - which
    /// cannot be read out of the assembly without its dependencies present.
    /// </summary>
    internal sealed class LspdfrApi
    {
        private const string FunctionsType = "LSPD_First_Response.Mod.API.Functions";
        private const string BackupResponseType = "LSPD_First_Response.EBackupResponseType";
        private const string BackupUnitType = "LSPD_First_Response.EBackupUnitType";
        private const string AcceptanceStateType = "LSPD_First_Response.Mod.Callouts.CalloutAcceptanceState";

        private Assembly _assembly;
        private Type _functions;
        private bool _available;
        private bool _probed;
        private DateTime _lastProbe = DateTime.MinValue;
        private bool _loggedOverloads;

        public bool Available { get { Probe(); return _available; } }

        public string Describe()
        {
            Probe();
            if (!_available) return "not installed";
            string version = "unknown";
            try
            {
                var v = Call("GetVersion");
                if (v != null) version = v.ToString();
            }
            catch { }
            return "detected, LSPDFR " + version + ", " + _functions.GetMethods(BindingFlags.Public | BindingFlags.Static).Length + " API methods";
        }

        /// <summary>
        /// The assemblies LSPDFR itself reports as loaded plugins - the only authoritative answer to
        /// "did it load?". Null when LSPDFR cannot be asked (not visible, or no such method), in
        /// which case PluginInventory falls back to what the AppDomain holds.
        /// </summary>
        public Assembly[] UserPlugins()
        {
            Probe();
            if (!_available) return null;

            try
            {
                var method = Select("GetAllUserPlugins", null);
                if (method == null) return null;
                return method.Invoke(null, null) as Assembly[];
            }
            catch (Exception ex) { Log.Error("lspdfr GetAllUserPlugins", ex); return null; }
        }

        private void Probe()
        {
            if (_available) return;

            var now = DateTime.UtcNow;
            if (_probed && (now - _lastProbe).TotalSeconds < 5) return;
            _probed = true;
            _lastProbe = now;

            try
            {
                var loaded = AppDomain.CurrentDomain.GetAssemblies();

                foreach (var assembly in loaded)
                {
                    string name;
                    try { name = assembly.GetName().Name; }
                    catch { continue; }

                    if (name == null) continue;
                    if (name.IndexOf("LSPD First Response", StringComparison.OrdinalIgnoreCase) < 0) continue;

                    var type = assembly.GetType(FunctionsType, false);
                    if (type == null) continue;

                    _assembly = assembly;
                    _functions = type;
                    _available = true;
                    return;
                }

                // Not found. Say why, because the usual cause is not a missing LSPDFR - it is this
                // plugin being loaded by the wrong host. RPH gives every plugin its own AppDomain,
                // so a plugin loaded by RPH cannot see LSPDFR's types at all; only a plugin loaded
                // by LSPDFR (from Plugins\LSPDFR\) shares its AppDomain and can call the API.
                var names = new System.Collections.Generic.List<string>();
                foreach (var candidate in loaded) names.Add(candidate.GetName().Name);
                Log.Line("lspdfr: not visible from AppDomain '" + AppDomain.CurrentDomain.FriendlyName +
                         "' which holds " + loaded.Length + " assemblies (" + string.Join(", ", names.ToArray()) + ")");
                Log.Line("lspdfr: if LSPDFR is installed, this plugin is in the wrong folder - it must be " +
                         "loaded by LSPDFR from Plugins\\LSPDFR, not by RPH from Plugins");
            }
            catch (Exception ex) { Log.Error("lspdfr probe", ex); }
        }

        // ------------------------------------------------------------------ invocation

        private object Call(string name, params object[] args)
        {
            Probe();
            if (!_available) return null;

            var method = Select(name, args);
            if (method == null)
            {
                Log.Line("lspdfr: no " + name + " overload taking " + (args == null ? 0 : args.Length) + " argument(s)");
                return null;
            }

            try { return method.Invoke(null, args); }
            catch (Exception ex) { Log.Error("lspdfr " + name, ex); return null; }
        }

        private MethodInfo Select(string name, object[] args)
        {
            int count = args == null ? 0 : args.Length;
            MethodInfo first = null;

            foreach (var method in _functions.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name != name) continue;
                var parameters = method.GetParameters();
                if (parameters.Length != count) continue;
                if (first == null) first = method;

                bool usable = true;
                for (int i = 0; i < count; i++)
                {
                    if (args[i] == null)
                    {
                        if (parameters[i].ParameterType.IsValueType) { usable = false; break; }
                        continue;
                    }
                    if (!parameters[i].ParameterType.IsInstanceOfType(args[i])) { usable = false; break; }
                }
                if (usable) return method;
            }

            return first;
        }

        private static bool AsBool(object value) { return value is bool && (bool)value; }
        private static string AsString(object value) { return value == null ? null : value.ToString(); }

        private Type EnumType(string fullName)
        {
            Probe();
            if (!_available) return null;
            try { return _assembly.GetType(fullName, false); }
            catch { return null; }
        }

        // ------------------------------------------------------------------ callouts

        public object CurrentCallout() { return Call("GetCurrentCallout"); }

        public bool CalloutRunning() { return AsBool(Call("IsCalloutRunning")); }

        public string CalloutName(object handle) { return AsString(Call("GetCalloutName", handle)); }

        public string CalloutFriendlyName(object handle) { return AsString(Call("GetCalloutFriendlyName", handle)); }

        /// <summary>"Pending", "Running", "Ended" or null.</summary>
        public string AcceptanceState(object handle) { return AsString(Call("GetCalloutAcceptanceState", handle)); }

        public void AcceptCallout(object handle) { Call("AcceptPendingCallout", handle); }

        public void StopCallout() { Call("StopCurrentCallout"); }

        public bool StartCallout(string name)
        {
            Probe();
            if (!_available || string.IsNullOrWhiteSpace(name)) return false;
            Call("StartCallout", name);
            return true;
        }

        public bool PlayerAvailable() { return AsBool(Call("IsPlayerAvailableForCalls")); }

        public void SetPlayerAvailable(bool value) { Call("SetPlayerAvailableForCalls", value); }

        // ------------------------------------------------------------------ pursuits and stops

        public object ActivePursuit() { return Call("GetActivePursuit"); }

        public object CurrentPullover() { return Call("GetCurrentPullover"); }

        public bool PlayerPerformingPullover() { return AsBool(Call("IsPlayerPerformingPullover")); }

        public bool PlayerArresting() { return AsBool(Call("IsPlayerArresting")); }

        public void ForceEndPullover() { Call("ForceEndCurrentPullover"); }

        public bool PursuitCalledIn(object pursuit) { return AsBool(Call("IsPursuitCalledIn", pursuit)); }

        public void MarkPursuitCalledIn(object pursuit) { Call("SetPursuitAsCalledIn", pursuit); }

        public void ForceEndPursuit(object pursuit) { Call("ForceEndPursuit", pursuit); }

        // ------------------------------------------------------------------ backup

        /// <summary>
        /// Ask for a unit. The overload shape differs between LSPDFR builds, so several plausible
        /// shapes are tried against the runtime signatures rather than assuming one.
        /// </summary>
        public Vehicle RequestBackup(string responseName, string unitName)
        {
            Probe();
            if (!_available) return null;

            var responseType = EnumType(BackupResponseType);
            var unitType = EnumType(BackupUnitType);
            if (responseType == null || unitType == null)
            {
                Log.Line("lspdfr: backup enums not found (" + BackupResponseType + " / " + BackupUnitType + ")");
                return null;
            }

            object response, unit;
            try
            {
                response = Enum.Parse(responseType, responseName, true);
                unit = Enum.Parse(unitType, unitName, true);
            }
            catch (Exception ex) { Log.Error("backup enum", ex); return null; }

            var position = PlayerPosition();

            // Verified against LSPDFR 0.4.9's own metadata - the overloads really are
            //   RequestBackup(Vector3, EBackupResponseType, EBackupUnitType)
            //   RequestBackup(Vector3, EBackupResponseType, EBackupUnitType, String)
            //   RequestBackup(Vector3, EBackupResponseType, EBackupUnitType, String, Boolean, Boolean)
            // and they return the unit they sent. A null return means no unit was created, which is
            // the difference between "dispatch asked" and "backup is actually coming".
            object dispatched;
            bool sent = TryInvokeValue("RequestBackup", new[]
            {
                new object[] { position, response, unit },
                new object[] { position, response, unit, "" },
                new object[] { position, response, unit, "", false, false }
            }, out dispatched);

            if (!sent) { LogBackupOverloads(); return null; }

            var backup = dispatched as Vehicle;
            Log.Line("backup " + responseName + "/" + unitName + " -> " +
                     (backup == null ? "accepted but no unit returned" : "unit " + Describe(backup)));
            return backup;
        }

        private static string Describe(Vehicle vehicle)
        {
            try
            {
                var plate = vehicle.LicensePlate;
                var model = vehicle.Model.Name;
                return (string.IsNullOrEmpty(model) ? "vehicle" : model) +
                       (string.IsNullOrEmpty(plate) ? "" : " (" + plate + ")");
            }
            catch { return "vehicle"; }
        }

        /// <summary>
        /// Invoke the first overload whose runtime parameter types accept these arguments, and hand
        /// back what it returned.
        ///
        /// This is how the bridge calls methods that take Rage entities: their signatures cannot be
        /// read out of the LSPDFR assembly by reflection (its dependencies are not present), so the
        /// argument shapes are matched against the live types instead of assumed.
        /// </summary>
        private bool TryInvokeValue(string name, object[][] shapes, out object result)
        {
            result = null;

            foreach (var shape in shapes)
            {
                var method = Select(name, shape);
                if (method == null) continue;

                var parameters = method.GetParameters();
                if (parameters.Length != shape.Length) continue;

                bool usable = true;
                for (int i = 0; i < shape.Length; i++)
                {
                    if (shape[i] == null) { if (parameters[i].ParameterType.IsValueType) { usable = false; break; } continue; }
                    if (!parameters[i].ParameterType.IsInstanceOfType(shape[i])) { usable = false; break; }
                }
                if (!usable) continue;

                try
                {
                    result = method.Invoke(null, shape);
                    Log.Line("lspdfr: " + name + " via " + Describe(method));
                    return true;
                }
                catch (Exception ex) { Log.Error(name, ex); }
            }

            return false;
        }

        private void LogBackupOverloads()
        {
            if (_loggedOverloads) return;
            _loggedOverloads = true;
            try
            {
                foreach (var method in _functions.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (method.Name != "RequestBackup") continue;
                    Log.Line("lspdfr: RequestBackup candidate " + Describe(method));
                }
            }
            catch { }
        }

        private static string Describe(MethodInfo method)
        {
            try
            {
                var parts = new System.Collections.Generic.List<string>();
                foreach (var parameter in method.GetParameters())
                    parts.Add(parameter.ParameterType.Name + " " + parameter.Name);
                return method.Name + "(" + string.Join(", ", parts.ToArray()) + ")";
            }
            catch { return method.Name + "(?)"; }
        }

        // ------------------------------------------------------------------ people and vehicles

        public void DisplayPedId(object ped) { Call("DisplayPedId", ped, true); }

        public void DisplayVehicleRecord(object vehicle) { Call("DisplayVehicleRecord", vehicle, true); }

        public string VehicleOwner(object vehicle) { return AsString(Call("GetVehicleOwnerName", vehicle)); }

        public bool PedArrested(object ped) { return AsBool(Call("IsPedArrested", ped)); }

        public bool PedFrisked(object ped) { return AsBool(Call("HasPedBeenFrisked", ped)); }

        public bool PedStoppedByPlayer(object ped) { return AsBool(Call("IsPedStoppedByPlayer", ped)); }

        public bool PedCarryingContraband(object ped) { return AsBool(Call("IsPedCarryingContraband", ped)); }

        public bool PedSurrendered(object ped) { return AsBool(Call("HasPedSurrendered", ped)); }

        public bool PedIdentified(object ped) { return AsBool(Call("HasPedBeenIdentified", ped)); }

        /// <summary>True when the ped is a police officer - for the player, that means on duty.</summary>
        public bool PedIsCop(object ped) { return AsBool(Call("IsPedACop", ped)); }

        public void ArrestPed(object ped) { Call("SetPedAsArrested", ped, true, true); }

        /// <summary>LSPDFR's own stopped state - the same one it applies to a detained pedestrian.</summary>
        public void StopPed(object ped) { Call("SetPedAsStopped", ped); }

        /// <summary>
        /// Take the cuffs off. LSPDFR's ForceClearPedArrestedState exists but has had more than one
        /// shape, so both are tried rather than guessed at.
        /// </summary>
        public bool ReleasePed(object ped)
        {
            Probe();
            if (!_available || ped == null) return false;

            return TryCall("ForceClearPedArrestedState",
                new object[] { ped },
                new object[] { ped, true });
        }

        /// <summary>Call something whose shape has changed across LSPDFR builds.</summary>
        public bool TryCall(string name, params object[][] shapes)
        {
            object result;
            return TryInvokeValue(name, shapes, out result);
        }

        public void RequestTransport(object ped) { Call("RequestSuspectTransport", ped); }

        public string PersonaForPed(object ped) { return AsString(Call("GetPersonaForPed", ped)); }

        public string ZoneAt(Vector3 position) { return AsString(Call("GetZoneAtPosition", position)); }

        public object CreatePursuit() { return Call("CreatePursuit"); }

        // ------------------------------------------------------------------ callout catalogue

        private sealed class CalloutEntry
        {
            public string Label;
            public string TypeName;

            /// <summary>Which pack this callout came from - the assembly that defines it.</summary>
            public string Pack;

            public string Display
            {
                get { return Label + (string.IsNullOrEmpty(Pack) ? "" : "  -  " + Pack); }
            }
        }

        private System.Collections.Generic.List<CalloutEntry> _callouts;

        public System.Collections.Generic.List<string> ListCallouts()
        {
            return ListCallouts(null, int.MaxValue, out int _);
        }

        /// <summary>
        /// The callout catalogue, filtered and capped.
        ///
        /// With a few hundred callouts installed - which is the whole point of a plugin like this -
        /// printing the lot would fill the chat box many times over, so the caller says how many it
        /// wants and gets told the true total so it can offer to narrow the search.
        /// </summary>
        public System.Collections.Generic.List<string> ListCallouts(string filter, int limit, out int total)
        {
            var shown = new System.Collections.Generic.List<string>();
            total = 0;

            foreach (var entry in FindCallouts())
            {
                if (!string.IsNullOrEmpty(filter) &&
                    entry.Display.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                    entry.TypeName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                total++;
                if (shown.Count < limit) shown.Add(entry.Display);
            }

            return shown;
        }

        /// <summary>Total callouts discovered, across LSPDFR and every installed pack.</summary>
        public int CalloutCount
        {
            get { return FindCallouts().Count; }
        }

        /// <summary>Names of the packs that contributed callouts, for the log and /calls.</summary>
        public System.Collections.Generic.List<string> CalloutPacks()
        {
            var packs = new System.Collections.Generic.List<string>();
            foreach (var entry in FindCallouts())
            {
                if (string.IsNullOrEmpty(entry.Pack)) continue;
                if (!packs.Contains(entry.Pack)) packs.Add(entry.Pack);
            }
            packs.Sort(StringComparer.OrdinalIgnoreCase);
            return packs;
        }

        /// <summary>
        /// What to hand to StartCallout for whatever the player typed.
        ///
        /// LSPDFR's StartCallout looks a callout up by the Name in its CalloutInfo attribute - the
        /// label /calls shows - and silently does nothing for anything else. Handing it the class
        /// name, which this used to do, meant "/callout armed robbery" asked for "ArmedRobbery" and
        /// never started anything. Either is accepted from the player; the label is what goes out.
        /// </summary>
        public string ResolveCallout(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return query;

            var needle = query.Trim();
            foreach (var entry in FindCallouts())
            {
                if (string.Equals(entry.TypeName, needle, StringComparison.OrdinalIgnoreCase)) return entry.Label;
                if (string.Equals(entry.Label, needle, StringComparison.OrdinalIgnoreCase)) return entry.Label;
            }

            foreach (var entry in FindCallouts())
            {
                if (entry.TypeName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return entry.Label;
                if (entry.Label.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return entry.Label;
            }

            return needle;
        }

        /// <summary>The name LSPDFR starts each callout by, for every callout this install has.</summary>
        public System.Collections.Generic.List<string> CalloutLabels()
        {
            var labels = new System.Collections.Generic.List<string>();
            foreach (var entry in FindCallouts())
                if (!labels.Contains(entry.Label)) labels.Add(entry.Label);
            return labels;
        }

        /// <summary>
        /// The callout whose name best fits a description such as a 911 call's: "man with a gun at the
        /// gas station" scores on every word it shares with a callout's name. Null when nothing shares
        /// a single meaningful word. Names in 'skip' are passed over - the ones that turned out not to
        /// be registered for this duty.
        /// </summary>
        public string MatchCallout(string details, System.Collections.Generic.ICollection<string> skip)
        {
            if (string.IsNullOrWhiteSpace(details)) return null;

            var words = Words(details);
            if (words.Count == 0) return null;

            string best = null;
            int bestScore = 0;
            foreach (var entry in FindCallouts())
            {
                if (skip != null && skip.Contains(entry.Label)) continue;

                int score = 0;
                var name = Words(entry.Label + " " + entry.TypeName.Replace('_', ' '));
                foreach (var word in words)
                    foreach (var part in name)
                        if (part == word || (word.Length >= 4 && part.StartsWith(word, StringComparison.Ordinal)) ||
                            (part.Length >= 4 && word.StartsWith(part, StringComparison.Ordinal)))
                        { score++; break; }

                if (score > bestScore) { bestScore = score; best = entry.Label; }
            }

            return best;
        }

        private static readonly System.Collections.Generic.HashSet<string> NoiseWords =
            new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal)
            {
                "a", "an", "the", "at", "in", "on", "of", "and", "or", "to", "with", "is", "are", "there",
                "some", "someone", "guy", "man", "woman", "person", "people", "near", "by", "my", "i", "we",
                "have", "has", "got", "call", "report", "reported", "reports", "please", "help", "911", "lspd"
            };

        private static System.Collections.Generic.List<string> Words(string text)
        {
            var words = new System.Collections.Generic.List<string>();
            var current = new StringBuilder();
            foreach (var c in (text ?? "").ToLowerInvariant() + " ")
            {
                if (char.IsLetterOrDigit(c)) { current.Append(c); continue; }
                if (current.Length > 0)
                {
                    var word = current.ToString();
                    if (word.Length > 1 && !NoiseWords.Contains(word)) words.Add(word);
                    current.Clear();
                }
            }
            return words;
        }

        private DateTime _calloutsFound = DateTime.MinValue;

        /// <summary>
        /// Every callout this install actually has - LSPDFR's own plus every callout pack's.
        /// Found by walking every assembly in LSPDFR's AppDomain and reading the callout attribute,
        /// so new packs appear here without this plugin knowing anything about them.
        ///
        /// Every assembly, not just LSPDFR's: the packs define their callouts in their own
        /// assemblies, and TextCallouts builds its recipes into a dynamic one when you go on duty -
        /// which can be after this first runs, so the list is rebuilt once it is a minute old.
        /// </summary>
        private System.Collections.Generic.List<CalloutEntry> FindCallouts()
        {
            if (_callouts != null && (DateTime.UtcNow - _calloutsFound).TotalSeconds < 60) return _callouts;

            var found = new System.Collections.Generic.List<CalloutEntry>();
            _callouts = found;
            _calloutsFound = DateTime.UtcNow;

            Probe();
            if (!_available) return found;

            Type baseType = null;
            Type infoType = null;
            try
            {
                baseType = _assembly.GetType("LSPD_First_Response.Mod.Callouts.Callout", false);
                infoType = _assembly.GetType("LSPD_First_Response.Mod.Callouts.CalloutInfoAttribute", false);
            }
            catch { }

            if (baseType == null) return found;

            var types = new System.Collections.Generic.List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { types.AddRange(assembly.GetTypes()); }
                catch (ReflectionTypeLoadException ex) { if (ex.Types != null) types.AddRange(ex.Types); }
                catch { }
            }

            foreach (var type in types)
            {
                try
                {
                    if (type == null || !type.IsClass || type.IsAbstract) continue;
                    if (!baseType.IsAssignableFrom(type)) continue;

                    var pack = PackNameFor(type);

                    string label = type.Name;
                    if (infoType != null)
                    {
                        var attribute = Attribute.GetCustomAttribute(type, infoType);
                        if (attribute != null)
                        {
                            var property = infoType.GetProperty("Name");
                            if (property != null)
                            {
                                var value = property.GetValue(attribute, null);
                                if (value != null && value.ToString().Length > 0) label = value.ToString();
                            }
                        }
                    }

                    // Two packs can share a class name, so identity is pack + type.
                    bool duplicate = false;
                    foreach (var existing in found)
                        if (existing.TypeName == type.Name && existing.Pack == pack) { duplicate = true; break; }
                    if (duplicate) continue;

                    found.Add(new CalloutEntry { Label = label, TypeName = type.Name, Pack = pack });
                }
                catch { }
            }

            found.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));

            if (_loggedCatalogue)
            {
                _loggedCatalogue = false;
                Log.Line("lspdfr: found " + found.Count + " callouts across " +
                         Math.Max(1, CalloutPacks().Count) + " pack(s): " + string.Join(", ", CalloutPacks().ToArray()));
            }

            return found;
        }

        private bool _loggedCatalogue = true;

        /// <summary>Which assembly a callout came from, so a big catalogue stays readable.</summary>
        private static string PackNameFor(Type type)
        {
            try
            {
                var name = type.Assembly.GetName().Name;
                if (string.IsNullOrEmpty(name)) return null;
                if (name.IndexOf("LSPD First Response", StringComparison.OrdinalIgnoreCase) >= 0) return "LSPDFR";
                return name;
            }
            catch { return null; }
        }

        // ------------------------------------------------------------------ helpers

        public Vector3 PlayerPosition()
        {
            var ped = Game.LocalPlayer.Character;
            return ped == null ? Vector3.Zero : ped.Position;
        }

        // ------------------------------------------------------------------ traffic stops

        /// <summary>The driver LSPDFR is currently holding at a traffic stop, or null.</summary>
        public Ped PulloverSuspect()
        {
            var pullover = Call("GetCurrentPullover");
            if (pullover == null) return null;
            return Call("GetPulloverSuspect", pullover) as Ped;
        }

        /// <summary>The car being stopped - not the player's own cruiser.</summary>
        public Vehicle PulloverVehicle()
        {
            var suspect = PulloverSuspect();
            if (suspect == null) return null;
            try { return suspect.CurrentVehicle; }
            catch { return null; }
        }

        public Vehicle PlayerVehicle()
        {
            try
            {
                var player = Game.LocalPlayer.Character;
                return player == null ? null : player.CurrentVehicle;
            }
            catch { return null; }
        }

        /// <summary>
        /// Ask LSPDFR to start a traffic stop on a vehicle.
        ///
        /// LSPDFR exposes exactly one overload - StartPulloverOnParkedVehicle(Vehicle, Boolean,
        /// Boolean), returning the pullover handle - so a single-argument call would never have
        /// matched anything and /stop could not have worked. A non-null handle is the confirmation.
        /// </summary>
        public bool StartPullover(Vehicle vehicle)
        {
            Probe();
            if (!_available || vehicle == null) return false;

            foreach (var shape in new[]
            {
                new object[] { vehicle, false, false },
                new object[] { vehicle, true, false },
                new object[] { vehicle, false, true },
                new object[] { vehicle, true, true }
            })
            {
                object handle;
                if (!TryInvokeValue("StartPulloverOnParkedVehicle", new[] { shape }, out handle)) continue;
                if (handle != null) return true;
            }

            return false;
        }

        public void EndPullover() { Call("ForceEndCurrentPullover"); }

        /// <summary>The car in front of you, for services that act on it rather than on you.</summary>
        public Vehicle NearestOtherVehicle(float radius)
        {
            return NearestVehicle(radius);
        }

        public Vehicle NearestVehicle(float radius)
        {
            var player = Game.LocalPlayer.Character;
            if (player == null) return null;

            Vehicle best = null;
            float bestDistance = radius;

            try
            {
                var own = player.CurrentVehicle;
                foreach (var vehicle in World.GetAllVehicles())
                {
                    if (vehicle == null || !vehicle.Exists()) continue;
                    if (own != null && ReferenceEquals(vehicle, own)) continue;

                    float distance = vehicle.Position.DistanceTo(player.Position);
                    if (distance < bestDistance) { bestDistance = distance; best = vehicle; }
                }
            }
            catch (Exception ex) { Log.Error("nearest vehicle", ex); }

            return best;
        }

        public Ped NearestPed(float radius)
        {
            var player = Game.LocalPlayer.Character;
            if (player == null) return null;

            Ped best = null;
            float bestDistance = radius;
            try
            {
                // RPH exposes the whole ped pool rather than a radius query, so the nearest one is
                // picked here. This runs on a command, never per frame, so the scan is cheap enough.
                foreach (var ped in World.GetAllPeds())
                {
                    if (ped == null) continue;
                    if (ReferenceEquals(ped, player)) continue;
                    if (!ped.Exists() || !ped.IsAlive) continue;

                    float distance = ped.Position.DistanceTo(player.Position);
                    if (distance < bestDistance) { bestDistance = distance; best = ped; }
                }
            }
            catch (Exception ex) { Log.Error("nearest ped", ex); }
            return best;
        }
    }
}
