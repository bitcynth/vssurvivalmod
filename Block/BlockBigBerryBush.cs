﻿using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBerryBush : BlockPlant
    {
        internal override bool CanPlantStay(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Block block = blockAccessor.GetBlock(pos.DownCopy());
            return block.Fertility > 0 || (Attributes?["stackable"]?.AsBool() == true && block.Attributes?["stackable"]?.AsBool() == true && block is BlockBerryBush);
        }


        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing)
        {
            if (Textures == null || Textures.Count == 0) return 0;
            BakedCompositeTexture tex = Textures?.First().Value?.Baked;
            if (tex == null) return 0;

            int color = capi.BlockTextureAtlas.GetRandomPixel(tex.TextureSubId);
            color = capi.ApplyColorTintOnRgba(1, color, pos.X, pos.Y, pos.Z);
            return color;
        }


        public override int GetColor(ICoreClientAPI capi, BlockPos pos)
        {
            int color = base.GetColor(capi, pos);

            return capi.ApplyColorTintOnRgba(1, color, pos.X, pos.Y, pos.Z, false);
        }
    }
}
