﻿#pragma warning disable CA1416
using Dalamud.Game.Command;
using Dalamud.Plugin;
using DeepDungeonDex.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static Dalamud.Game.Command.CommandInfo;
// ReSharper disable ForCanBeConvertedToForeach

namespace DeepDungeonDex
{
    public class PluginCommandManager<THost> : IDisposable
    {
        private readonly DalamudPluginInterface _pluginInterface;
        private readonly (string, CommandInfo)[] _pluginCommands;
        private readonly THost _host;
        private readonly CommandManager _commands;

        public PluginCommandManager(THost host, DalamudPluginInterface pluginInterface, CommandManager commands)
        {
            this._pluginInterface = pluginInterface;
            this._host = host;
            this._commands = commands;

            this._pluginCommands = host.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                .Where(method => method.GetCustomAttribute<CommandAttribute>() != null)
                .SelectMany(GetCommandInfoTuple)
                .ToArray();

            AddCommandHandlers();
        }

        // http://codebetter.com/patricksmacchia/2008/11/19/an-easy-and-efficient-way-to-improve-net-code-performances/
        // Benchmarking this myself gave similar results, so I'm doing this to somewhat counteract using reflection to access command attributes.
        // I like the convenience of attributes, but in principle it's a bit slower to use them as opposed to just initializing CommandInfos directly.
        // It's usually sub-1 millisecond anyways, though. It probably doesn't matter at all.
        private void AddCommandHandlers()
        {
            for (var i = 0; i < this._pluginCommands.Length; i++)
            {
                var (command, commandInfo) = this._pluginCommands[i];
                this._commands.AddHandler(command, commandInfo);
            }
        }

        private void RemoveCommandHandlers()
        {
            for (var i = 0; i < this._pluginCommands.Length; i++)
            {
                var (command, _) = this._pluginCommands[i];
                this._commands.RemoveHandler(command);
            }
        }

        private IEnumerable<(string, CommandInfo)> GetCommandInfoTuple(MethodInfo method)
        {
            var handlerDelegate = (HandlerDelegate)Delegate.CreateDelegate(typeof(HandlerDelegate), this._host, method);

            var command = handlerDelegate.Method.GetCustomAttribute<CommandAttribute>();
            var aliases = handlerDelegate.Method.GetCustomAttribute<AliasesAttribute>();
            var helpMessage = handlerDelegate.Method.GetCustomAttribute<HelpMessageAttribute>();
            var doNotShowInHelp = handlerDelegate.Method.GetCustomAttribute<DoNotShowInHelpAttribute>();

            var commandInfo = new CommandInfo(handlerDelegate)
            {
                HelpMessage = helpMessage?.HelpMessage ?? string.Empty,
                ShowInHelp = doNotShowInHelp == null,
            };

            // Create list of tuples that will be filled with one tuple per alias, in addition to the base command tuple.
            var commandInfoTuples = new List<(string, CommandInfo)> { (command.Command, commandInfo) };
            if (aliases == null) return commandInfoTuples;
            // ReSharper disable once LoopCanBeConvertedToQuery
            for (var i = 0; i < aliases.Aliases.Length; i++)
            {
                commandInfoTuples.Add((aliases.Aliases[i], commandInfo));
            }

            return commandInfoTuples;
        }

        public void Dispose()
        {
            RemoveCommandHandlers();
        }
    }
}
#pragma warning restore CA1416