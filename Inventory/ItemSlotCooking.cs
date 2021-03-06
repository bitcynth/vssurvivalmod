﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    public class ItemSlotCooking : ItemSlotSurvival
    {
        public ItemSlotCooking(InventoryBase inventory) : base(inventory)
        {
        }

        public override bool CanTake()
        {
            bool isLiquid = !Empty && itemstack.Collectible.IsLiquid();
            if (isLiquid) return false;

            return base.CanTake();
        }

        protected override void ActivateSlotLeftClick(ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            IWorldAccessor world = inventory.Api.World;
            BlockBucket bucketblock = sourceSlot.Itemstack?.Block as BlockBucket;
            
            if (bucketblock != null)
            {
                ItemStack bucketContents = bucketblock.GetContent(world, sourceSlot.Itemstack);
                bool stackable = !Empty && itemstack.Equals(world, bucketContents, GlobalConstants.IgnoredStackAttributes);

                if ((Empty || stackable) && bucketContents != null)
                {
                    ItemStack bucketStack = sourceSlot.Itemstack;
                    ItemStack takenContent = bucketblock.TryTakeContent(world, bucketStack, 1);
                    sourceSlot.Itemstack = bucketStack;
                    takenContent.StackSize += StackSize;
                    this.itemstack = takenContent;
                    MarkDirty();
                    return;
                }

                return;
            }            

            base.ActivateSlotLeftClick(sourceSlot, ref op);
        }

        protected override void ActivateSlotRightClick(ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            IWorldAccessor world = inventory.Api.World;
            BlockBucket bucketblock = sourceSlot.Itemstack?.Block as BlockBucket;

            if (bucketblock != null)
            {
                if (Empty) return;

                ItemStack bucketContents = bucketblock.GetContent(world, sourceSlot.Itemstack);

                if (bucketContents == null)
                {
                    TakeOut(bucketblock.TryAddContent(world, sourceSlot.Itemstack, Itemstack, 1));
                    MarkDirty();
                } else
                {
                    if (itemstack.Equals(world, bucketContents, GlobalConstants.IgnoredStackAttributes))
                    {
                        TakeOut(bucketblock.TryAddContent(world, sourceSlot.Itemstack, bucketblock.GetContent(world, sourceSlot.Itemstack), 1));
                        MarkDirty();
                        return;
                    }
                }
                

                return;
            }
            

            base.ActivateSlotRightClick(sourceSlot, ref op);
        }
    }
}
