using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;

namespace Sigil.Server.Client.Extensions;

public static class BlazorComponentExtensions
{
    public static bool TryGetRouteData(this Delegate renderFragment, [NotNullWhen(true)] out RouteData? routeData)
    {
        routeData = null;

        if (renderFragment?.Target is RouteView routeView)
        {
            routeData = routeView.RouteData;
        }
        else if (renderFragment?.Target != null)
        {
            // the target may be a CompilerGenerated class
            var bodyField = renderFragment.Target.GetType().GetField("bodyParam");

            if (bodyField is not null)
            {
                var value = bodyField.GetValue(renderFragment.Target);

                if (value is Delegate childDel)
                {
                    TryGetRouteData(childDel, out routeData);
                }
            }
        }

        return routeData is not null;
    }
}