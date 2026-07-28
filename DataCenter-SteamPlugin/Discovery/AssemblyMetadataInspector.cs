using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace DataCenter_SteamPlugin.Discovery;

public sealed record AssemblyMetadata(string Name, Version Version, bool HasMelonInfo, bool HasMod, bool HasPlugin);

public sealed class AssemblyMetadataInspector
{
    public bool TryInspect(string path, out AssemblyMetadata? metadata)
    {
        metadata = null;
        try
        {
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata) return false;
            var md = pe.GetMetadataReader();
            var asm = md.GetAssemblyDefinition();
            var name = md.GetString(asm.Name);
            var version = asm.Version ?? new Version(0, 0, 0, 0);
            bool info = false, mod = false, plugin = false;
            foreach (var attrHandle in asm.GetCustomAttributes())
            {
                var attr = md.GetCustomAttribute(attrHandle);
                var typeName = GetAttributeTypeName(md, attr.Constructor);
                if (typeName is "MelonInfoAttribute" or "MelonPluginInfoAttribute") info = true;
            }
            foreach (var handle in md.TypeDefinitions)
            {
                var type = md.GetTypeDefinition(handle);
                var baseName = GetTypeName(md, type.BaseType);
                if (baseName == "MelonMod") mod = true;
                if (baseName == "MelonPlugin") plugin = true;
            }
            metadata = new AssemblyMetadata(name, version, info, mod && info, plugin && info);
            return true;
        }
        catch { return false; }
    }

    public IEnumerable<AssemblyMetadata> InspectDirectory(string directory)
    {
        foreach (var dll in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
            if (TryInspect(dll, out var metadata) && metadata != null) yield return metadata;
    }

    private static string? GetAttributeTypeName(MetadataReader md, EntityHandle ctor)
    {
        return ctor.Kind switch
        {
            HandleKind.MemberReference => GetTypeName(md, md.GetMemberReference((MemberReferenceHandle)ctor).Parent),
            HandleKind.MethodDefinition => GetTypeName(md, md.GetMethodDefinition((MethodDefinitionHandle)ctor).GetDeclaringType()),
            _ => null,
        };
    }

    private static string? GetTypeName(MetadataReader md, EntityHandle handle)
    {
        if (handle.IsNil) return null;
        return handle.Kind switch
        {
            HandleKind.TypeDefinition => md.GetString(md.GetTypeDefinition((TypeDefinitionHandle)handle).Name),
            HandleKind.TypeReference => md.GetString(md.GetTypeReference((TypeReferenceHandle)handle).Name),
            HandleKind.TypeSpecification => null,
            _ => null,
        };
    }
}
