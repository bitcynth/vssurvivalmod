﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class RandomStack
    {
        public EnumItemClass Type;
        public string Code;
        public NatFloat Quantity = NatFloat.createUniform(1, 0);
        public float Chance;
        public ItemStack ResolvedStack;
        [JsonProperty, JsonConverter(typeof(JsonAttributesConverter))]
        public JsonObject Attributes;

        internal void Resolve(IWorldAccessor world)
        {    
            if (Type == EnumItemClass.Block)
            {
                Block block = world.GetBlock(new AssetLocation(Code));
                if (block == null)
                {
                    world.Logger.Error("Cannot resolve stack randomizer block with code {0}, wrong code?", Code);
                    return;
                }
                ResolvedStack = new ItemStack(block);
                if (Attributes != null) ResolvedStack.Attributes = Attributes.ToAttribute() as ITreeAttribute;
            } else
            {
                Item item = world.GetItem(new AssetLocation(Code));
                if (item == null)
                {
                    world.Logger.Error("Cannot resolve stack randomizer item with code {0}, wrong code?", Code);
                    return;
                }
                ResolvedStack = new ItemStack(item);
                if (Attributes != null) ResolvedStack.Attributes = Attributes.ToAttribute() as ITreeAttribute;
            }
        }
    }

    public class ItemStackRandomizer : Item
    {
        RandomStack[] Stacks;

        public override void OnLoaded(ICoreAPI api)
        {
            this.Stacks = Attributes["stacks"].AsObject<RandomStack[]>();
            float totalchance = 0;

            string code = this.Code.Path;

            for (int i = 0; i < Stacks.Length; i++)
            {
                totalchance += Stacks[i].Chance;
                Stacks[i].Resolve(api.World);
            }

            float scale = 1 / totalchance;

            for (int i = 0; i < Stacks.Length; i++)
            {
                Stacks[i].Chance *= scale;
            }
            

            base.OnLoaded(api);
        }

        public override void OnHeldInteractStart(IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            IPlayer byPlayer = null;
            if (byEntity is IEntityPlayer) byPlayer = byEntity.World.PlayerByUid(((IEntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) return;


            TreeAttribute tree = new TreeAttribute();
            tree.SetFloat("totalChance", slot.Itemstack.Attributes.GetFloat("totalChance", 1));

            tree.SetString("inventoryId", slot.Inventory.InventoryID);
            tree.SetInt("slotId", slot.Inventory.GetSlotId(slot));

            api.Event.PushEvent("OpenStackRandomizerDialog", tree);
            
        }



        internal void Resolve(ItemSlot intoslot, IWorldAccessor worldForResolve)
        {
            // uh oh - background thread rand again?
            double diceRoll = worldForResolve.Rand.NextDouble();
            ITreeAttribute attributes = intoslot.Itemstack.Attributes;

            if (attributes.GetFloat("totalChance", 1) < worldForResolve.Rand.NextDouble())
            {
                intoslot.Itemstack = null;
                return;
            }

            ItemStack ownstack = intoslot.Itemstack;
            intoslot.Itemstack = null;
            
            if (Stacks == null)
            {
                worldForResolve.Logger.Warning("ItemStackRandomizer 'Stacks' was null! Won't resolve into something.");
                return;
            }

            Stacks.Shuffle(worldForResolve.Rand);

            for (int i = 0; i < Stacks.Length; i++)
            {
                if (Stacks[i].Chance > diceRoll)
                {
                    intoslot.Itemstack = Stacks[i].ResolvedStack.Clone();
                    return;
                }

                diceRoll -= Stacks[i].Chance;

            }
        }


        public override void GetHeldItemInfo(ItemStack stack, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(stack, dsc, world, withDebugInfo);
            float total = stack.Attributes.GetFloat("totalChance", 1);

            dsc.AppendLine(Lang.Get("With a {0}% chance, will generate one of the following:", (total * 100).ToString("0.#")));

            for (int i = 0; i < Stacks.Length; i++)
            {
                if (Stacks[i].ResolvedStack == null) continue;

                if (Stacks[i].Quantity.var == 0)
                {
                    dsc.AppendLine(Lang.Get("{0}%: {1}x {2}",
                        (Stacks[i].Chance * 100).ToString("0.#"),
                        Stacks[i].Quantity.avg,
                        Stacks[i].ResolvedStack.GetName())
                    );
                } else
                {
                    dsc.AppendLine(Lang.Get("{0}%: {1}-{2}x {3}",
                        (Stacks[i].Chance * 100).ToString("0.#"),
                        Stacks[i].Quantity.avg - Stacks[i].Quantity.var,
                        Stacks[i].Quantity.avg + Stacks[i].Quantity.var,
                        Stacks[i].ResolvedStack.GetName())
                    );
                }
            }

            

        }
    }
}
