﻿using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemOpenedBeenade : Item
    {
        public override string GetHeldTpUseAnimation(IItemSlot activeHotbarSlot, Entity byEntity)
        {
            return null;
        }

        public override void OnHeldInteractStart(IItemSlot itemslot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;

            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
            if (block is BlockSkep && block.FirstCodePart(1).Equals("populated"))
            {
                handling = EnumHandHandling.PreventDefaultAction;
            }
        }

        public override bool OnHeldInteractStep(float secondsUsed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return false;

            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                float offset = GameMath.Clamp(secondsUsed * 3, 0, 2f);

                tf.Translation.Set(-offset, offset / 4f, 0);

                byEntity.Controls.UsingHeldItemTransformBefore = tf;
            }

            SimpleParticleProperties bees = BlockEntityBeehive.Bees;
            BlockPos pos = blockSel.Position;
            Random rand = byEntity.World.Rand;

            Vec3d startPos = new Vec3d(pos.X + rand.NextDouble(), pos.Y + rand.NextDouble() * 0.25f, pos.Z + rand.NextDouble());
            Vec3d endPos = new Vec3d(byEntity.LocalPos.X, byEntity.LocalPos.Y + byEntity.EyeHeight - 0.2f, byEntity.LocalPos.Z);

            Vec3f minVelo = new Vec3f((float)(endPos.X - startPos.X), (float)(endPos.Y - startPos.Y), (float)(endPos.Z - startPos.Z));
            minVelo.Normalize();
            minVelo *= 2;

            bees.minPos = startPos;
            bees.minVelocity = minVelo;
            bees.WithTerrainCollision = true;

            IPlayer byPlayer = null;
            if (byEntity is IEntityPlayer) byPlayer = byEntity.World.PlayerByUid(((IEntityPlayer)byEntity).PlayerUID);

            byEntity.World.SpawnParticles(bees, byPlayer);

            return secondsUsed < 4;
        }


        public override bool OnHeldInteractCancel(float secondsUsed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            
            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return;

            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
            bool ok = block is BlockSkep && block.FirstCodePart(1).Equals("populated");
            if (!ok) return;

            if (secondsUsed < 3.9f) return;


            slot.TakeOut(1);

            IPlayer byPlayer = null;
            if (byEntity is IEntityPlayer) byPlayer = byEntity.World.PlayerByUid(((IEntityPlayer)byEntity).PlayerUID);
            byPlayer?.InventoryManager.TryGiveItemstack(new ItemStack(byEntity.World.GetItem(new AssetLocation("beenade-closed"))));

            Block skepemtpyblock = byEntity.World.GetBlock(new AssetLocation("skep-empty-" + block.LastCodePart()));
            byEntity.World.BlockAccessor.SetBlock(skepemtpyblock.BlockId, blockSel.Position);
        }


        public override void GetHeldItemInfo(ItemStack stack, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(stack, dsc, world, withDebugInfo);
            if (stack.Collectible.Attributes == null) return;
            dsc.AppendLine(Lang.Get("Fill it up with bees and throw it for a stingy surprise"));
        }

    }
}