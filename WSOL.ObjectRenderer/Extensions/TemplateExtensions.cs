namespace WSOL.ObjectRenderer.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Web;
    using System.Web.UI;
    using WSOL.IocContainer;
    using WSOL.ObjectRenderer.Attributes;
    using WSOL.ObjectRenderer.Delegates;
    using WSOL.ObjectRenderer.Interfaces;

    public static class TemplateExtensions
    {
        internal static SafeDictionary<CustomTuple<Type, string>, TemplateDescriptorAttribute> _ResolvedTemplates;
        internal static HashSet<TemplateDescriptorAttribute> _TemplateDescriptorList;
        private static TemplateControl _ControlLoader = new UserControl();
        private static object _Lock = new object();
        private static ILogger _Logger = InitializationContext.Locator.Get<ILogger>();
        private static volatile bool _Updating;

        /// <summary>
        /// Gets a list of all registered templates for a given type
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static IEnumerable<TemplateDescriptorAttribute> GetTemplates(this Type t)
        {
            if (_TemplateDescriptorList == null)
            {
                BuildDescriptorList(false);
            }

            foreach (var item in _TemplateDescriptorList)
            {
                if (item.ModelType == t)
                    yield return item;
            }

            yield break;
        }

        /// <summary>
        /// Adds a template for given type
        /// </summary>
        /// <param name="t">Should match generic argument of ControlBase or TemplateBase class.</param>
        /// <param name="templateDescriptor"></param>
        /// <returns></returns>
        public static bool RegisterTemplate(this Type t, TemplateDescriptorAttribute templateDescriptor, bool replace = false)
        {
            if (templateDescriptor == null || t == null)
            {
                return false;
            }

            if (_TemplateDescriptorList == null)
            {
                BuildDescriptorList(false);
            }

            templateDescriptor.SetModelType(t);

            bool added = _TemplateDescriptorList.Add(templateDescriptor);

            if (!added)
            {
                _TemplateDescriptorList.Remove(templateDescriptor);
                added = _TemplateDescriptorList.Add(templateDescriptor);
            }

            return added;
        }

        /// <summary>
        /// Adds template descriptor to static list
        /// </summary>
        /// <param name="type"></param>
        /// <param name="replace"></param>
        /// <returns></returns>
        internal static TemplateDescriptorAttribute AddTemplateDescriptor(this Type type, bool replace = false)
        {
            var descriptor = Attribute.GetCustomAttribute(type, typeof(TemplateDescriptorAttribute)) as TemplateDescriptorAttribute;

            if (descriptor == null)
                return null;

            Type modelType = null;

            if (type.BaseType.IsGenericType)
            {
                modelType = type.BaseType.GetGenericArguments().FirstOrDefault();
                descriptor.SetModelType(modelType); // assign the generic T
            }

            descriptor.SetTemplateObjectType(type);

            if (!_Updating)
            {
                _Updating = true;
                EnsureTemplateDescriptorList();

                if (!_TemplateDescriptorList.Add(descriptor) && replace)
                {
                    var toRemoveResolved = _ResolvedTemplates.Keys.Where(x => x.Item1 == modelType).Select(x => x);

                    foreach (var item in toRemoveResolved)
                        _ResolvedTemplates.Remove(item);

                    _TemplateDescriptorList.Remove(descriptor);
                    _TemplateDescriptorList.Add(descriptor);
                }

                _Updating = false;
            }

            return descriptor;
        }

        /// <summary>
        /// Build _TemplateDescriptorList and assign the ModelType for each TemplateDescriptor based on its generic type in ControlBase&gt;T&lt;
        /// </summary>
        internal static void BuildDescriptorList(bool regen = false)
        {
            if (_TemplateDescriptorList == null || regen)
            {
                lock (_Lock)
                {
                    EnsureTemplateDescriptorList(regen);

                    // Resets the descriptors for web sites, not really needed for webapps
                    _ResolvedTemplates = new SafeDictionary<CustomTuple<Type, string>, TemplateDescriptorAttribute>();
                    var descriptors = _TemplateDescriptorList.ScanForAttribute<TemplateDescriptorAttribute>(regen, false, false, true);

                    foreach (var type in descriptors)
                    {
                        var descriptor = type.AddTemplateDescriptor();

                        //try
                        //{
                        //    if(descriptor != null)
                        //         added = _TemplateDescriptorList.Add(descriptor);
                        //    //_TemplateDescriptorList.Add(descriptor, type);
                        //}
                        //catch (ArgumentException)
                        //{
                        //    var message = new Exception("The template descriptor paths must be unique for every instance! Multiple have been found for: " +
                        //            descriptor.Path + ". If this site is compiled please add \"WSOL.ObjectRenderer.Compiled\" application setting equal to \"true\". Stored instance Type = " +
                        //            "hashset shouldn't have this error" + //_TemplateDescriptorList.[descriptor].FullName +
                        //            " and new instance = " + type.FullName + " for " + descriptor.ToString());

                        //    message.Log(typeof(TemplateExtensions).FullName, System.Diagnostics.EventLogEntryType.Information, descriptor);
                        //}
                    }
                }
            }
        }

        /// <summary>
        /// Loads the given control path and assigns the current data (renderData) to control.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="renderData"></param>
        /// <param name="bindDataAction"></param>
        /// <returns></returns>
        internal static Control Load(this string path, object renderData, Action<Control, object> bindDataAction = null)
        {
            Control control = null;

            if (bindDataAction == null)
            {
                bindDataAction = new Action<Control, object>(LoadContentData);
            }

            try
            {
                control = (HttpContext.Current.CurrentHandler as TemplateControl ?? _ControlLoader).LoadControl(path);
            }
            catch (Exception ex)
            {
                ex.Log(typeof(TemplateExtensions).FullName, System.Diagnostics.EventLogEntryType.Error, path);

                if (HttpContext.Current != null && HttpContext.Current.IsDebuggingEnabled)
                    throw ex;

                return null;
            }

            PartialCachingControl cachedControl = control as PartialCachingControl;

            if (cachedControl != null)
            {
                control.Init += (EventHandler)((sender, e) => bindDataAction(cachedControl.CachedControl, renderData));
            }
            else
            {
                bindDataAction(control, renderData);
            }

            return control;
        }

        /// <summary>
        /// Resolution engine for models to find the appropriate render
        /// </summary>
        /// <param name="T"></param>
        /// <param name="OnResolved"></param>
        /// <param name="Tags"></param>
        /// <returns>resolved TemplateDescriptorAttribute or null</returns>
        internal static TemplateDescriptorAttribute ResolveTemplate(this Type T, TemplateResolverHandler OnResolved, string[] Tags, object Item)
        {
            TemplateDescriptorAttribute descriptor = null;

            if (Tags == null)
            {
                Tags = new string[] { };
            }

            CustomTuple<Type, string> tuple = CustomTuple.Create(T, string.Join(",", Tags));

            if (!_ResolvedTemplates.TryGetValue(tuple, out descriptor))
            {
                BuildDescriptorList(false);

                if (_TemplateDescriptorList != null && _TemplateDescriptorList.Count > 0)
                {
                    // Only get the base Types that have descriptors
                    IEnumerable<Type> baseTypes = T.GetBaseTypes().Intersect(_TemplateDescriptorList.Select(x => x.ModelType));

                    lock (_Lock)
                    {
                        // Check tags
                        if (Tags != null && Tags.Length > 0)
                        {
                            var matches = _TemplateDescriptorList.Where(x => x.Tags.Any(y => Tags.Contains(y)));

                            if (matches.Any())
                            {
                                // Get the highest number of matches
                                var counts = (from x in matches select new { Frequency = x.Tags.Count(y => Tags.Contains(y)), Descriptor = x })
                                    .OrderByDescending(x => x.Frequency).ThenByDescending(x => x.Descriptor.Default);

                                #region Required Tags

                                // Check type
                                var typecheck = counts.Where(x => x.Descriptor.ModelType == T && x.Descriptor.RequireTags);//.OrderBy(x => x.Descriptor.Path);

                                if (typecheck.Any())
                                {
                                    var defaultCheck = typecheck.Where(x => x.Descriptor.Default).FirstOrDefault();

                                    if (defaultCheck != null) // defaults should win
                                    {
                                        descriptor = defaultCheck.Descriptor;
                                    }
                                    else
                                    {
                                        descriptor = typecheck.First().Descriptor;
                                    }
                                }
                                else
                                {
                                    // Inheritance check, pushing require tags to top
                                    var inheritanceCheck = counts.Where(x => x.Descriptor.Inherited && x.Descriptor.RequireTags && x.Descriptor.ModelType.IsAssignableFrom(T));

                                    if (inheritanceCheck.Any())
                                    {
                                        var defaultCheck = inheritanceCheck.Where(x => x.Descriptor.Default);

                                        if (defaultCheck.Any()) // defaults should win
                                        {
                                            // check baseTypes against ModelType, sort by baseTypes order
                                            var orderedTypes = from type in baseTypes join item in defaultCheck on type equals item.Descriptor.ModelType select item;
                                            descriptor = orderedTypes.First().Descriptor;
                                        }
                                        else
                                        {
                                            var orderedTypes = from type in baseTypes join item in inheritanceCheck on type equals item.Descriptor.ModelType select item;
                                            descriptor = orderedTypes.First().Descriptor;
                                        }
                                    }
                                }

                                #endregion Required Tags

                                #region Non Required Tags

                                // Check for non required tags
                                if (descriptor == null)
                                {
                                    // Only look at descriptors with no required tags
                                    var noRequiredTags = counts.Where(x => !x.Descriptor.RequireTags).OrderBy(x => x.Descriptor.Path);

                                    // Check type
                                    var typecheck2 = noRequiredTags.Where(x => x.Descriptor.ModelType == T);

                                    if (typecheck2.Any())
                                    {
                                        var defaultCheck = typecheck2.Where(x => x.Descriptor.Default).OrderBy(x => x.Descriptor.Path).FirstOrDefault();

                                        if (defaultCheck != null) // defaults should win
                                        {
                                            descriptor = defaultCheck.Descriptor;
                                        }
                                        else
                                        {
                                            descriptor = typecheck2.First().Descriptor;
                                        }
                                    }
                                    else
                                    {
                                        // Inheritance check
                                        var inheritanceCheck = noRequiredTags.Where(x => x.Descriptor.Inherited && x.Descriptor.ModelType.IsAssignableFrom(T));

                                        if (inheritanceCheck.Any())
                                        {
                                            var defaultCheck = inheritanceCheck.Where(x => x.Descriptor.Default);

                                            if (defaultCheck.Any()) // defaults should win
                                            {
                                                var orderedTypes = from type in baseTypes join item in defaultCheck on type equals item.Descriptor.ModelType select item;
                                                descriptor = orderedTypes.First().Descriptor;
                                            }
                                            else
                                            {
                                                var orderedTypes = from type in baseTypes join item in inheritanceCheck on type equals item.Descriptor.ModelType select item;
                                                descriptor = orderedTypes.First().Descriptor;
                                            }
                                        }
                                    }
                                }

                                #endregion Non Required Tags
                            }
                        }

                        #region Default Check

                        if (descriptor == null)
                        {
                            var matches = _TemplateDescriptorList.Where(x => x.Default).OrderBy(x => x.Path);

                            if (matches.Any())
                            {
                                // Check type
                                var typecheck = matches.Where(x => x.ModelType == T).FirstOrDefault();

                                if (typecheck != null)
                                {
                                    descriptor = typecheck;
                                }
                                else
                                {
                                    // Inheritance check
                                    var inheritanceCheck = matches.Where(x => x.Inherited && x.ModelType.IsAssignableFrom(T));

                                    if (inheritanceCheck.Any())
                                    {
                                        var orderedTypes = from type in baseTypes join item in inheritanceCheck on type equals item.ModelType select item;
                                        descriptor = inheritanceCheck.First();
                                    }
                                }
                            }
                        }

                        #endregion Default Check

                        #region Non Required Tags, final check if no defaults are located.

                        // Check for non required tags
                        if (descriptor == null)
                        {
                            // Only look at descriptors with no required tags
                            var noRequiredTags = _TemplateDescriptorList.Where(x => !x.RequireTags).OrderBy(x => x.Path);

                            // Check type
                            var typecheck = noRequiredTags.Where(x => x.ModelType == T);

                            if (typecheck.Any())
                            {
                                var defaultCheck = typecheck.Where(x => x.Default).FirstOrDefault();

                                if (defaultCheck != null) // defaults should win
                                {
                                    descriptor = defaultCheck;
                                }
                                else
                                {
                                    descriptor = typecheck.First();
                                }
                            }
                            else
                            {
                                // Inheritance check
                                var inheritanceCheck = noRequiredTags.Where(x => x.Inherited && x.ModelType.IsAssignableFrom(T));

                                if (inheritanceCheck.Any())
                                {
                                    var defaultCheck = inheritanceCheck.Where(x => x.Default);

                                    if (defaultCheck.Any()) // defaults should win
                                    {
                                        var orderedTypes = from type in baseTypes join item in defaultCheck on type equals item.ModelType select item;
                                        descriptor = orderedTypes.First();
                                    }
                                    else
                                    {
                                        var orderedTypes = from type in baseTypes join item in inheritanceCheck on type equals item.ModelType select item;
                                        descriptor = inheritanceCheck.First();
                                    }
                                }
                            }
                        }

                        #endregion Non Required Tags, final check if no defaults are located.
                    }

                    // Add to dictionary for quicker lookups
                    if (descriptor != null)
                    {
                        _ResolvedTemplates.Add(tuple, descriptor);
                    }
                }
            }

            if (descriptor == null)
            {
                if (_Logger != null)
                {
                    _Logger.Log
                    (
                        "Null template descriptor found, dictionary had following types defined " + string.Join(",", _ResolvedTemplates.Select(x => x.Key.Item1.FullName).ToArray()),
                        typeof(TemplateExtensions).FullName,
                        null,
                        System.Diagnostics.EventLogEntryType.Warning,
                        null
                    );
                }
            }

            // Allows a developer to change the resolved template descriptor.
            if (OnResolved != null)
            {
                descriptor = OnResolved(descriptor, Tags, Item);
            }

            return descriptor;
        }

        /// <summary>
        /// Creates instance for _TemplateDescriptorList if null
        /// </summary>
        /// <param name="regen"></param>
        private static void EnsureTemplateDescriptorList(bool regen = false)
        {
            if (_TemplateDescriptorList == null || regen)
            {
                lock (_Lock)
                {
                    _TemplateDescriptorList = new HashSet<TemplateDescriptorAttribute>();
                }
            }
        }

        /// <summary>
        /// Populates the loaded user control's CurrentData property with Data
        /// </summary>
        /// <param name="control"></param>
        /// <param name="data"></param>
        private static void LoadContentData(Control control, object data)
        {
            IContentControl contentDataControl = control as IContentControl;

            if (contentDataControl == null)
                return;

            contentDataControl.CurrentData = data;
        }
    }
}