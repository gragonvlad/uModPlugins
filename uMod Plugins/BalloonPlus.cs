using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Balloon Plus", "Iv Misticos", "1.0.6")]
    [Description("Control your balloon's flight")]
    class BalloonPlus : RustPlugin
    {
        #region Cache
        
        private static Dictionary<string, SpeedData> _cachedSpeedData = new Dictionary<string, SpeedData>();

        private void UpdateCacheSpeedData(string id)
        {
            var highest = (SpeedData) null;
            foreach (var modifier in _config.Modifiers)
            {
                if (!CacheSpeedDataAllowed(id, modifier))
                    continue;
                
                if (highest == null || highest.Modifier < modifier.Modifier)
                    highest = modifier;
            }

            _cachedSpeedData[id] = highest;
        }

        private bool CacheSpeedDataAllowed(string id, SpeedData data)
        {
            return string.IsNullOrEmpty(data.Permission) || permission.UserHasPermission(id, data.Permission);
        }
        
        #endregion
        
        #region Configuration

        private static Configuration _config;
        
        private class Configuration
        {
            [JsonProperty(PropertyName = "Speed Modifiers", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<SpeedData> Modifiers = new List<SpeedData> {new SpeedData {Modifier = 250f, Permission = ""}};
            
            [JsonProperty(PropertyName = "Move Button")]
            public string MoveButton = "SPRINT";
            
            [JsonProperty(PropertyName = "Disable Wind Force")]
            public bool DisableWindForce = true;
            
            [JsonProperty(PropertyName = "Move Frequency")]
            public float Frequency = 0.2f;

            [JsonProperty(PropertyName = "Speed Modifier", NullValueHandling = NullValueHandling.Ignore)]
            public float? SpeedModifier = null;

            [JsonIgnore] public BUTTON ParsedMoveButton;
        }

        private class SpeedData
        {
            [JsonProperty(PropertyName = "Permission")]
            public string Permission = "balloonplus.vip";
            
            [JsonProperty(PropertyName = "Speed Modifier")]
            public float Modifier = 300f;
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
        
        private void OnEntitySpawned(BaseNetworkable entity)
        {
            var balloon = entity as HotAirBalloon;
            if (balloon == null)
                return;

            balloon.windForce = 0;
        }

        private void Init()
        {
            if (!Enum.TryParse(_config.MoveButton, out _config.ParsedMoveButton))
            {
                PrintError("Unable to parse the moving button");
                _config.ParsedMoveButton = BUTTON.SPRINT;
            }

            for (var i = 0; i < _config.Modifiers.Count; i++)
            {
                var modifier = _config.Modifiers[i];
                if (string.IsNullOrEmpty(modifier.Permission))
                    continue;
                
                permission.RegisterPermission(modifier.Permission, this);
            }

            if (_config.SpeedModifier.HasValue)
            {
                _config.Modifiers.Add(new SpeedData {Permission = "", Modifier = _config.SpeedModifier.Value});
                _config.SpeedModifier = null;
            }

            BasePlayer.activePlayerList.ForEach(player => UpdateCacheSpeedData(player.UserIDString));

            if (_config.DisableWindForce)
                return;
            
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnServerInitialized));
        }

        private void OnServerInitialized()
        {
            var objects = UnityEngine.Object.FindObjectsOfType<HotAirBalloon>();
            for (var index = 0; index < objects.Length; index++)
            {
                OnEntitySpawned(objects[index]);
            }

            timer.Every(_config.Frequency, HandleUpdate);
        }

        private void OnPlayerInit(BasePlayer player) => UpdateCacheSpeedData(player.UserIDString);
        private void OnUserGroupAdded(string id, string groupName) => UpdateCacheSpeedData(id);
        private void OnUserGroupRemoved(string id, string groupName) => UpdateCacheSpeedData(id);        
        private void OnUserPermissionGranted(string id, string permName) => UpdateCacheSpeedData(id);        
        private void OnUserPermissionRevoked(string id, string permName) => UpdateCacheSpeedData(id);
        
        #endregion
        
        #region Helpers

        private void HandleUpdate() => BasePlayer.activePlayerList.ForEach(HandleUpdate);

        private void HandleUpdate(BasePlayer player)
        {
            if (!player.serverInput.IsDown(_config.ParsedMoveButton) || !player.HasParent())
                return;
            
            var balloon = player.GetParentEntity() as HotAirBalloon;
            if (balloon == null)
                return;

            var modifier = _cachedSpeedData[player.UserIDString];
            if (modifier == null)
                return;

            var direction = player.eyes.HeadForward() * modifier.Modifier;
            balloon.myRigidbody.AddForce(direction.x, 0, direction.z, ForceMode.Force); // We shouldn't move the balloon up or down, so I use 0 here as y.
        }
        
        #endregion
    }
}