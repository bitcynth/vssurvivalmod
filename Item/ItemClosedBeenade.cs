﻿using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemClosedBeenade : Item
    {
        public override string GetHeldTpUseAnimation(IItemSlot activeHotbarSlot, Entity byEntity)
        {
            return null;
        }

        public override void OnHeldInteractStart(IItemSlot itemslot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (blockSel != null && byEntity.World.BlockAccessor.GetBlock(blockSel.Position).FirstCodePart() == "skep") return;

            // Not ideal to code the aiming controls this way. Needs an elegant solution - maybe an event bus?
            byEntity.Attributes.SetInt("aiming", 1);
            byEntity.StartAnimation("aim");

            handling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldInteractStep(float secondsUsed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                float offset = GameMath.Clamp(secondsUsed * 3, 0, 2f);

                tf.Translation.Set(offset, -offset / 4f, 0);

                byEntity.Controls.UsingHeldItemTransformBefore = tf;
            }

            return true;
        }


        public override bool OnHeldInteractCancel(float secondsUsed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            byEntity.Attributes.SetInt("aiming", 0);
            byEntity.StopAnimation("aim");
            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.Attributes.GetInt("aiming") == 0) return;

            byEntity.Attributes.SetInt("aiming", 0);
            byEntity.StopAnimation("aim");

            if (secondsUsed < 0.35f) return;

            float damage = 0.5f;
            string rockType = slot.Itemstack.Collectible.FirstCodePart(1);

            ItemStack stack = slot.TakeOut(1);
            slot.MarkDirty();

            IPlayer byPlayer = null;
            if (byEntity is IEntityPlayer) byPlayer = byEntity.World.PlayerByUid(((IEntityPlayer)byEntity).PlayerUID);
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/throw"), byEntity, byPlayer, false, 8);

            EntityProperties type = byEntity.World.GetEntityType(new AssetLocation("thrownbeenade"));
            Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);
            ((EntityThrownBeenade)entity).FiredBy = byEntity;
            ((EntityThrownBeenade)entity).Damage = damage;
            ((EntityThrownBeenade)entity).ProjectileStack = stack;
            
            float acc = (1 - byEntity.Attributes.GetFloat("aimingAccuracy", 0));
            double rndpitch = byEntity.WatchedAttributes.GetDouble("aimingRandPitch", 1) * acc * 0.75;
            double rndyaw = byEntity.WatchedAttributes.GetDouble("aimingRandYaw", 1) * acc * 0.75;

            Vec3d pos = byEntity.ServerPos.XYZ.Add(0, byEntity.EyeHeight - 0.2, 0);
            Vec3d aheadPos = pos.AheadCopy(1, byEntity.ServerPos.Pitch + rndpitch, byEntity.ServerPos.Yaw + rndyaw);
            Vec3d velocity = (aheadPos - pos) * 0.5;

            entity.ServerPos.SetPos(byEntity.ServerPos.BehindCopy(0.21).XYZ.Add(0, byEntity.EyeHeight - 0.2, 0).Ahead(0.25, 0, byEntity.ServerPos.Yaw + GameMath.PIHALF));
            entity.ServerPos.Motion.Set(velocity);

            entity.Pos.SetFrom(entity.ServerPos);
            entity.World = byEntity.World;

            byEntity.World.SpawnEntity(entity);
            byEntity.StartAnimation("throw");
        }


        public override void GetHeldItemInfo(ItemStack stack, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(stack, dsc, world, withDebugInfo);
            if (stack.Collectible.Attributes == null) return;
            dsc.AppendLine(Lang.Get("0.5 blunt damage when thrown. Spawns an angry mob of bees upon impact."));
        }
        
    }
}