using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Balloon Plus", "Iv Misticos", "1.0.4")]
    [Description("Control your balloon's flight")]
    class BalloonPlus : RustPlugin
    {
        #region Variables

        private static BalloonPlus _ins;
        
        #endregion
        
        #region Configuration

        private static Configuration _config;
        
        private class Configuration
        {
            [JsonProperty(PropertyName = "Speed Modifier")]
            public float Modifier = 250f;

            [JsonProperty(PropertyName = "Speed Modifiers", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<SpeedData> Modifiers = new List<SpeedData> {new SpeedData()};
            
            [JsonProperty(PropertyName = "Move Button")]
            public string MoveButton = "SPRINT";
            
            [JsonProperty(PropertyName = "Disable Wind Force")]
            public bool DisableWindForce = true;

            [JsonIgnore] public BUTTON ParsedMoveButton;
        }

        private class SpeedData
        {
            [JsonProperty(PropertyName = "Permission")]
            public string Permission = "balloonplus.vip";
            
            [JsonProperty(PropertyName = "Speed Modifier")]
            public float Modifier = 300f;

            public static float GetModifier(string id)
            {
                for (var i = 0; i < _config.Modifiers.Count; i++)
                {
                    var modifier = _config.Modifiers[i];
                    if (_ins.permission.UserHasPermission(id, modifier.Permission))
                        return modifier.Modifier;
                }

                return _config.Modifier;
            }
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
            if (balloon == null || !_config.DisableWindForce)
                return;

            balloon.windForce = 0;
        }
        
        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!input.IsDown(_config.ParsedMoveButton) || !player.HasParent()) return;
            
            var balloon = player.GetParentEntity() as HotAirBalloon;
            if (balloon == null)
                return;

            var direction = player.eyes.HeadForward() * SpeedData.GetModifier(player.UserIDString);
            balloon.myRigidbody.AddForce(direction.x, 0, direction.z, ForceMode.Force); // We shouldn't move the balloon up or down, so I use 0 here as y.
        }

        private void OnServerInitialized()
        {
            _ins = this;
            
            if (!Enum.TryParse(_config.MoveButton, out _config.ParsedMoveButton))
            {
                PrintError("Unable to parse the moving button");
                _config.ParsedMoveButton = BUTTON.SPRINT;
            }

            for (var i = 0; i < _config.Modifiers.Count; i++)
            {
                permission.RegisterPermission(_config.Modifiers[i].Permission, this);
            }

            var objects = UnityEngine.Object.FindObjectsOfType<HotAirBalloon>();
            for (var index = 0; index < objects.Length; index++)
            {
                OnEntitySpawned(objects[index]);
            }
        }
        
        #endregion
    }
}