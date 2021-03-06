﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Inventory with one normal slot and one output slot
    /// </summary>
    public class InventoryQuern : InventoryBase, ISlotProvider
    {
        ItemSlot[] slots;
        public IItemSlot[] Slots { get { return slots; } }
        

        public override int QuantitySlots
        {
            get { return 2; }
        }

        public InventoryQuern(string inventoryID, ICoreAPI api) : base(inventoryID, api)
        {
            // slot 0 = input
            // slot 1 = output
            slots = GenEmptySlots(2);
        }

        public InventoryQuern(string className, string instanceID, ICoreAPI api) : base(className, instanceID, api)
        {
            slots = GenEmptySlots(2);
        }

        public override ItemSlot GetSlot(int slotId)
        {
            return slots[slotId];
        }


        public override void FromTreeAttributes(ITreeAttribute tree)
        {
            slots = SlotsFromTreeAttributes(tree);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            SlotsToTreeAttributes(slots, tree);
        }

        protected override ItemSlot NewSlot(int i)
        {
            if (i == 0) return new ItemSlotSurvival(this);
            return new ItemSlotOutput(this);
        }
    }
}
