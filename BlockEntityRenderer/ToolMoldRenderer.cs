﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ToolMoldRenderer : IRenderer
    {
        BlockPos pos;
        ICoreClientAPI api;


        MeshRef[] quadModelRefs;

        public double RenderOrder
        {
            get { return 0; }
        }

        public int RenderRange
        {
            get { return 24; }
        }

        /// <summary>
        /// 0..1
        /// </summary>
        public float Level = 0;
        /// <summary>
        /// 0..1300
        /// </summary>
        public float Temperature = 0;

        public AssetLocation TextureName = null;

        internal RectangleFloat[] fillQuadsByLevel;

        public static RectangleFloat[] stdFillQuadsByLevel = new RectangleFloat[]
        {
            new RectangleFloat(2, 2, 14, 14),
        };

        public ToolMoldRenderer(BlockPos pos, ICoreClientAPI api, RectangleFloat[] fillQuadsByLevel = null)
        {
            this.pos = pos;
            this.api = api;
            if (fillQuadsByLevel == null) fillQuadsByLevel = stdFillQuadsByLevel;

            this.fillQuadsByLevel = fillQuadsByLevel;

            quadModelRefs = new MeshRef[fillQuadsByLevel.Length];
            MeshData modeldata = QuadMeshUtil.GetQuad();
            modeldata.Rgba = new byte[4 * 4];
            modeldata.Rgba.Fill((byte)255);
            modeldata.Flags = new int[4 * 4];

            for (int i = 0; i < quadModelRefs.Length; i++)
            {
                RectangleFloat size = fillQuadsByLevel[i];

                modeldata.Uv = new float[]
                {
                    size.X/16f + size.Width/16f, size.Y/16f + size.Height/16f,
                    size.X/16f, size.Y/16f + size.Height/16f,
                    size.X/16f, size.Y/16f,
                    size.X/16f + size.Width/16f, size.Y/16f
                };

                quadModelRefs[i] = api.Render.UploadMesh(modeldata);
            }
        }


        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (Level <= 0 || TextureName == null) return;

            int voxelY = (int)GameMath.Clamp(Level, 0, fillQuadsByLevel.Length - 1);

            IRenderAPI rpi = api.Render;
            IClientWorldAccessor worldAccess = api.World;
            EntityPos plrPos = worldAccess.Player.Entity.Pos;
            Vec3d camPos = worldAccess.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            IStandardShaderProgram prog = rpi.StandardShader;
            prog.Use();
            prog.RgbaAmbientIn = rpi.AmbientColor;
            prog.RgbaFogIn = rpi.FogColor;
            prog.FogMinIn = rpi.FogMin;
            prog.FogDensityIn = rpi.FogDensity;
            prog.RgbaTint = ColorUtil.WhiteArgbVec;


            Vec4f lightrgbs = api.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);
            float[] glowColor = ColorUtil.getIncandescenceColorAsColor4f((int)Temperature);
            lightrgbs.R += 2 * glowColor[0];
            lightrgbs.G += 2 * glowColor[1];
            lightrgbs.B += 2 * glowColor[2];

            prog.RgbaLightIn = lightrgbs;
            prog.RgbaBlockIn = ColorUtil.WhiteArgbVec;
            prog.ExtraGlow = (int)GameMath.Clamp((Temperature - 500) / 4, 0, 255);

            int texid = api.Render.GetOrLoadTexture(TextureName);
            rpi.BindTexture2d(texid);
            rpi.GlPushMatrix();
            rpi.GlLoadMatrix(api.Render.CameraMatrixOrigin);
            rpi.GlTranslate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z);

            RectangleFloat rect = fillQuadsByLevel[voxelY];

            rpi.GlTranslate(rect.X / 32f + rect.Width / 32f, 1.01f / 16f + Math.Max(0, Level / 16f - 0.0625f/3), rect.Y / 32f + rect.Height / 32f);
            rpi.GlRotate(90, 1, 0, 0);
            rpi.GlScale(0.5f * rect.Width / 16f, 0.5f * rect.Height / 16f, 0.5f);

            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
            prog.ModelViewMatrix = rpi.CurrentModelviewMatrix;
            rpi.RenderMesh(quadModelRefs[voxelY]);
            rpi.GlPopMatrix();
            

            prog.Stop();

            rpi.GlEnableCullFace();
        }


        public void Unregister()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        }

        // Called by UnregisterRenderer
        public void Dispose()
        {
            for (int i = 0; i < quadModelRefs.Length; i++)
            {
                api.Render.DeleteMesh(quadModelRefs[i]);
            }

            
        }
    }
}
