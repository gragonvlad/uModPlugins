using System;
using System.Collections;
using System.Collections.Generic;
using ConVar;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using Physics = UnityEngine.Physics;
using Random = System.Random;

namespace Oxide.Plugins
{
    [Info("Loot Plus", "Iv Misticos", "2.1.5")]
    [Description("Modify loot on your server.")]
    public class LootPlus : RustPlugin
    {
        #region Variables

        private static Random _random = new Random();

        private bool _initialized = false;

        private const string PermissionLootSave = "lootplus.lootsave";
        
        private const string PermissionLootRefill = "lootplus.lootrefill";
        
        private const string PermissionLoadConfig = "lootplus.loadconfig";
        
        #endregion
        
        #region Configuration

        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Refill Loot On Plugin Load")]
            public bool RefillOnLoad = false;
            
            [JsonProperty(PropertyName = "Container Loot Save Command")]
            public string LootSaveCommand = "lootsave";
            
            [JsonProperty(PropertyName = "Container Refill Command")]
            public string LootRefillCommand = "lootrefill";
            
            [JsonProperty(PropertyName = "Load Config Command")]
            public string LoadConfigCommand = "lootconfig";
            
            [JsonProperty(PropertyName = "Containers", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ContainerData> Containers = new List<ContainerData> {new ContainerData()};

            [JsonProperty(PropertyName = "Shuffle Items", NullValueHandling = NullValueHandling.Ignore)]
            public bool? ShuffleItems = null;

            [JsonProperty(PropertyName = "Allow Duplicate Items", NullValueHandling = NullValueHandling.Ignore)]
            public bool? DuplicateItems = null;

            [JsonProperty(PropertyName = "Allow Duplicate Items With Different Skins", NullValueHandling = NullValueHandling.Ignore)]
            public bool? DuplicateItemsDifferentSkins = null;

            [JsonProperty(PropertyName = "Debug")]
            public bool Debug = false;
        }

        private class ContainerData
        {
            [JsonProperty(PropertyName = "Entity Shortname")]
            public string Shortname = "entity.shortname";

            [JsonProperty(PropertyName = "Exclude Entities Shortnames", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Exclude = new List<string> {"stocking_large_deployed", "stocking_small_deployed"};
            
            [JsonProperty(PropertyName = "API Shortname")]
            public string APIShortname = "";
            
            [JsonProperty(PropertyName = "Monument Prefab (Empty To Ignore)")]
            public string Monument = "";

            [JsonProperty(PropertyName = "Shuffle Items")]
            public bool ShuffleItems = true;

            [JsonProperty(PropertyName = "Allow Duplicate Items")]
            public bool DuplicateItems = false;

            [JsonProperty(PropertyName = "Allow Duplicate Items With Different Skins")]
            public bool DuplicateItemsDifferentSkins = true;
            
            [JsonProperty(PropertyName = "Item Container Indexes", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<int> ContainerIndexes = new List<int> {0};

            [JsonProperty(PropertyName = "Replace Items")]
            public bool ReplaceItems = true;

            [JsonProperty(PropertyName = "Add Items")]
            public bool AddItems = false;

            [JsonProperty(PropertyName = "Modify Items")]
            public bool ModifyItems = false;

            [JsonProperty(PropertyName = "Online Condition")]
            public OnlineData Online = new OnlineData();

            [JsonProperty(PropertyName = "Maximal Failures To Add An Item")]
            public int MaxRetries = 5;
            
            [JsonProperty(PropertyName = "Capacity", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<CapacityData> Capacity = new List<CapacityData> {new CapacityData()};
            
            [JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ItemData> Items = new List<ItemData> {new ItemData()};
        }

        private class ItemData : ChanceData
        {
            [JsonProperty(PropertyName = "Item Shortname")]
            public string Shortname = "item.shortname";

            [JsonProperty(PropertyName = "Item Name (Empty To Ignore)")]
            public string Name = "";

            [JsonProperty(PropertyName = "Is Blueprint")]
            public bool IsBlueprint = false;

            [JsonProperty(PropertyName = "Allow Stacking")]
            public bool AllowStacking = true;

            [JsonProperty(PropertyName = "Remove Item")]
            public bool RemoveItem = false;

            [JsonProperty(PropertyName = "Conditions", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ConditionData> Conditions = new List<ConditionData> {new ConditionData()};

            [JsonProperty(PropertyName = "Skins", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<SkinData> Skins = new List<SkinData> {new SkinData()};
            
            [JsonProperty(PropertyName = "Amount", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<AmountData> Amount = new List<AmountData> {new AmountData()};
        }
        
        #region Additional

        private class ConditionData : ChanceData
        {
            [JsonProperty(PropertyName = "Condition")]
            public float Condition = 100f;
        }

        private class SkinData : ChanceData
        {
            [JsonProperty(PropertyName = "Skin")]
            // ReSharper disable once RedundantDefaultMemberInitializer
            public ulong Skin = 0;
        }

        private class AmountData : ChanceData
        {
            [JsonProperty(PropertyName = "Amount", NullValueHandling = NullValueHandling.Ignore)]
            public int? Amount = null;
            
            [JsonProperty(PropertyName = "Minimal Amount")]
            public int MinAmount = 3;
            
            [JsonProperty(PropertyName = "Maximal Amount")]
            public int MaxAmount = 3;
            
            [JsonProperty(PropertyName = "Rate")]
            public float Rate = -1f;

            public int SelectAmount() => _random.Next(MinAmount, MaxAmount + 1);

            public bool IsValid() => MinAmount > 0 && MaxAmount > 0;
        }

        private class CapacityData : ChanceData
        {
            [JsonProperty(PropertyName = "Capacity")]
            public int Capacity = 3;
        }

        private class OnlineData
        {
            // sorry, dont want anything in config to be "private" :)
            // ReSharper disable MemberCanBePrivate.Local
            [JsonProperty(PropertyName = "Minimal Online")]
            public int MinOnline = -1;
            
            [JsonProperty(PropertyName = "Maximal Online")]
            public int MaxOnline = -1;
            // ReSharper restore MemberCanBePrivate.Local

            public bool IsOkay()
            {
                var online = BasePlayer.activePlayerList.Count;
                return MinOnline == -1 && MaxOnline == -1 || online > MinOnline && online < MaxOnline;
            }
        }

        public class ChanceData
        {
            [JsonProperty(PropertyName = "Chance")]
            // ReSharper disable once MemberCanBePrivate.Global
            public int Chance = 1;
            
            public static T Select<T>(IReadOnlyList<T> data) where T : ChanceData
            {
                // xD

                if (data == null)
                {
                    PrintDebug("Data is null");
                    return null;
                }

                if (data.Count == 0)
                {
                    PrintDebug("Data is empty");
                    return null;
                }

                var sum1 = 0;
                for (var i = 0; i < data.Count; i++)
                {
                    var entry = data[i];
                    sum1 += entry?.Chance ?? 0;
                }

                if (sum1 < 1)
                {
                    PrintDebug("Sum is less than 1");
                    return null;
                }

                var random = _random.Next(1, sum1 + 1); // include the sum1 number itself and exclude the 0
                
                PrintDebug($"Selected random: {random}; Sum: {sum1}");
                
                var sum2 = 0;
                for (var i = 0; i < data.Count; i++)
                {
                    var entry = data[i];
                    sum2 += entry?.Chance ?? 0;
                    PrintDebug($"Current sum: {sum2}, random: {random}");
                    if (random <= sum2)
                        return entry;
                }
                
                return null;
            }
        }
        
        #endregion

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

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion
        
        #region Commands

        private void CommandLootSave(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            if (player == null)
            {
                iplayer.Reply(GetMsg("In-Game Only", iplayer.Id));
                return;
            }

            if (!iplayer.HasPermission(PermissionLootSave))
            {
                iplayer.Reply(GetMsg("No Permission", iplayer.Id));
                return;
            }

            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 10f) || hit.GetEntity() == null ||
                !(hit.GetEntity() is LootContainer))
            {
                iplayer.Reply(GetMsg("No Loot Container", iplayer.Id));
                return;
            }

            var container = hit.GetEntity() as LootContainer;
            if (container == null || container.inventory == null)
            {
                // Shouldn't really happen
                return;
            }

            var inventory = player.inventory.containerMain;

            var containerData = new ContainerData
            {
                ModifyItems = false,
                AddItems = false,
                ReplaceItems = true,
                Shortname = container.ShortPrefabName,
                Capacity = new List<CapacityData>
                {
                    new CapacityData
                    {
                        Capacity = inventory.itemList.Count
                    }
                },
                Items = new List<ItemData>(),
                MaxRetries = inventory.itemList.Count * 2
            };

            for (var i = 0; i < inventory.itemList.Count; i++)
            {
                var item = inventory.itemList[i];
                var isBlueprint = item.IsBlueprint();
                var itemData = new ItemData
                {
                    Amount = new List<AmountData>
                    {
                        new AmountData
                        {
                            Amount = item.amount
                        }
                    },
                    Conditions = new List<ConditionData>(),
                    Skins = new List<SkinData>
                    {
                        new SkinData
                        {
                            Skin = item.skin
                        }
                    },
                    Shortname = isBlueprint ? item.blueprintTargetDef.shortname : item.info.shortname,
                    Name = item.name ?? string.Empty, // yes but it doesnt work :( lol?
                    AllowStacking = true,
                    IsBlueprint = isBlueprint
                };

                if (!isBlueprint)
                {
                    if (item.hasCondition)
                        itemData.Conditions.Add(new ConditionData
                        {
                            Condition = item.condition
                        });
                }

                containerData.Items.Add(itemData);
            }

            _config.Containers.Add(containerData);
            SaveConfig();
            
            iplayer.Reply(GetMsg("Loot Container Saved", iplayer.Id));
        }

        private void CommandLootRefill(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PermissionLootRefill))
            {
                player.Reply(GetMsg("No Permission", player.Id));
                return;
            }
            
            player.Reply(GetMsg("Loot Refill Started", player.Id));
            LootRefill();
        }

        private void CommandLoadConfig(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PermissionLoadConfig))
            {
                player.Reply(GetMsg("No Permission", player.Id));
                return;
            }
            
            LoadConfig(); // What if something has changed there? :o
            player.Reply(GetMsg("Config Loaded", player.Id));
        }

        #endregion
        
        #region Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"In-Game Only", "Please, use this only while you're in the game"},
                {"No Permission", "You don't have enough permissions"},
                {"No Loot Container", "Please, look at the loot container in 10m"},
                {"Loot Container Saved", "You have saved this loot container data to configuration"},
                {"Loot Refill Started", "Loot refill process just started"},
                {"Config Loaded", "Your configuration was loaded"}
            }, this);
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(PermissionLootSave, this);
            permission.RegisterPermission(PermissionLootRefill, this);
            permission.RegisterPermission(PermissionLoadConfig, this);

            for (var i = 0; i < _config.Containers.Count; i++)
            {
                var container = _config.Containers[i];
                if (_config.ShuffleItems.HasValue)
                    container.ShuffleItems = _config.ShuffleItems.Value;
                if (_config.DuplicateItems.HasValue)
                    container.DuplicateItems = _config.DuplicateItems.Value;
                if (_config.DuplicateItemsDifferentSkins.HasValue)
                    container.DuplicateItemsDifferentSkins = _config.DuplicateItemsDifferentSkins.Value;

                for (var j = 0; j < container.Items.Count; j++)
                {
                    var item = container.Items[j];
                    for (var k = 0; k < item.Amount.Count; k++)
                    {
                        var amount = item.Amount[k];
                        if (!amount.Amount.HasValue)
                            continue;

                        amount.MinAmount = amount.Amount.Value;
                        amount.MaxAmount = amount.Amount.Value;
                        amount.Amount = null;
                    }
                }
            }

            if (_config.ShuffleItems.HasValue)
                _config.ShuffleItems = null;
            if (_config.DuplicateItems.HasValue)
                _config.DuplicateItems = null;
            if (_config.DuplicateItemsDifferentSkins.HasValue)
                _config.DuplicateItemsDifferentSkins = null;
            
            SaveConfig();

            AddCovalenceCommand(_config.LootSaveCommand, nameof(CommandLootSave));
            AddCovalenceCommand(_config.LootRefillCommand, nameof(CommandLootRefill));
            AddCovalenceCommand(_config.LoadConfigCommand, nameof(CommandLoadConfig));

            _initialized = true;
            
            if (!_config.RefillOnLoad)
            {
                PrintWarning("Loot refill on plugin load is disabled in configuration");
                return;
            }

            NextFrame(LootRefill);
        }

        private void Unload()
        {
            LootPlusController.TryDestroy();
            _initialized = false;
            
            // LOOT IS BACK
            using (var entitiesEnumerator = BaseNetworkable.serverEntities.GetEnumerator())
            {
                while (entitiesEnumerator.MoveNext())
                {
                    var entity = entitiesEnumerator.Current;
                    var lootContainer = entity as LootContainer;
                    if (lootContainer == null)
                        continue;

                    if (lootContainer is Stocking && !XMas.enabled)
                    {
                        PrintDebug("Stocking entity but XMas is disabled");
                        continue;
                    }

                    if (lootContainer.shouldRefreshContents && !lootContainer.IsInvoking(lootContainer.SpawnLoot))
                    {
                        PrintDebug("Entity should refresh content but does NOT.");
                        continue;
                    }

                    PrintDebug(
                        $"Restoring loot for {lootContainer.ShortPrefabName}. SRC: {lootContainer.shouldRefreshContents} / II: {lootContainer.IsInvoking(lootContainer.SpawnLoot)}");
                    
                    // Creating an inventory
                    if (lootContainer.inventory == null)
                    {
                        lootContainer.CreateInventory(true);
                    }
                    else
                    {
                        lootContainer.inventory.Clear();
                        ItemManager.DoRemoves();
                    }

                    // Spawning loot
                    lootContainer.SpawnLoot();

                    // Changing the capacity
                    lootContainer.inventory.capacity = lootContainer.inventory.itemList.Count;

                    // no i wont do anything with npc and other containers >:(
                }
            }
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            var container = entity as LootContainer;
            if (container != null)
            {
                PrintDebug("Entity is a LootContainer. Waiting for OnLootSpawn");
                return; // So this entity's content is modified in OnLootSpawn
            }
            
            OnLootSpawn(entity);
        }

        private void OnLootSpawn(BaseNetworkable entity)
        {
            if (!_initialized || entity == null)
                return;
            
            var corpse = entity as LootableCorpse;
            if (corpse != null)
            {
                PrintDebug($"{entity.ShortPrefabName} is a corpse");
                if (corpse.containers == null || corpse.containers.Length == 0)
                {
                    PrintDebug("Entity has no containers");
                    return;
                }
                
                for (var i = 0; i < corpse.containers.Length; i++)
                {
                    var inventory = corpse.containers[i];
                    RunLootHandler(entity, inventory, i);
                }
            }

            var lootContainer = entity as LootContainer;
            // ReSharper disable once InvertIf
            if (lootContainer != null)
            {
                PrintDebug($"{entity.ShortPrefabName} is a loot container");
                RunLootHandler(entity, lootContainer.inventory, -1);
            }
        }

        #endregion
        
        #region Controller
        
        private class LootPlusController : FacepunchBehaviour
        {
            private static LootPlusController _instance;

            public static LootPlusController Instance => _instance ? _instance : new GameObject().AddComponent<LootPlusController>();

            private void Awake()
            {
                TryDestroy();
                _instance = this;
            }

            public static void TryDestroy()
            {
                if (_instance)
                    Destroy(_instance.gameObject);
            }
        }
        
        #endregion
        
        #region API

        private void FillContainer(ItemContainer container, string apiShortname, int containerIndex = -1)
        {
            HandleAPIFill(container, apiShortname, containerIndex);
        }
        
        #endregion
        
        #region Helpers

        private void RunLootHandler(BaseNetworkable networkable, ItemContainer inventory, int containerIndex)
        {
            NextFrame(() => LootPlusController.Instance.StartCoroutine(LootHandler(networkable, inventory, containerIndex)));
        }

        private IEnumerator LootHandler(BaseNetworkable networkable, ItemContainer inventory, int containerIndex, string apiShortname = null)
        {
            if (inventory == null)
                yield break;

            for (var i = 0; i < _config.Containers.Count; i++)
            {
                var container = _config.Containers[i];
                if (networkable != null && apiShortname == null)
                {
                    if (container.Shortname != "global" && container.Shortname != networkable.ShortPrefabName ||
                        container.Exclude.Contains(networkable.ShortPrefabName))
                        continue;
                }
                else
                {
                    if (apiShortname != container.APIShortname)
                        continue;
                }

                if (containerIndex != -1 && !container.ContainerIndexes.Contains(containerIndex))
                {
                    continue;
                }

                if (networkable != null && apiShortname == null && !string.IsNullOrEmpty(container.Monument))
                {
                    var monument = GetMonumentName(networkable.transform.position);
                    PrintDebug($"Found monument: {monument}");
                    
                    if (monument != container.Monument)
                    {
                        continue;
                    }
                }

                yield return HandleInventory(networkable, inventory, container);
            }
        }

        private IEnumerator HandleInventory(BaseNetworkable networkable, ItemContainer inventory, ContainerData container)
        {
            PrintDebug(
                $"Handling container. S:{container.Shortname} / API:{container.APIShortname}");
            
            if (!container.Online.IsOkay())
            {
                PrintDebug("Online check failed");
                yield break;
            }

            if (!((container.AddItems || container.ReplaceItems) ^ container.ModifyItems))
            {
                PrintWarning("Multiple options (Add / Replace / Modify) are selected");
                yield break;
            }

            if (container.ShuffleItems && !container.ModifyItems && container.Items != null) // No need to shuffle for items modification
                Shuffle(container.Items);

            inventory.capacity = inventory.itemList.Count;

            if (container.ReplaceItems || container.AddItems)
            {
                var dataCapacity = ChanceData.Select(container.Capacity);
                if (dataCapacity == null)
                {
                    PrintDebug("Could not select a correct capacity");
                    yield break;
                }

                if (container.ReplaceItems)
                {
                    inventory.Clear();
                    ItemManager.DoRemoves();
                    inventory.capacity = dataCapacity.Capacity;
                    yield return HandleInventoryAddReplace(inventory, container);
                    yield break;
                }

                if (container.AddItems)
                {
                    inventory.capacity += dataCapacity.Capacity;
                    yield return HandleInventoryAddReplace(inventory, container);
                    yield break;
                }
            }

            if (container.ModifyItems)
            {
                yield return HandleInventoryModify(inventory, container);
            }
        }

        private void HandleAPIFill(ItemContainer container, string apiShortname, int containerIndex)
        {
            PrintDebug($"Handling API fill with API Shortname: {apiShortname}");
            // ReSharper disable once IteratorMethodResultIsIgnored
            LootHandler(null, container, -1, apiShortname);
        }

        private static IEnumerator HandleInventoryAddReplace(ItemContainer inventory, ContainerData container)
        {
            PrintDebug("Using add or replace");
            
            var failures = 0;
            while (inventory.itemList.Count < inventory.capacity)
            {
                yield return null;
                
                PrintDebug($"Count: {inventory.itemList.Count} / {inventory.capacity}");

                var dataItem = ChanceData.Select(container.Items);
                if (dataItem == null)
                {
                    PrintDebug("Could not select a correct item");
                    continue;
                }

                PrintDebug($"Handling item {dataItem.Shortname} (Blueprint: {dataItem.IsBlueprint} / Stacking: {dataItem.AllowStacking})");

                var skin = ChanceData.Select(dataItem.Skins)?.Skin ?? 0UL;

                if (!container.DuplicateItems) // Duplicate items are not allowed
                {
                    if (IsDuplicate(inventory.itemList, container, dataItem, skin))
                    {
                        if (++failures > container.MaxRetries)
                        {
                            PrintDebug("Stopping because of duplicates");
                            break;
                        }

                        continue;
                    }

                    PrintDebug("No duplicates");
                }

                var dataAmount = ChanceData.Select(dataItem.Amount);
                if (dataAmount == null)
                {
                    PrintDebug("Could not select a correct amount");
                    continue;
                }

                var amount = 1;
                if (dataAmount.IsValid())
                    amount = dataAmount.SelectAmount();

                if (dataAmount.Rate > 0f)
                    amount = (int) (dataAmount.Rate * amount);
                
                PrintDebug($"Creating with amount: {amount} (Amount: {dataAmount.Amount} / Rate: {dataAmount.Rate})");

                var definition =
                    ItemManager.FindItemDefinition(dataItem.IsBlueprint ? "blueprintbase" : dataItem.Shortname);
                if (definition == null)
                {
                    PrintDebug("Could not find an item definition");
                    continue;
                }

                var createdItem = ItemManager.Create(definition, amount, skin);
                if (createdItem == null)
                {
                    PrintDebug("Could not create an item");
                    continue;
                }

                if (dataItem.IsBlueprint)
                {
                    createdItem.blueprintTarget = ItemManager.FindItemDefinition(dataItem.Shortname).itemid;
                }
                else
                {
                    var dataCondition = ChanceData.Select(dataItem.Conditions);
                    if (createdItem.hasCondition)
                    {
                        if (dataCondition == null)
                        {
                            PrintDebug("Could not select a correct condition");
                        }
                        else
                        {
                            PrintDebug($"Selected condition: {dataCondition.Condition}");
                            createdItem.condition = dataCondition.Condition;
                        }
                    }
                    else if (dataCondition != null)
                    {
                        PrintDebug("Configurated item has a condition but item doesn't have condition");
                    }
                }

                if (!string.IsNullOrEmpty(dataItem.Name))
                    createdItem.name = dataItem.Name;

                var moved = createdItem.MoveToContainer(inventory, allowStack: dataItem.AllowStacking);
                if (moved) continue;

                PrintDebug("Could not move item to a container");
            }
        }

        private static IEnumerator HandleInventoryModify(ItemContainer inventory, ContainerData container)
        {
            PrintDebug("Using modify");
            
            // Reversed, because an item can be removed.
            for (var i = inventory.itemList.Count - 1; i >= 0; i--)
            {
                var item = inventory.itemList[i];
                for (var j = 0; j < container.Items.Count; j++)
                {
                    yield return null;

                    var dataItem = container.Items[j];
                    if (dataItem.Shortname != "global" && dataItem.Shortname != item.info.shortname ||
                        dataItem.IsBlueprint != item.IsBlueprint())
                        continue;

                    PrintDebug(
                        $"Handling item {dataItem.Shortname} (Blueprint: {dataItem.IsBlueprint} / Stacking: {dataItem.AllowStacking})");

                    if (dataItem.RemoveItem)
                    {
                        PrintDebug("Removing item");
                        item.DoRemove();
                        break;
                    }

                    var skin = ChanceData.Select(dataItem.Skins)?.Skin;
                    if (skin.HasValue)
                        item.skin = skin.Value;

                    var dataAmount = ChanceData.Select(dataItem.Amount);
                    if (dataAmount == null)
                    {
                        PrintDebug("Could not select a correct amount");
                        continue;
                    }

                    var amount = item.amount;
                    if (dataAmount.IsValid())
                        amount = dataAmount.SelectAmount();

                    if (dataAmount.Rate > 0f)
                        amount = (int) (dataAmount.Rate * amount);
                    
                    PrintDebug($"Selected amount: {amount} (Amount: {dataAmount.Amount} / Rate: {dataAmount.Rate})");

                    item.amount = amount;

                    var dataCondition = ChanceData.Select(dataItem.Conditions);
                    if (item.hasCondition)
                    {
                        if (dataCondition == null)
                        {
                            PrintDebug("Could not select a correct condition");
                        }
                        else
                        {
                            PrintDebug($"Selected condition: {dataCondition.Condition}");
                            item.condition = dataCondition.Condition;
                        }
                    }
                    else if (dataCondition != null)
                    {
                        PrintDebug("Configurated item has a condition but item doesn't have condition");
                    }

                    if (!string.IsNullOrEmpty(dataItem.Name))
                        item.name = dataItem.Name;
                }
            }
        }

        private static bool IsDuplicate(IReadOnlyList<Item> list, ContainerData container, ItemData dataItem, ulong skin)
        {
            for (var j = 0; j < list.Count; j++)
            {
                var item = list[j];
                if (dataItem.IsBlueprint)
                {
                    if (!item.IsBlueprint() || item.blueprintTargetDef.shortname != dataItem.Shortname) continue;

                    PrintDebug("Found a duplicate blueprint");
                    return true;
                }

                if (item.IsBlueprint() || item.info.shortname != dataItem.Shortname) continue;
                if (container.DuplicateItemsDifferentSkins && item.skin != skin)
                    continue;

                PrintDebug("Found a duplicate");
                return true;
            }

            return false;
        }

        private string GetMsg(string key, string userId) => lang.GetMessage(key, this, userId);

        private static void PrintDebug(string message)
        {
            if (_config.Debug)
                Interface.Oxide.LogDebug(message);
        }

        private void LootRefill()
        {
            using (var enumerator = BaseNetworkable.serverEntities.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    OnEntitySpawned(enumerator.Current);
                }
            }
        }

        private string GetMonumentName(Vector3 position)
        {
            var monuments = TerrainMeta.Path.Monuments;
            for (var i = 0; i < monuments.Count; i++)
            {
                var monument = monuments[i];
                var obb = new OBB(monument.transform.position, Quaternion.identity, monument.Bounds);
                if (obb.Contains(position))
                    return monument.name;
            }

            return string.Empty;
        }

        private static void Shuffle<T>(IList<T> list)
        {
            var count = list.Count;
            while (count > 1)
            {
                count--;
                var index = _random.Next(count + 1);
                var value = list[index];
                list[index] = list[count];
                list[count] = value;
            }
        }
        
        #endregion
    }
}