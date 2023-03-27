namespace Episerver.Labs.Cognitive.Attributes
{
    public class SmartThumbnailAttribute : Attribute
    {
        public int Width { get; set; }

        public int Height { get; set; }

        public SmartThumbnailAttribute()
        {
            Width = 100;
            Height = 100;
        }

        public SmartThumbnailAttribute(int Width, int Height)
        {
            this.Width = Width;
            this.Height = Height;
        }
    }
}