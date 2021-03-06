﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class QuernTopRenderer : IRenderer
    {
        internal bool ShouldRender;
        internal bool ShouldRotate;

        private ICoreClientAPI api;
        private BlockPos pos;
        

        MeshRef meshref;
        public Matrixf ModelMat = new Matrixf();

        public float Angle;

        public QuernTopRenderer(ICoreClientAPI coreClientAPI, BlockPos pos, MeshData mesh)
        {
            this.api = coreClientAPI;
            this.pos = pos;

            meshref = coreClientAPI.Render.UploadMesh(mesh);
        }

        public double RenderOrder
        {
            get { return 0.5; }
        }

        public int RenderRange => 24;




        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (meshref == null || !ShouldRender) return;

            IRenderAPI rpi = api.Render;
            Vec3d camPos = api.World.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);
            prog.Tex2D = api.BlockTextureAtlas.AtlasTextureIds[0];


            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
                .Translate(0.5f, 11f / 16f, 0.5f)
                .RotateY(Angle * GameMath.DEG2RAD)
                .Translate(-0.5f, 0, -0.5f)
                .Values
            ;

            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
            rpi.RenderMesh(meshref);
            prog.Stop();



            if (ShouldRotate)
            {
                Angle += deltaTime * 40;
            }
        }


        internal void Unregister()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        }

        public void Dispose()
        {
            meshref.Dispose();
        }


    }
}
