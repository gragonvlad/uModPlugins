using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Monument Teleporter", "Iv Misticos", "1.0.0")]
    [Description("Teleport to monuments found with Monument Finder")]
    class MonumentTeleporter : RustPlugin
    {
        #region Variables

        // ReSharper disable once InconsistentNaming
        [PluginReference]
        #pragma warning disable 649
        private Plugin MonumentFinder;
        #pragma warning restore 649
        
        #endregion
        
        #region Configuration
        
        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Usage Permission")]
            public string Permission = "monumentteleporter.use";
            
            [JsonProperty(PropertyName = "Command")]
            public string Command = "mtp";
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
                {"No Permission", "You don't have enough permissions"},
                {"Monument Finder Not Found", "Monument Finder isn't loaded"},
                {"Monument Not Found", "Monument not found"},
                {"Position Not Found", "Position to spawn not found"},
                {"Teleported", "You were teleported"},
                {"Only Players", "Only players are allowed to use this command"}
            }, this);
        }

        private void Init()
        {
            if (!string.IsNullOrEmpty(_config.Permission))
                permission.RegisterPermission(_config.Permission, this);
            
            AddCovalenceCommand(_config.Command, nameof(CommandTeleport));
        }
        
        #endregion
        
        #region Commands

        private void CommandTeleport(IPlayer player, string command, string[] args)
        {
            if (!string.IsNullOrEmpty(_config.Permission) && !player.HasPermission(_config.Permission))
            {
                player.Reply(GetMsg("No Permission", player.Id));
                return;
            }

            if (player.Object == null)
            {
                player.Reply(GetMsg("Only Players", player.Id));
                return;
            }

            if (MonumentFinder == null || !MonumentFinder.IsLoaded)
            {
                player.Reply(GetMsg("Monument Finder Not Found", player.Id));
                return;
            }

            var filter = args == null || args.Length != 1 ? string.Empty : args[0];
            var monuments = MonumentFinder.Call<List<MonumentInfo>>("FindMonuments", filter);
            if (monuments == null || monuments.Count == 0)
            {
                player.Reply(GetMsg("Monument Not Found", player.Id));
                return;
            }
            
            var pos = monuments[0].transform.position;
            RaycastHit hit;
            if (!Physics.Raycast(new Ray(pos + Vector3.up * 10, Vector3.down), out hit))
            {
                player.Reply(GetMsg("Position Not Found", player.Id));
                return;
            }

            pos = hit.point;
            
            player.Teleport(pos.x, pos.y, pos.z);
            player.Reply(GetMsg("Teleported", player.Id));
        }
        
        #endregion
        
        #region Helpers

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
    }
}