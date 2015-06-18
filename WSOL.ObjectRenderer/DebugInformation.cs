namespace WSOL.ObjectRenderer
{
    using System;
    using System.Collections.Generic;
    using WSOL.IocContainer;
    using WSOL.ObjectRenderer.Attributes;
    using WSOL.ObjectRenderer.Extensions;
    using WSOL.ObjectRenderer.HttpModules;

    public class DebugInformation
    {
        public static bool EnableViewWatcher { get { return FileWatcherModule.EnableViewWatcher; } }

        public static bool IsWebsite { get { return FileWatcherModule.IsWebsite; } }

        public static Dictionary<CustomTuple<Type, string>, TemplateDescriptorAttribute> ResolvedTemplates
        {
            get
            {
                return new Dictionary<CustomTuple<Type, string>, TemplateDescriptorAttribute>(TemplateExtensions._ResolvedTemplates);
            }
        }

        public static HashSet<TemplateDescriptorAttribute> TemplateDescriptors
        {
            get
            {
                return new HashSet<TemplateDescriptorAttribute>(TemplateExtensions._TemplateDescriptorList);
                //return new Dictionary <TemplateDescriptorAttribute, Type>(TemplateExtensions._TemplateDescriptorList);
            }
        }

        public static bool ViewWatcherSet { get { return FileWatcherModule.ViewWatcher != null; } }
    }
}
