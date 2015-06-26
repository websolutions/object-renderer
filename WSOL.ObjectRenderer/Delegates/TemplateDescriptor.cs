namespace WSOL.ObjectRenderer.Delegates
{
    using System.Collections.Generic;
    using System.Web.UI;
    using System.Web.UI.HtmlControls;
    using WSOL.ObjectRenderer.Attributes;

    public delegate TemplateDescriptorAttribute TemplateResolverHandler(TemplateDescriptorAttribute T, string[] Tags, object Item);

    public delegate Control InsertWrapperHandler(HtmlGenericControl Wrapper, object Item);

    public delegate void NullContentHandler(HtmlTextWriter writer);

    public delegate string DebugContentHandler(IDictionary<object, TemplateDescriptorAttribute> items, string[] Tags);

}