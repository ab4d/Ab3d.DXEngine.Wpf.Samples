using Ab3d.DirectX.Materials;
using Ab3d.DirectX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using System.Collections.Generic;
using System;
using System.IO;
using Ab3d.Assimp;
using Assimp;
using System.Linq;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using SharpDX;
using SharpDX.DXGI;
using Scene = Assimp.Scene;
using AssimpMaterial = Assimp.Material;

namespace Ab3d.DXEngine.Wpf.Samples.Common
{
    /// <summary>
    /// Native Assimp importer does not fully support PBR materials and does not provide a standard way of providing the PBR textures. The provided information also depends on the file format.
    /// The AssimpPbrHelper class provides static methods that help read PBR textures. The textures can be also found by checking the bitmap file names.
    /// </summary>
    public static class AssimpPbrHelper
    {
        public struct KnownTextureFile
        {
            public TextureMapTypes TextureMapType;
            public List<string> FileSuffixes;

            public KnownTextureFile(TextureMapTypes textureMapType)
            {
                TextureMapType = textureMapType;
                FileSuffixes = new List<string>();
            }

            public KnownTextureFile(TextureMapTypes textureMapType, string fileSuffixes)
            {
                TextureMapType = textureMapType;
                FileSuffixes = fileSuffixes.Split(',').Select(f => f.Trim()).ToList();
            }
        }

        public static List<KnownTextureFile> TextureFiles;

        public static readonly TextureMapTypes[] PBRSupportedTextureMapTypes = new TextureMapTypes[]
        {
            TextureMapTypes.DiffuseColor, // DiffuseColor is used as BaseColor
            TextureMapTypes.BaseColor,
            TextureMapTypes.OcclusionRoughnessMetalness,
            TextureMapTypes.RoughnessMetalness,
            TextureMapTypes.Metalness,
            TextureMapTypes.Roughness,
            TextureMapTypes.AmbientOcclusion,
            TextureMapTypes.NormalMap,
            TextureMapTypes.Emissive
        };

        static AssimpPbrHelper()
        {
            // Based on https://help.sketchfab.com/hc/en-us/articles/202600873-Materials-and-Textures

            TextureFiles = new List<KnownTextureFile>();
            TextureFiles.Add(new KnownTextureFile(TextureMapTypes.DiffuseColor, "color, diffuse"));
            TextureFiles.Add(new KnownTextureFile(TextureMapTypes.Albedo, "albedo"));
            TextureFiles.Add(new KnownTextureFile(TextureMapTypes.BaseColor, "basecolor, base_color"));
            TextureFiles.Add(new KnownTextureFile(TextureMapTypes.Metalness, "metalness, metallic, metal, m"));
            TextureFiles.Add(new KnownTextureFile(TextureMapTypes.SpecularColor, "specular, spec, s"));
            TextureFiles.Add(new KnownTextureFile(TextureMapTypes.SpecularF0, "specularf0, f0"));
            TextureFiles.Add(new KnownTextureFile(TextureMapTypes.Roughness, "roughness, rough, r"));
            TextureFiles.Add(new KnownTextureFile(TextureMapTypes.MetalnessRoughness, "metalnessroughness, metalrough"));
            TextureFiles.Add(new KnownTextureFile(TextureMapTypes.Glossiness, "glossiness, glossness, gloss, g, glossy"));
            TextureFiles.Add(new KnownTextureFile(TextureMapTypes.AmbientOcclusion, "ambientocclusion, ambient occlusion, ao, occlusion, lightmap, diffuseintensity"));
            TextureFiles.Add(new KnownTextureFile(TextureMapTypes.Cavity, "cavity"));
            TextureFiles.Add(new KnownTextureFile(TextureMapTypes.NormalMap, "normal, nrm, nmap, normalmap"));
            TextureFiles.Add(new KnownTextureFile(TextureMapTypes.Height, "height"));
            TextureFiles.Add(new KnownTextureFile(TextureMapTypes.Emissive, "emission, emit, emissive"));
            TextureFiles.Add(new KnownTextureFile(TextureMapTypes.Transparency, "transparency, transparent, opacity, mask, alpha"));
            TextureFiles.Add(new KnownTextureFile(TextureMapTypes.ReflectionMap, "reflection, reflect"));
            TextureFiles.Add(new KnownTextureFile(TextureMapTypes.OcclusionRoughnessMetalness, "orm, occlusionroughnessmetallic, occlusionroughnessmetalness"));
            TextureFiles.Add(new KnownTextureFile(TextureMapTypes.RoughnessMetalness, "roughnessmetallic, roughnessmetalness, roughmetal"));
        }



