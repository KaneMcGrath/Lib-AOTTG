// SPDX-License-Identifier: MIT
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;

namespace LibAottg.AssetTool;

public sealed record ConversionResult(string GlbPath, int MeshCount, int ClipCount, int ByteLength, int WarningCount);

public static class CharacterConverter
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    public static ConversionResult Convert(string input, string output, float metersPerUnit = 1, bool unlit = false)
    {
        Capture.Require(float.IsFinite(metersPerUnit) && metersPerUnit > 0, "Meters per source unit must be finite and positive.");
        output = Path.GetFullPath(output);
        input = Path.GetFullPath(input);
        Capture.Require(!output.Equals(input, StringComparison.OrdinalIgnoreCase) && !input.StartsWith(output + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Output must not replace the capture or its parent directory.");
        using var capture = new Capture(input);
        using var glb = new GlbBuilder();
        var skeleton = new BindSkeleton(capture);
        List<string> warnings = [
            "Meter scale is an explicit conversion setting, not a calibrated RCMod measurement.",
            "Rest skeleton is reconstructed from bind matrices, preserving the highest reference joint's captured global frame/scale; it is not certified by an authored bind scene.",
            "Animation tracks are baked solo poses. Authored channel masks, additive/layer blending, events, procedural posing and non-transform curves are unavailable.",
            "Captured playback speeds are observations, not verified gameplay rules.",
            "Materials approximate legacy shaders with glTF metallic-roughness; source properties are retained in extras."
        ];
        var nodes = BuildNodes(capture, skeleton, metersPerUnit);
        var roots = capture.Nodes.Where(n => n.GetProperty("parent").GetInt32() < 0).Select(n => n.GetProperty("id").GetInt32()).ToList();
        var images = new List<object>();
        var textures = new List<object>();
        var samplers = new List<object>();
        foreach (var texture in capture.Manifest.GetProperty("textures").EnumerateArray())
        {
            Capture.Require(!texture.TryGetProperty("error", out _), "A source texture has a readback error.");
            byte[] png = File.ReadAllBytes(capture.FilePath(texture.Text("file")));
            Capture.Require(png.Length >= 24 && png.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), "Invalid PNG texture.");
            images.Add(new { name = texture.Text("name"), mimeType = "image/png", bufferView = glb.View(png) });
            string wrap = texture.Text("wrapMode"), filter = texture.Text("filterMode");
            Capture.Require(wrap is "Repeat" or "Clamp" or "Mirror" && filter is "Point" or "Bilinear" or "Trilinear", "Unsupported texture sampler setting.");
            int wrapping = wrap == "Clamp" ? 33071 : wrap == "Mirror" ? 33648 : 10497;
            int filtering = filter == "Point" ? 9728 : 9729;
            if (filter == "Trilinear") warnings.Add($"Texture '{texture.Text("name")}' uses bilinear filtering without source mipmaps.");
            samplers.Add(new { magFilter = filtering, minFilter = filtering, wrapS = wrapping, wrapT = wrapping });
            textures.Add(new { source = images.Count - 1, sampler = samplers.Count - 1 });
        }
        var sourceMaterials = capture.Manifest.GetProperty("materials").EnumerateArray().ToArray();
        var materials = BuildMaterials(sourceMaterials, textures.Count, unlit, warnings);
        var meshes = new List<object>();
        var skins = new List<object>();
        var omitted = new List<object>();
        foreach (var mesh in capture.Meshes)
        {
            int nodeId = mesh.GetProperty("node").GetInt32();
            Capture.Require(nodeId >= 0 && nodeId < capture.Nodes.Length, "Mesh node out of range.");
            bool active = true;
            for (int n = nodeId; n >= 0; n = capture.Nodes[n].GetProperty("parent").GetInt32()) active &= capture.Nodes[n].Flag("activeSelf");
            if (!mesh.Flag("enabled") || !active || !capture.Nodes[nodeId].Flag("activeInHierarchy"))
            {
                omitted.Add(new { sourceNode = nodeId, name = mesh.Text("name"), reason = "renderer disabled or node inactive" });
                continue;
            }
            bool skinned = mesh.Flag("skinned", false);
            Matrix4x4 bindShape = skinned ? skeleton.MeshBindTransforms[nodeId] : Matrix4x4.Identity;
            Matrix4x4 vertexTransform = bindShape * Matrix4x4.CreateScale(metersPerUnit, metersPerUnit, -metersPerUnit);
            var attributes = new Dictionary<string, int>();
            float[] positions = capture.ReadFloats(mesh.GetProperty("positions"), 3);
            int vertices = positions.Length / 3;
            Capture.Require(vertices > 0, "Empty mesh.");
            TransformVectors(positions, vertexTransform, false);
            attributes["POSITION"] = glb.Floats(positions, 3, "VEC3", 34962, true);
            AddAttribute("normals", "NORMAL", 3, "VEC3", true);
            AddAttribute("tangents", "TANGENT", 4, "VEC4", true);
            AddAttribute("colors", "COLOR_0", 4, "VEC4", false);
            if (skinned)
            {
                int[] bones = BindSkeleton.Bones(capture, mesh);
                int[] indices = capture.ReadInts(mesh.GetProperty("jointIndices"), 4);
                float[] weights = capture.ReadFloats(mesh.GetProperty("jointWeights"), 4);
                Capture.Require(indices.Length == vertices * 4 && weights.Length == vertices * 4, "Skin attribute count mismatch.");
                for (int v = 0; v < vertices; v++)
                {
                    float sum = 0;
                    for (int j = 0; j < 4; j++)
                    {
                        int i = v * 4 + j;
                        Capture.Require(weights[i] >= 0 && indices[i] >= 0 && indices[i] < bones.Length, "Invalid skin influence.");
                        if (weights[i] == 0) indices[i] = 0;
                        sum += weights[i];
                    }
                    Capture.Require(float.IsFinite(sum) && sum > 0, "Vertex has no skin weight.");
                    for (int j = 0; j < 4; j++) weights[v * 4 + j] /= sum;
                }
                attributes["JOINTS_0"] = glb.Integers(indices, 4, true);
                attributes["WEIGHTS_0"] = glb.Floats(weights, 4, "VEC4", 34962);
                var inverseBinds = bones.SelectMany(b => CoordinateMath.Elements(CoordinateMath.ConvertMatrix(CoordinateMath.Inverse(skeleton.RestGlobals[b]), metersPerUnit))).ToArray();
                skins.Add(new { name = mesh.Text("name"), joints = bones, inverseBindMatrices = glb.Floats(inverseBinds, 16, "MAT4") });
            }
            var primitives = new List<object>();
            int[] slots = mesh.GetProperty("materials").Ints();
            int slot = 0;
            foreach (var submesh in mesh.GetProperty("submeshes").EnumerateArray())
            {
                int[] triangles = capture.ReadInts(submesh, 1);
                Capture.Require(triangles.Length % 3 == 0 && triangles.All(x => x >= 0 && x < vertices), "Invalid triangle indices.");
                int material = slot < slots.Length ? slots[slot] : -1;
                Capture.Require(material >= -1 && material < materials.Count, "Material slot out of range.");
                slot++;
                if (triangles.Length == 0) continue;
                if (vertexTransform.GetDeterminant() < 0)
                    for (int t = 0; t < triangles.Length; t += 3) (triangles[t + 1], triangles[t + 2]) = (triangles[t + 2], triangles[t + 1]);
                var primitiveAttributes = new Dictionary<string, int>(attributes);
                // Bake material UV transforms per primitive, then flip V once.
                // This supports different offsets on submeshes without extensions.
                float[] uv = capture.ReadFloats(mesh.GetProperty("uv0"), 2);
                if (uv.Length > 0)
                {
                    Capture.Require(uv.Length == vertices * 2, "UV count mismatch.");
                    float[] scale = [1, 1], offset = [0, 0];
                    if (material >= 0 && sourceMaterials[material].GetProperty("textures").TryGetProperty("_MainTex", out var main))
                    {
                        scale = main.GetProperty("scale").Floats(); offset = main.GetProperty("offset").Floats();
                        Capture.Require(scale.Length == 2 && offset.Length == 2 && scale.All(float.IsFinite) && offset.All(float.IsFinite), "Invalid texture transform.");
                    }
                    for (int i = 0; i < uv.Length; i += 2) { uv[i] = uv[i] * scale[0] + offset[0]; uv[i + 1] = 1 - (uv[i + 1] * scale[1] + offset[1]); }
                    primitiveAttributes["TEXCOORD_0"] = glb.Floats(uv, 2, "VEC2", 34962);
                }
                else Capture.Require(material < 0 || !sourceMaterials[material].GetProperty("textures").TryGetProperty("_MainTex", out _), "Textured primitive has no UVs.");
                float[] uv1 = capture.ReadFloats(mesh.GetProperty("uv1"), 2);
                if (uv1.Length > 0)
                {
                    Capture.Require(uv1.Length == vertices * 2, "UV1 count mismatch.");
                    for (int i = 1; i < uv1.Length; i += 2) uv1[i] = 1 - uv1[i];
                    primitiveAttributes["TEXCOORD_1"] = glb.Floats(uv1, 2, "VEC2", 34962);
                }
                var primitive = new Dictionary<string, object> { ["attributes"] = primitiveAttributes, ["indices"] = glb.Integers(triangles, 1, false), ["mode"] = 4 };
                if (material >= 0) primitive["material"] = material;
                primitives.Add(primitive);
            }
            Capture.Require(primitives.Count > 0, "Visible mesh has no triangles.");
            int meshId = meshes.Count;
            meshes.Add(new { name = mesh.Text("name"), primitives, extras = new { sourceNode = nodeId, sourceMesh = mesh } });
            if (skinned)
            {
                // glTF ignores skinned renderer transforms. Geometry is in character
                // bind space, so use an identity scene root and preserve source nodes.
                roots.Add(nodes.Count);
                nodes.Add(new() { ["name"] = mesh.Text("name") + "_skin", ["mesh"] = meshId, ["skin"] = skins.Count - 1, ["extras"] = new { sourceNode = nodeId } });
            }
            else
            {
                Capture.Require(!nodes[nodeId].ContainsKey("mesh"), "Multiple rigid renderers on one node are unsupported.");
                nodes[nodeId]["mesh"] = meshId;
            }

            void AddAttribute(string source, string semantic, int components, string type, bool vector)
            {
                float[] values = capture.ReadFloats(mesh.GetProperty(source), components);
                if (values.Length == 0) return;
                Capture.Require(values.Length == vertices * components, "Attribute count mismatch: " + source);
                if (vector && components == 3) TransformVectors(values, Matrix4x4.Transpose(CoordinateMath.Inverse(vertexTransform)), true);
                else if (vector)
                {
                    for (int i = 0; i < values.Length; i += 4)
                    {
                        var tangent = Vector3.TransformNormal(new(values[i], values[i + 1], values[i + 2]), vertexTransform);
                        Capture.Require(tangent.LengthSquared() > 1e-12f, "Zero tangent.");
                        tangent = Vector3.Normalize(tangent);
                        values[i] = tangent.X; values[i + 1] = tangent.Y; values[i + 2] = tangent.Z;
                        values[i + 3] = Math.Sign(values[i + 3]) * (vertexTransform.GetDeterminant() < 0 ? -1 : 1) * -1; // also flip UV V
                        Capture.Require(Math.Abs(values[i + 3]) == 1, "Invalid tangent handedness.");
                    }
                }
                else Capture.Require(values.All(x => x >= 0 && x <= 1), "Vertex color outside [0,1].");
                attributes[semantic] = glb.Floats(values, components, type, 34962);
            }
        }
        Capture.Require(meshes.Count > 0, "No visible meshes.");
        var (animations, rules) = AnimationConversion.Build(capture, glb, nodes, metersPerUnit);
        var json = new Dictionary<string, object>
        {
            ["asset"] = new { version = "2.0", generator = "LibAottg.AssetTool 1" },
            ["scene"] = 0, ["scenes"] = new[] { new { name = "character", nodes = roots } },
            ["nodes"] = nodes, ["meshes"] = meshes, ["materials"] = materials,
            ["extras"] = new { sourceSchema = "libaottg.source-capture", metersPerUnit, handednessConversion = "reflect Z", textureConversion = "UV V flipped once after material transform" }
        };
        if (skins.Count > 0) json["skins"] = skins;
        if (textures.Count > 0) { json["textures"] = textures; json["images"] = images; json["samplers"] = samplers; }
        if (animations.Count > 0) json["animations"] = animations;
        if (unlit) json["extensionsUsed"] = new[] { "KHR_materials_unlit" };
        byte[] bytes = glb.Build(json);
        string hash = System.Convert.ToHexStringLower(SHA256.HashData(bytes));
        // Everything above is validated/built before the output is touched. Refuse
        // collisions so a typo cannot replace unrelated user files.
        string[] outputs = ["character.glb", "manifest.json", "animation-rules.json", "sockets.json", "physics.json", "bind-pose.json", "profiles/rcmod-human-blades.json"];
        foreach (string file in outputs) Capture.Require(!File.Exists(Path.Combine(output, file)), "Output already exists: " + Path.Combine(output, file) + ". Choose a new package directory.");
        Directory.CreateDirectory(Path.Combine(output, "profiles"));
        File.WriteAllBytes(Path.Combine(output, "character.glb"), bytes);
        Save("animation-rules.json", new { schema = "libaottg.animation-rules", version = 1, fidelity = "baked-solo-poses; authored masks unknown", rootMotion = "preserved in visual tracks; controller must not apply it again", clips = rules });
        Save("bind-pose.json", new { schema = "libaottg.bind-pose", version = 1, coordinates = "Unity source coordinates/units", anchorSourceNode = skeleton.AnchorNode, anchorJoint = skeleton.AnchorJoint, anchorPolicy = "preserve highest reference joint captured global frame and scale", tolerance = 2e-4, meshes = skeleton.Checks, joints = skeleton.Joints.Order().Select(j => new { sourceNode = j, worldBindMatrix = CoordinateMath.Elements(skeleton.RestGlobals[j]) }) });
        Save("sockets.json", new { schema = "libaottg.sockets", version = 1, coordinates = "glTF right-handed, Y-up, meters", sockets = capture.Nodes.Where(n => n.Text("name") is "hookRef" or "hookRefL1" or "hookRefR1" or "bladeStart" or "bladeEnd" or "hand_L" or "hand_R").Select(n => new { name = n.Text("name"), node = n.GetProperty("id").GetInt32(), parent = n.GetProperty("parent").GetInt32(), path = NodePath(capture, n.GetProperty("id").GetInt32()) }) });
        Save("physics.json", Physics(capture, metersPerUnit));
        Save("profiles/rcmod-human-blades.json", new { schema = "libaottg.profile", version = 1, id = "rcmod-human-blades", status = "asset-capture-only; behavior-unverified", behaviorVerified = false, source = capture.Manifest.GetProperty("source"), missing = new[] { "hero stats", "source revision/local diff", "mod settings", "animation action windows", "reference motion/camera traces" } });
        // Completion manifest is written last, matching the source capture lifecycle.
        Save("manifest.json", new { schema = "libaottg.character-package", version = 1, complete = true, converterRevision = 1, asset = "character.glb", sha256 = hash, sourceCapture = Path.GetFileName(input), sourceManifestSha256 = System.Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(capture.FilePath("manifest.json")))), source = capture.Manifest.GetProperty("source"), coordinates = "right-handed, Y-up, source forward maps to -Z", metersPerSourceUnit = metersPerUnit, scaleCalibrated = false, meshCount = meshes.Count, clipCount = animations.Count, textureCount = textures.Count, omittedMeshes = omitted, validation = "not run by converter; see validation-report.json when available", warnings, sourceWarnings = capture.Manifest.GetProperty("warnings"), files = outputs });
        return new(Path.Combine(output, "character.glb"), meshes.Count, animations.Count, bytes.Length, warnings.Count);

        void Save(string file, object value) => File.WriteAllText(Path.Combine(output, file), JsonSerializer.Serialize(value, Pretty) + Environment.NewLine);
    }

    private static List<Dictionary<string, object>> BuildNodes(Capture capture, BindSkeleton skeleton, float unit)
    {
        var nodes = new List<Dictionary<string, object>>();
        for (int i = 0; i < capture.Nodes.Length; i++)
        {
            var source = capture.Nodes[i];
            int parent = source.GetProperty("parent").GetInt32();
            Matrix4x4 local = parent < 0 ? skeleton.RestGlobals[i] : skeleton.RestGlobals[i] * CoordinateMath.Inverse(skeleton.RestGlobals[parent]);
            var trs = CoordinateMath.Decompose(CoordinateMath.ConvertMatrix(local, unit));
            var node = new Dictionary<string, object> { ["name"] = source.Text("name"), ["translation"] = trs.Position, ["rotation"] = trs.Rotation, ["scale"] = trs.Scale, ["extras"] = new { sourceNode = i, sourceMetadata = source } };
            int[] children = capture.Nodes.Where(n => n.GetProperty("parent").GetInt32() == i).Select(n => n.GetProperty("id").GetInt32()).ToArray();
            if (children.Length > 0) node["children"] = children;
            nodes.Add(node);
        }
        return nodes;
    }

    private static List<object> BuildMaterials(JsonElement[] sources, int textures, bool unlit, List<string> warnings)
    {
        var materials = new List<object>();
        foreach (var source in sources)
        {
            string shader = source.Text("shader");
            float[] color = source.TryGetProperty("color", out var tint) ? tint.Floats() : [1, 1, 1, 1];
            Capture.Require(color.Length == 4 && color.All(float.IsFinite), "Invalid material tint.");
            if (color.Any(x => x < 0 || x > 1)) warnings.Add($"Clamped HDR tint on '{source.Text("name")}'.");
            color = color.Select(x => Math.Clamp(x, 0, 1)).ToArray();
            var pbr = new Dictionary<string, object> { ["baseColorFactor"] = color, ["metallicFactor"] = 0, ["roughnessFactor"] = 1 };
            var maps = source.GetProperty("textures");
            if (maps.TryGetProperty("_MainTex", out var main))
            {
                int id = main.GetProperty("texture").GetInt32();
                Capture.Require(id >= 0 && id < textures, "Material texture reference out of range.");
                pbr["baseColorTexture"] = new { index = id };
            }
            foreach (var map in maps.EnumerateObject().Where(m => m.Name != "_MainTex")) warnings.Add($"Unsupported texture map '{map.Name}' in '{source.Text("name")}' retained only as metadata.");
            bool cutout = shader.Contains("Cutout", StringComparison.OrdinalIgnoreCase);
            bool transparent = shader.Contains("Transparent", StringComparison.OrdinalIgnoreCase) || shader.Contains("Particles", StringComparison.OrdinalIgnoreCase);
            var material = new Dictionary<string, object> { ["name"] = source.Text("name"), ["pbrMetallicRoughness"] = pbr, ["alphaMode"] = cutout ? "MASK" : transparent ? "BLEND" : "OPAQUE", ["doubleSided"] = false, ["extras"] = new { sourceMaterial = source, approximation = "legacy diffuse/specular to nonmetallic rough PBR" } };
            if (cutout) material["alphaCutoff"] = source.TryGetProperty("_Cutoff", out var cutoff) ? cutoff.GetSingle() : 0.5f;
            if (shader.Contains("Specular", StringComparison.OrdinalIgnoreCase) || shader.Contains("Additive", StringComparison.OrdinalIgnoreCase)) warnings.Add($"Shader '{shader}' in '{source.Text("name")}' approximated; specular/additive blending is not reproduced.");
            if (unlit) material["extensions"] = new Dictionary<string, object> { ["KHR_materials_unlit"] = new { } };
            materials.Add(material);
        }
        return materials;
    }

    private static void TransformVectors(float[] values, Matrix4x4 transform, bool normal)
    {
        for (int i = 0; i < values.Length; i += 3)
        {
            var value = new Vector3(values[i], values[i + 1], values[i + 2]);
            value = normal ? Vector3.TransformNormal(value, transform) : Vector3.Transform(value, transform);
            if (normal) { Capture.Require(value.LengthSquared() > 1e-12f, "Zero vertex normal."); value = Vector3.Normalize(value); }
            values[i] = value.X; values[i + 1] = value.Y; values[i + 2] = value.Z;
        }
    }

    private static string NodePath(Capture capture, int id)
    {
        var names = new List<string>();
        for (int n = id; n >= 0; n = capture.Nodes[n].GetProperty("parent").GetInt32()) names.Add(capture.Nodes[n].Text("name") + "[" + n + "]");
        names.Reverse(); return string.Join("/", names);
    }

    private static object Physics(Capture capture, float unit)
    {
        var colliders = new List<Dictionary<string, object>>();
        foreach (var source in capture.Manifest.GetProperty("colliders").EnumerateArray())
        {
            int id = source.GetProperty("node").GetInt32();
            Capture.Require(id >= 0 && id < capture.Nodes.Length, "Collider node out of range.");
            var collider = source.EnumerateObject().ToDictionary(p => p.Name, p => (object)p.Value.Clone());
            collider["center"] = CoordinateMath.Position(source.GetProperty("center").Floats(), unit);
            if (source.TryGetProperty("size", out var size)) collider["size"] = size.Floats().Select(x => x * unit).ToArray();
            foreach (string field in new[] { "radius", "height" }) if (source.TryGetProperty(field, out var value)) collider[field] = value.GetSingle() * unit;
            colliders.Add(collider);
        }
        return new { schema = "libaottg.physics", version = 1, coordinates = "glTF right-handed, Y-up, meters; collider dimensions are node-local", gravity = CoordinateMath.Position(capture.Manifest.GetProperty("source").GetProperty("physicsGravity").Floats(), unit), rigidbody = capture.Manifest.GetProperty("source").GetProperty("rigidbody"), colliders, note = "Apply node world scale to shapes. Layer rules, trigger windows and controller mechanics are not encoded." };
    }
}
