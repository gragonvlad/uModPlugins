using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;

namespace Oxide.Plugins
{
    [Info("Auto Purge", "misticos", "2.0.0")]
    [Description("Remove entities if the owner becomes inactive")]
    public class AutoPurge : RustPlugin
    {
        #region Variables

        private Time _time = GetLibrary<Time>();

        private static AutoPurge _ins;
	    
        #endregion

        #region Configuration

        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Purge Settings", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<PurgeSettings> Purges = new List<PurgeSettings> {new PurgeSettings()};

            [JsonProperty(PropertyName = "Purge Timer Frequency")]
            public float PurgeFrequency = 300f;

            [JsonProperty(PropertyName = "Last Seen Timer Frequency")]
            public float LastSeenFrequency = 300f;

            public class PurgeSettings
            {
                [JsonProperty(PropertyName = "Permission")]
                public string Permission = "";
			    
                [JsonProperty(PropertyName = "Lifetime")]
                public string LifetimeRaw = "none";

                [JsonIgnore]
                public uint Lifetime = 0;

                [JsonIgnore]
                public bool NoPurge = false;

                public static PurgeSettings Find(string playerId)
                {
                    PurgeSettings highest = null;

                    for (var i = 0; i < _config.Purges.Count; i++)
                    {
                        var purge = _config.Purges[i];
                        if (!string.IsNullOrEmpty(purge.Permission) &&
                            !_ins.permission.UserHasPermission(playerId, purge.Permission))
                            continue;

                        if (purge.NoPurge)
                            return purge;
					    
                        if (highest == null || highest.Lifetime < purge.Lifetime)
                            highest = purge;
                    }

                    return highest;
                }
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

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();
        
        #endregion
	    
        #region Work with Data

        private static PluginData _data;

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _data);

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

        // ReSharper disable MemberCanBePrivate.Local
        private class PluginData
        {
            public List<UserData> Users = new List<UserData>();
		    
            public class UserData
            {
                public string ID;

                public uint LastSeen;

                public static UserData Find(string playerId)
                {
                    foreach (var user in _data.Users)
                    {
                        if (user.ID == playerId)
                            return user;
                    }

                    var newUser = new UserData {ID = playerId, LastSeen = 0};
                    
                    _data.Users.Add(newUser);
                    
                    return newUser;
                }
            }
        }
        // ReSharper restore MemberCanBePrivate.Local

        #endregion
	    
        #region Hooks

        private void Init()
        {
            _ins = this;
		    
            const string noPurgeLifetime = "none";

            for (var i = 0; i < _config.Purges.Count; i++)
            {
                var purge = _config.Purges[i];
                if (!string.IsNullOrEmpty(purge.Permission))
                    permission.RegisterPermission(purge.Permission, this);

                var isNoPurge = purge.LifetimeRaw.Equals(noPurgeLifetime, StringComparison.CurrentCultureIgnoreCase);
                if (!isNoPurge && !ConvertToSeconds(purge.LifetimeRaw, out purge.Lifetime))
                {
                    PrintWarning(
                        $"Unable to parse {purge.LifetimeRaw} value as Lifetime. Disabling purge for this purge setup");
                    purge.NoPurge = true;
                }

                if (isNoPurge)
                    purge.NoPurge = true;
            }
        }

        private void OnServerInitialized()
        {
            ServerMgr.Instance.InvokeRepeating(UpdateLastSeen, 0f, _config.LastSeenFrequency);
            ServerMgr.Instance.InvokeRepeating(RunPurge, 1f, _config.PurgeFrequency);
        }

        private void Unload()
        {
            ServerMgr.Instance.CancelInvoke(RunPurge);
            ServerMgr.Instance.CancelInvoke(UpdateLastSeen);
		    
            _config = null;
            _data = null;
            _ins = null;
        }
	    
        #endregion
        
        #region Last Seen

        private void UpdateLastSeen()
        {
            var now = _time.GetUnixTimestamp();
            
            foreach (var player in covalence.Players.Connected)
            {
                PluginData.UserData.Find(player.Id).LastSeen = now;
            }
        }
        
        #endregion
	    
        #region Purge

        private void RunPurge()
        {
            ServerMgr.Instance.StartCoroutine(RunPurgeEnumerator());
        }

        private IEnumerator RunPurgeEnumerator()
        {
            using (var enumerator = BaseNetworkable.serverEntities.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    var entity = enumerator.Current;
                    var isDecay = entity is DecayEntity;
                    if (!isDecay)
                        continue;
                    
                    ProcessPurgeEntity(entity as BaseEntity);
                    yield return null;
                }
            }
        }

        private void ProcessPurgeEntity(BaseEntity entity)
        {
            if (!CanPurge(entity))
                return;
            
            entity.Kill();
        }

        private bool CanPurge(BaseEntity entity)
        {
            var canPurge = CanPurgeUser(entity.OwnerID.ToString());
            var privilege = entity.GetBuildingPrivilege();
            if (privilege == null)
                return canPurge;

            for (var i = 0; i < privilege.authorizedPlayers.Count; i++)
            {
                var user = privilege.authorizedPlayers[i];
                if (!CanPurgeUser(user.userid.ToString()))
                    return false;
            }

            return canPurge;
        }

        private bool CanPurgeUser(string playerId)
        {
            var purge = Configuration.PurgeSettings.Find(playerId);
            if (purge == null || purge.NoPurge)
                return false;

            var user = PluginData.UserData.Find(playerId);
            if (user.LastSeen == 0)
                return false;
            
            return _time.GetUnixTimestamp() > user.LastSeen + purge.Lifetime;
        }
	    
        #endregion
        
        #region Parsers
        
        private static readonly Regex RegexStringTime = new Regex(@"(\d+)([dhms])", RegexOptions.Compiled);
        private static bool ConvertToSeconds(string time, out uint seconds)
        {
            seconds = 0;
            if (time == "0" || string.IsNullOrEmpty(time)) return true;
            var matches = RegexStringTime.Matches(time);
            if (matches.Count == 0) return false;
            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (match.Groups[2].Value)
                {
                    case "d":
                    {
                        seconds += uint.Parse(match.Groups[1].Value) * 24 * 60 * 60;
                        break;
                    }
                    case "h":
                    {
                        seconds += uint.Parse(match.Groups[1].Value) * 60 * 60;
                        break;
                    }
                    case "m":
                    {
                        seconds += uint.Parse(match.Groups[1].Value) * 60;
                        break;
                    }
                    case "s":
                    {
                        seconds += uint.Parse(match.Groups[1].Value);
                        break;
                    }
                }
            }
            return true;
        }
        
        #endregion
    }
}