        // Returns TextureMapTypes from file name
        // File name must have any of the suffixes defined in the KnownTextureFiles.
        // The suffix must start with '-' or '_' and must be the last part of the file name before file extension.
        // Returns TextureMapTypes.Unknown when texture type cannot be determined
        public static TextureMapTypes GetTextureType(string fileName)
        {
            string rawFileName = System.IO.Path.GetFileNameWithoutExtension(fileName);

            if (string.IsNullOrEmpty(rawFileName))
                return TextureMapTypes.Unknown;

            int pos1 = rawFileName.LastIndexOf('_');
            int pos2 = rawFileName.LastIndexOf('-');

            if (pos2 > pos1)
                pos1 = pos2;

            if (pos1 == -1)
                return TextureMapTypes.Unknown;

            string fileSuffix = rawFileName.Substring(pos1 + 1).ToLower();

            for (var i = 0; i < TextureFiles.Count; i++)
            {
                if (TextureFiles[i].FileSuffixes.Contains(fileSuffix))
                    return TextureFiles[i].TextureMapType;
            }

            return TextureMapTypes.Unknown;
        }

        public static string GetFileNameWithoutKnownSuffix(string fileName)
        {
            string rawFileName = System.IO.Path.GetFileNameWithoutExtension(fileName);

            if (string.IsNullOrEmpty(rawFileName))
                return rawFileName;

            if (rawFileName.EndsWith("_base_color"))
            {
                // Special case for glTF samples from https://github.com/KhronosGroup/glTF-Sample-Assets
                return rawFileName.Substring(0, rawFileName.Length - "_base_color".Length);
            }

            int pos1 = rawFileName.LastIndexOf('_');
            int pos2 = rawFileName.LastIndexOf('-');

            if (pos2 > pos1)
                pos1 = pos2;

            if (pos1 == -1)
                return rawFileName;

            return rawFileName.Substring(0, pos1);
        }
        

        public static Dictionary<AssimpMaterial, PhysicallyBasedMaterial> SetupPbrMaterials(AssimpWpfImporter assimpWpfImporter, 
                                                                                            DXDevice dxDevice,
                                                                                            bool findPbrMapsFromFileNames,
                                                                                            bool loadDdsIfPresent = true,
                                                                                            DXCubeMap environmentCubeMap = null,
                                                                                            Dictionary<string, ShaderResourceView> texturesCache = null)
        {
            if (assimpWpfImporter == null) throw new ArgumentNullException(nameof(assimpWpfImporter));
            if (dxDevice == null) throw new ArgumentNullException(nameof(dxDevice));

            var fileName = assimpWpfImporter.LastImportedFileName;
            if (fileName == null)
                throw new InvalidOperationException("Cannot setup PBR materials because assimpWpfImporter.LastImportedFileName is null.");

            var assimpScene = assimpWpfImporter.ImportedAssimpScene;
            if (assimpScene == null)
                throw new InvalidOperationException("Cannot setup PBR materials because assimpWpfImporter.ImportedAssimpScene is null.");
            
            var assimpWpfConverter = assimpWpfImporter.AssimpWpfConverter;
            if (assimpWpfConverter == null)
                throw new InvalidOperationException("Cannot setup PBR materials because assimpWpfImporter.AssimpWpfConverter is null.");


            //// Dispose existing resources
            //Dispose();

            //TextureMapsPanel.Children.Clear();

            //Log("Materials:");

            var dxMaterials = new Dictionary<AssimpMaterial, PhysicallyBasedMaterial>(assimpScene.Materials.Count);

            foreach (var assimpMaterial in assimpScene.Materials)
            {
                var physicallyBasedMaterial = CreatePbrMaterial(assimpScene, dxDevice, assimpMaterial, fileName, assimpWpfImporter.LastUsedTexturesPath, findPbrMapsFromFileNames, loadDdsIfPresent, environmentCubeMap, texturesCache);

                if (physicallyBasedMaterial != null)
                    dxMaterials.Add(assimpMaterial, physicallyBasedMaterial);
            }



            // Go through all assimp meshes and for each 
            //Log("Meshes:");
            foreach (var assimpMesh in assimpScene.Meshes)
            {
                //Log($"  {assimpMesh.Name ?? "<undefined mesh name>"}");

                var geometryModel3D = assimpWpfConverter.GetGeometryModel3DForAssimpMesh(assimpMesh);

                if (geometryModel3D == null)
                    continue; // This should not happen but just in case do a check to prevent null reference exception


                var wpfMaterial = geometryModel3D.Material;

                if (wpfMaterial == null)
                    continue;

                // NOTE: We do not check the back material - the DiffuseSpecularNormalMapEffect does not support rendering them (we would need to update the shader to invert normals to support that).

                var assimpMaterial = assimpWpfConverter.GetAssimpMaterialForWpfMaterial(wpfMaterial);

                // PBR material must have a diffuse texture defined (this can be also used as albedo or base color in PBR)
                // The diffuse texture file name will be used as a base file name for looking at other textures that are needed for PBR
                if (assimpMaterial == null)
                    continue;



                PhysicallyBasedMaterial physicallyBasedMaterial;
                if (dxMaterials.TryGetValue(assimpMaterial, out physicallyBasedMaterial))
                {
                    // ... and tangent data
                    SetMeshTangentData(assimpMesh, geometryModel3D);

                    // Finally call SetUsedDXMaterial on WPF material.
                    // This will tell DXEngine to use the diffuseSpecularNormalMapMaterial instead of creating a standard WpfMaterial.
                    if (wpfMaterial.GetUsedDXMaterial(dxDevice) == null)
                        wpfMaterial.SetUsedDXMaterial(physicallyBasedMaterial);

                    // Test TwoSided materials:
                    //physicallyBasedMaterial.BaseColor = new Color4(1, 1, 1, 0.5f);
                    //physicallyBasedMaterial.HasTransparency = true;
                    //physicallyBasedMaterial.IsTwoSided = true;
                }
            }

            return dxMaterials;
        }

