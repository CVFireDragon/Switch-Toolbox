﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Syroot.NintenTools.NSW.Bfres;
using Syroot.NintenTools.NSW.Bfres.Helpers;
using Syroot.NintenTools.NSW.Bfres.GFX;
using Switch_Toolbox.Library.IO;
using Switch_Toolbox.Library;
using Switch_Toolbox.Library.Rendering;
using OpenTK;
using System.Windows.Forms;
using Bfres.Structs;

namespace FirstPlugin
{
    public static class BfresSwitch
    {
        public static void Read(BFRESRender renderer, ResFile resFile, TreeNode ResFileNode)
        {
            int CurMdl = 0;
            foreach (Model mdl in resFile.Models)
            {
                FMDL model = new FMDL();
                model.Text = mdl.Name;
                model.Skeleton = new FSKL(mdl.Skeleton);
                model.Nodes.Add(model.Skeleton.node);
                model.Skeleton.reset();
                model.Skeleton.update();
                model.BFRESRender = renderer;
                model.Skeleton.node.BFRESRender = renderer;
                model.Model = mdl;
                foreach (Material mat in mdl.Materials)
                {
                    FMAT FMAT = new FMAT();
                    FMAT.BFRESRender = renderer;
                    FMAT.Text = mat.Name;
                    FMAT.ReadMaterial(mat);
                    model.Nodes[1].Nodes.Add(FMAT);
                    model.materials.Add(FMAT.Text, FMAT);
                }
                foreach (Shape shp in mdl.Shapes)
                {
                    VertexBuffer vertexBuffer = mdl.VertexBuffers[shp.VertexBufferIndex];
                    Material material = mdl.Materials[shp.MaterialIndex];
                    FSHP mesh = new FSHP();
                    mesh.BFRESRender = renderer;
                    mesh.ModelIndex = CurMdl;
                    ReadShapesVertices(mesh, shp, vertexBuffer, model);
                    mesh.MaterialIndex = shp.MaterialIndex;

                    model.Nodes[0].Nodes.Add(mesh);
                    model.shapes.Add(mesh);
                }
                ResFileNode.Nodes[0].Nodes.Add(model);
                renderer.models.Add(model);

                CurMdl++;
            }
        }
        public static Shape SaveShape(FSHP fshp)
        {
            Shape Shape = new Shape();
            Shape.VertexSkinCount = (byte)fshp.VertexSkinCount;
            Shape.Flags = ShapeFlags.HasVertexBuffer;
            Shape.BoneIndex = (ushort)fshp.boneIndx;
            Shape.MaterialIndex = (ushort)fshp.MaterialIndex;
            Shape.VertexBufferIndex = (ushort)fshp.VertexBufferIndex;
            Shape.KeyShapes = new List<KeyShape>();
            Shape.KeyShapeDict = new ResDict();
            Shape.Name = fshp.Text;
            Shape.SkinBoneIndices = fshp.GetIndices();
            Shape.TargetAttribCount = (byte)fshp.TargetAttribCount;
            Shape.SkinBoneIndices = fshp.BoneIndices;
            Shape.SubMeshBoundings = new List<Bounding>();
            Shape.RadiusArray = new List<float>();
            Shape.RadiusArray = fshp.boundingRadius;

            Shape.Meshes = new List<Mesh>();

            foreach (FSHP.BoundingBox box in fshp.boundingBoxes)
            {
                Bounding bnd = new Bounding();
                bnd.Center = new Syroot.Maths.Vector3F(box.Center.X, box.Center.Y, box.Center.Z);
                bnd.Extent = new Syroot.Maths.Vector3F(box.Extend.X, box.Extend.Y, box.Extend.Z);
                Shape.SubMeshBoundings.Add(bnd);
            }

            foreach (FSHP.LOD_Mesh mesh in fshp.lodMeshes)
            {
                Mesh msh = new Mesh();
                msh.MemoryPool = new MemoryPool();
                msh.SubMeshes = new List<SubMesh>();
                msh.PrimitiveType = (PrimitiveType)mesh.PrimitiveType;
                msh.FirstVertex = mesh.FirstVertex;

                foreach (FSHP.LOD_Mesh.SubMesh sub in mesh.subMeshes)
                {
                    SubMesh subMesh = new SubMesh();
                    subMesh.Offset = sub.offset;
                    subMesh.Count = (uint)mesh.faces.Count;
                    msh.SubMeshes.Add(subMesh);
                }

                IList<uint> faceList = new List<uint>();
                foreach (int f in mesh.faces)
                {
                    faceList.Add((uint)f);
                }
                if (faceList.Count > 65000)
                {
                    MessageBox.Show($"Warning! Your poly count for a single mesh {fshp.Text} is pretty high! ({faceList.Count})." +
                        $" You may want to split this!");
                    msh.SetIndices(faceList, IndexFormat.UInt32);
                }
                else
                    msh.SetIndices(faceList, IndexFormat.UInt16);

                Shape.Meshes.Add(msh);
                break;
            }
            return Shape;
        }

