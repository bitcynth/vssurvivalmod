﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockGenericTypedContainer : Block, ITexPositionSource
    {
        public int AtlasSize { get { return tmpTextureSource.AtlasSize; } }

        string curType;
        ITexPositionSource tmpTextureSource;
        ICoreAPI api;
             

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                return tmpTextureSource[curType + "-" + textureCode];
            }
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            this.api = api;
        }


        public string GetType(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityGenericTypedContainer be = blockAccessor.GetBlockEntity(pos) as BlockEntityGenericTypedContainer;
            if (be != null)
            {
                return be.type;
            }

            return "normal-generic";
        }



        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            Dictionary<string, MeshRef> meshrefs = new Dictionary<string, MeshRef>();

            object obj;
            if (capi.ObjectCache.TryGetValue("genericTypedContainerMeshRefs", out obj))
            {
                meshrefs = obj as Dictionary<string, MeshRef>;
            }
            else
            {
                Dictionary<string, MeshData> meshes = GenGuiMeshes(capi);

                foreach (var val in meshes)
                {
                    meshrefs[val.Key] = capi.Render.UploadMesh(val.Value);
                }

                capi.ObjectCache["genericTypedContainerMeshRefs"] = meshrefs;
            }

            string type = itemstack.Attributes.GetString("type", "normal-generic");

            meshrefs.TryGetValue(type, out renderinfo.ModelRef);
        }




        public override void OnUnloaded(ICoreAPI api)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi == null) return;

            object obj;
            if (capi.ObjectCache.TryGetValue("genericTypedContainerMeshRefs", out obj))
            {
                Dictionary<string, MeshRef> meshrefs = obj as Dictionary<string, MeshRef>;

                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove("genericTypedContainerMeshRefs");
            }
        }

        public Dictionary<string, MeshData> GenGuiMeshes(ICoreClientAPI capi)
        {
            string[] types = this.Attributes["types"].AsStringArray();
            

            Dictionary<string, MeshData> meshes = new Dictionary<string, MeshData>();

            foreach (string type in types)
            {
                string shapename = this.Attributes["shape"][type].AsString();
                meshes[type] = GenMesh(capi, type, shapename);
            }

            return meshes;
        }


        public MeshData GenMesh(ICoreClientAPI capi, string type, string shapename)
        {
            tmpTextureSource = capi.Tesselator.GetTexSource(this);

            Shape shape = capi.Assets.TryGet("shapes/block/wood/chest/" + shapename + ".json")?.ToObject<Shape>();
            if (shape == null)
            {
                shape = capi.Assets.TryGet("shapes/block/wood/chest/" + shapename + "1.json").ToObject<Shape>();
            }
            
            curType = type;
            MeshData mesh;
            capi.Tesselator.TesselateShape("typedcontainer", shape, out mesh, this, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));
            return mesh;
        }
        
        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            BlockEntityGenericTypedContainer be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityGenericTypedContainer;
            if (be != null)
            {
                ICoreClientAPI capi = api as ICoreClientAPI;
                string shapename = this.Attributes["shape"][be.type].AsString();
                blockModelData = GenMesh(capi, be.type, shapename);

                Shape shape = capi.Assets.TryGet("shapes/block/wood/chest/" + shapename + ".json")?.ToObject<Shape>();
                if (shape == null)
                {
                    shape = capi.Assets.TryGet("shapes/block/wood/chest/" + shapename + "1.json").ToObject<Shape>();
                }

                MeshData md;
                capi.Tesselator.TesselateShape("typedcontainer-decal", shape, out md, decalTexSource, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));
                decalModelData = md;

                string facing = LastCodePart();
                if (facing == "north") { decalModelData.Rotate(new API.MathTools.Vec3f(0.5f, 0.5f, 0.5f), 0, 1 * GameMath.PIHALF, 0); }
                if (facing == "east") { decalModelData.Rotate(new API.MathTools.Vec3f(0.5f, 0.5f, 0.5f), 0, 0 * GameMath.PIHALF, 0); }
                if (facing == "south") { decalModelData.Rotate(new API.MathTools.Vec3f(0.5f, 0.5f, 0.5f), 0, 3 * GameMath.PIHALF, 0); }
                if (facing == "west") { decalModelData.Rotate(new API.MathTools.Vec3f(0.5f, 0.5f, 0.5f), 0, 2 * GameMath.PIHALF, 0); }


                return;
            }

            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
        }

        public override void DoPlaceBlock(IWorldAccessor world, BlockPos pos, BlockFacing onBlockFace, ItemStack byItemStack)
        {
            base.DoPlaceBlock(world, pos, onBlockFace, byItemStack);
            
        }


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack stack = base.OnPickBlock(world, pos);

            BlockEntityGenericTypedContainer be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityGenericTypedContainer;
            if (be != null)
            {
                stack.Attributes.SetString("type", be.type);
            }
            else
            {
                stack.Attributes.SetString("type", "normal-generic");
            }

            return stack;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                ItemStack[] drops = new ItemStack[] { OnPickBlock(world, pos) };

                if (this.Attributes["drop"]?[GetType(world.BlockAccessor, pos)]?.AsBool() == true && drops != null)
                {
                    for (int i = 0; i < drops.Length; i++)
                    {
                        world.SpawnItemEntity(drops[i], new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5), null);
                    }
                }

                if (Sounds?.Break != null)
                {
                    world.PlaySoundAt(Sounds.Break, pos.X, pos.Y, pos.Z, byPlayer);
                }
            }

            world.BlockAccessor.SetBlock(0, pos);
        }



        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override void GetHeldItemInfo(ItemStack stack, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(stack, dsc, world, withDebugInfo);

            string type = stack.Attributes.GetString("type");

            dsc.AppendLine(Lang.Get("Type: {0}", Lang.Get("generictype-" + type)));
        }


        public override int TextureSubIdForRandomBlockPixel(IWorldAccessor world, BlockPos pos, BlockFacing facing, ref int tintIndex)
        {
            BlockEntityGenericTypedContainer be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityGenericTypedContainer;
            if (be != null)
            {
                CompositeTexture tex = null;
                if (!Textures.TryGetValue(be.type + "-lid", out tex)) {
                    Textures.TryGetValue(be.type + "-top", out tex);
                }
                return tex?.Baked == null ? 0 : tex.Baked.TextureSubId;
            }

            return base.TextureSubIdForRandomBlockPixel(world, pos, facing, ref tintIndex);
        }
    }
}