        public static PhysicallyBasedMaterial CreatePbrMaterial(Scene assimpScene,
                                                                DXDevice dxDevice,
                                                                AssimpMaterial assimpMaterial,
                                                                string fileName,
                                                                string customTexturesFolder,
                                                                bool findPbrMapsFromFileNames,
                                                                bool loadDdsIfPresent,
                                                                DXCubeMap environmentCubeMap,
                                                                Dictionary<string, ShaderResourceView> texturesCache)
        {
            //#if DEBUG
            //            // DUMP ALL MATERIAL PROPERTIES
            //            DumpAllMaterialProperties(assimpMaterial);
            //#endif
            
            if (!assimpMaterial.HasTextureDiffuse) // If there is no diffuse texture defined, then do not consider this a PBR material
                return null;


            string[] allFilesInTextureFolder;

            string diffuseTextureFileName = assimpMaterial.TextureDiffuse.FilePath;


            if (diffuseTextureFileName.StartsWith("*")) // when file name in assimp starts by *, then it is embedded file with index following the *
            {
                allFilesInTextureFolder = null;
            }
            else
            {
                if (!string.IsNullOrEmpty(customTexturesFolder))
                {
                    diffuseTextureFileName = System.IO.Path.Combine(customTexturesFolder, diffuseTextureFileName);

                    if (!System.IO.File.Exists(diffuseTextureFileName))
                        diffuseTextureFileName = Path.Combine(customTexturesFolder, System.IO.Path.GetFileName(diffuseTextureFileName)); // remove any path from diffuseTextureFileName and try again

                }
                else if (!System.IO.Path.IsPathRooted(diffuseTextureFileName))
                {
                    string filePath = System.IO.Path.GetDirectoryName(fileName);

                    if (filePath != null)
                    {
                        diffuseTextureFileName = Path.Combine(filePath, diffuseTextureFileName);

                        if (!System.IO.File.Exists(diffuseTextureFileName))
                            diffuseTextureFileName = Path.Combine(filePath, System.IO.Path.GetFileName(diffuseTextureFileName)); // remove any path from diffuseTextureFileName and try again
                    }
                }


                string diffuseTextureFolderName = System.IO.Path.GetDirectoryName(diffuseTextureFileName);

                
                if (findPbrMapsFromFileNames && System.IO.Directory.Exists(diffuseTextureFolderName))
                {
                    allFilesInTextureFolder = System.IO.Directory.GetFiles(diffuseTextureFolderName, "*.*", System.IO.SearchOption.TopDirectoryOnly)
                                                                 .Where(f => TextureLoader.IsSupportedFile(f, loadDdsIfPresent)) // Skip unsupported files (materialFiles may also include bin or mtl or other related files) 
                                                                 .ToArray();
                }
                else
                {
                    allFilesInTextureFolder = null;
                }
            }

            
            string fileExtension = System.IO.Path.GetExtension(fileName);
            bool isGltfFile = fileExtension.Equals(".gltf", StringComparison.OrdinalIgnoreCase) ||
                              fileExtension.Equals(".glb", StringComparison.OrdinalIgnoreCase);
            
            return CreatePbrMaterial(assimpScene, dxDevice, assimpMaterial, diffuseTextureFileName, allFilesInTextureFolder, isGltfFile, loadDdsIfPresent, environmentCubeMap, texturesCache);
        }

