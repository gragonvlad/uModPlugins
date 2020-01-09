namespace Oxide.Plugins
{
    [Info("Held Entity Fix", "Iv Misticos", "1.0.0")]
    [Description("Fixes held entities that are not deleted by plugins when items are")]
    class HeldEntityFix : CovalencePlugin
    {
        #region Hooks

        private void OnServerInitialized()
        {
            var before = BaseNetworkable.serverEntities.Count;
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is HeldEntity)
                    ProcessHeldEntity(entity as HeldEntity);
            }

            PrintWarning($"Removed {before - BaseNetworkable.serverEntities.Count} invalid HeldEntities");
        }

        private void OnItemRemove(Item item)
        {
            var heldEntity = item.GetHeldEntity();
            if (heldEntity != null && heldEntity.IsValid())
                heldEntity.Kill();
        }
        
        #endregion
        
        #region Helpers

        private void ProcessHeldEntity(BaseEntity entity)
        {
            if (entity == null || !entity.IsValid())
                return;
            
            var item = entity.GetItem();
            if (item == null || !item.IsValid())
                entity.Kill();
        }
        
        #endregion
    }
}