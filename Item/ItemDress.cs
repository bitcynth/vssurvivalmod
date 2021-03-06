﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    public class ItemDress : Item
    {
        public override void OnHeldInteractStart(IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling)
        {
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null) return;

            EnumCharacterDressType dresstype;
            string strdress = slot.Itemstack.ItemAttributes["clothescategory"].AsString();
            if (!Enum.TryParse(strdress, true, out dresstype)) return;

            IInventory inv = byPlayer.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (inv == null) return;

            if (inv.GetSlot((int)dresstype).TryFlipWith(slot))
            {
                handHandling = EnumHandHandling.PreventDefault;
            }
        }

        public override void GetHeldItemInfo(ItemStack stack, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(stack, dsc, world, withDebugInfo);

            EnumCharacterDressType dresstype;
            string strdress = stack.ItemAttributes["clothescategory"].AsString();
            if (!Enum.TryParse(strdress, true, out dresstype))
            {
                dsc.AppendLine(Lang.Get("Cloth Category: Unknown"));
            } else
            {
                dsc.AppendLine(Lang.Get("Cloth Category: {0}", Lang.Get("clothcategory-" + stack.ItemAttributes["clothescategory"].AsString())));
            }

            

        }
    }
}
