// [2022.3 PORT] Compatibility shim for ConsoleWindowUtility, which only exists in Unity 6.0+.
//
// Unity 6 exposes UnityEditor.ConsoleWindowUtility with:
//      static event Action consoleLogsChanged;
//      static void GetConsoleLogCounts(out int errors, out int warnings, out int logs);
//
// Unity 2022.3 has neither. What it does have is UnityEditor.LogEntries — an internal class that
// is a member of the UnityEditor.CoreModule assembly and is NOT covered by an InternalsVisibleTo
// for this package, so it has to be reached by reflection here (the same technique MCPForUnity
// uses for the same type). The count helper maps 1:1 onto LogEntries.GetCountsByType:
//      static void GetCountsByType(ref int errorCount, ref int warningCount, ref int logCount);
//
// The change event has no direct equivalent — ConsoleWindow holds an internal static event of the
// same name, bound below by reflection. When it cannot be found the event simply never fires,
// which only costs a sampling delay: EditorConsoleGroundTruth samples on EditorApplication.update
// every 0.25s regardless, and uses this event solely to pull the next sample forward.
#if !UNITY_6000_0_OR_NEWER
using System;
using System.Reflection;
using UnityEditor;

namespace UnityEditor
{
    internal static class ConsoleWindowUtility
    {
        static readonly Type s_LogEntries = FindType("UnityEditor.LogEntries");
        static readonly MethodInfo s_GetCountsByType =
            s_LogEntries?.GetMethod("GetCountsByType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        /// <summary>
        /// Raised when the Console gains entries. Backed by ConsoleWindow's internal event, so it
        /// stays silent if that event cannot be found on this Unity version.
        /// </summary>
        internal static event Action consoleLogsChanged;

        static readonly object s_Relay = BuildRelay();

        static Type FindType(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(name, false);
                if (t != null)
                    return t;
            }
            return null;
        }

        /// <summary>
        /// Subscribes to ConsoleWindow.consoleLogsChanged and re-raises it, so this event keeps the
        /// add/remove-only shape of the Unity 6 original. Returns null when the ConsoleWindow event
        /// is missing, leaving <see cref="consoleLogsChanged"/> unraised.
        /// </summary>
        static object BuildRelay()
        {
            var consoleWindow = FindType("UnityEditor.ConsoleWindow");
            var evt = consoleWindow?.GetEvent("consoleLogsChanged",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (evt?.EventHandlerType == null)
                return null;

            Action relay = () => consoleLogsChanged?.Invoke();

            // Wrap the relay in a delegate of whatever shape the event declares, since add_/remove_
            // reject a mismatched delegate type.
            Delegate handler;
            try
            {
                handler = Delegate.CreateDelegate(evt.EventHandlerType, relay.Target, relay.Method, false);
            }
            catch (ArgumentException)
            {
                return null;
            }
            if (handler == null)
                return null;

            evt.AddEventHandler(null, handler);
            return handler;
        }

        /// <summary>
        /// Console entry counts by severity, read from the Editor's own console store.
        /// </summary>
        internal static void GetConsoleLogCounts(out int errors, out int warnings, out int logs)
        {
            errors = warnings = logs = 0;
            if (s_GetCountsByType == null)
                return;

            // GetCountsByType takes its counters by ref, so the locals must be initialized before
            // the call and marshalled back out afterwards.
            var args = new object[] { errors, warnings, logs };
            s_GetCountsByType.Invoke(null, args);
            errors = (int)args[0];
            warnings = (int)args[1];
            logs = (int)args[2];
        }
    }
}
#endif
