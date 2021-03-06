﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Common;

namespace Vintagestory.ServerMods.NoObf
{
    [JsonObject(MemberSerialization.OptIn)]
    public class BlockPatchConfig
    {
        [JsonProperty]
        public NatFloat ChanceMultiplier;
        [JsonProperty]
        public BlockPatch[] Patches;

        internal void ResolveBlockIds(ICoreServerAPI api, RockstrataWorldProperty rockstrata)
        {
            for (int i = 0; i < Patches.Length; i++)
            {
                BlockPatch patch = Patches[i];

                List<Block> blocks = new List<Block>();

                for (int j = 0; j < patch.blockCodes.Length; j++)
                {
                    AssetLocation code = patch.blockCodes[j];

                    if (code.Path.Contains("{rocktype}"))
                    {
                        if (patch.BlocksByRockType == null) patch.BlocksByRockType = new Dictionary<ushort, Block[]>();                                

                        for (int k = 0; k < rockstrata.Variants.Length; k++)
                        {
                            string rocktype = rockstrata.Variants[k].BlockCode.Path.Split('-')[1];
                            patch.BlocksByRockType.Add(
                                api.WorldManager.GetBlockId(rockstrata.Variants[k].BlockCode), 
                                new Block[] { api.World.GetBlock(code.CopyWithPath(code.Path.Replace("{rocktype}", rocktype))) }
                            );
                        }
                    }
                    else
                    {
                        blocks.Add(api.WorldManager.GetBlockType(api.WorldManager.GetBlockId(code)));
                    }
                }

                patch.Blocks = blocks.ToArray();

                if (patch.BlockCodeIndex == null)
                {
                    patch.BlockCodeIndex = NatFloat.createUniform(0, patch.Blocks.Length);
                }
            }
        }



        int rain;
        float rainRel;
        int temp;
        float sealevelDistRel;
        float fertilityRel;

        public void InitPos(IWorldManagerAPI world, int climate, float forestRel, int y)
        {
            rain = TerraGenConfig.GetRainFall((climate >> 8) & 0xff, y);
            rainRel = rain / 255f;
            temp = TerraGenConfig.GetScaledAdjustedTemperature((climate >> 16) & 0xff, y - TerraGenConfig.seaLevel);
            sealevelDistRel = ((float)y - TerraGenConfig.seaLevel) / ((float)world.MapSizeY - TerraGenConfig.seaLevel);
            fertilityRel = TerraGenConfig.GetFertility(rain, temp, sealevelDistRel) / 255f;
        }

        internal bool IsPatchSuitableAt(BlockPatch patch, Block onBlock, IWorldManagerAPI world, int climate, int y, float forestRel)
        {
            if ((patch.Placement == EnumBlockPatchPlacement.NearWater || patch.Placement == EnumBlockPatchPlacement.UnderWater) && !onBlock.IsWater()) return false;

            rain = TerraGenConfig.GetRainFall((climate >> 8) & 0xff, y);
            rainRel = rain / 255f;
            temp = TerraGenConfig.GetScaledAdjustedTemperature((climate >> 16) & 0xff, y - TerraGenConfig.seaLevel);
            sealevelDistRel = ((float)y - TerraGenConfig.seaLevel) / ((float)world.MapSizeY - TerraGenConfig.seaLevel);
            fertilityRel = TerraGenConfig.GetFertility(rain, temp, sealevelDistRel) / 255f;


            return
                fertilityRel >= patch.MinFertility && fertilityRel <= patch.MaxFertility &&
                rainRel >= patch.MinRain && rainRel <= patch.MaxRain &&
                temp >= patch.MinTemp && temp <= patch.MaxTemp &&
                sealevelDistRel >= patch.MinY && sealevelDistRel <= patch.MaxY &&
                forestRel >= patch.MinForest && forestRel <= patch.MaxForest
            ;
        }
    }
}
