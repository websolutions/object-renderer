namespace WSOL.ObjectRenderer.HttpModules
{
    using Extensions;
    using System;
    using System.Configuration;
    using System.IO;
    using System.Web;
    using System.Web.Compilation;
    using WSOL.IocContainer;
    using WSOL.ObjectRenderer.Attributes;

    /// <summary>
    /// This module builds ASCX files in the ~/Views folder to be used by the WSOL:ObjectRenderer control.
    /// </summary>
    public class FileWatcherModule : IApplicationModule
    {
        /// <summary>
        /// Enables FileSystemWatcher
        /// </summary>
        internal static readonly bool EnableViewWatcher = string.IsNullOrEmpty(ConfigurationManager.AppSettings[_AppKeyEnableFileWatcher]) ? false : bool.Parse(ConfigurationManager.AppSettings[_AppKeyEnableFileWatcher]);

        /// <summary>
        /// Allows watch path to be changed from default of ~/Views
        /// </summary>
        internal static readonly string FolderPath = string.IsNullOrEmpty(ConfigurationManager.AppSettings[_AppKeyViewsPath]) ? "~/Views" : ConfigurationManager.AppSettings[_AppKeyViewsPath];

        /// <summary>
        /// Allows buildfiles to be skipped if its a compiled site, default is true
        /// </summary>
        internal static readonly bool IsWebsite = string.IsNullOrEmpty(ConfigurationManager.AppSettings[_AppKeyWebApp]) ? true : !bool.Parse(ConfigurationManager.AppSettings[_AppKeyWebApp]);
        
        internal static System.IO.FileSystemWatcher ViewWatcher = null;
        
        private const string _AppKeyEnableFileWatcher = "WSOL.ObjectRenderer.EnableFileWatcher";
        
        private const string _AppKeyViewsPath = "WSOL.ObjectRenderer.ViewsPath";
        
        private const string _AppKeyWebApp = "WSOL.ObjectRenderer.Compiled";
        
        private static IApplicationHelper _ApplicationHelper;

        // Processing Flag
        private static volatile bool IsBuilding = false;

        public void Dispose()
        {
            if (ViewWatcher != null)
                ViewWatcher.Dispose();
        }

        public void Init(HttpApplication context)
        {
            _ApplicationHelper = InitializationContext.Locator.Get<IApplicationHelper>();
            string path = _ApplicationHelper.MapPath(FolderPath);

            if (ViewWatcher == null && EnableViewWatcher && IsWebsite)
            {
                ViewWatcher = new FileSystemWatcher(path: path);
                ViewWatcher.IncludeSubdirectories = true;
                ViewWatcher.Filter = "*.cs";// "*.ascx";
                ViewWatcher.NotifyFilter =
                    NotifyFilters.LastWrite |
                    NotifyFilters.FileName |
                    NotifyFilters.DirectoryName;

                ViewWatcher.Changed += ViewWatcher_CUD;
                ViewWatcher.Deleted += ViewWatcher_CUD;
                ViewWatcher.Created += ViewWatcher_CUD;
                ViewWatcher.Renamed += ViewWatcher_Renamed;
            }

            BuildFiles(path: path, compiled: !IsWebsite);
        }

        private static void BuildFiles(string path, bool compiled = false, bool deleted = false, bool replace = false)
        {
            FileAttributes attr = File.GetAttributes(path);

            if (IsBuilding)
                return;
            else
                IsBuilding = true;

            if (ViewWatcher != null)
                ViewWatcher.EnableRaisingEvents = false; // Keeps from firing twice

            bool rebuildList = true;
            Type compiledType = null;
            TemplateDescriptorAttribute descriptor = null;

            if (!deleted && !compiled)
            {
                if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    var files = System.IO.Directory.GetFiles(path, "*.ascx", SearchOption.AllDirectories);

                    foreach (var file in files)
                    {
                        compiledType = BuildManager.GetCompiledType(_ApplicationHelper.MapPathReverse(file));

                        if (compiledType != null && compiledType.BaseType != null)
                            descriptor = compiledType.BaseType.AddTemplateDescriptor(replace: replace);
                    }
                }
                else
                {
                    if (path.ToLower().EndsWith(".cs")) // requires convention that code behind is *.ascx.cs
                    {
                        string file = _ApplicationHelper.MapPathReverse(TrimEnd(path, ".cs"));

                        if (file.EndsWith(".ascx", StringComparison.InvariantCultureIgnoreCase))
                        {
                            compiledType = BuildManager.GetCompiledType(file);

                            if (compiledType != null && compiledType.BaseType != null)
                                descriptor = compiledType.BaseType.AddTemplateDescriptor(replace: replace);
                        }

                        rebuildList = descriptor == null;
                    }
                }
            }

            // Re/build the views after compiling them
            Extensions.TemplateExtensions.BuildDescriptorList(regen: rebuildList);

            if (ViewWatcher != null)
                ViewWatcher.EnableRaisingEvents = true; // re-enable watcher after its completed

            IsBuilding = false;
        }

        private static string TrimEnd(string source, string value)
        {
            if (!source.EndsWith(value))
                return source;

            return source.Remove(source.LastIndexOf(value));
        }

        private void ViewWatcher_CUD(object sender, System.IO.FileSystemEventArgs e)
        {
            BuildFiles(path: e.FullPath, deleted: e.ChangeType == WatcherChangeTypes.Deleted, replace: true);
        }

        private void ViewWatcher_Renamed(object sender, System.IO.RenamedEventArgs e)
        {
            BuildFiles(path: e.FullPath, replace: true);
        }
    }
}