﻿using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorFiniteSpreadingLiquid : BlockBehavior
    {
        public static Vec2i[] downPaths = ShapeUtil.GetSquarePointsSortedByMDist(3);

        public static SimpleParticleProperties steamParticles;

        public static int ReplacableThreshold = 5000;

        //The sound to play when a liquid collision causes blocks to be replaced
        private AssetLocation collisionReplaceSound;

        //Controls how fast the liquid spreads
        private int spreadDelay = 150;

        //The liquid this one can collide with
        private string collidesWith;

        //Block code to use when colliding with the source block of a different liquid

        AssetLocation liquidSourceCollisionReplacement;

        //Block code to use when colliding with a flowing block of a different liquid
        AssetLocation liquidFlowingCollisionReplacement;

        
        public BlockBehaviorFiniteSpreadingLiquid(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            spreadDelay = properties["spreadDelay"].AsInt();

            collisionReplaceSound = CreateAssetLocation(properties, "sounds/", "liquidCollisionSound");
            liquidSourceCollisionReplacement = CreateAssetLocation(properties, "sourceReplacementCode");
            liquidFlowingCollisionReplacement = CreateAssetLocation(properties, "flowingReplacementCode");
            collidesWith = properties["collidesWith"]?.AsString();
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handled)
        {
            if (world is IServerWorldAccessor)
            {
                world.RegisterCallbackUnique(OnDelayedWaterUpdateCheck, blockSel.Position, spreadDelay);
            }
            return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref handled);
        }

        public override void OnNeighourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            if (world is IServerWorldAccessor)
            {
                world.RegisterCallbackUnique(OnDelayedWaterUpdateCheck, pos, spreadDelay);
            }
        }

        private void OnDelayedWaterUpdateCheck(IWorldAccessor world, BlockPos pos, float dt)
        {
            SpreadAndUpdateLiquidLevels(world, pos);
            world.BulkBlockAccessor.Commit();
        }

        private void SpreadAndUpdateLiquidLevels(IWorldAccessor world, BlockPos pos)
        {
            // Slightly weird hack 
            // 1. We call this method also for other blocks, so can't rely on this.bock
            // 2. our own liquid level might have changed from other nearby liquid sources
            Block block = world.BlockAccessor.GetBlock(pos);

            int liquidLevel = block.LiquidLevel;
            if (liquidLevel > 0)
            {
                if (TryLoweringLiquidLevel(block, world, pos) == false)//Lower liquid if not connected to source block
                {
                    Block downBlock = world.BlockAccessor.GetBlock(pos.DownCopy());
                    bool onSolidGround = downBlock.Replaceable < ReplacableThreshold;
                    if (!onSolidGround)
                    {
                        TrySpreadDownwards(block, world, pos);
                    }
                    else if (liquidLevel > 1)//Can we still spread somewhere
                    {
                        List<PosAndDist> downwardPaths = FindDownwardPaths(world, pos, block);
                        if (downwardPaths.Count > 0)//Prefer flowing to downward paths rather than outward
                        {
                            FlowTowardDownwardPaths(downwardPaths, block, world);
                        }
                        else if (TryFindSourceAndSpread(pos, world) == false)
                        {
                            TrySpreadHorizontal(block, world, pos);
                        }
                    }
                }
            }
        }

        private void FlowTowardDownwardPaths(List<PosAndDist> downwardPaths, Block block, IWorldAccessor world)
        {
            foreach (PosAndDist pod in downwardPaths)
            {
                if (CanSpreadIntoBlock(block, pod.pos, world))
                {
                    Block neighborBlock = world.BlockAccessor.GetBlock(pod.pos);
                    if (IsDifferentCollidableLiquid(block, neighborBlock))
                    {
                        ReplaceLiquidBlock(neighborBlock, pod.pos, world);
                    }
                    else
                    {
                        SpreadLiquid(GetLessLiquidBlockId(world, block), pod.pos, world);
                    }
                }
            }
        }

        private void TrySpreadDownwards(Block ourBlock, IWorldAccessor world, BlockPos pos)
        {
            BlockPos npos = pos.DownCopy();

            Block neighborBlock = world.BlockAccessor.GetBlock(npos);
            if (CanSpreadIntoBlock(block, neighborBlock, world))
            {
                IBlockAccessor blockAccess = world.BulkBlockAccessor;
                if (IsDifferentCollidableLiquid(block, neighborBlock))
                {
                    ReplaceLiquidBlock(neighborBlock, npos, world);
                    TryFindSourceAndSpread(npos, world);
                }
                else
                {
                    SpreadLiquid(GetFallingLiquidBlockId(block, world), npos, world);
                }
            }
        }

        private void TrySpreadHorizontal(Block ourblock, IWorldAccessor world, BlockPos pos)
        {
            foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
            {
                TrySpreadIntoBlock(ourblock, pos.AddCopy(facing), world);
            }
        }

        private void ReplaceLiquidBlock(Block liquidBlock, BlockPos pos, IWorldAccessor world)
        {
            Block replacementBlock = GetReplacementBlock(liquidBlock, world);
            if (replacementBlock != null)
            {
                world.BulkBlockAccessor.SetBlock(replacementBlock.BlockId, pos);
                NotifyNeighborsOfBlockChange(pos, world);
                GenerateSteamParticles(pos, world);
                world.PlaySoundAt(collisionReplaceSound, pos.X, pos.Y, pos.Z);
                world.RegisterCallbackUnique(OnDelayedWaterUpdateCheck, pos.UpCopy(), spreadDelay);
            }
        }

        private void SpreadLiquid(ushort blockId, BlockPos pos, IWorldAccessor world)
        {
            world.BulkBlockAccessor.SetBlock(blockId, pos);
            world.RegisterCallbackUnique(OnDelayedWaterUpdateCheck, pos, spreadDelay);

            Block ourBlock = world.GetBlock(blockId);
            TryReplaceNearbyLiquidBlocks(ourBlock, pos, world);
        }

        /// <summary>
        /// Replaces nearby liquid if it's not the same as this liquid. Prevents lava and water from being adjacent blocks
        /// </summary>
        /// <param name="ourBlock"></param>
        /// <param name="pos"></param>
        /// <param name="world"></param>
        private void TryReplaceNearbyLiquidBlocks(Block ourBlock, BlockPos pos, IWorldAccessor world)
        {
            foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
            {
                BlockPos npos = pos.AddCopy(facing);
                Block neib = world.BlockAccessor.GetBlock(npos);
                if (IsDifferentCollidableLiquid(ourBlock, neib))
                {
                    ReplaceLiquidBlock(ourBlock, npos, world);
                }
            }
        }

        /// <summary>
        /// Traverses upward until the source liquid block is found and spreads outward from there.
        /// </summary>
        /// <param name="startingPos"></param>
        /// <param name="world"></param>
        /// <returns>True if the source liquid block was found, false otherwise</returns>
        private bool TryFindSourceAndSpread(BlockPos startingPos, IWorldAccessor world)
        {
            BlockPos sourceBlockPos = startingPos.UpCopy();
            Block sourceBlock = world.BlockAccessor.GetBlock(sourceBlockPos);
            while (sourceBlock.IsLiquid())
            {
                if (IsLiquidSourceBlock(sourceBlock))
                {
                    TrySpreadHorizontal(sourceBlock, world, sourceBlockPos);
                    return true;
                }
                sourceBlockPos.Add(0, 1, 0);
                sourceBlock = world.BlockAccessor.GetBlock(sourceBlockPos);
            }
            return false;
        }

        private void GenerateSteamParticles(BlockPos pos, IWorldAccessor world)
        {
            float minQuantity = 50;
            float maxQuantity = 100;
            int color = ColorUtil.ToRgba(100, 225, 225, 225);
            Vec3d minPos = new Vec3d();
            Vec3d addPos = new Vec3d();
            Vec3f minVelocity = new Vec3f(-0.25f, 0.1f, -0.25f);
            Vec3f maxVelocity = new Vec3f(0.25f, 0.1f, 0.25f);
            float lifeLength = 2.0f;
            float gravityEffect = -0.015f;
            float minSize = 0.1f;
            float maxSize = 0.1f;

            SimpleParticleProperties steamParticles = new SimpleParticleProperties(
                minQuantity, maxQuantity,
                color,
                minPos, addPos,
                minVelocity, maxVelocity,
                lifeLength,
                gravityEffect,
                minSize, maxSize,
                EnumParticleModel.Quad
            );
            steamParticles.minPos.Set(pos.ToVec3d().AddCopy(0.5, 1.1, 0.5));
            steamParticles.addPos.Set(new Vec3d(0.5, 1.0, 0.5));
            steamParticles.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEARINCREASE, 1.0f);
            world.SpawnParticles(steamParticles);
        }

        private void NotifyNeighborsOfBlockChange(BlockPos pos, IWorldAccessor world)
        {
            foreach (BlockFacing facing in BlockFacing.ALLFACES)
            {
                BlockPos npos = pos.AddCopy(facing);
                Block neib = world.BlockAccessor.GetBlock(npos);
                neib.OnNeighourBlockChange(world, npos, pos);
            }
        }

        private Block GetReplacementBlock(Block neighborBlock, IWorldAccessor world)
        {
            AssetLocation replacementLocation = liquidFlowingCollisionReplacement;
            if (IsLiquidSourceBlock(neighborBlock))
            {
                replacementLocation = liquidSourceCollisionReplacement;
            }
            return replacementLocation == null ? null : world.GetBlock(replacementLocation);
        }

        /// <summary>
        /// Returns true when this block and the other block have a matterstate of liquid and are different types of liquids.
        /// Only counts liquids with block codes of length 2. This allows us to exclude plants that live under water and only
        /// consider things like water and lava which have block codes that look like "water-7" and "lava-7". The other
        /// block must be the one configured in the collidesWith property.
        /// </summary>
        /// <param name="block">The block owning this behavior</param>
        /// <param name="other">The block we are colliding with</param>
        /// <returns>True if the two blocks are different liquids that can collide, false otherwise</returns>
        private bool IsDifferentCollidableLiquid(Block block, Block other)
        {
            if(other.IsLiquid() && block.IsLiquid())
            {
                string[] blockParts = block.Code.Path.Split('-');
                string[] otherBlockParts = other.Code.Path.Split('-');
                return collidesWith != null && collidesWith.Equals(otherBlockParts[0]) && otherBlockParts.Length == 2 && blockParts.Length == 2 && blockParts[0] != otherBlockParts[0];
            }
            return false;
        }

        private bool IsSameLiquid(Block block, Block other)
        {
            return block.CodeWithoutParts(1) == other.CodeWithoutParts(1);
        }

        private bool IsLiquidSourceBlock(Block block)
        {
            return block.LiquidLevel == 7;
        }

        /// <summary>
        /// Tries to lower the liquid level at the given position if the liquid is not connected to a source.
        /// 
        /// </summary>
        /// <param name="ourBlock"></param>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns>True if the liquid was lowered at the given position, false otherwise</returns>
        private bool TryLoweringLiquidLevel(Block ourBlock, IWorldAccessor world, BlockPos pos)
        {
            if (IsLiquidSourceBlock(ourBlock) == false)
            {
                int nlevel = GetMaxNeighbourLiquidLevel(ourBlock, world, pos);
                if (nlevel <= ourBlock.LiquidLevel)
                {
                    LowerLiquidLevelAndNotifyNeighbors(ourBlock, pos, world);
                    return true;
                }
            }
            return false;
        }

        private void LowerLiquidLevelAndNotifyNeighbors(Block block, BlockPos pos, IWorldAccessor world)
        {
            SpreadLiquid(GetLessLiquidBlockId(world, block), pos, world);

            for (int i = 0; i < BlockFacing.ALLFACES.Length; i++)
            {
                BlockPos npos = pos.AddCopy(BlockFacing.ALLFACES[i]);
                world.BlockAccessor.GetBlock(npos).OnNeighourBlockChange(world, npos, pos);
            }
        }

        private void TrySpreadIntoBlock(Block ourblock, BlockPos npos, IWorldAccessor world)
        {
            IBlockAccessor blockAccess = world.BulkBlockAccessor;
            Block neighborBlock = world.BlockAccessor.GetBlock(npos);
            if (CanSpreadIntoBlock(ourblock, neighborBlock, world))
            {
                if (IsDifferentCollidableLiquid(block, neighborBlock))
                {
                    ReplaceLiquidBlock(neighborBlock, npos, world);
                }
                else
                {
                    SpreadLiquid(GetLessLiquidBlockId(world, ourblock), npos, world);
                }
            }
        }

        public ushort GetLessLiquidBlockId(IWorldAccessor world, Block block)
        {
            if (block.LiquidLevel == 1) return 0;
            return world.GetBlock(block.CodeWithParts("" + (block.LiquidLevel - 1))).BlockId;
        }

        private ushort GetFallingLiquidBlockId(Block ourBlock, IWorldAccessor world)
        {
            return world.GetBlock(ourBlock.CodeWithParts("6")).BlockId;
        }

        public int GetMaxNeighbourLiquidLevel(Block ourblock, IWorldAccessor world, BlockPos pos)
        {
            Block ublock = world.BlockAccessor.GetBlock(pos.X, pos.Y + 1, pos.Z);
            if (IsSameLiquid(ourblock, ublock))
            {
                return 7;
            }
            else
            {
                Block nblock = world.BlockAccessor.GetBlock(pos.X + 1, pos.Y, pos.Z);
                Block eblock = world.BlockAccessor.GetBlock(pos.X, pos.Y, pos.Z + 1);
                Block sblock = world.BlockAccessor.GetBlock(pos.X - 1, pos.Y, pos.Z);
                Block wblock = world.BlockAccessor.GetBlock(pos.X, pos.Y, pos.Z - 1);

                int level = 0;
                if (IsSameLiquid(ourblock, nblock)) level = Math.Max(level, nblock.LiquidLevel);
                if (IsSameLiquid(ourblock, eblock)) level = Math.Max(level, eblock.LiquidLevel);
                if (IsSameLiquid(ourblock, sblock)) level = Math.Max(level, sblock.LiquidLevel);
                if (IsSameLiquid(ourblock, wblock)) level = Math.Max(level, wblock.LiquidLevel);
                return level;
            }
        }

        public bool CanSpreadIntoBlock(Block ourblock, Block neighborBlock, IWorldAccessor world)
        {
            bool isSameLiquid = neighborBlock.CodeWithoutParts(1) == ourblock.CodeWithoutParts(1);

            return
                // Either neighbour liquid at a lower level
                (isSameLiquid && neighborBlock.LiquidLevel < ourblock.LiquidLevel) ||
                // Or the neighbour block can hold liquid and neighbour is below or we are on solid ground
                (!isSameLiquid && neighborBlock.Replaceable >= ReplacableThreshold)
            ;
        }

        public bool CanSpreadIntoBlock(Block ourblock, BlockPos npos, IWorldAccessor world)
        {
            Block block = world.BlockAccessor.GetBlock(npos);

            return CanSpreadIntoBlock(ourblock, block, world);
        }

        public override bool IsReplacableBy(Block byBlock, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;
            return (block.IsLiquid() || block.Replaceable >= ReplacableThreshold) && byBlock.Replaceable <= block.Replaceable;
        }

        public List<PosAndDist> FindDownwardPaths(IWorldAccessor world, BlockPos pos, Block ourBlock)
        {
            List<PosAndDist> paths = new List<PosAndDist>();
            Queue<BlockPos> uncheckedPositions = new Queue<BlockPos>();
            int shortestPath = 99;

            for (int i = 0; i < downPaths.Length; i++)
            {
                Vec2i offset = downPaths[i];

                Block block = world.BlockAccessor.GetBlock(pos.X + offset.X, pos.Y - 1, pos.Z + offset.Y);
                Block aboveblock = world.BlockAccessor.GetBlock(pos.X + offset.X, pos.Y, pos.Z + offset.Y);

                if (aboveblock.LiquidLevel < ourBlock.LiquidLevel && block.Replaceable >= ReplacableThreshold && aboveblock.Replaceable >= ReplacableThreshold)
                {
                    uncheckedPositions.Enqueue(new BlockPos(pos.X + offset.X, pos.Y, pos.Z + offset.Y));

                    BlockPos foundPos = BfsSearchPath(world, uncheckedPositions, pos, ourBlock);
                    if (foundPos != null)
                    {
                        PosAndDist pad = new PosAndDist() { pos = foundPos, dist = pos.ManhattenDistance(pos.X + offset.X, pos.Y, pos.Z + offset.Y) };

                        if (pad.dist == 1 && ourBlock.LiquidLevel < 7)
                        {
                            paths.Clear();
                            paths.Add(pad);
                            return paths;
                        }

                        paths.Add(pad);
                        shortestPath = Math.Min(shortestPath, pad.dist);
                    }
                }
            }

            // Now we remove all suboptimal paths
            for (int i = 0; i < paths.Count; i++)
            {
                if (paths[i].dist > shortestPath)
                {
                    paths.RemoveAt(i);
                    i--;
                }
            }

            return paths;
        }


        private BlockPos BfsSearchPath(IWorldAccessor world, Queue<BlockPos> uncheckedPositions, BlockPos target, Block ourBlock)
        {
            BlockPos pos, npos = new BlockPos();
            while (uncheckedPositions.Count > 0)
            {
                pos = uncheckedPositions.Dequeue();
                int curDist = pos.ManhattenDistance(target);

                for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
                {
                    BlockFacing facing = BlockFacing.HORIZONTALS[i];
                    npos.Set(pos.X + facing.Normali.X, target.Y, pos.Z + facing.Normali.Z);
                    if (npos.ManhattenDistance(target) > curDist) continue;

                    if (npos.Equals(target)) return pos;

                    Block block = world.BlockAccessor.GetBlock(npos);
                    if (!block.IsLiquid() && block.Replaceable < ReplacableThreshold) continue;

                    uncheckedPositions.Enqueue(npos.Copy());
                }

            }

            return null;
        }


        public override bool ShouldReceiveClientGameTicks(IWorldAccessor world, IPlayer byPlayer, BlockPos pos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            if (block.ParticleProperties == null || block.ParticleProperties.Length == 0) return false;

            //Would be better to configure with a property but since the client does not call Initialize, the properties are not honored
            //in this method. This will have to do until the properties are honored or this method becomes a separate client behavior.
            if (block.Code.Path.StartsWith("lava"))
            {
                int r = world.BlockAccessor.GetBlock(pos.X, pos.Y + 1, pos.Z).Replaceable;
                return r > ReplacableThreshold;
            }
            else
            {
                return
                    pos.Y >= 2 &&
                    world.BlockAccessor.GetBlock(pos.X, pos.Y - 1, pos.Z).Replaceable >= ReplacableThreshold &&
                    world.BlockAccessor.GetBlock(pos.X, pos.Y - 2, pos.Z).Replaceable >= ReplacableThreshold
                ;
            }
        }

        private static AssetLocation CreateAssetLocation(JsonObject properties, string propertyName)
        {
            return CreateAssetLocation(properties, null, propertyName);
        }

        private static AssetLocation CreateAssetLocation(JsonObject properties, string prefix, string propertyName)
        {
            string value = properties[propertyName]?.AsString();
            if (value == null)
            {
                return null;
            }
            else
            {
                return prefix == null ? new AssetLocation(value) : new AssetLocation(prefix + value);
            }
        }

    }


    public class PosAndDist
    {
        public BlockPos pos;
        public int dist;
    }

}