        public static void ReadShapesVertices(FSHP fshp, Shape shp, VertexBuffer vertexBuffer, FMDL model)
        {
            fshp.boundingBoxes.Clear();
            fshp.boundingRadius.Clear();
            fshp.BoneIndices.Clear();

            foreach (Bounding bnd in shp.SubMeshBoundings)
            {
                FSHP. BoundingBox box = new FSHP.BoundingBox();
                box.Center = new Vector3(bnd.Center.X, bnd.Center.Y, bnd.Center.Z);
                box.Extend = new Vector3(bnd.Extent.X, bnd.Extent.Y, bnd.Extent.Z);
                fshp. boundingBoxes.Add(box);
            }
            foreach (float rad in shp.RadiusArray)
            {
                fshp.boundingRadius.Add(rad);
            }

            fshp.VertexBufferIndex = shp.VertexBufferIndex;
            fshp.Shape = shp;
            fshp.VertexBuffer = vertexBuffer;
            fshp.VertexSkinCount = shp.VertexSkinCount;
            fshp.boneIndx = shp.BoneIndex;
            fshp.Text = shp.Name;
            fshp.TargetAttribCount = shp.TargetAttribCount;
            fshp.MaterialIndex = shp.MaterialIndex;

            if (shp.SkinBoneIndices != null)
            {
                foreach (ushort bn in shp.SkinBoneIndices)
                    fshp.BoneIndices.Add(bn);
            }

            ReadMeshes(fshp, shp);
            ReadVertexBuffer(fshp, vertexBuffer, model);
        }
        private static void ReadMeshes(FSHP fshp, Shape shp)
        {
            fshp.lodMeshes.Clear();

            foreach (Mesh msh in shp.Meshes)
            {
                uint FaceCount = msh.IndexCount;
                uint[] indicesArray = msh.GetIndices().ToArray();

                FSHP.LOD_Mesh lod = new FSHP.LOD_Mesh();
                foreach (SubMesh subMsh in msh.SubMeshes)
                {
                    FSHP.LOD_Mesh.SubMesh sub = new FSHP.LOD_Mesh.SubMesh();
                    sub.size = subMsh.Count;
                    sub.offset = subMsh.Offset;
                    lod.subMeshes.Add(sub);
                }
                lod.IndexFormat = (STIndexFormat)msh.IndexFormat;
                lod.PrimitiveType = (STPolygonType)msh.PrimitiveType;
                lod.FirstVertex = msh.FirstVertex;

                for (int face = 0; face < FaceCount; face++)
                    lod.faces.Add((int)indicesArray[face] + (int)msh.FirstVertex);

                fshp.lodMeshes.Add(lod);
            }
        }
        private static void ReadVertexBuffer(FSHP fshp, VertexBuffer vtx, FMDL model)
        {
            fshp.vertices.Clear();
            fshp.vertexAttributes.Clear();

            //Create a buffer instance which stores all the buffer data
            VertexBufferHelper helper = new VertexBufferHelper(vtx);

            //Set each array first from the lib if exist. Then add the data all in one loop
            Syroot.Maths.Vector4F[] vec4Positions = new Syroot.Maths.Vector4F[0];
            Syroot.Maths.Vector4F[] vec4Normals = new Syroot.Maths.Vector4F[0];
            Syroot.Maths.Vector4F[] vec4uv0 = new Syroot.Maths.Vector4F[0];
            Syroot.Maths.Vector4F[] vec4uv1 = new Syroot.Maths.Vector4F[0];
            Syroot.Maths.Vector4F[] vec4uv2 = new Syroot.Maths.Vector4F[0];
            Syroot.Maths.Vector4F[] vec4c0 = new Syroot.Maths.Vector4F[0];
            Syroot.Maths.Vector4F[] vec4t0 = new Syroot.Maths.Vector4F[0];
            Syroot.Maths.Vector4F[] vec4b0 = new Syroot.Maths.Vector4F[0];
            Syroot.Maths.Vector4F[] vec4w0 = new Syroot.Maths.Vector4F[0];
            Syroot.Maths.Vector4F[] vec4i0 = new Syroot.Maths.Vector4F[0];

            //For shape morphing
            Syroot.Maths.Vector4F[] vec4Positions1 = new Syroot.Maths.Vector4F[0];
            Syroot.Maths.Vector4F[] vec4Positions2 = new Syroot.Maths.Vector4F[0];

            foreach (VertexAttrib att in vtx.Attributes)
            {
                FSHP.VertexAttribute attr = new FSHP.VertexAttribute();
                attr.Name = att.Name;
                attr.Format = att.Format;

                if (att.Name == "_p0")
                    vec4Positions = AttributeData(att, helper, "_p0");
                if (att.Name == "_n0")
                    vec4Normals = AttributeData(att, helper, "_n0");
                if (att.Name == "_u0")
                    vec4uv0 = AttributeData(att, helper, "_u0");
                if (att.Name == "_u1")
                    vec4uv1 = AttributeData(att, helper, "_u1");
                if (att.Name == "_u2")
                    vec4uv2 = AttributeData(att, helper, "_u2");
                if (att.Name == "_c0")
                    vec4c0 = AttributeData(att, helper, "_c0");
                if (att.Name == "_t0")
                    vec4t0 = AttributeData(att, helper, "_t0");
                if (att.Name == "_b0")
                    vec4b0 = AttributeData(att, helper, "_b0");
                if (att.Name == "_w0")
                    vec4w0 = AttributeData(att, helper, "_w0");
                if (att.Name == "_i0")
                    vec4i0 = AttributeData(att, helper, "_i0");

                if (att.Name == "_p1")
                    vec4Positions1 = AttributeData(att, helper, "_p1");
                if (att.Name == "_p2")
                    vec4Positions2 = AttributeData(att, helper, "_p2");

                fshp.vertexAttributes.Add(attr);
            }
            for (int i = 0; i < vec4Positions.Length; i++)
            {
                Vertex v = new Vertex();
                if (vec4Positions.Length > 0)
                    v.pos = new Vector3(vec4Positions[i].X, vec4Positions[i].Y, vec4Positions[i].Z);
                if (vec4Positions1.Length > 0)
                    v.pos1 = new Vector3(vec4Positions1[i].X, vec4Positions1[i].Y, vec4Positions1[i].Z);
                if (vec4Positions2.Length > 0)
                    v.pos2 = new Vector3(vec4Positions2[i].X, vec4Positions2[i].Y, vec4Positions2[i].Z);
                if (vec4Normals.Length > 0)
                    v.nrm = new Vector3(vec4Normals[i].X, vec4Normals[i].Y, vec4Normals[i].Z);
                if (vec4uv0.Length > 0)
                    v.uv0 = new Vector2(vec4uv0[i].X, vec4uv0[i].Y);
                if (vec4uv1.Length > 0)
                    v.uv1 = new Vector2(vec4uv1[i].X, vec4uv1[i].Y);
                if (vec4uv2.Length > 0)
                    v.uv2 = new Vector2(vec4uv2[i].X, vec4uv2[i].Y);
                if (vec4w0.Length > 0)
                {
                    v.boneWeights.Add(vec4w0[i].X);
                    v.boneWeights.Add(vec4w0[i].Y);
                    v.boneWeights.Add(vec4w0[i].Z);
                    v.boneWeights.Add(vec4w0[i].W);
                }
                if (vec4i0.Length > 0)
                {
                    v.boneIds.Add((int)vec4i0[i].X);
                    v.boneIds.Add((int)vec4i0[i].Y);
                    v.boneIds.Add((int)vec4i0[i].Z);
                    v.boneIds.Add((int)vec4i0[i].W);
                }

                if (vec4t0.Length > 0)
                    v.tan = new Vector4(vec4t0[i].X, vec4t0[i].Y, vec4t0[i].Z, vec4t0[i].W);
                if (vec4b0.Length > 0)
                    v.bitan = new Vector4(vec4b0[i].X, vec4b0[i].Y, vec4b0[i].Z, vec4b0[i].W);
                if (vec4c0.Length > 0)
                    v.col = new Vector4(vec4c0[i].X, vec4c0[i].Y, vec4c0[i].Z, vec4c0[i].W);

                if (fshp.VertexSkinCount == 1)
                {
                    Matrix4 sb = model.Skeleton.bones[model.Skeleton.Node_Array[v.boneIds[0]]].transform;
                    v.pos = Vector3.TransformPosition(v.pos, sb);
                    v.nrm = Vector3.TransformNormal(v.nrm, sb);
                }
                if (fshp.VertexSkinCount == 0)
                {
                    Matrix4 NoBindFix = model.Skeleton.bones[fshp.boneIndx].transform;
                    v.pos = Vector3.TransformPosition(v.pos, NoBindFix);
                    v.nrm = Vector3.TransformNormal(v.nrm, NoBindFix);
                }
                fshp.vertices.Add(v);
            }
        }
        private static Syroot.Maths.Vector4F[] AttributeData(VertexAttrib att, VertexBufferHelper helper, string attName)
        {
            VertexBufferHelperAttrib attd = helper[attName];
            return attd.Data;
        }

