using Microsoft.AspNetCore.Components.Web;

namespace Sigil.Server.Client.Framework;

public static class RenderMode
{
    public static InteractiveWebAssemblyRenderMode InteractiveWebNoPrerender { get; } = new (prerender: false);
}
