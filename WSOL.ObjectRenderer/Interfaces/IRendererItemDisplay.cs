namespace WSOL.ObjectRenderer.Interfaces
{
    using WSOL.ObjectRenderer.Attributes;

    public interface IRendererItemDisplay
    {
        bool DisplayItem(TemplateDescriptorAttribute templateDescriptor, string[] Tags);
    }
}
