using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Placeholder API", "Iv Misticos", "1.0.0")]
    [Description("API for placeholders in plugins' messages")]
    class PlaceholderAPI : CovalencePlugin
    {
        #region Configuration

        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Placeholders", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Placeholder> Placeholders = new List<Placeholder> {new Placeholder()};

            public class Placeholder
            {
                [JsonProperty(PropertyName = "Key")]
                public string Key = "{key}";
                
                [JsonProperty(PropertyName = "Value")]
                public string Value = "Custom value";
            }
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
        
        #region Temporary data

        private static PluginData _data = new PluginData();
        
        private class PluginData
        {
            public StringBuilder Builder = new StringBuilder();
            
            public List<DynamicPlaceholder> Placeholders = new List<DynamicPlaceholder>();
            
            public class DynamicPlaceholder
            {
                public string ID;
                
                public Plugin Owner;
                
                public Action<IPlayer, StringBuilder> Action;
            }
        }
        
        #endregion
        
        #region Hooks

        // Remove all placeholders registered for this plugin
        private void OnPluginUnloaded(Plugin plugin)
        {
            for (var i = _data.Placeholders.Count - 1; i >= 0; i--)
            {
                var placeholder = _data.Placeholders[i];
                if (placeholder.Owner == plugin)
                    _data.Placeholders.RemoveAt(i);
            }
        }
        
        private void Unload()
        {
            _data = null;
            _config = null;
        }
        
        #endregion
        
        #region API

        [HookMethod(nameof(ProcessPlaceholders))]
        private void ProcessPlaceholders(IPlayer player, string content)
        {
            _data.Builder.Clear();
            _data.Builder.Append(content);
            
            foreach (var placeholder in _data.Placeholders)
            {
                placeholder.Action?.Invoke(player, _data.Builder);
            }

            foreach (var placeholder in _config.Placeholders)
            {
                _data.Builder.Replace(placeholder.Key, placeholder.Value);
            }

            // ReSharper disable once RedundantAssignment
            // String is a reference type, no need to specify ref keyword or something.
            content = _data.Builder.ToString();
        }

        [HookMethod(nameof(ExistsPlaceholder))]
        private bool ExistsPlaceholder(string id)
        {
            foreach (var placeholder in _data.Placeholders)
            {
                if (placeholder.ID == id)
                    return true;
            }

            return false;
        }

        [HookMethod(nameof(AddPlaceholder))]
        private bool AddPlaceholder(Plugin plugin, string id, Action<IPlayer, StringBuilder> action)
        {
            if (ExistsPlaceholder(id) || plugin == null || !plugin.IsLoaded)
                return false;

            _data.Placeholders.Add(new PluginData.DynamicPlaceholder
            {
                ID = id,
                Owner = plugin,
                Action = action
            });

            return true;
        }

        [HookMethod(nameof(RemovePlaceholder))]
        private void RemovePlaceholder(string id)
        {
            for (var i = _data.Placeholders.Count - 1; i >= 0; i--)
            {
                var placeholder = _data.Placeholders[i];
                if (placeholder.ID != id)
                    continue;
                
                _data.Placeholders.RemoveAt(i);
                break;
            }
        }
        
        #endregion
    }
}