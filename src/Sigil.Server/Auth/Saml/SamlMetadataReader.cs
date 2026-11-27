using System.Xml;
using Sustainsys.Saml2.Metadata;

namespace Sigil.Server.Auth.Saml;

internal static class SamlMetadataReader
{
    internal const int MaxMetadataLength = 1_048_576;

    public static EntityDescriptor Read(string metadataXml, string expectedEntityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataXml);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedEntityId);

        if (metadataXml.Length > MaxMetadataLength)
            throw new InvalidOperationException(
                $"The metadata document is larger than the {MaxMetadataLength} character limit.");

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxMetadataLength,
            IgnoreComments = true,
            IgnoreWhitespace = true
        };

        using var textReader = new StringReader(metadataXml);
        using var xmlReader = XmlReader.Create(textReader, settings);

        var metadata = new MetadataSerializer().ReadMetadata(xmlReader);

        return metadata switch
        {
            EntityDescriptor descriptor => VerifyEntity(descriptor, expectedEntityId),
            EntitiesDescriptor federation => SelectEntity(federation, expectedEntityId),
            _ => throw new InvalidOperationException("The metadata does not contain an entity descriptor.")
        };
    }

    private static EntityDescriptor VerifyEntity(EntityDescriptor descriptor, string expectedEntityId)
    {
        if (descriptor.EntityId.Id != expectedEntityId)
        {
            throw new InvalidOperationException(
                $"The metadata describes '{descriptor.EntityId.Id}' but the connection is configured for '{expectedEntityId}'.");
        }

        return descriptor;
    }

    private static EntityDescriptor SelectEntity(EntitiesDescriptor federation, string expectedEntityId)
    {
        foreach (var entity in federation.ChildEntities)
        {
            if (entity.EntityId.Id == expectedEntityId)
                return entity;
        }

        foreach (var group in federation.ChildEntityGroups)
        {
            foreach (var entity in group.ChildEntities)
            {
                if (entity.EntityId.Id == expectedEntityId)
                    return entity;
            }
        }

        throw new InvalidOperationException(
            $"The metadata does not contain an entity descriptor for '{expectedEntityId}'.");
    }
}
