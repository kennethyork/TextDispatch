using System;
using System.Reflection;
using Rage;

namespace TextDispatch.Bridges
{
    /// <summary>
    /// The callout's own lines, for the people the callout spawned.
    ///
    /// TextCallouts builds a scene and, when a recipe says so, registers what its suspect or patient
    /// will say when they are spoken to. This asks for those lines before anything else - before the
    /// built-in script and before the model - because they are what that character was written to say,
    /// they arrive instantly, and they need no model running at all.
    ///
    /// Reflected rather than referenced, like the rest of the bridge: the callout pack is found by name
    /// in this process, so having it installed is a bonus and not having it costs nothing. A suspect in
    /// a callout with no script simply falls through to the model as they always did.
    /// </summary>
    internal sealed class CalloutScriptBridge
    {
        private const string TypeName = "TextCallouts.Scripts.CalloutScript";

        private Type _type;
        private MethodInfo _has;
        private MethodInfo _reply;
        private MethodInfo _name;
        private bool _looked;
        private bool _reported;

        /// <summary>Whether the callout pack's scripts were found. Discovery is done once.</summary>
        public bool Available
        {
            get { Discover(); return _type != null; }
        }

        private void Discover()
        {
            if (_looked) return;
            _looked = true;

            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly == null) continue;

                    var found = assembly.GetType(TypeName, false);
                    if (found == null) continue;

                    _type = found;
                    _has = Method(found, "Has", 1);
                    _reply = Method(found, "Reply", 2);
                    _name = Method(found, "NameOf", 1);

                    Log.Line("callout scripts: found " + TypeName + " - a suspect in a callout will answer in character");
                    return;
                }
            }
            catch (Exception ex) { Log.Error("looking for the callout pack's scripts", ex); }
        }

        private static MethodInfo Method(Type type, string name, int parameters)
        {
            try
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    if (method.Name == name && method.GetParameters().Length == parameters) return method;
            }
            catch { }

            return null;
        }

        /// <summary>The name to put in front of their line, or null if they are not scripted.</summary>
        public string NameOf(Ped ped)
        {
            if (!Available || _name == null || ped == null) return null;

            try { return (string)_name.Invoke(null, new object[] { ped }); }
            catch { return null; }
        }

        /// <summary>What this person says next, or null - which is the usual answer, for everybody the
        /// callouts did not write lines for.</summary>
        public string Reply(Ped ped, string said)
        {
            if (!Available || _reply == null || ped == null) return null;

            try
            {
                // Ask whether they are scripted at all before asking what they say: the second call is
                // the one that advances the script, and it should not advance for anybody else.
                if (_has != null && !(bool)_has.Invoke(null, new object[] { ped })) return null;

                if (!_reported)
                {
                    _reported = true;
                    Log.Line("callout scripts: in use - a scripted suspect answers from the callout's own lines");
                }

                return (string)_reply.Invoke(null, new object[] { ped, said });
            }
            catch (Exception ex) { Log.Error("reading a callout script", ex); return null; }
        }
    }
}
