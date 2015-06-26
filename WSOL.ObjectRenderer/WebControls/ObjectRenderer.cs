namespace WSOL.ObjectRenderer.WebControls
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Web.UI;
    using System.Web.UI.HtmlControls;
    using System.Web.UI.WebControls;
    using WSOL.IocContainer;
    using WSOL.ObjectRenderer.Attributes;
    using WSOL.ObjectRenderer.Delegates;
    using WSOL.ObjectRenderer.Enums;
    using WSOL.ObjectRenderer.Extensions;
    using WSOL.ObjectRenderer.Interfaces;

    /// <summary>
    /// Renders an Item object or ItemList of objects with registered templates
    /// </summary>
    [DefaultProperty("Item"), ToolboxData("<{0}:ObjectRenderer runat=server></{0}:ObjectRenderer>")]
    public class ObjectRenderer : WebControl, INamingContainer
    {
        private static IApplicationHelper _ApplicationHelper = InitializationContext.Locator.Get<IApplicationHelper>();
        private static ICacheManager _CacheManager = InitializationContext.Locator.Get<ICacheManager>();
        private static ILogger _Logger = InitializationContext.Locator.Get<ILogger>();
        private static IXmlSerializer _XmlSerializer = InitializationContext.Locator.Get<IXmlSerializer>();
        private static IXsltTransformer _XsltTransformer = InitializationContext.Locator.Get<IXsltTransformer>();

        #region Constructor

        public ObjectRenderer()
        {
            CacheInterval = _CacheManager.QuickInterval;
            ShowXsltErrors = false;
            Item = null;
            ItemList = null;
            Tags = null;
        }

        #endregion Constructor

        #region Properties

        /// <summary>
        /// Tracks when BuildRenderer has executed.
        /// </summary>
        private bool _Executed = false;

        /// <summary>
        /// Tracks how many items where rendered
        /// </summary>
        private int _RenderedCount = 0;

        /// <summary>
        /// Executes if DebugMode is true
        /// </summary>
        public event DebugContentHandler DebugWrite;

        /// <summary>
        /// Executes when an item wrapper is specified and added to the renderer
        /// </summary>
        public event InsertWrapperHandler InsertItemWrapper;

        /// <summary>
        /// Executes when the Item or Items is null
        /// </summary>
        public event NullContentHandler NullContent;

        /// <summary>
        /// Executes after the template has been looked up.
        /// </summary>
        public event TemplateResolverHandler TemplateResolved;

        /// <summary>
        /// Used for XSLT transform only
        /// </summary>
        public int CacheInterval { get; set; }

        /// <summary>
        /// If true, executes DebugContentHandler to write out debug information
        /// </summary>
        public bool DebugMode { get; set; }

        /// <summary>
        /// Renders a single items, ignored if Items is set;
        /// </summary>
        public object Item { get; set; }

        /// <summary>
        /// Renders a list of items
        /// </summary>
        public IEnumerable<object> ItemList { get; set; }

        /// <summary>
        /// Wraps each item in the given tag.
        /// </summary>
        public string ItemWrapTag { get; set; }

        /// <summary>
        /// If true, any view selected that doesn't contain a tag set on the renderer does not get displayed.
        /// </summary>
        public bool RequireTags { get; set; }

        /// <summary>
        /// Used for XSLT transforms only
        /// </summary>
        public bool ShowXsltErrors { get; set; }

        /// <summary>
        /// Array of tags to use for this renderer
        /// </summary>
        public string[] Tags { get; set; }

        /// <summary>
        /// Comma delimited string of tags to use for this renderer, overrides Tags[] if used
        /// </summary>
        public string TagsString { get; set; }

        /// <summary>
        /// Wrapper tag for rendered items.
        /// </summary>
        public string WrapTag { get; set; }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Creates a PRE HTML tag of Item Ids, Names, XmlConfigIDs for IContent and TemplateType, Template Path for all types
        /// </summary>
        /// <param name="items"></param>
        /// <param name="Tags"></param>
        /// <returns></returns>
        public string Default_DebugWrite(IDictionary<object, TemplateDescriptorAttribute> items, string[] Tags)
        {
            StringBuilder sb = new StringBuilder(200);

            if (Tags != null && Tags.Any())
            {
                sb.AppendLine(string.Format("Tags: {0}", string.Join(",", Tags)));
            }
            else
            {
                sb.AppendLine("ObjectRenderer Tags: Null or Empty!");
            }

            sb.AppendLine("Tags Required: " + (RequireTags ? "Yes" : "No"));

            if (items == null || !items.Any())
            {
                sb.AppendLine("Items: Null or Empty!");
            }
            else
            {
                sb.AppendLine("Item Count: " + items.Count);
                sb.AppendLine("Null Item Count: " + (ItemList != null ? ItemList.Where(x => x == null).Count() : 0));
                sb.AppendLine(_RenderedCount + " items rendered of " + items.Count);
                sb.AppendLine("If rendered count is lower than item count, then check if tags are required, the items is not null, and if the item implements IRendererItemDisplay it returns as expected.");

                foreach (var item in items)
                {
                    IRendererDebugString debugInfo = item.Key as IRendererDebugString;
                    string variantInfo = debugInfo != null ? debugInfo.DebugText : string.Empty;

                    // has a template
                    if (item.Value != null)
                    {
                        sb.AppendLine(string.Format("{0}Item Type: {1}, Default Template: {2}, Tags Required: {3}, Inherited Template: {4}, Template Path: {5}",
                            variantInfo,
                            item.Key.GetType().FullName,
                            item.Value.Default,
                            item.Value.RequireTags,
                            item.Value.Inherited,
                            item.Value.Path
                        ));
                    }
                    else
                    {
                        sb.AppendLine(string.Format("{0}Item Type: {1}, No template!",
                           variantInfo,
                           item.Key.GetType().FullName
                        ));
                    }
                }
            }

            return string.Format("<pre>ObjectRenderer Debug Information\r\n{0}</pre>", sb.ToString());
        }

        /// <summary>
        /// Writes custom HTML tag if WrapTag is set
        /// </summary>
        /// <param name="writer"></param>
        public override void RenderBeginTag(HtmlTextWriter writer)
        {
            if (!string.IsNullOrEmpty(WrapTag) && _RenderedCount > 0)
            {
                writer.WriteBeginTag(WrapTag);

                if (!string.IsNullOrEmpty(CssClass))
                {
                    writer.WriteAttribute("class", CssClass);
                }

                if (Attributes != null && Attributes.Count > 0)
                {
                    foreach (string key in Attributes.Keys)
                    {
                        writer.WriteAttribute(key, Attributes[key]);
                    }
                }

                writer.Write(HtmlTextWriter.TagRightChar);
            }
        }

        /// <summary>
        /// Writes custom HTML tag if WrapTag is set
        /// </summary>
        /// <param name="writer"></param>
        public override void RenderEndTag(HtmlTextWriter writer)
        {
            if (!string.IsNullOrEmpty(WrapTag) && _RenderedCount > 0)
            {
                writer.WriteEndTag(WrapTag);
            }
        }

        protected override void OnDataBinding(EventArgs e)
        {
            base.OnDataBinding(e);

            BuildRenderer();
        }

        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            BuildRenderer();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            BuildRenderer();
        }

        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            BuildRenderer();
        }

        protected override void Render(HtmlTextWriter writer)
        {
            // handle empty rendering
            if ((ItemList == null || _RenderedCount == 0) && NullContent != null)
            {
                NullContent(writer);

                return;
            }

            base.Render(writer);
        }

        public override string ToString()
        {
            StringBuilder sBuilder = new StringBuilder(200);

            using (StringWriter sWriter = new StringWriter(sBuilder))
            {
                using (HtmlTextWriter hWriter = new HtmlTextWriter(sWriter))
                {
                    this.RenderControl(hWriter);
                }
            }            

            return sBuilder.ToString();
        }

        /// <summary>
        /// Checked during several events of the page life cycle, Ideally should be one OnLoad or OnInit if form elements are used.
        /// </summary>
        private void BuildRenderer()
        {
            if (_Executed)
            {
                return;
            }

            // Create a list of 1
            if ((ItemList == null || !ItemList.Any()) && Item != null)
            {
                ItemList = new List<object>() { Item };
            }

            // Stop processing if no items are found.
            if (ItemList == null)
            {
                return;
            }

            // stores item for debug write
            Dictionary<object, TemplateDescriptorAttribute> debugItems = null;

            if (ItemList.Any())
            {
                // Keeps from executing several times in page life cycle
                _Executed = true;

                if (DebugMode)
                    debugItems = new Dictionary<object, TemplateDescriptorAttribute>();

                // Control to be loaded
                Control c = null;

                if (!String.IsNullOrEmpty(TagsString))
                    Tags = TagsString.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

                if (Tags == null)
                    Tags = new string[] { };

                // Render each item that isn't null
                foreach (object item in ItemList.Where(x => x != null))
                {
                    HtmlGenericControl wrapper = null;
                    Type contentType = item.GetType();
                    IRendererItemDisplay displayableItem = item as IRendererItemDisplay;

                    if (!string.IsNullOrEmpty(ItemWrapTag))
                    {
                        wrapper = new HtmlGenericControl() { TagName = ItemWrapTag };
                    }

                    TemplateDescriptorAttribute t = contentType.ResolveTemplate(TemplateResolved, Tags, item);

                    if (DebugMode)
                    {
                        debugItems.Add(item, t);
                    }

                    // If no template or not displayable determined by IRendererItemDisplay
                    if (t == null ||
                        string.IsNullOrEmpty(t.Path) ||
                        (displayableItem != null && !displayableItem.DisplayItem(t, Tags)))
                    {
                        continue;
                    }

                    // If template doesn't meet tag requirement and renderer or template requires tags, then skip
                    if (RequireTags || t.RequireTags)
                    {
                        if (Tags == null || Tags.Length == 0)
                            continue; // requires tags, but none given
                        else if (t.Tags != null && !t.Tags.Intersect(Tags, StringComparer.InvariantCultureIgnoreCase).Any())
                            continue; // tags were given but nothing matched
                    }

                    // TODO: Opportunity to add Razor renderings
                    switch (t.TemplateType)
                    {
                        //case TemplateType.Razor:
                        //    RazorTemplates.Core.ITemplate<IContent> template = RazorTemplates.Core.Template.Compile<IContent>(_ApplicationHelper.GetFileAsString(t.Path));
                        //    c = new LiteralControl(template.Render(item));

                        //    break;

                        case TemplateType.XSLT:
                            ICacheKey cacheableItem = item as ICacheKey;
                            string xml = _XmlSerializer.Serialize(data: Item, additionalTypes: t.AdditionalTypes);

                            c = new LiteralControl(
                                _XsltTransformer.Transform(
                                    xml: xml,
                                    xslt: t.Path,
                                    cacheKey:
                                        string.Format("WSOL:Cache:ObjectRenderer:Type={0},Tags={1},Path={2},Variant={4}", item.GetType().FullName, string.Join(",", Tags), t.Path, cacheableItem != null ? cacheableItem.CacheKey : string.Empty),
                                    cacheInterval: (cacheableItem != null) ? CacheInterval : 0,
                                    returnExceptionMessage: ShowXsltErrors
                                )
                            );

                            break;

                        case TemplateType.UserControl:
                        default:
                            c = t.Path.Load(item);

                            break;
                    }

                    // Add control to renderer
                    if (c != null)
                    {
                        bool controlAdded = false;

                        if (wrapper != null)
                        {
                            wrapper.Controls.Add(c);
                            Control ctl = wrapper;

                            if (InsertItemWrapper != null)
                            {
                                ctl = InsertItemWrapper(wrapper, item);
                            }

                            if (ctl != null)
                            {
                                this.Controls.Add(ctl);
                                controlAdded = true;
                            }
                        }

                        if (!controlAdded)
                        {
                            this.Controls.Add(c);
                        }

                        _RenderedCount++;
                    }
                }
            }

            if (DebugMode)
            {
                string output = string.Empty;

                if (DebugWrite == null)
                    DebugWrite += Default_DebugWrite;

                output += DebugWrite(debugItems, Tags);

                this.Controls.Add(new LiteralControl(output));
            }
        }

        #endregion Methods
    }
}