using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Monument Finder", "misticos", "2.0.1")]
    [Description("Find monuments with commands or API")]
    class MonumentFinder : CovalencePlugin
    {
        #region Variables

        private const string PermissionFind = "monumentfinder.find";
        
        #endregion
        
        #region Configuration
        
        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Command")]
            public string Command = "mf";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();
        
        #endregion
        
        #region Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"No Permission", "You don't have enough permissions (monumentfinder.find)"},
                {"Find: Syntax", "Syntax: mf\n" +
                                 "list - List all available monuments and their centers\n" +
                                 "show (Name) - Show specific monument info"},
                {"Find: Header", "Showing monuments:"},
                {"Find: Entry", "{name} ({type}) @ {position}"}
            }, this);
        }

        private void Init()
        {
            permission.RegisterPermission(PermissionFind, this);

            AddCovalenceCommand(_config.Command, nameof(CommandFind));
        }
        
        #endregion
        
        #region Commands

        private void CommandFind(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PermissionFind))
            {
                player.Reply(GetMsg("No Permission", player.Id));
                return;
            }

            if (args == null || args.Length == 0)
            {
                goto syntax;
            }

            switch (args[0].ToLower())
            {
                case "list":
                case "l":
                {
                    PrintMonumentList(player, FindMonuments(string.Empty));
                    break;
                }

                case "show":
                case "s":
                {
                    if (args.Length != 2)
                    {
                        goto syntax;
                    }

                    PrintMonumentList(player, FindMonuments(args[1]));
                    break;
                }

                default:
                {
                    goto syntax;
                }
            }

            return;
            
            syntax:
            player.Reply(GetMsg("Find: Syntax", player.Id));
        }
        
        #endregion
        
        #region Helpers

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        private List<MonumentInfo> FindMonuments(string filter)
        {
            var monuments = new List<MonumentInfo>();

            foreach (var monument in TerrainMeta.Path.Monuments)
            {
                if (!monument.name.Contains("/monument/") || !string.IsNullOrEmpty(filter) &&
                    !monument.Type.ToString().Contains(filter, CompareOptions.IgnoreCase) &&
                    !monument.name.Contains(filter, CompareOptions.IgnoreCase))
                    continue;
                        
                monuments.Add(monument);
            }

            return monuments;
        }

        private void PrintMonumentList(IPlayer player, IEnumerable<MonumentInfo> monuments)
        {
            player.Reply(GetMsg("Find: Header", player.Id));

            var builder = new StringBuilder();
            foreach (var monument in monuments)
            {
                builder.Length = 0;
                builder.Append(GetMsg("Find: Entry", player.Id));

                builder.Replace("{name}", monument.name);
                builder.Replace("{type}", monument.Type.ToString());
                builder.Replace("{position}", monument.transform.position.ToString());
                
                player.Reply(builder.ToString());
            }
        }
        
        #endregion
    }
}