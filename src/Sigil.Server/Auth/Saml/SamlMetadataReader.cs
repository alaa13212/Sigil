using Sustainsys.Saml2.Metadata;

namespace Sigil.Server.Auth.Saml;

internal static class SamlMetadataReader
{
    public static EntityDescriptor Read(string metadataXml, string expectedEntityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataXml);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedEntityId);

        var path = Path.Combine(Path.GetTempPath(), $"sigil-saml-{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, metadataXml);

        try
        {
            var location = new Uri(path).AbsoluteUri;

            EntityDescriptor descriptor;
            try
            {
                descriptor = MetadataLoader.LoadIdp(location, true);
            }
            catch (InvalidOperationException)
            {
                var federation = MetadataLoader.LoadFederation(location);
                descriptor = federation.ChildEntities
                    .FirstOrDefault(e => e.EntityId.Id == expectedEntityId)
                    ?? throw new InvalidOperationException(
                        $"The metadata does not contain an entity descriptor for '{expectedEntityId}'.");
            }

            if (descriptor.EntityId.Id != expectedEntityId)
            {
                throw new InvalidOperationException(
                    $"The metadata describes '{descriptor.EntityId.Id}' but the connection is configured for '{expectedEntityId}'.");
            }

            return descriptor;
        }
        finally
        {
            File.Delete(path);
        }
    }
}