        public static PhysicallyBasedMaterial CreatePbrMaterial(Scene assimpScene, 
                                                                DXDevice dxDevice,
                                                                AssimpMaterial assimpMaterial, 
                                                                string diffuseTextureFileName, 
                                                                string[] allFilesInTextureFolder, 
                                                                bool isGltfFile,
                                                                bool loadDdsIfPresent,
                                                                DXCubeMap environmentCubeMap,
                                                                Dictionary<string, ShaderResourceView> texturesCache)
        {

            // Check material properties and try to read metallicFactor and roughnessFactor
            // We also store the file name of the texture that is used when metallicFactor and roughnessFactor are defined
            float metalness = 1;
            float roughness = 1;

            string lastTextureFileName = null;
            string pbrTextureFileName = null;
            var allProperties = assimpMaterial.GetAllProperties();
            foreach (var property in allProperties)
            {
                // See also: https://assimp.sourceforge.net/lib_html/materials.html
                switch (property.Name)
                {
                    case "$tex.file":
                        lastTextureFileName = property.GetStringValue();
                        break;
                    
                    case "$mat.metallicFactor":
                        metalness = property.GetFloatValue();
                        pbrTextureFileName = lastTextureFileName;
                        break;
                    
                    case "$mat.roughnessFactor":
                        roughness = property.GetFloatValue();
                        pbrTextureFileName = lastTextureFileName;
                        break;
                }
            }
            

            string diffuseTextureFolderName = System.IO.Path.GetDirectoryName(diffuseTextureFileName);

            string diffuseTextureFileNameWithoutKnownSuffix = GetFileNameWithoutKnownSuffix(diffuseTextureFileName);

            // Get material files that start with the diffuse texture file name without a suffix
            string[] allMaterialFiles;
            if (allFilesInTextureFolder != null)
                allMaterialFiles = allFilesInTextureFolder.Where(f => GetFileNameWithoutKnownSuffix(f).Equals(diffuseTextureFileNameWithoutKnownSuffix, StringComparison.OrdinalIgnoreCase)).ToArray();
            else
                allMaterialFiles = null;

            var textureFiles = new Dictionary<TextureMapTypes, string>();

            bool hasDiffuseTexture;

            // First try to get texture maps based on the assimp textures
            // Note that for many texture file formats (for example for fbx), only the TextureDiffuse is supported.
            // Other PBR textures we need to extract from the file name
            if (assimpMaterial.HasTextureDiffuse)
            {
                textureFiles.Add(TextureMapTypes.DiffuseColor, assimpMaterial.TextureDiffuse.FilePath);
                hasDiffuseTexture = true;
            }
            else
            {
                hasDiffuseTexture = false;
            }

            if (assimpMaterial.HasTextureEmissive)
                textureFiles.Add(TextureMapTypes.Emissive, assimpMaterial.TextureEmissive.FilePath);
                    
            if (assimpMaterial.HasTextureNormal)
                textureFiles.Add(TextureMapTypes.NormalMap, assimpMaterial.TextureNormal.FilePath);

            if (assimpMaterial.HasTextureLightMap) // AmbientOcclusion?
            {
                // In gltf AmbientOcclusion is part of OcclusionRoughnessMetalness
                if (isGltfFile)
                {
                    var textureMapType = GetTextureType(assimpMaterial.TextureLightMap.FilePath);
                    if (textureMapType == TextureMapTypes.AmbientOcclusion) // Is this only AmbientOcclusion?
                        textureFiles.Add(TextureMapTypes.AmbientOcclusion, assimpMaterial.TextureLightMap.FilePath);
                    else
                        textureFiles.Add(TextureMapTypes.OcclusionRoughnessMetalness, assimpMaterial.TextureLightMap.FilePath);
                }
                else
                {
                    textureFiles.Add(TextureMapTypes.AmbientOcclusion, assimpMaterial.TextureLightMap.FilePath);
                }
            }

            if (textureFiles.Count <= 1                                         // When only diffuse texture set from Assimp and ...
                && (allMaterialFiles == null || allMaterialFiles.Length == 0))  // ... no other potential file is available for the map
            {
                return null;
            }


            if (allMaterialFiles != null)
            {
                bool hasRoughnessMetalness = textureFiles.ContainsKey(TextureMapTypes.OcclusionRoughnessMetalness) ||
                                             textureFiles.ContainsKey(TextureMapTypes.RoughnessMetalness);

                foreach (var materialFile in allMaterialFiles)
                {
                    if (!TextureLoader.IsSupportedFile(materialFile, loadDdsIfPresent)) // Skip unsupported files (materialFiles may also include bin or mtl or other related files) 
                        continue;

                    var textureMapType = GetTextureType(materialFile);
                    if (textureMapType == TextureMapTypes.Unknown)
                    {
                        if (!hasDiffuseTexture)
                            textureMapType = TextureMapTypes.DiffuseColor; // First unknown file type is considered to be diffuse texture file
                        else
                            continue; // Unknown file type
                    }

                    // Is this texture type already added, for example by using Assimp's textures (TextureDiffuse, TextureEmissive, TextureNormal or TextureLightMap)
                    if (textureFiles.ContainsKey(textureMapType) ||
                       (textureMapType == TextureMapTypes.Metalness && hasRoughnessMetalness) ||
                       (textureMapType == TextureMapTypes.Roughness && hasRoughnessMetalness) ||
                       (textureMapType == TextureMapTypes.AmbientOcclusion && textureFiles.ContainsKey(TextureMapTypes.OcclusionRoughnessMetalness)) ||
                       (textureMapType == TextureMapTypes.DiffuseColor && hasDiffuseTexture) ||
                       (textureMapType == TextureMapTypes.Albedo && hasDiffuseTexture) ||
                       (textureMapType == TextureMapTypes.BaseColor && hasDiffuseTexture))
                    {
                        continue;
                    }

                    
                    bool isDiffuseTexture = (textureMapType == TextureMapTypes.DiffuseColor ||
                                             textureMapType == TextureMapTypes.Albedo ||
                                             textureMapType == TextureMapTypes.BaseColor);

                    hasDiffuseTexture |= isDiffuseTexture;

                    textureFiles.Add(textureMapType, materialFile);
                }
            }

            // If we are loading a glTF file and we did not find Metalness or OcclusionRoughnessMetalness texture file from file extension,
            // then check material properties and try to find OcclusionRoughnessMetalness texture from there
            if (isGltfFile &&
                !textureFiles.ContainsKey(TextureMapTypes.Metalness) &&
                !textureFiles.ContainsKey(TextureMapTypes.OcclusionRoughnessMetalness) &&
                !textureFiles.ContainsKey(TextureMapTypes.RoughnessMetalness) &&
                !textureFiles.ContainsKey(TextureMapTypes.MetalnessRoughness) &&
                pbrTextureFileName != null &&
                metalness > 0 && // If "$mat.metallicFactor" is set to 0, then this is not a PBR material
                TextureLoader.IsSupportedFile(pbrTextureFileName, loadDdsIfPresent))
            {
                if (pbrTextureFileName.StartsWith("*")) // Is this an embedded texture
                {
                    textureFiles.Add(TextureMapTypes.OcclusionRoughnessMetalness, pbrTextureFileName);
                }
                else
                {
                    // This is an actual file - check if it really exists
                    string fullFileName = System.IO.Path.Combine(diffuseTextureFolderName, pbrTextureFileName);

                    if (!System.IO.File.Exists(fullFileName))
                    {
                        // If file does not exist, then stip off any directory name from the pbrTextureFileName and get the fullFileName again
                        fullFileName = System.IO.Path.Combine(diffuseTextureFolderName, System.IO.Path.GetFileName(pbrTextureFileName));
                    }

                    if (System.IO.File.Exists(fullFileName))
                        textureFiles.Add(TextureMapTypes.OcclusionRoughnessMetalness, fullFileName);
                }
            }

            if (textureFiles.Count == 0)
                return null;

            var physicallyBasedMaterial = new PhysicallyBasedMaterial();
            
            if (assimpMaterial.Name != null)
                physicallyBasedMaterial.Name = assimpMaterial.Name;

            physicallyBasedMaterial.Metalness = metalness;
            physicallyBasedMaterial.Roughness = roughness;

            if (environmentCubeMap != null)
                physicallyBasedMaterial.SetTextureMap(TextureMapTypes.EnvironmentCubeMap, environmentCubeMap.ShaderResourceView);


            foreach (var oneTextureFile in textureFiles)
            {
                var textureType = oneTextureFile.Key;
                var oneFileName = oneTextureFile.Value;

                UpdatePbrMap(physicallyBasedMaterial, dxDevice, assimpScene, oneFileName, diffuseTextureFolderName, textureType, loadDdsIfPresent, texturesCache);
            }

            
            // Set BaseColor based on the DiffuseColor 
            if (assimpMaterial.HasColorDiffuse)
                physicallyBasedMaterial.BaseColor = new Color4(assimpMaterial.ColorDiffuse.R, assimpMaterial.ColorDiffuse.G, assimpMaterial.ColorDiffuse.B, assimpMaterial.ColorDiffuse.A);

            // When there is no Metalness texture defined, then set Metalness to zero - use plastic
            if (!physicallyBasedMaterial.HasTextureMap(TextureMapTypes.Metalness) && 
                !physicallyBasedMaterial.HasTextureMap(TextureMapTypes.OcclusionRoughnessMetalness) && 
                !physicallyBasedMaterial.HasTextureMap(TextureMapTypes.RoughnessMetalness) &&
                !physicallyBasedMaterial.HasTextureMap(TextureMapTypes.MetalnessRoughness))
            {
                physicallyBasedMaterial.Metalness = 0;
            }


            return physicallyBasedMaterial;
        }


