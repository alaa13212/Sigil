using System.Xml;
using Sigil.Server.Auth.Saml;

namespace Sigil.Server.Tests.Auth;

public class SamlMetadataReaderTests
{
    private const string EntityId = "https://idp.example.com/metadata";
    private const string OtherEntityId = "https://other.example.com/metadata";

    private static string SingleEntity(string entityId = EntityId) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <EntityDescriptor xmlns="urn:oasis:names:tc:SAML:2.0:metadata" entityID="{entityId}">
          <IDPSSODescriptor protocolSupportEnumeration="urn:oasis:names:tc:SAML:2.0:protocol">
            <SingleSignOnService Binding="urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect" Location="https://idp.example.com/sso" />
          </IDPSSODescriptor>
        </EntityDescriptor>
        """;

    private static string Federation(params string[] entityIds) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <EntitiesDescriptor xmlns="urn:oasis:names:tc:SAML:2.0:metadata" Name="urn:example:federation">
          {string.Join("\n  ", entityIds.Select(id => $"""
            <EntityDescriptor entityID="{id}">
              <IDPSSODescriptor protocolSupportEnumeration="urn:oasis:names:tc:SAML:2.0:protocol">
                <SingleSignOnService Binding="urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect" Location="https://idp.example.com/sso" />
              </IDPSSODescriptor>
            </EntityDescriptor>
            """))}
        </EntitiesDescriptor>
        """;

    [Fact]
    public void Read_SingleEntityDescriptor_ReturnsDescriptor()
    {
        var descriptor = SamlMetadataReader.Read(SingleEntity(), EntityId);

        descriptor.EntityId.Id.Should().Be(EntityId);
        descriptor.RoleDescriptors.Should().NotBeEmpty();
    }

    [Fact]
    public void Read_EntitiesDescriptor_SelectsExpectedEntity()
    {
        var descriptor = SamlMetadataReader.Read(Federation(OtherEntityId, EntityId), EntityId);

        descriptor.EntityId.Id.Should().Be(EntityId);
    }

    [Fact]
    public void Read_EntitiesDescriptorWithoutExpectedEntity_Throws()
    {
        var act = () => SamlMetadataReader.Read(Federation(OtherEntityId), EntityId);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{EntityId}*");
    }

    [Fact]
    public void Read_UnexpectedEntityId_Throws()
    {
        var act = () => SamlMetadataReader.Read(SingleEntity(OtherEntityId), EntityId);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{OtherEntityId}*");
    }

    [Fact]
    public void Read_DocumentWithDoctype_Throws()
    {
        const string xxe = """
            <?xml version="1.0" encoding="utf-8"?>
            <!DOCTYPE EntityDescriptor [ <!ENTITY xxe SYSTEM "file:///etc/passwd"> ]>
            <EntityDescriptor xmlns="urn:oasis:names:tc:SAML:2.0:metadata" entityID="https://idp.example.com/metadata">
              <IDPSSODescriptor protocolSupportEnumeration="urn:oasis:names:tc:SAML:2.0:protocol">
                <SingleSignOnService Binding="urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect" Location="&xxe;" />
              </IDPSSODescriptor>
            </EntityDescriptor>
            """;

        var act = () => SamlMetadataReader.Read(xxe, EntityId);

        act.Should().Throw<XmlException>();
    }

    [Fact]
    public void Read_MetadataAboveSizeLimit_Throws()
    {
        var oversized = SingleEntity() + new string('x', SamlMetadataReader.MaxMetadataLength);

        var act = () => SamlMetadataReader.Read(oversized, EntityId);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*larger than*");
    }

    [Fact]
    public void Read_DoesNotWriteTemporaryFiles()
    {
        var before = Directory.GetFiles(Path.GetTempPath(), "sigil-saml-*.xml");

        SamlMetadataReader.Read(SingleEntity(), EntityId);

        var after = Directory.GetFiles(Path.GetTempPath(), "sigil-saml-*.xml");
        after.Should().BeEquivalentTo(before);
    }
}
