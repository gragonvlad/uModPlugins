using System;
using ConVar;
using Facepunch;
using Facepunch.Math;
using UnityEngine;
using Time = UnityEngine.Time;

namespace Oxide.Plugins
{
    [Info("No Green", "Iv Misticos", "1.3.7")]
    [Description("Remove admins' green names")]
    class NoGreen : RustPlugin
    {
        private bool OnPlayerChat(ConsoleSystem.Arg arg, Chat.ChatChannel channel)
        {
            var player = arg.Player();
            var text = arg.GetString(0, "text").Replace("\n", "").Replace("\r", "").Trim().EscapeRichText();
            
            if (Chat.serverlog)
            {
                ServerConsole.PrintColoured(ConsoleColor.DarkYellow, string.Concat("[", channel.ToString(), "] ", player.displayName, ": "), ConsoleColor.DarkGreen, text);
                DebugEx.Log(channel == Chat.ChatChannel.Team
                    ? $"[TEAM CHAT] {player} : {text}"
                    : $"[CHAT] {player} : {text}");
            }
            
            var color = "#5af";
            var displayName = player.displayName.EscapeRichText();
            
            player.NextChatTime = Time.realtimeSinceStartup + 1.5f;
            
            var chatEntry = new Chat.ChatEntry
            {
                Channel = channel,
                Message = text,
                UserId = player.UserIDString,
                Username = player.displayName,
                Color = color,
                Time = Epoch.Current
            };
            
            RCon.Broadcast(RCon.LogType.Chat, chatEntry);
            
            if (channel != Chat.ChatChannel.Global)
            {
                if (channel == Chat.ChatChannel.Team)
                {
                    var team = arg.Player().Team;
                    var list = team?.GetOnlineMemberConnections();
                    if (list == null)
                    {
                        return false;
                    }
                    
                    ConsoleNetwork.SendClientCommand(list, "chat.add2", new object[]
                    {
                        1,
                        player.userID,
                        text,
                        displayName,
                        color,
                        1f
                    });
                    
                    return false;
                }
            }
            else if (ConVar.Server.globalchat)
            {
                ConsoleNetwork.BroadcastToAllClients("chat.add2", 0, player.userID, text, displayName, color, 1f);
                return false;
            }
            
            var radius = 2500f;
            foreach (var basePlayer2 in BasePlayer.activePlayerList)
            {
                var sqrMagnitude = (basePlayer2.transform.position - player.transform.position).sqrMagnitude;
                if (sqrMagnitude <= radius)
                {
                    ConsoleNetwork.SendClientCommand(basePlayer2.net.connection, "chat.add2", 0, player.userID, text, displayName, color, Mathf.Clamp01(radius - sqrMagnitude + 0.2f));
                }
            }

            return false;
        }
    }
}