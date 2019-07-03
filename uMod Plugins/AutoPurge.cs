using System;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Auto Purge", "misticos", "2.0.0")]
    [Description("Remove entities if the owner becomes inactive")]
    public class AutoPurge : RustPlugin
    {
	    #region Configuration

	    private static Configuration _config;

	    private class Configuration
	    {
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

	    protected override void SaveConfig() => Config.WriteObject(_config);

	    protected override void LoadDefaultConfig() => _config = new Configuration();
        
	    #endregion
	}
}