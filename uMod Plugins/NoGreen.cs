using System;
using ConVar;
using Facepunch;
using Facepunch.Math;
using UnityEngine;
using Time = UnityEngine.Time;

namespace Oxide.Plugins
{
    [Info("No Green", "Iv Misticos", "1.3.6")]
    [Description("Remove admins' green names")]
    class NoGreen : RustPlugin
    {
        private bool OnPlayerChat(ConsoleSystem.Arg arg)
        {
            if (!Chat.enabled)
            {
                arg.ReplyWith("Chat is disabled.");
                return true;
            }
            
            var basePlayer = arg.Player();
            var chatChannel = (Chat.ChatChannel)arg.GetInt(0);
            var text = arg.GetString(1, "text").Replace("\n", "").Replace("\r", "").Trim();
			
            if (text.Length > 128)
            {
                text = text.Substring(1, 128);
            }
			
            if (text.Length <= 0)
            {
                return true;
            }
			
            if (Chat.serverlog)
            {
                ServerConsole.PrintColoured(ConsoleColor.DarkYellow, string.Concat("[", chatChannel.ToString(), "] ", basePlayer.displayName, ": "), ConsoleColor.DarkGreen, text);
                DebugEx.Log(chatChannel == Chat.ChatChannel.Team
                    ? $"[TEAM CHAT] {basePlayer} : {text}"
                    : $"[CHAT] {basePlayer} : {text}");
            }
				
            var nameColor = "#5af";
            var name = basePlayer.displayName.EscapeRichText();
            basePlayer.NextChatTime = Time.realtimeSinceStartup + 1.5f;
            var chatEntry = new Chat.ChatEntry
            {
                Channel = chatChannel,
                Message = text,
                UserId = basePlayer.UserIDString,
                Username = basePlayer.displayName,
                Color = nameColor,
                Time = Epoch.Current
            };
				
            RCon.Broadcast(RCon.LogType.Chat, chatEntry);
				
            if (chatChannel != Chat.ChatChannel.Global)
            {
                if (chatChannel == Chat.ChatChannel.Team)
                {
                    var team = arg.Player().Team;
						
                    var list = team?.GetOnlineMemberConnections();
                    if (list == null)
                    {
                        return true;
                    }
						
                    ConsoleNetwork.SendClientCommand(list, "chat.add2", 1, basePlayer.userID, text, name, nameColor, 1f);
						
                    return true;
                }
            }
            else if (ConVar.Server.globalchat)
            {
                ConsoleNetwork.BroadcastToAllClients("chat.add2", 0, basePlayer.userID, text, name, nameColor, 1f);
                arg.ReplyWith("");
                return true;
            }
				
            var radius = 2500f;
            foreach (var basePlayer2 in BasePlayer.activePlayerList)
            {
                var sqrMagnitude = (basePlayer2.transform.position - basePlayer.transform.position).sqrMagnitude;
                if (sqrMagnitude <= radius)
                {
                    ConsoleNetwork.SendClientCommand(basePlayer2.net.connection, "chat.add2", 0, basePlayer.userID, text, name, nameColor, Mathf.Clamp01(radius - sqrMagnitude + 0.2f));
                }
            }
				
            arg.ReplyWith("");
            return true;
        }
    }
}