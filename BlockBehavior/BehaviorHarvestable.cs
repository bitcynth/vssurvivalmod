﻿using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorHarvestable : BlockBehavior
    {
        public BlockBehaviorHarvestable(Block block) : base(block)
        {
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;

            if (block.Code.Path.Contains("ripe") && block.Drops != null && block.Drops.Length >= 1)
            {
                BlockDropItemStack drop = block.Drops.Length == 1 ? block.Drops[0] : block.Drops[1];

                ItemStack stack = drop.GetNextItemStack();

                if (!byPlayer.InventoryManager.TryGiveItemstack(stack)) {
                    world.SpawnItemEntity(drop.GetNextItemStack(), blockSel.Position.ToVec3d().Add(0.5, 0.5, 0.5));
                }
                
                world.BlockAccessor.SetBlock(world.GetBlock(block.Code.CopyWithPath(block.Code.Path.Replace("ripe", "empty"))).BlockId, blockSel.Position);

                world.PlaySoundAt(new AssetLocation("sounds/block/plant"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);

                return true;
            }

            return false;
        }
    }
}
