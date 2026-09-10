using System;
using System.Collections.Generic;
using UnityEngine;

namespace HierarchyDecorator
{
    /// <summary>
    /// Logging that can never spam. Everything the decoration path reports goes through here, keyed so a
    /// failure that repeats on every row is written exactly once per domain.
    /// </summary>
    public static class HierarchyLog
    {
        private static readonly HashSet<string> s_Reported = new HashSet<string>(StringComparer.Ordinal);

        public static void Once(string key, string message)
        {
            if (!s_Reported.Add(key))
            {
                return;
            }

            Debug.LogWarning(PackageInfo.LogPrefix + message);
        }

        public static void Once(string key, string message, Exception exception)
        {
            if (!s_Reported.Add(key))
            {
                return;
            }

            Debug.LogWarning($"{PackageInfo.LogPrefix}{message}\n{exception}");
        }

        public static void Info(string message)
        {
            Debug.Log(PackageInfo.LogPrefix + message);
        }

        internal static void Reset()
        {
            s_Reported.Clear();
        }
    }
}
