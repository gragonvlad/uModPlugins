using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Group Limits", "Kappasaurrus & misticos", "2.0.2")]
    [Description("Prevent rulebreakers from breaking group limits on your server and notify your staff")]
    class GroupLimits : RustPlugin
    {
        [PluginReference]
        private Plugin Slack, DiscordMessages;

        #region Configuration

        private struct Configuration
        {
            public static bool EnforceCupboards = true;
            public static bool EnforceLocks = true;
            public static bool EnforceTurrets = true;

            public static bool SendToDiscord = false;
            public static bool SendToSlack = false;
            public static bool SendToStaff = true;
            public static bool WarnPlayers = true;

            public static int MaxPlayers = 5;
            
            public static int DiscordEmbedColor = 7506394;
            public static string DiscordEmbedTitle = "Group Limits";
            public static string DiscordWebhook = string.Empty;
        }

        private new void LoadConfig()
        {
            GetConfig(ref Configuration.EnforceCupboards, "Enforcement settings", "Cupboards");
            GetConfig(ref Configuration.EnforceLocks, "Enforcement settings", "Locks");
            GetConfig(ref Configuration.EnforceTurrets, "Enforcement settings", "Turrets");

            GetConfig(ref Configuration.SendToDiscord, "Messages", "Send to Discord");
            GetConfig(ref Configuration.SendToSlack, "Messages", "Send to Slack");
            GetConfig(ref Configuration.SendToStaff, "Messages", "Notify staff");
            GetConfig(ref Configuration.WarnPlayers, "Messages", "Warn players");

            GetConfig(ref Configuration.MaxPlayers, "Other settings", "Max players");
            
            GetConfig(ref Configuration.DiscordEmbedColor, "Discord settings", "Embed Color");
            GetConfig(ref Configuration.DiscordEmbedTitle, "Discord settings", "Embed Title");
            GetConfig(ref Configuration.DiscordWebhook, "Discord settings", "Webhook");
    
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new config file...");

        #endregion

        #region Helpers

        private void GetConfig<T>(ref T variable, params string[] path)
        {
            if (path.Length == 0)
            {
                return;
            }

            if (Config.Get(path) == null)
            {
                Config.Set(path.Concat(new object[] { variable }).ToArray());
                PrintWarning($"Added field to config: {string.Join("/", path)}");
            }

            variable = (T)Convert.ChangeType(Config.Get(path), typeof(T));
        }

        private bool HasPermission(BasePlayer player, string perm = "grouplimits.exclude") => permission.UserHasPermission(player.UserIDString, perm);

        private string FormattedCoordinates(BasePlayer player) => $"{player.transform.position.x},{player.transform.position.y},{player.transform.position.z}";

        private void ProcessConfiguration(BasePlayer player, string type)
        {
            var entityName = type.Contains("cupboard") ? "Tool Cupboard" : (type.Contains("lock") ? "Code Lock" : "Turret");

            if (Configuration.SendToDiscord && DiscordMessages)
            {
                object fields = new[]
                {
                    new
                    {
                        name = "Player", value = $"{player.displayName} ({player.UserIDString})", inline = true
                    },
                    new
                    {
                        name = "Coordinates", value = $"teleportpos {FormattedCoordinates(player)}", inline = true
                    },
                    new
                    {
                        name = "Violation Type", value = $"{entityName}", inline = true
                    }
                };

                DiscordMessages?.Call("API_SendFancyMessage", Configuration.DiscordWebhook,
                    Configuration.DiscordEmbedTitle, Configuration.DiscordEmbedColor,
                    JsonConvert.SerializeObject(fields));
            }

            if (Configuration.SendToSlack && Slack)
            {
                Slack.CallHook("Message", Lang("slackMessage")
                    .Replace("{player}", player.displayName)
                    .Replace("{steamID}", player.UserIDString)
                    .Replace("{type}", entityName)
                    .Replace("{coordinates}", FormattedCoordinates(player)));
            }

            if (Configuration.SendToStaff)
            {
                foreach (var target in BasePlayer.activePlayerList.Where(x => x.IsAdmin || HasPermission(x, "grouplimits.notify")))
                {
                    PrintToChat(target, Lang("staffMessage")
                        .Replace("{player}", player.displayName)
                        .Replace("{steamID}", player.UserIDString)
                        .Replace("{type}", entityName)
                        .Replace("{coordinates}", FormattedCoordinates(player)));
                }
            }

            if (!Configuration.WarnPlayers)
            {
                return;
            }

            PrintToChat(player, Lang("playerMessage").Replace("{limit}", Configuration.MaxPlayers.ToString()));
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["slackMessage"] = ">{player} ({steamID}) tried to authorize on a {type} at coordinates ({coordinates}) but broke the group limit.",
                ["staffMessage"] = "{player} ({steamID}) tried to authorize on a {type} at coordinates ({coordinates}) but broke the group limit.",
                ["logMessage"] = "[{time}] {player} ({steamID}) tried to authorize at coordinates ({coordinates}) but broke the group limit.",
                ["playerMessage"] = "Error, you're exceeding the {limit} player group limit."
            }, this);
        }

        private string Lang(string key, BasePlayer player = null) => lang.GetMessage(key, this, player?.UserIDString);

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            LoadConfig();
            permission.RegisterPermission("grouplimits.notify", this);
            permission.RegisterPermission("grouplimits.exclude", this);
        }

        private object OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (HasPermission(player))
            {
                return null;
            }

            if (!Configuration.EnforceCupboards)
            {
                return null;
            }

            if (privilege.authorizedPlayers.Count < Configuration.MaxPlayers)
            {
                return null;
            }

            ProcessConfiguration(player, "cupboard");
            LogToFile("Cupboard", Lang("logMessage")
                .Replace("{time}", DateTime.UtcNow.ToShortDateString())
                .Replace("{player}", player.displayName)
                .Replace("{steamID}", player.UserIDString)
                .Replace("{coordinates}", FormattedCoordinates(player)), this);

            return false;
        }

        private object OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
        {
            if (HasPermission(player))
            {
                return null;
            }

            if (!Configuration.EnforceLocks)
            {
                return null;
            }

            if (!(code == codeLock.guestCode || code == codeLock.code))
            {
                return null;
            }

            if (codeLock.whitelistPlayers.Count + codeLock.guestPlayers.Count < Configuration.MaxPlayers)
            {
                return null;
            }

            ProcessConfiguration(player, "lock");
            LogToFile("CodeLock", Lang("logMessage")
                .Replace("{time}", DateTime.UtcNow.ToShortDateString())
                .Replace("{player}", player.displayName)
                .Replace("{steamID}", player.UserIDString)
                .Replace("{coordinates}", FormattedCoordinates(player)), this);

            return false;
        }

        private object OnTurretAuthorize(AutoTurret turret, BasePlayer player)
        {
            if (HasPermission(player))
            {
                return null;
            }

            if (!Configuration.EnforceTurrets)
            {
                return null;
            }

            if (turret.authorizedPlayers.Count < Configuration.MaxPlayers)
            {
                return null;
            }

            ProcessConfiguration(player, "turret");
            LogToFile("Turret", Lang("logMessage")
                .Replace("{time}", DateTime.UtcNow.ToShortDateString())
                .Replace("{player}", player.displayName)
                .Replace("{steamID}", player.UserIDString)
                .Replace("{coordinates}", FormattedCoordinates(player)), this);

            return false;

        }

        #endregion
    }
}