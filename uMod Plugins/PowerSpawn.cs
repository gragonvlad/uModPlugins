using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Rust;
using UnityEngine;
using Random = System.Random;

namespace Oxide.Plugins
{
    [Info("Power Spawn", "Iv Misticos", "1.2.0")]
    [Description("Control spawns of players and other plugins stuff")]
    class PowerSpawn : RustPlugin
    {
        #region Variables

        private static PowerSpawn _ins;

        private readonly Random _random = new Random();

        private Coroutine _coroutine = null;

        private int _worldSize;
        private readonly int _layerTerrain = LayerMask.NameToLayer("Terrain");

        private List<Vector3> _positions = new List<Vector3>();

        private List<BuildingBlock> _listBuildingBlocks = new List<BuildingBlock>();
        private List<Collider> _listColliders = new List<Collider>();
        
        #endregion
        
        #region Configuration

        private static Configuration _config;
        
        private class Configuration
        {
            [JsonProperty(PropertyName = "Minimal Distance To Building")]
            public int DistanceBuilding = 10;

            [JsonProperty(PropertyName = "Minimal Distance To Collider")]
            public int DistanceCollider = 10;

            [JsonProperty(PropertyName = "Number Of Attempts To Find A Position Per Frame")]
            public int AttemptsPerFrame = 250;

            [JsonProperty(PropertyName = "Number Of Attempts To Find A Pregenerated Position")]
            public int AttemptsPregenerated = 250;

            [JsonProperty(PropertyName = "Pregenerated Positions Amount")]
            public int PregeneratedAmount = 1000;

            [JsonProperty(PropertyName = "Pregenerated Amount Check Frequency (Seconds)")]
            public float PregeneratedCheck = 60f;

            [JsonProperty(PropertyName = "Respawn Locations Group")]
            public int RespawnGroup = -2;

            [JsonProperty(PropertyName = "Enable Respawn Locations Group")]
            public bool EnableRespawnGroup = false;

            [JsonProperty(PropertyName = "Location Management Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] LocationCommand = {"loc", "location", "ps"};

            [JsonProperty(PropertyName = "Location Management Permission")]
            public string LocationPermission = "powerspawn.location";

            [JsonProperty(PropertyName = "Debug")]
            public bool Debug = false;
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

        #region Work with Data

        private static PluginData _data = new PluginData();

        private class PluginData
        {
            public List<Location> Locations = new List<Location>();

            // ReSharper disable once MemberCanBePrivate.Local
            public int LastID = 0;
            
            public class Location
            {
                public string Name;
                public int ID = _data.LastID++;
                public int Group = -1;
                public Vector3 Position;

                public string Format(string player)
                {
                    var text = new StringBuilder(GetMsg("Location: Format", player));
                    text.Replace("{name}", Name);
                    text.Replace("{id}", ID.ToString());
                    text.Replace("{group}", Group.ToString());
                    text.Replace("{position}", Position.ToString());

                    return text.ToString();
                }

                public static int? FindIndex(int id)
                {
                    for (var i = 0; i < _data.Locations.Count; i++)
                    {
                        if (_data.Locations[i].ID == id)
                            return i;
                    }

                    return null;
                }

                public static IEnumerable<Location> FindByGroup(int group)
                {
                    for (var i = 0; i < _data.Locations.Count; i++)
                    {
                        var location = _data.Locations[i];
                        if (location.Group == group)
                            yield return location;
                    }
                }
            }
        }

        private static void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(_ins.Name, _data);

        private void LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_data == null) _data = new PluginData();
        }
        
        #endregion
        
