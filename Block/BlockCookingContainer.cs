﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockCookingContainer : Block, IInFirepitRendererSupplier
    {
        public int MaxServingSize = 6;

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemStack stack = OnPickBlock(world, blockSel.Position);

            if (byPlayer.InventoryManager.TryGiveItemstack(stack, true))
            {
                world.BlockAccessor.SetBlock(0, blockSel.Position);
                world.PlaySoundAt(this.Sounds.Place, byPlayer, byPlayer);
                return true;
            }
            return false;
        }


        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel)
        {
            if (!byPlayer.Entity.Controls.Sneak) return false;

            if (!world.TestPlayerAccessBlock(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return false;
            }

            if (IsSuitablePosition(world, blockSel.Position) && world.BlockAccessor.GetBlock(blockSel.Position.DownCopy()).SideSolid[BlockFacing.UP.Index])
            {
                DoPlaceBlock(world, blockSel.Position, blockSel.Face, itemstack);
                return true;
            }

            return false;
        }

        public override float GetMeltingDuration(IWorldAccessor world, ISlotProvider cookingSlotsProvider, IItemSlot inputSlot)
        {
            float duration = 0;

            ItemStack[] stacks = GetCookingStacks(cookingSlotsProvider, false);
            for (int i = 0; i < stacks.Length; i++)
            {
                if (stacks[i].Collectible?.CombustibleProps == null)
                {
                    duration += 30 * stacks[i].StackSize;
                    continue;
                }

                float singleDuration = stacks[i].Collectible.GetMeltingDuration(world, cookingSlotsProvider, inputSlot);
                duration += singleDuration * stacks[i].StackSize / stacks[i].Collectible.CombustibleProps.SmeltedRatio;
            }

            duration = Math.Max(60, duration / 3);

            return duration;
        }


        public override float GetMeltingPoint(IWorldAccessor world, ISlotProvider cookingSlotsProvider, IItemSlot inputSlot)
        {
            float meltpoint = 0;

            ItemStack[] stacks = GetCookingStacks(cookingSlotsProvider, false);
            for (int i = 0; i < stacks.Length; i++)
            {
                meltpoint = Math.Max(meltpoint, stacks[i].Collectible.GetMeltingPoint(world, cookingSlotsProvider, inputSlot));
            }

            return Math.Max(100, meltpoint);
        }


        public override bool CanSmelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemStack inputStack, ItemStack outputStack)
        {
            ItemStack[] stacks = GetCookingStacks(cookingSlotsProvider, false);

            // Got recipe?
            if (GetMatchingCookingRecipe(world, stacks) != null)
            {
                return true;
            }

            return false;
        }


        public override void DoSmelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider, IItemSlot inputSlot, IItemSlot outputSlot)
        {
            ItemStack[] stacks = GetCookingStacks(cookingSlotsProvider);

            CookingRecipe recipe = GetMatchingCookingRecipe(world, stacks);

            Block block = world.GetBlock(CodeWithPath(FirstCodePart() + "-cooked"));
            ItemStack outputStack = new ItemStack(block);

            if (recipe != null)
            {
                int quantityServings = recipe.GetQuantityServings(stacks);
                for (int i = 0; i < stacks.Length; i++)
                {
                    stacks[i].StackSize /= quantityServings;
                }
                // Not active. Let's sacrifice mergability for letting players select how meals should look and named like
                //stacks = stacks.OrderBy(stack => stack.Collectible.Code.ToShortString()).ToArray(); // Required so that different arrangments of ingredients still create mergable meal bowls

                ((BlockCookedContainer)block).SetContents(recipe.Code, quantityServings, outputStack, stacks);
                
                outputStack.Collectible.SetTemperature(world, outputStack, GetIngredientsTemperature(world, stacks));
                outputSlot.Itemstack = outputStack;
                inputSlot.Itemstack = null;

                for (int i = 0; i < cookingSlotsProvider.Slots.Length; i++)
                {
                    cookingSlotsProvider.Slots[i].Itemstack = null;
                }
                return;
            }
        }

        private void CookStacks(ItemStack[] stacks)
        {
            for (int i = 0; i < stacks.Length; i++)
            {
                if (stacks[i] == null) continue;
                CollectibleObject obj = stacks[i].Collectible;
                if (obj.CombustibleProps != null && obj.CombustibleProps.SmeltedStack != null)
                {
                    stacks[i] = obj.CombustibleProps.SmeltedStack.ResolvedItemstack;
                }
            }
        }


        public string GetOutputText(IWorldAccessor world, ISlotProvider cookingSlotsProvider, IItemSlot inputSlot)
        {
            if (inputSlot.Itemstack == null) return null;
            if (!(inputSlot.Itemstack.Collectible is BlockCookingContainer)) return null;            

            ItemStack[] stacks = GetCookingStacks(cookingSlotsProvider);
            
            CookingRecipe recipe = GetMatchingCookingRecipe(world, stacks);

            if (recipe != null)
            {
                double quantity = recipe.GetQuantityServings(stacks);
                if (quantity != 1)
                {
                    return string.Format("Will create {0} servings of {1}", (int)quantity, recipe.GetOutputName(world, stacks).ToLowerInvariant());
                } else
                {
                    return string.Format("Will create {0} serving of {1}", (int)quantity, recipe.GetOutputName(world, stacks).ToLowerInvariant());
                }
            }

            return null;
        
        }

        



        public CookingRecipe GetMatchingCookingRecipe(IWorldAccessor world, ItemStack[] stacks)
        {
            CookingRecipe[] recipes = world.CookingRecipes;
            if (recipes == null) return null;

            for (int j = 0; j < recipes.Length; j++)
            {
                if (recipes[j].Matches(stacks))
                {
                    if (recipes[j].GetQuantityServings(stacks) > MaxServingSize) continue;

                    return recipes[j];
                }
            }

            return null;
        }


        public float GetIngredientsTemperature(IWorldAccessor world, ItemStack[] ingredients)
        {
            bool haveStack = false;
            float lowestTemp = 0;
            for (int i = 0; i < ingredients.Length; i++)
            {
                if (ingredients[i] != null)
                {
                    float stackTemp = ingredients[i].Collectible.GetTemperature(world, ingredients[i]);
                    lowestTemp = haveStack ? Math.Min(lowestTemp, stackTemp) : stackTemp;
                    haveStack = true;
                }

            }

            return lowestTemp;
        }


        


        public ItemStack[] GetCookingStacks(ISlotProvider cookingSlotsProvider, bool clone = true)
        {
            List<ItemStack> stacks = new List<ItemStack>(4);

            for (int i = 0; i < cookingSlotsProvider.Slots.Length; i++)
            {
                ItemStack stack = cookingSlotsProvider.Slots[i].Itemstack;
                if (stack == null) continue;
                stacks.Add(clone ? stack.Clone() : stack);
            }

            return stacks.ToArray();
        }

        public IInFirepitRenderer GetRendererWhenInFirepit(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot)
        {
            return new PotInFirepitRenderer(api as ICoreClientAPI, stack, firepit.pos, forOutputSlot);
        }

        public EnumFirepitModel GetDesiredFirepitModel(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot)
        {
            return EnumFirepitModel.Wide;
        }



        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing)
        {
            return capi.BlockTextureAtlas.GetRandomPixel(Textures["ceramic"].Baked.TextureSubId);
        }
    }
}
