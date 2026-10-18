using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
namespace SignalMonitor;
public class Settings : BasePluginConfiguration { public string TunerUrls { get; set; } = ""; }
public class Plugin : BasePlugin<Settings>, IHasWebPages {
 public static Plugin? Instance {get;private set;}
 public Plugin(IApplicationPaths paths, IXmlSerializer serializer):base(paths,serializer){Instance=this;}
 public override string Name => "HDHomeRun Signal Monitor";
 public override Guid Id => Guid.Parse("a75283c1-427c-4d56-9f52-e4f9725b709a");
 public IEnumerable<PluginPageInfo> GetPages() => [new(){Name="signalmonitor",EmbeddedResourcePath="SignalMonitor.config.html"}];
}