        #region Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "No Permission", "nope" },
                { "Location: Syntax", "Location Syntax:\n" +
                                      "new (Name) - Create a new location with a specified name\n" +
                                      "delete (ID) - Delete a location with the specified ID\n" +
                                      "edit (ID) <Parameter 1> <Value> <...> - Edit a location with the specified ID\n" +
                                      "update - Apply datafile changes\n" +
                                      "list - Get a list of locations\n" +
                                      "validate (ID) - Validate location for buildings and colliders" },
                { "Location: Edit Syntax", "Location Edit Parameters:\n" +
                                           "move (x;y;z / here) - Move a location to the specified position\n" +
                                           "group (ID / reset) - Set group of a location or reset the group" },
                { "Location: Unable To Parse Position", "Unable to parse the position" },
                { "Location: Unable To Parse Group", "Unable to parse the entered group" },
                { "Location: Format", "Location ID: {id}; Group: {group}; Position: {position}; Name: {name}" },
                { "Location: Not Found", "Location was not found." },
                { "Location: Edit Finished", "Edit was finished." },
                { "Location: Removed", "Location was removed from our database." },
                { "Location: Updated", "Datafile changes were applied." },
                { "Location: Validation Format", "Buildings invalid: {buildings}; Colliders invalid: {colliders}" }
            }, this);
        }

        private void Init()
        {
            _ins = this;
            _worldSize = ConVar.Server.worldsize;
            LoadData();

            permission.RegisterPermission(_config.LocationPermission, this);
            AddCovalenceCommand(_config.LocationCommand, nameof(CommandLocation));
        }

        private void OnServerInitialized()
        {
            _coroutine = InvokeHandler.Instance.StartCoroutine(PositionGeneration());
        }

        private void Unload()
        {
            InvokeHandler.Instance.StopCoroutine(_coroutine);
        }

        private object OnPlayerRespawn(BasePlayer player)
        {
            Vector3? position;
            if (_config.EnableRespawnGroup)
            {
                var positions = new List<PluginData.Location>(PluginData.Location.FindByGroup(_config.RespawnGroup));
                position = positions[_random.Next(0, positions.Count)].Position;
            }
            else
            {
                position = FindPregeneratedPosition();
            }

            if (!position.HasValue)
            {
                PrintDebug($"Haven't found a position for {player.displayName}");
                return null;
            }
            
            PrintDebug($"Found position for {player.displayName}: {position}");
            
            return new BasePlayer.SpawnPoint
            {
                pos = position.Value
            };
        }

        #endregion
        
        #region Commands

        private void CommandLocation(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(_config.LocationPermission))
            {
                player.Reply(GetMsg("No Permission", player.Id));
                return;
            }

            if (args.Length == 0)
            {
                goto syntax;
            }

            switch (args[0])
            {
                case "new":
                case "n":
                {
                    if (args.Length != 2)
                    {
                        goto syntax;
                    }

                    var location = new PluginData.Location
                    {
                        Name = args[1]
                    };

                    player.Position(out location.Position.x, out location.Position.y, out location.Position.z);
                    _data.Locations.Add(location);
                    
                    player.Reply(location.Format(player.Id));
                    goto saveData;
                }

                case "delete":
                case "remove":
                case "d":
                case "r":
                {
                    int id;
                    if (args.Length != 2 || !int.TryParse(args[1], out id))
                    {
                        goto syntax;
                    }

                    var locationIndex = PluginData.Location.FindIndex(id);
                    if (!locationIndex.HasValue)
                    {
                        player.Reply(GetMsg("Location: Not Found", player.Id));
                        return;
                    }
                    
                    _data.Locations.RemoveAt(locationIndex.Value);
                    player.Reply(GetMsg("Location: Removed", player.Id));
                    goto saveData;
                }

                case "edit":
                case "e":
                {
                    int id;
                    if (args.Length < 4 || !int.TryParse(args[1], out id))
                    {
                        player.Reply(GetMsg("Location: Edit Syntax", player.Id));
                        return;
                    }

                    var locationIndex = PluginData.Location.FindIndex(id);
                    if (!locationIndex.HasValue)
                    {
                        player.Reply(GetMsg("Location: Not Found", player.Id));
                        return;
                    }

                    var locationCD = new CommandLocationData
                    {
                        Player = player,
                        Location = _data.Locations[locationIndex.Value]
                    };
                    
                    locationCD.Apply(args);
                    player.Reply(GetMsg("Location: Edit Finished", player.Id));
                    goto saveData;
                }

                case "update":
                case "u":
                {
                    LoadData();
                    player.Reply(GetMsg("Location: Updated", player.Id));
                    return;
                }

                case "list":
                case "l":
                {
                    var table = new TextTable();
                    table.AddColumns("ID", "Name", "Group", "Position");

                    foreach (var location in _data.Locations)
                    {
                        table.AddRow(location.ID.ToString(), location.Name, location.Group.ToString(), location.Position.ToString());
                    }

                    player.Reply(table.ToString());
                    return;
                }

                case "valid":
                case "validate":
                case "v":
                {
                    int id;
                    if (args.Length < 2 || !int.TryParse(args[1], out id))
                    {
                        player.Reply(GetMsg("Location: Syntax", player.Id));
                        return;
                    }
                    
                    var locationIndex = PluginData.Location.FindIndex(id);
                    if (!locationIndex.HasValue)
                    {
                        player.Reply(GetMsg("Location: Not Found", player.Id));
                        return;
                    }

                    var location = _data.Locations[locationIndex.Value];
                    player.Reply(GetMsg("Location: Validation Format", player.Id)
                        .Replace("{buildings}", IsValidForBuilding(location.Position).ToString())
                        .Replace("{colliders}", IsValidForColliders(location.Position).ToString()));
                    return;
                }

                default:
                {
                    goto syntax;
                }
            }
            
            syntax:
            player.Reply(GetMsg("Location: Syntax", player.Id));
            return;
            
            saveData:
            SaveData();
        }

        private class CommandLocationData
        {
            public IPlayer Player;
            
            public PluginData.Location Location;
            
            private const int FirstArgumentIndex = 2;
            
            public void Apply(string[] args)
            {
                for (var i = FirstArgumentIndex; i + 1 < args.Length; i += 2)
                {
                    switch (args[i])
                    {
                        case "move":
                        {
                            var position = ParseVector(args[i + 1]);
                            if (!position.HasValue)
                            {
                                Player.Reply(GetMsg("Location: Unable To Parse Position", Player.Id));
                                break;
                            }

                            Location.Position = position.Value;
                            break;
                        }

                        case "group":
                        {
                            int group = -1;
                            if (args[i + 1] != "reset" && !int.TryParse(args[i + 1], out group))
                            {
                                Player.Reply(GetMsg("Location: Unable To Parse Group", Player.Id));
                                break;
                            }

                            Location.Group = group;
                            break;
                        }
                    }
                }

                SaveData();
            }

            private Vector3? ParseVector(string argument)
            {
                var vector = new Vector3();
                
                if (argument == "here")
                {
                    Player.Position(out vector.x, out vector.y, out vector.z);
                }
                else
                {
                    var coordinates = argument.Split(';');
                    if (coordinates.Length != 3 || !float.TryParse(coordinates[0], out vector.x) ||
                        !float.TryParse(coordinates[1], out vector.y) || !float.TryParse(coordinates[2], out vector.z))
                    {
                        return null;
                    }
                }
                
                return vector;
            }
        }
        
        #endregion
        
        #region API

        private Vector3? GetLocation(int id)
        {
            var locationIndex = PluginData.Location.FindIndex(id);
            if (!locationIndex.HasValue)
                return null;

            return _data.Locations[locationIndex.Value].Position;
        }

        private JObject GetGroupLocations(int group)
        {
            var locations = PluginData.Location.FindByGroup(group);
            return JObject.FromObject(locations);
        }

        private Vector3? GetPregeneratedLocation() => FindPregeneratedPosition();
        
        #endregion
        
        #region Helpers

        // ReSharper disable once IteratorNeverReturns
        private IEnumerator PositionGeneration()
        {
            Vector3? position = null;
            
            while (true)
            {
                if (_positions.Count >= _config.PregeneratedAmount)
                    yield return new WaitForSeconds(_config.PregeneratedCheck);

                var attempts = 0;
                while (attempts++ < _config.AttemptsPerFrame && !position.HasValue)
                {
                    position = TryFindPosition();
                }

                if (position.HasValue)
                {
                    _positions.Add(position.Value);
                    position = null; // Null it to prevent while loop skip
                }

                yield return null;
            }
        }

        private Vector3? FindPregeneratedPosition()
        {
            Vector3? position = null;
            for (var i = 0; i < _config.AttemptsPregenerated; i++)
            {
                if (_positions.Count <= 0)
                {
                    PrintDebug($"FindPregeneratedPosition: Positions were not generated");
                    return null;
                }

                var index = _random.Next(0, _positions.Count);
                position = _positions[index];
                
                PrintDebug($"FindPregeneratedPosition: Found {position.Value}");
                
                // If it's a "bad" position, continue trying to find a good one.
                if (IsValidForBuilding(position.Value) && IsValidForColliders(position.Value))
                    break;
                
                // Remove invalid position
                _positions.RemoveAt(index);
                
                // Reset position value
                position = null;
            }

            return position;
        }

        private Vector3? TryFindPosition()
        {
            var position = new Vector3(GetRandomPosition(), 0, GetRandomPosition());
            position.y = TerrainMeta.HeightMap.GetHeight(position);

            // Invalid if under the water
            if (position.y < TerrainMeta.WaterMap.GetHeight(position))
                return null;

            // Invalid if has buildings or colliders in the configured range
            if (!IsValidForBuilding(position) || !IsValidForColliders(position))
                return null;

            return position;
        }

        private int GetRandomPosition() => _random.Next(_worldSize / -2, _worldSize / 2);

        private void PrintDebug(string message)
        {
            if (_config.Debug)
                Interface.Oxide.LogDebug($"{Name} > " + message);
        }

        private bool IsValidForBuilding(Vector3 position)
        {
            _listBuildingBlocks.Clear();
            
            Vis.Entities(position, _config.DistanceBuilding, _listBuildingBlocks, Layers.Construction);
            return _listBuildingBlocks.Count <= 0;
        }

        private bool IsValidForColliders(Vector3 position)
        {
            _listColliders.Clear();
            
            Vis.Components(position, _config.DistanceCollider, _listColliders);
            foreach (var collider in _listColliders)
            {
                var gameObject = collider.gameObject;
                if (gameObject.layer == _layerTerrain)
                {
                    continue;
                }

                return false;
            }
            
            return true;
        }

        private static string GetMsg(string key, string userId = null) => _ins.lang.GetMessage(key, _ins, userId);

        #endregion
    }
}