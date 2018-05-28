﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Vintagestory.GameContent
{
    public class ItemCattailRoot : Item
    {
        public override bool OnHeldInteractStart(IItemSlot itemslot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null || byEntity?.World == null || !byEntity.Controls.Sneak)
            {
                return base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel);
            }

            bool waterBlock = byEntity.World.BlockAccessor.GetBlock(blockSel.Position.AddCopy(blockSel.Face)).IsWater();

            Block block = byEntity.World.GetBlock(new AssetLocation(waterBlock ? "tallplant-coopersreed-water-harvested" : "tallplant-coopersreed-free-harvested"));

            if (block == null)
            {
                return base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel);
            }

            IPlayer byPlayer = null;
            if (byEntity is IEntityPlayer) byPlayer = byEntity.World.PlayerByUid(((IEntityPlayer)byEntity).PlayerUID);

            blockSel = blockSel.Clone();
            blockSel.Position.Add(blockSel.Face);

            bool ok = block.TryPlaceBlock(byEntity.World, byPlayer, itemslot.Itemstack, blockSel);

            if (ok)
            {
                itemslot.TakeOut(1);
                itemslot.MarkDirty();
            }

            return ok;
        }
    }
}