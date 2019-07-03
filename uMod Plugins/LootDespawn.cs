using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Loot Despawn", "Iv Misticos", "2.0.3")]
    [Description("Customize loot despawn")]
    public class LootDespawn : RustPlugin
    {
        #region Configuration
        
        private static Configuration _config = new Configuration();

        private class Configuration
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled = true;
            
            [JsonProperty(PropertyName = "Multiplier Inside Building Privilege")]
            public float MultiplierCupboard = 2;
            
            [JsonProperty(PropertyName = "Multiplier Outside Building Privilege")]
            public float MultiplierNonCupboard = 0.5f;
            
            [JsonProperty(PropertyName = "Items' Multipliers", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, float> MultiplierItems = new Dictionary<string, float>
            {
                { "item.shortname", 1.0f }
            };
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

        private void OnItemDropped(Item item, BaseEntity entity) => SetDespawnTime(item, entity as DroppedItem);
        
        #endregion

        #region Helpers
        
        private void SetDespawnTime(Item item, DroppedItem droppedItem)
        {
            if (!_config.Enabled || droppedItem == null || item?.info == null)
                return;
            
            droppedItem.CancelInvoke(nameof(DroppedItem.IdleDestroy));
            droppedItem.Invoke(nameof(DroppedItem.IdleDestroy), GetItemRate(item, droppedItem));
        }

        private float GetItemRate(Item item, DroppedItem droppedItem)
        {
            float current;
            if (!_config.MultiplierItems.TryGetValue(item.info.shortname, out current))
                current = 1.0f;

            current *= droppedItem.GetDespawnDuration() * (droppedItem.GetBuildingPrivilege() == null
                           ? _config.MultiplierNonCupboard
                           : _config.MultiplierCupboard);

            return current;
        }
        
        #endregion
    }
}