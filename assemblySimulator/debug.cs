using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace assemblySimulator
{
    public static class DebugLevel
    {
        private struct DebugContainer
        {
            public uint level;
            public bool enabled;
        }
        static readonly Dictionary<string,DebugContainer> debugLevels = [];
        static uint CurrentLevel = 0;
        public static void SetDebugLevel(uint level)
        {
            CurrentLevel = level;
            foreach (var key in debugLevels.Keys.ToList())
            {
                DebugContainer current = debugLevels[key];
                current.enabled = CurrentLevel >= current.level;
                debugLevels[key] = current;
            }
        }
        public static void EnableType(string key)
        {
            if (debugLevels.ContainsKey(key))
            {
                DebugContainer current = debugLevels[key];
                current.enabled = true;
                debugLevels[key] = current;
            }
            else
            {
                debugLevels[key] = new DebugContainer { level = uint.MaxValue, enabled = true };
            }
        }
        public static string[] GetAllDebugKeys()
        {
            return [.. debugLevels.Keys];
        }
        public static bool IsDebugEnabled(string key)
        {
            return debugLevels.ContainsKey(key) && debugLevels[key].enabled;
        }
        public static void AddType(string key, uint level)
        {
            Debug.Assert(level > 0);
            if (debugLevels.ContainsKey(key))
            {
                DebugContainer current = debugLevels[key];
                current.level = level;
                debugLevels[key] = current;
                return;
            }
            debugLevels[key] = new DebugContainer { level = level, enabled = CurrentLevel >= level };
        }
    }
    public class TeeTextWriter(TextWriter console, TextWriter? log) : TextWriter
        {
        public override System.Text.Encoding Encoding => console.Encoding;

        public override void Write(char value)
        {
            console.Write(value);
            log?.Write(value);
        }

        public override void Write(string? value)
        {
            console.Write(value);
            log?.Write(value);
        }

        public override void WriteLine()
        {
            console.WriteLine();
            log?.WriteLine();
            Flush();
        }

        public override void WriteLine(string? value)
        {
            console.WriteLine(value);
            log?.WriteLine(value);
            Flush();
        }

        public override void Flush()
        {
            console.Flush();
            log?.Flush();
        }
    }
}
