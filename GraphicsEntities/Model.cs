using Silk.NET.Assimp;
using YASV.RHI;
using Texture = YASV.RHI.Texture;

namespace YASV.GraphicsEntities;

public class Model(Vertex[] vertices, uint[] indices, List<Texture> textures)
{
    public Vertex[] Vertices { get; private set; } = vertices;
    public uint[] Indices { get; private set; } = indices;
    public List<Texture> Textures { get; private set; } = textures;
    public VertexBuffer? VertexBuffer { get; set; }
    public IndexBuffer? IndexBuffer { get; set; }
}

public static class ModelExtensions
{
    private static readonly Assimp _assimp = Assimp.GetApi();

    public static unsafe List<Model> LoadModels(string path, Func<string, Texture> createTexture)
    {
        var scene = _assimp.ImportFile(path, (uint)PostProcessSteps.Triangulate);

        if (scene == null || scene->MFlags == Assimp.SceneFlagsIncomplete || scene->MRootNode == null)
        {
            var error = _assimp.GetErrorStringS();
            throw new Exception(error);
        }

        var models = new List<Model>();
        ProcessNode(scene->MRootNode, scene, ref models, createTexture);
        return models;
    }

    private static unsafe void ProcessNode(Node* node, Scene* scene, ref List<Model> models, Func<string, Texture> createTexture)
    {
        for (var i = 0; i < node->MNumMeshes; i++)
        {
            var mesh = scene->MMeshes[node->MMeshes[i]];
            models.Add(ProcessMesh(mesh, scene, createTexture));

        }

        for (var i = 0; i < node->MNumChildren; i++)
        {
            ProcessNode(node->MChildren[i], scene, ref models, createTexture);
        }
    }

    private static unsafe Model ProcessMesh(Mesh* mesh, Scene* scene, Func<string, Texture> createTexture)
    {
        var vertices = new Vertex[mesh->MNumVertices];
        var indices = new List<uint>();
        var textures = new List<Texture>();

        for (uint i = 0; i < mesh->MNumVertices; i++)
        {
            var mv = mesh->MVertices[i];
            var tc = mesh->MTextureCoords[0][i];
            vertices[i] = new Vertex(new(mv.X, mv.Y, mv.Z), new(1.0f, 1.0f, 1.0f), new(tc.X, 1.0f - tc.Y));
        }

        for (uint i = 0; i < mesh->MNumFaces; i++)
        {
            Face face = mesh->MFaces[i];
            for (uint j = 0; j < face.MNumIndices; j++)
            {
                indices.Add(face.MIndices[j]);
            }
        }

        Material* material = scene->MMaterials[mesh->MMaterialIndex];

        var diffuseMaps = LoadMaterialTextures(material, TextureType.Diffuse, createTexture);
        if (diffuseMaps.Count != 0)
        {
            textures.AddRange(diffuseMaps);
        }

        return new Model(vertices, [.. indices], textures);
    }

    private static unsafe List<Texture> LoadMaterialTextures(Material* mat, TextureType type, Func<string, Texture> createTexture)
    {
        var textureCount = _assimp.GetMaterialTextureCount(mat, type);
        var textures = new List<Texture>();
        for (uint i = 0; i < textureCount; i++)
        {
            AssimpString path;
            _assimp.GetMaterialTexture(mat, type, i, &path, null, null, null, null, null, null);
            textures.Add(createTexture(path));
        }
        return textures;
    }
}