        public static void SetMeshTangentData(Mesh assimpMesh, GeometryModel3D geometryModel3D)
        {
            var assimpTangents = assimpMesh.Tangents;

            var meshGeometry3D = geometryModel3D.Geometry as MeshGeometry3D;

            SharpDX.Vector3[] dxTangents;

            // First check if tangent data is read by the Assimp importer
            // Tangent data can be also generated by Assimp importer by using CalculateTangentSpace post-process:
            //assimpWpfImporter.AssimpPostProcessSteps = PostProcessSteps.CalculateTangentSpace;
            if (assimpTangents != null && assimpTangents.Count > 0)
            {
                var count = assimpTangents.Count;
                dxTangents = new SharpDX.Vector3[assimpTangents.Count];

                for (int i = 0; i < count; i++)
                    dxTangents[i] = new SharpDX.Vector3(assimpTangents[i].X, assimpTangents[i].Y, assimpTangents[i].Z);
            }
            else
            {
                // If there is no tangent data in the Assimp's mesh, then calculate that by ourselfs
                if (meshGeometry3D != null)
                    dxTangents = Ab3d.DirectX.Utilities.MeshUtils.CalculateTangentVectors(meshGeometry3D);
                else
                    dxTangents = null;
            }

            if (dxTangents != null && meshGeometry3D != null)
            {
                // Tangent values are stored with the MeshGeometry3D object.
                // This is done with using DXAttributeType.MeshTangentArray:
                meshGeometry3D.SetDXAttribute(DXAttributeType.MeshTangentArray, dxTangents);
            }
        }

