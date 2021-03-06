﻿using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    abstract public class BaseDoor : Block
    {
        public abstract string GetKnobOrientation();
        public abstract BlockFacing GetDirection();
        protected abstract BlockPos TryGetConnectedDoorPos(BlockPos pos);
        protected abstract void Open(IWorldAccessor world, IPlayer byPlayer, BlockPos position);

        public bool IsSameDoor(Block block)
        {
            string[] parts = Code.Path.Split('-');
            string[] otherParts = block.Code.Path.Split('-');
            return parts[0] == otherParts[0];
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockPos pos = blockSel.Position;
            Open(world, byPlayer, pos);

            world.PlaySoundAt(new AssetLocation("sounds/block/door"), pos.X + 0.5f, pos.Y + 0.5f, pos.Z + 0.5f, byPlayer);

            TryOpenConnectedDoor(world, byPlayer, pos);
            return true;
        }

        protected void TryOpenConnectedDoor(IWorldAccessor world, IPlayer byPlayer, BlockPos pos)
        {
            BlockPos door2Pos = TryGetConnectedDoorPos(pos);
            if (door2Pos != null)
            {
                Block nBlock1 = world.BlockAccessor.GetBlock(pos);
                Block nBlock2 = world.BlockAccessor.GetBlock(door2Pos);

                bool isDoor1 = IsSameDoor(nBlock1);
                bool isDoor2 = IsSameDoor(nBlock2);
                if (isDoor1 && isDoor2)
                {
                    if(nBlock2 is BaseDoor)
                    {
                        BaseDoor door2 = (BaseDoor)nBlock2;
                        door2.Open(world, byPlayer, door2Pos);
                    }
                }
            }
        }
    }
}