        public static void SetSkeleton(this TreeNodeCustom skl, Skeleton skeleton, FSKL RenderableSkeleton)
        {
            if (skeleton.MatrixToBoneList == null)
                skeleton.MatrixToBoneList = new List<ushort>();

            RenderableSkeleton.Node_Array = new int[skeleton.MatrixToBoneList.Count];
            int nodes = 0;
            foreach (ushort node in skeleton.MatrixToBoneList)
            {
                RenderableSkeleton.Node_Array[nodes] = node;
                nodes++;
            }

            foreach (Bone bone in skeleton.Bones)
            {
                BfresBone STBone = new BfresBone(RenderableSkeleton);
                SetBone(STBone, bone);
                STBone.BFRESRender = RenderableSkeleton.node.BFRESRender; //to update viewport on bone edits
                RenderableSkeleton.bones.Add(STBone);
            }
            RenderableSkeleton.update();
            RenderableSkeleton.reset();

            foreach (var bone in RenderableSkeleton.bones)
                if (bone.Parent == null)
                    skl.Nodes.Add(bone);

            Runtime.abstractGlDrawables.Add(RenderableSkeleton);
        }
        public static void SetBone(this BfresBone bone, Bone bn)
        {
            bone.Bone = bn;
            bone.Text = bn.Name;
            bone.BillboardIndex = bn.BillboardIndex;
            bone.parentIndex = bn.ParentIndex;
            bone.scale = new float[3];
            bone.rotation = new float[4];
            bone.position = new float[3];
            if (bn.FlagsRotation == BoneFlagsRotation.Quaternion)
                bone.boneRotationType = 1;
            else
                bone.boneRotationType = 0;
            bone.scale[0] = bn.Scale.X;
            bone.scale[1] = bn.Scale.Y;
            bone.scale[2] = bn.Scale.Z;
            bone.rotation[0] = bn.Rotation.X;
            bone.rotation[1] = bn.Rotation.Y;
            bone.rotation[2] = bn.Rotation.Z;
            bone.rotation[3] = bn.Rotation.W;
            bone.position[0] = bn.Position.X;
            bone.position[1] = bn.Position.Y;
            bone.position[2] = bn.Position.Z;
        }
        public static void SetShape(this FSHP s, Shape shp)
        {
            shp.Name = s.Text;
            shp.MaterialIndex = (ushort)s.MaterialIndex;
            shp.BoneIndex = (ushort)s.boneIndx;
        }
        public static void CreateNewMaterial(string Name)
        {
            FMAT mat = new FMAT();
            mat.Text = Name;
            mat.Material = new Material();

            SetMaterial(mat, mat.Material);
        }
        public static void SetMaterial(this FMAT m, Material mat)
        {
            mat.Name = m.Text;

            if (m.Enabled)
                mat.Flags = MaterialFlags.Visible;
            else
                mat.Flags = MaterialFlags.None;

            if (mat.ShaderParamData == null)
                mat.ShaderParamData = new byte[0];

            byte[] ParamData = WriteShaderParams(m, mat);

            if (ParamData.Length != mat.ShaderParamData.Length)
                throw new Exception("Param size mis match!");
            else
                mat.ShaderParamData = ParamData;

            WriteRenderInfo(m, mat);
            WriteTextureRefs(m, mat);
            WriteShaderAssign(m.shaderassign, mat);
        }
        public static void ReadMaterial(this FMAT m, Material mat)
        {
            if (mat.Flags == MaterialFlags.Visible)
                m.Enabled = true;
            else
                m.Enabled = false;

            m.ReadRenderInfo(mat);
            m.ReadShaderAssign(mat);
            m.SetActiveGame();
            m.ReadShaderParams(mat);
            m.Material = mat;
            m.ReadTextureRefs(mat);
        }
        public static void ReadTextureRefs(this FMAT m, Material mat)
        {
            m.textures.Clear();

            int AlbedoCount = 0;
            int id = 0;
            string TextureName = "";
            if (mat.TextureRefs == null)
                mat.TextureRefs = new List<string>();

            foreach (string tex in mat.TextureRefs)
            {
                TextureName = tex;

                MatTexture texture = new MatTexture();

                texture.wrapModeS = (int)mat.Samplers[id].WrapModeU;
                texture.wrapModeT = (int)mat.Samplers[id].WrapModeV;
                texture.wrapModeW = (int)mat.Samplers[id].WrapModeW;
                texture.SamplerName = mat.SamplerDict.GetKey(id);

                bool IsAlbedo = Misc.HackyTextureList.Any(TextureName.Contains);

                if (Runtime.activeGame == Runtime.ActiveGame.MK8D)
                {
                    if (texture.SamplerName == "_a0")
                    {
                        if (AlbedoCount == 0)
                        {
                            m.HasDiffuseMap = true;
                            AlbedoCount++;
                            texture.hash = 0;
                            texture.Type = MatTexture.TextureType.Diffuse;
                        }
                    }
                    if (texture.SamplerName == "_n0")
                    {
                        texture.hash = 1;
                        m.HasNormalMap = true;
                        texture.Type = MatTexture.TextureType.Normal;
                    }
                    if (texture.SamplerName == "_e0")
                    {
                        texture.hash = 8;
                        m.HasEmissionMap = true;
                        texture.Type = MatTexture.TextureType.Emission;
                    }
                    if (texture.SamplerName == "_s0")
                    {
                        texture.hash = 4;
                        m.HasSpecularMap = true;
                        texture.Type = MatTexture.TextureType.Specular;
                    }
                    if (texture.SamplerName == "_x0")
                    {
                        texture.hash = 6;
                        m.HasSphereMap = true;
                        texture.Type = MatTexture.TextureType.SphereMap;
                    }
                    if (texture.SamplerName == "_b0")
                    {
                        texture.hash = 2;
                        m.HasShadowMap = true;
                        texture.Type = MatTexture.TextureType.Shadow;
                    }
                    if (texture.SamplerName == "_b1")
                    {
                        texture.hash = 3;
                        m.HasLightMap = true;
                        texture.Type = MatTexture.TextureType.Light;
                    }
                }
                else if (Runtime.activeGame == Runtime.ActiveGame.SMO)
                {
                    if (texture.SamplerName == "_a0")
                    {
                        if (AlbedoCount == 0)
                        {
                            m.HasDiffuseMap = true;
                            AlbedoCount++;
                            texture.hash = 0;
                            texture.Type = MatTexture.TextureType.Diffuse;
                        }
                    }
                    if (texture.SamplerName == "_n0")
                    {
                        texture.hash = 1;
                        m.HasNormalMap = true;
                        texture.Type = MatTexture.TextureType.Normal;
                    }
                    if (texture.SamplerName == "_e0")
                    {
                        texture.hash = 8;
                        m.HasEmissionMap = true;
                        texture.Type = MatTexture.TextureType.Emission;
                    }
                    if (TextureName.Contains("mtl"))
                    {
                        texture.hash = 16;
                        m.HasMetalnessMap = true;
                        texture.Type = MatTexture.TextureType.Metalness;
                    }
                    else if (TextureName.Contains("rgh"))
                    {
                        texture.Type = MatTexture.TextureType.Roughness;
                        texture.hash = 18;
                        m.HasRoughnessMap = true;
                    }
                    else if (TextureName.Contains("sss"))
                    {

                        texture.Type = MatTexture.TextureType.SubSurfaceScattering;
                        texture.hash = 19;
                        m.HasSubSurfaceScatteringMap = true;
                    }
                }
                else
                {
                    //This works decently for now. I tried samplers but Kirby Star Allies doesn't map with samplers properly? 
                    if (IsAlbedo)
                    {
                        if (AlbedoCount == 0)
                        {
                            m.HasDiffuseMap = true;
                            AlbedoCount++;
                            texture.hash = 0;
                            texture.Type = MatTexture.TextureType.Diffuse;
                        }
                        if (AlbedoCount == 1)
                        {
                            //   poly.material.HasDiffuseLayer = true;
                            //    texture.hash = 19;
                            //   texture.Type = MatTexture.TextureType.DiffuseLayer2;

                        }
                    }

                    else if (TextureName.Contains("Nrm") || TextureName.Contains("Norm") || TextureName.Contains("norm") || TextureName.Contains("nrm"))
                    {
                        texture.hash = 1;
                        m.HasNormalMap = true;
                        texture.Type = MatTexture.TextureType.Normal;
                    }
                    else if (TextureName.Contains("Emm"))
                    {
                        texture.hash = 8;
                        m.HasEmissionMap = true;
                        texture.Type = MatTexture.TextureType.Emission;
                    }
                    else if (TextureName.Contains("Spm"))
                    {
                        texture.hash = 4;
                        m.HasSpecularMap = true;
                        texture.Type = MatTexture.TextureType.Specular;
                    }
                    else if (TextureName.Contains("b00"))
                    {
                        texture.hash = 2;
                        m.HasShadowMap = true;
                        texture.Type = MatTexture.TextureType.Shadow;
                    }
                    else if (TextureName.Contains("Moc") || TextureName.Contains("AO"))
                    {
                        texture.hash = 2;
                        m.HasAmbientOcclusionMap = true;
                        texture.Type = MatTexture.TextureType.AO;
                    }
                    else if (TextureName.Contains("b01"))
                    {
                        texture.hash = 3;
                        m.HasLightMap = true;
                        texture.Type = MatTexture.TextureType.Light;
                    }
                    else if (TextureName.Contains("MRA")) //Metalness, Roughness, and Cavity Map in one
                    {
                        texture.hash = 17;
                        m.HasRoughnessMap = true;
                        texture.Type = MatTexture.TextureType.MRA;
                    }
                    else if (TextureName.Contains("mtl"))
                    {
                        texture.hash = 16;
                        m.HasMetalnessMap = true;
                        texture.Type = MatTexture.TextureType.Metalness;
                    }
                    else if (TextureName.Contains("rgh"))
                    {
                        texture.Type = MatTexture.TextureType.Roughness;
                        texture.hash = 18;
                        m.HasRoughnessMap = true;
                    }
                    else if (TextureName.Contains("sss"))
                    {

                        texture.Type = MatTexture.TextureType.SubSurfaceScattering;
                        texture.hash = 19;
                        m.HasSubSurfaceScatteringMap = true;
                    }
                }
                
                texture.Name = TextureName;

                m.textures.Add(texture);

                id++;
            }
        }
        public static void ReadShaderParams(this FMAT m, Material mat)
        {
            m.matparam.Clear();

            if (mat.ShaderParamData == null)
                return;

            using (FileReader reader = new FileReader(new System.IO.MemoryStream(mat.ShaderParamData)))
            {
                reader.ByteOrder = Syroot.BinaryData.ByteOrder.LittleEndian;
                foreach (ShaderParam param in mat.ShaderParams)
                {
                    BfresShaderParam shaderParam = new BfresShaderParam();
                    shaderParam.Type = param.Type;
                    shaderParam.Name = param.Name;

                    reader.Seek(param.DataOffset, System.IO.SeekOrigin.Begin);
                    shaderParam.ReadValue(reader, (int)param.DataSize);

                    m.matparam.Add(param.Name, shaderParam);
                }
                reader.Close();
            }
        }
        public static byte[] WriteShaderParams(this FMAT m, Material mat)
        {
            mat.ShaderParams = new List<ShaderParam>();

            System.IO.MemoryStream mem = new System.IO.MemoryStream();
            using (FileWriter writer = new FileWriter(mem))
            {
                uint Offset = 0;
                int index = 0;
                writer.ByteOrder = Syroot.BinaryData.ByteOrder.LittleEndian;
                foreach (BfresShaderParam shaderParam in m.matparam.Values)
                {
                    ShaderParam param = new ShaderParam();
                    param.Name = shaderParam.Name;
                    param.Type = shaderParam.Type;
                    param.DataOffset = (ushort)Offset;
                    param.DependedIndex = (ushort)index;
                    param.DependIndex = (ushort)index;

                    writer.Seek(param.DataOffset, System.IO.SeekOrigin.Begin);
                    shaderParam.WriteValue(writer);

                    Offset += param.DataSize;
                    mat.ShaderParams.Add(param);
                    index++;
                }
                writer.Close();
            }
            return mem.ToArray();
        }
        public static void ReadRenderInfo(this FMAT m, Material mat)
        {
            m.renderinfo.Clear();

            foreach (RenderInfo rnd in mat.RenderInfos)
            {
                BfresRenderInfo r = new BfresRenderInfo();
                r.Name = rnd.Name;
                r.Type = rnd.Type;
                switch (rnd.Type)
                {
                    case RenderInfoType.Int32: r.ValueInt = rnd.GetValueInt32s(); break;
                    case RenderInfoType.Single: r.ValueFloat = rnd.GetValueSingles(); break;
                    case RenderInfoType.String: r.ValueString = rnd.GetValueStrings(); break;
                }
                m.renderinfo.Add(r);
            }
        }
        public static void WriteTextureRefs(this FMAT m, Material mat)
        {
            mat.TextureRefs = new List<string>();
            mat.TextureRefs.Clear();

            foreach (var textu in m.textures)
                mat.TextureRefs.Add(textu.Name);
            
        }
        public static void WriteRenderInfo(this FMAT m, Material mat)
        {
            if (mat.RenderInfos == null)
                mat.RenderInfos = new List<RenderInfo>();

            mat.RenderInfos.Clear();
            foreach (BfresRenderInfo rnd in m.renderinfo)
            {
                RenderInfo r = new RenderInfo();
                r.Name = rnd.Name;
                switch (rnd.Type)
                {
                    case RenderInfoType.Int32: r.SetValue(rnd.ValueInt); break;
                    case RenderInfoType.Single: r.SetValue(rnd.ValueFloat); break;
                    case RenderInfoType.String: r.SetValue(rnd.ValueString); break;
                }
                mat.RenderInfos.Add(r);
            }
        }
        public static void ReadShaderAssign(this FMAT m, Material mat)
        {
            m.shaderassign = new FMAT.ShaderAssign();

            if (mat.ShaderAssign == null)
                mat.ShaderAssign = new ShaderAssign();
            if (mat.ShaderAssign.ShaderOptions == null)
                mat.ShaderAssign.ShaderOptions = new List<string>();
            if (mat.ShaderAssign.AttribAssigns == null)
                mat.ShaderAssign.AttribAssigns = new List<string>();
            if (mat.ShaderAssign.SamplerAssigns == null)
                mat.ShaderAssign.SamplerAssigns = new List<string>();

            m.shaderassign.options.Clear();
            m.shaderassign.samplers.Clear();
            m.shaderassign.attributes.Clear();

            m.shaderassign = new FMAT.ShaderAssign();
            m.shaderassign.ShaderArchive = mat.ShaderAssign.ShaderArchiveName;
            m.shaderassign.ShaderModel = mat.ShaderAssign.ShadingModelName;

            for (int op = 0; op < mat.ShaderAssign.ShaderOptions.Count; op++)
                m.shaderassign.options.Add(mat.ShaderAssign.ShaderOptionDict.GetKey(op), mat.ShaderAssign.ShaderOptions[op]);

            if (mat.ShaderAssign.SamplerAssigns != null)
            {
                for (int op = 0; op < mat.ShaderAssign.SamplerAssigns.Count; op++)
                    m.shaderassign.samplers.Add(mat.ShaderAssign.SamplerAssignDict.GetKey(op), mat.ShaderAssign.SamplerAssigns[op]);
            }
            if (mat.ShaderAssign.AttribAssigns != null)
            {
                for (int op = 0; op < mat.ShaderAssign.AttribAssigns.Count; op++)
                    m.shaderassign.attributes.Add(mat.ShaderAssign.AttribAssignDict.GetKey(op), mat.ShaderAssign.AttribAssigns[op]);
            }

     
            
       
            
        }
        public static void WriteShaderAssign(this FMAT.ShaderAssign shd, Material mat)
        {
            mat.ShaderAssign = new ShaderAssign();
            mat.ShaderAssign.ShaderOptionDict = new ResDict();
            mat.ShaderAssign.SamplerAssignDict = new ResDict();
            mat.ShaderAssign.AttribAssignDict = new ResDict();
            mat.ShaderAssign.ShaderOptions = new List<string>();
            mat.ShaderAssign.AttribAssigns = new List<string>();
            mat.ShaderAssign.SamplerAssigns = new List<string>();

            mat.ShaderAssign.ShaderArchiveName = shd.ShaderArchive;
            mat.ShaderAssign.ShadingModelName = shd.ShaderModel;

            foreach (var option in shd.options)
            {
                mat.ShaderAssign.ShaderOptionDict.Add(option.Key);
                mat.ShaderAssign.ShaderOptions.Add(option.Value);
            }
            foreach (var samp in shd.samplers)
            {
                mat.ShaderAssign.SamplerAssignDict.Add(samp.Key);
                mat.ShaderAssign.SamplerAssigns.Add(samp.Value);
            }
            foreach (var att in shd.attributes)
            {
                mat.ShaderAssign.AttribAssignDict.Add(att.Key);
                mat.ShaderAssign.AttribAssigns.Add(att.Value);
            }
        }

        public static void WriteExternalFiles(ResFile resFile, TreeNode EditorRoot)
        {
            resFile.ExternalFiles.Clear();
            if (EditorRoot.Nodes.ContainsKey("EXT"))
            {
                foreach (TreeNode node in EditorRoot.Nodes["EXT"].Nodes)
                {
                    ExternalFile ext = new ExternalFile();
                    if (node is BinaryTextureContainer)
                    {
                        BinaryTextureContainer bntx = (BinaryTextureContainer)node;
                        ext.Data = bntx.Save();
                    }
                    else if (node is BfshaFileData)
                    {
                        ext.Data = ((BfshaFileData)node).Data;
                    }
                    else
                    {
                        ext.Data = ((ExternalFileData)node).Data;
                    }
                    resFile.ExternalFiles.Add(ext);
                }
            }
        }
    }
}