        public static byte[] ConvertToOcclusionRoughnessMetalness(BitmapImage wpfBitmapImage, TextureMapTypes currentTextureType)
        {
            if (wpfBitmapImage == null)
                return null;

            int textureWidth = wpfBitmapImage.PixelWidth;
            int textureHeight = wpfBitmapImage.PixelHeight;

            int textureStride = textureWidth * 4;

            byte[] textureBytes = new byte[textureHeight * textureStride];
            wpfBitmapImage.CopyPixels(textureBytes, textureStride, 0);

            if (currentTextureType == TextureMapTypes.MetalnessRoughness)
            {
                // Convert MetalnessRoughness => OcclusionRoughnessMetalness: AmbientOcclusion in red channel, Roughness in green channel and Metalness in blue channel
                var bytesLength = textureBytes.Length;
                for (var i = 0; i < bytesLength; i+=4)
                {
                    // Just set AmbientOcclusion to 1
                    textureBytes[i + 2] = (byte)255; // AmbientOcclusion
                }
            }
            else if (currentTextureType == TextureMapTypes.RoughnessMetalness)
            {
                // Convert RoughnessMetalness => OcclusionRoughnessMetalness: AmbientOcclusion in red channel, Roughness in green channel and Metalness in blue channel
                var bytesLength = textureBytes.Length;
                for (var i = 0; i < bytesLength; i+=4)
                {
                    // Swap Metalness and Roughness and set AmbientOcclusion to 1
                    var temp = textureBytes[i + 0];
                    textureBytes[i + 0] = textureBytes[i + 1]; // Metalness
                    textureBytes[i + 1] = temp;                // Roughness  
                    textureBytes[i + 2] = (byte)255;           // AmbientOcclusion
                }
            }

            return textureBytes;
        }

