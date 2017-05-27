using System;
using System.Collections.Generic;
using Hacknet;
using Microsoft.Xna.Framework;
using Pathfinder.Event;
using Pathfinder.OS;
using ModOptions = Pathfinder.GUI.ModOptions;

namespace Pathfinder.Internal
{
    static class HandlerListener
    {
        public static void CommandListener(CommandSentEvent e)
        {
            Command.Handler.CommandFunc f;
            if (Command.Handler.ModCommands.TryGetValue(e[0], out f))
            {
                e.IsCancelled = true;
                try
                {
                    e.Disconnects = f(e.OS, e.Arguments);
                }
                catch (Exception ex)
                {
                    e.OS.WriteF("Command {0} threw Exception:\n    {1}('{2}')", e.Arguments[0], ex.GetType().FullName, ex.Message);
                    throw ex;
                }
            }
        }

        public static void DaemonLoadListener(LoadComputerXmlReadEvent e)
        {
            Daemon.IInterface i;
            var id = e.Reader.GetAttribute("interfaceId");
            if (id != null && Daemon.Handler.ModDaemons.TryGetValue(id, out i))
            {
                var objs = new Dictionary<string, string>();
                var storedObjects = e.Reader.GetAttribute("storedObjects")?.Split(' ');
                if (storedObjects != null)
                    foreach (var s in storedObjects)
                        objs[s.Remove(s.IndexOf('|'))] = s.Substring(s.IndexOf('|') + 1);
                e.Computer.daemons.Add(Daemon.Instance.CreateInstance(id, e.Computer, objs));
            }
        }

        public static void ExecutableListener(ExecutableExecuteEvent e)
        {
            Tuple<Executable.IInterface, string> tuple;
            if (Executable.Handler.IsFileDataForModExe(e.ExecutableFile.data)
                && Executable.Handler.ModExecutables.TryGetValue(e.ExecutableFile.data.Split('\n')[0], out tuple))
            {
                int num = e.OS.ram.bounds.Y + RamModule.contentStartOffset;
                foreach (var exe in e.OS.exes)
                    num += exe.bounds.Height;
                var location = new Rectangle(e.OS.ram.bounds.X, num, RamModule.MODULE_WIDTH, (int)Hacknet.OS.EXE_MODULE_HEIGHT);
                e.OS.addExe(Executable.Instance.CreateInstance(tuple.Item1, e.ExecutableFile, e.OS, e.Arguments, location));
                e.Result = Executable.ExecutionResult.StartupSuccess;
            }
        }

        public static void OptionsMenuLoadContentListener(OptionsMenuLoadContentEvent e)
        {
            foreach (var o in ModOptions.Handler.ModOptions)
                o.Value.LoadContent(e.OptionsMenu);
        }

        public static void OptionsMenuApplyListener(OptionsMenuApplyEvent e)
        {
            foreach (var o in ModOptions.Handler.ModOptions)
                o.Value.Apply(e.OptionsMenu);
        }

        public static void OptionsMenuUpdateListener(OptionsMenuUpdateEvent e)
        {
            foreach (var o in ModOptions.Handler.ModOptions)
                o.Value.Update(e.OptionsMenu, e.GameTime, e.ScreenNotFocused, e.ScreenIsCovered);
        }

        private static string selected;
        public static void OptionsMenuDrawListener(OptionsMenuDrawEvent e)
        {
            if (selected == null || !ModOptions.Handler.ModOptions.ContainsKey(selected)) return;
            e.IsCancelled = true;
            ModOptions.Handler.ModOptions[selected].Draw(e.OptionsMenu, e.GameTime);
        }
    }
}
