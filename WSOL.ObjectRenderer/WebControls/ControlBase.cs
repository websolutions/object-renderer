namespace WSOL.ObjectRenderer.WebControls
{
    using WSOL.ObjectRenderer.Interfaces;

    /// <summary>
    /// Base class to define a renderer and Item
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ControlBase<T> : System.Web.UI.UserControl, IContentControl
    {
        public ControlBase() { }

        public T CurrentData { get; set; }

        object IContentControl.CurrentData
        {
            get
            {
                return this.CurrentData;
            }
            set
            {
                this.CurrentData = (T)value;
            }
        }
    }
}