        public static bool UpdatePbrMap(IMultiMapMaterial material, 
                                        DXDevice dxDevice,
                                        Scene assimpScene, 
                                        string fileName, 
                                        string baseFolder, 
                                        TextureMapTypes textureType, 
                                        bool loadDdsIfPresent,
                                        Dictionary<string, ShaderResourceView> texturesCache = null)
        {
            ShaderResourceView shaderResourceView;

            var isBaseColor = (textureType == TextureMapTypes.BaseColor ||
                               textureType == TextureMapTypes.Albedo ||
                               textureType == TextureMapTypes.DiffuseColor);


            if (texturesCache == null || !texturesCache.TryGetValue(fileName, out shaderResourceView))
            {
                TextureInfo textureInfo = null;

                if (fileName.StartsWith("*") && assimpScene != null && dxDevice != null) // Is file an embedded texture
                {
                    var embeddedTexture = Ab3d.Assimp.AssimpWpfImporter.GetEmbeddedTexture(assimpScene, fileName);

                    if (embeddedTexture != null)
                    {
                        if (textureType == TextureMapTypes.MetalnessRoughness || textureType == TextureMapTypes.RoughnessMetalness)
                        {
                            var textureBytes = ConvertToOcclusionRoughnessMetalness(embeddedTexture, textureType);
                            shaderResourceView = Ab3d.DirectX.TextureLoader.CreateShaderResourceView(dxDevice, textureBytes, embeddedTexture.PixelWidth, embeddedTexture.PixelHeight, embeddedTexture.PixelWidth * 4, Format.B8G8R8A8_UNorm, generateMipMaps: true);
                            textureType = TextureMapTypes.OcclusionRoughnessMetalness;
                        }
                        else
                        {
                            shaderResourceView = WpfMaterial.CreateTexture2D(dxDevice, embeddedTexture, out textureInfo);
                        }
                    }
                    else
                    {
                        shaderResourceView = null;
                    }
                }
                else
                {
                    if (!TextureLoader.IsSupportedFile(fileName, loadDdsIfPresent) || dxDevice == null) // Skip unsupported files (materialFiles may also include bin or mtl or other related files) 
                    {
                        shaderResourceView = null;
                    }
                    else
                    {
                        string usedFileName = fileName; // preserve original file name so we can cache it with that name
                        if (!System.IO.File.Exists(usedFileName) && baseFolder != null)
                            usedFileName = System.IO.Path.Combine(baseFolder, System.IO.Path.GetFileName(usedFileName));

                        if (System.IO.File.Exists(usedFileName))
                        {
                            if (textureType == TextureMapTypes.MetalnessRoughness || textureType == TextureMapTypes.RoughnessMetalness)
                            {
                                var wpfBitmapImage = new BitmapImage(new Uri(usedFileName)) { CacheOption = BitmapCacheOption.OnLoad };
                                var textureBytes = ConvertToOcclusionRoughnessMetalness(wpfBitmapImage, textureType);
                                shaderResourceView = Ab3d.DirectX.TextureLoader.CreateShaderResourceView(dxDevice, textureBytes, wpfBitmapImage.PixelWidth, wpfBitmapImage.PixelHeight, wpfBitmapImage.PixelWidth * 4, Format.B8G8R8A8_UNorm, generateMipMaps: true);
                                textureType = TextureMapTypes.OcclusionRoughnessMetalness;
                            }
                            else
                            {
                                // To load a texture from file, you can use the TextureLoader.LoadShaderResourceView (this supports loading standard image files and also loading dds files).
                                // This method returns a ShaderResourceView and it can also set a textureInfo parameter that defines some of the properties of the loaded texture (bitmap size, dpi, format, hasTransparency).
                                shaderResourceView = Ab3d.DirectX.TextureLoader.LoadShaderResourceView(dxDevice.Device,
                                                                                                       usedFileName,
                                                                                                       loadDdsIfPresent: loadDdsIfPresent,
                                                                                                       convertTo32bppPRGBA: isBaseColor,
                                                                                                       generateMipMaps: true,
                                                                                                       textureInfo: out textureInfo);
                            }
                        }
                        else
                        {
                            shaderResourceView = null;
                        }
                    }
                }

                if (shaderResourceView == null)
                    return false;

                // Only 2D textures are supported
                if (shaderResourceView.Description.Dimension != ShaderResourceViewDimension.Texture2D)
                    return false;

                if (isBaseColor && textureInfo != null)
                {
                    var physicallyBasedMaterial = material as PhysicallyBasedMaterial;

                    if (physicallyBasedMaterial != null)
                    {
                        // Get recommended BlendState based on HasTransparency and HasPreMultipliedAlpha values.
                        // Possible values are: CommonStates.Opaque, CommonStates.PremultipliedAlphaBlend or CommonStates.NonPremultipliedAlphaBlend.
                        var recommendedBlendState = dxDevice.CommonStates.GetRecommendedBlendState(textureInfo.HasTransparency, textureInfo.HasPremultipliedAlpha);

                        physicallyBasedMaterial.BlendState = recommendedBlendState;
                        physicallyBasedMaterial.HasTransparency = textureInfo.HasTransparency;
                    }
                }

                if (texturesCache != null)
                    texturesCache.Add(fileName, shaderResourceView);
            }
            
            MultiMapMaterial.SetTextureMap(material, textureType, shaderResourceView, null, fileName);

            return true;
        }

