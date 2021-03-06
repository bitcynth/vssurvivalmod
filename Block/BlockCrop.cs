﻿using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockCrop : Block
    {
        private static readonly float defaultGrowthProbability = 0.8f;

        private float tickGrowthProbability;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            tickGrowthProbability = Attributes?["tickGrowthProbability"] != null ? Attributes["tickGrowthProbability"].AsFloat(defaultGrowthProbability) : defaultGrowthProbability;
        }

        public override bool ShouldReceiveServerGameTicks(IWorldAccessor world, BlockPos pos, out object extra)
        {
            extra = null;

            if(world.Rand.NextDouble() < tickGrowthProbability && IsNotOnFarmland(world, pos))
            {
                extra = GetNextGrowthStageBlock(world, pos);
                return true;
            }
            return false;
        }

        public override void OnServerGameTick(IWorldAccessor world, BlockPos pos, object extra = null)
        {
            Block block = extra as Block;
            world.BlockAccessor.ExchangeBlock(block.BlockId, pos);
        }

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace)
        {
            Block block = blockAccessor.GetBlock(pos.X, pos.Y - 1, pos.Z);
            if (block.Fertility == 0) return false;

            if (blockAccessor.GetBlock(pos).IsReplacableBy(this))
            {
                blockAccessor.SetBlock(BlockId, pos);
                return true;
            }

            return false;
        }

        public int CurrentStage()
        {
            int stage;
            int.TryParse(LastCodePart(), out stage);
            return stage;
        }


        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            string info = world.BlockAccessor.GetBlock(pos.DownCopy()).GetPlacedBlockInfo(world, pos.DownCopy(), forPlayer);

            return
                "Required Nutrient: " + CropProps.RequiredNutrient + "\n" +
                "Growth stage: " + CurrentStage() + " / " + CropProps.GrowthStages +
                (info != null && info.Length > 0 ? "\n\nSoil:\n" + info : "")
            ;
        }

        private bool IsNotOnFarmland(IWorldAccessor world, BlockPos pos)
        {
            Block onBlock = world.BlockAccessor.GetBlock(pos.DownCopy());
            return onBlock.FirstCodePart().Equals("farmland") == false;
        }

        private Block GetNextGrowthStageBlock(IWorldAccessor world, BlockPos pos)
        {
            int nextStage = CurrentStage() + 1;
            Block block = world.GetBlock(CodeWithParts(nextStage.ToString()));
            if (block == null)
            {
                nextStage = 1;
            }
            return world.GetBlock(CodeWithParts(nextStage.ToString()));
        }
    }
}
