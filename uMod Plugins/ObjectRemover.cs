﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using Time = UnityEngine.Time;

namespace Oxide.Plugins
{
    [Info("Object Remover", "Iv Misticos", "3.0.4")]
    [Description("Removes furnaces, lanterns, campfires, buildings etc. on command")]
    class ObjectRemover : RustPlugin
    {
        #region Variables
        
        private const string ShortnameCupboard = "cupboard.tool";

        #endregion

        #region Configuration

        private Configuration _config = new Configuration();

        private class Configuration
        {
            [JsonProperty(PropertyName = "Object Command Permission")]
            public string PermissionUse = "objectremover.use";
            
            [JsonProperty(PropertyName = "Object Command")]
            public string Command = "object";
            
            public string Prefix = "[<color=#ffbf00> Object Remover </color>] ";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion
        
        #region Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "No Rights", "You do not have enough rights" },
                { "Count", "We found {count} entities in {time}s." },
                { "Removed", "You have removed {count} entities in {time}s." },
                { "Help", "Object command usage:\n" +
                          "/object (entity) [parameters]\n" +
                          "Parameters:\n" +
                          "action count/remove - Count or remove entities\n" +
                          "radius NUM - Radius\n" +
                          "inside true/false - Entities inside the cupboard\n" +
                          "outside true/false - Entities outside the cupboard" },
                { "No Console", "Please log in as a player to use that command" }
            }, this);
        }

        private void OnServerInitialized()
        {
            LoadDefaultMessages();
            LoadConfig();
            
            if (!permission.PermissionExists(_config.PermissionUse))
                permission.RegisterPermission(_config.PermissionUse, this);

            AddCovalenceCommand(_config.Command, "CommandObject");
        }
        
        #endregion

        #region Commands

        private void CommandObject(IPlayer player, string command, string[] args)
        {
            var id = player.Id;
            var prefix = player.IsServer ? string.Empty : _config.Prefix;
            if (!player.IsServer && !permission.UserHasPermission(id, _config.PermissionUse))
            {
                player.Reply(prefix + GetMsg("No Rights", id));
                return;
            }

            if (args.Length < 1)
            {
                player.Reply(prefix + GetMsg("Help", id));
                return;
            }

            var options = new RemoveOptions();
            options.Parse(args);
            if (player.IsServer)
                options.Radius = 0f;
            
            var entity = args[0];
            var position = (player.Object as BasePlayer)?.transform.position ?? Vector3.zero;
            var before = Time.realtimeSinceStartup;
            var objects = FindObjects(position, entity, options);
            var count = objects.Count;

            if (!options.Count)
            {
                for (var i = 0; i < count; i++)
                {
                    var ent = objects[i];
                    if (ent == null || ent.IsDestroyed)
                        continue;
                    ent.Kill();
                }
            }

            player.Reply(prefix + GetMsg(options.Count ? "Count" : "Removed", id)
                                   .Replace("{count}", count.ToString()).Replace("{time}",
                                       (Time.realtimeSinceStartup - before).ToString("0.###")));
        }

//        private void CommandChatObject(BasePlayer player, string command, string[] args)
//        {
//            var id = player.UserIDString;
//            if (!permission.UserHasPermission(id, _config.PermissionUse))
//            {
//                player.ChatMessage(_config.Prefix + GetMsg("No Rights", id));
//                return;
//            }
//
//            if (args.Length < 1)
//            {
//                player.ChatMessage(_config.Prefix + GetMsg("Help", id));
//                return;
//            }
//
//            var options = new RemoveOptions();
//            options.Parse(args);
//            
//            var entity = args[0];
//            var before = Time.realtimeSinceStartup;
//            var objects = FindObjects(player.transform.position, entity, options);
//            var count = objects.Count;
//
//            if (!options.Count)
//            {
//                for (var i = 0; i < count; i++)
//                {
//                    var ent = objects[i];
//                    if (ent == null || ent.IsDestroyed)
//                        continue;
//                    ent.Kill();
//                }
//            }
//
//            player.ChatMessage(_config.Prefix + GetMsg(options.Count ? "Count" : "Removed", id)
//                                   .Replace("{count}", count.ToString()).Replace("{time}",
//                                       (Time.realtimeSinceStartup - before).ToString("0.###")));
//        }
//        
//        private bool CommandConsoleObject(ConsoleSystem.Arg arg)
//        {
//            var player = arg.Player();
//            if (player == null)
//            {
//                arg.ReplyWith(GetMsg("No Console"));
//                return true;
//            }
//            
//            CommandChatObject(player, string.Empty, arg.Args ?? new string[0]);
//            return false;
//        }

        #endregion
        
        #region Remove Options

        private class RemoveOptions
        {
            public float Radius = 0f;
            public bool Count = true;
            public bool OutsideCupboard = true;
            public bool InsideCupboard = true;

            public void Parse(string[] args)
            {
                for (var i = 1; i + 1 < args.Length; i += 2)
                {
                    switch (args[i])
                    {
                        case "a":
                        case "action":
                        {
                            Count = args[i + 1] != "remove";

                            break;
                        }
                        
                        case "r":
                        case "radius":
                        {
                            if (!float.TryParse(args[i + 1], out Radius))
                                Radius = 0f;

                            break;
                        }

                        case "oc":
                        case "outside":
                        {
                            if (!bool.TryParse(args[i + 1], out OutsideCupboard))
                                OutsideCupboard = true;
                            
                            break;
                        }

                        case "ic":
                        case "inside":
                        {
                            if (!bool.TryParse(args[i + 1], out InsideCupboard))
                                InsideCupboard = true;

                            break;
                        }
                    }
                }
            }
        }
        
        #endregion

        #region Helpers

        private List<BaseEntity> FindObjects(Vector3 startPos, string entity, RemoveOptions options)
        {
            var entities = new List<BaseEntity>();
            var isAll = entity.Equals("all");
            if (options.Radius > 0)
            {
                var entitiesList = new List<BaseEntity>();
                Vis.Entities(startPos, options.Radius, entitiesList);
                var entitiesCount = entitiesList.Count;
                for (var i = entitiesCount - 1; i >= 0; i--)
                {
                    var ent = entitiesList[i];

                    if (IsNeededObject(ent, entity, isAll, options))
                        entities.Add(ent);
                }
            }
            else
            {
                var ents = UnityEngine.Object.FindObjectsOfType<BaseEntity>();
                var entsCount = ents.Length;
                for (var i = 0; i < entsCount; i++)
                {
                    var ent = ents[i];
                    if (IsNeededObject(ent, entity, isAll, options))
                        entities.Add(ent);
                }
            }

            return entities;
        }

        private bool IsNeededObject(BaseEntity entity, string shortname, bool isAll, RemoveOptions options)
        {
            return (isAll || entity.ShortPrefabName.IndexOf(shortname, StringComparison.CurrentCultureIgnoreCase) != -1) &&
                   (options.InsideCupboard && entity.GetBuildingPrivilege() != null ||
                    options.OutsideCupboard && entity.GetBuildingPrivilege() == null);
        }

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
    }
}