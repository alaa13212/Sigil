using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;
using Sigil.Domain.Interfaces;

namespace Sigil.Application.Services;

public class PredefinedTagsEnricher : IEventEnricher
{
   // Predefined tags
   private const string TagEnvironment = "environment";
   private const string TagRelease = "release";
   private const string TagEventLevel = "level";
   private const string TagServerName = "server_name";
   private const string TagRuntime = "runtime";
   private const string TagRuntimeName = "runtime.name";
   
   public void Enrich(ParsedEvent parsedEvent, EventParsingContext context)
   {
       parsedEvent.Tags ??= [];
       
       // Add predefined tags
       if (parsedEvent.Environment != null)
           parsedEvent.Tags[TagEnvironment] = parsedEvent.Environment;

       if (parsedEvent.Release != null)
           parsedEvent.Tags[TagRelease] = parsedEvent.Release;
       
       parsedEvent.Tags[TagEventLevel] = parsedEvent.Level.ToStringValue();
       
       if (parsedEvent.ServerName != null)
           parsedEvent.Tags[TagServerName] = parsedEvent.ServerName;
           
       if(parsedEvent.Runtime is {} runtime)
       {
           parsedEvent.Tags[TagRuntime] = runtime.ToString();
           parsedEvent.Tags[TagRuntimeName] = runtime.Name;
       }
   }
}