        // When dumpLineAction is null, then System.Diagnostics.Debug.WriteLine is used
        public static void DumpAllMaterialProperties(AssimpMaterial assimpMaterial, Action<string> dumpLineAction = null)
        {
            if (dumpLineAction == null)
                dumpLineAction = (text) => System.Diagnostics.Debug.WriteLine(text);

            var allProperties = assimpMaterial.GetAllProperties();
            dumpLineAction($"Material '{assimpMaterial.Name}':");


            foreach (var property in allProperties)
            {
                // See also: https://assimp.sourceforge.net/lib_html/materials.html
                switch (property.Name)
                {
                    case "?mat.name":
                    case "$tex.file":
                    case "$tex.mappingid": // glTF id, for example: "samplers[0]"
                    case "$mat.gltf.alphaMode": // for example: "OPAQUE"
                    case "$raw.ShadingModel": // in fbx, for example: "phong"
                        dumpLineAction($"  {property.Name}: '{property.GetStringValue()}'");
                        break;
                    
                    case "$mat.metallicFactor":
                    case "$mat.roughnessFactor":
                    case "$tex.scale":
                    case "$tex.file.strength":
                    case "$mat.shininess":
                    case "$mat.opacity":
                    case "$mat.transparent":
                    case "$mat.transparencyfactor":
                    case "$mat.shinpercent":
                    case "$mat.reflectivity":
                    case "$mat.bumpscaling":
                    case "$mat.displacementscaling":
                    case "$mat.gltf.alphaCutoff":
                        dumpLineAction($"  {property.Name}: {property.GetFloatValue()}");
                        break;

                    case "$tex.mappingname":
                    case "$tex.uvwsrc":
                    case "$tex.mapmodeu":
                    case "$tex.mapmodev":
                    case "$tex.mappingfiltermag":
                    case "$tex.mappingfiltermin":
                    case "$mat.shadingm":
                        dumpLineAction($"  {property.Name}: {property.GetIntegerValue()}");
                        break;
                    case "$clr.diffuse":
                    case "$clr.base":
                    case "$clr.emissive":
                    case "$clr.ambient":
                    case "$clr.specular":
                    case "$clr.transparent":
                    case "$clr.reflective":
                        dumpLineAction($"  {property.Name}: {property.GetColor4DValue()}");
                        break;
                    
                    case "$mat.twosided":
                        dumpLineAction($"  {property.Name}: {property.GetBooleanValue()}");
                        break;

                    default:
                        dumpLineAction($"  {property.Name} UNKNOWN TYPE");
                        break;
                }
            }
        }
    }
}