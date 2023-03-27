namespace Episerver.Labs.Cognitive.Attributes
{
    public class VisionAttribute : Attribute
    {
        public VisionTypes VisionType { get; set; }

        public string Separator { get; set; }

        public VisionAttribute()
        {
            this.VisionType = VisionTypes.Description;
            this.Separator = ",";
        }

        public VisionAttribute(VisionTypes TypeOfVision, string Separator = ",")
        {
            this.VisionType = TypeOfVision;
            this.Separator = Separator;
        }
    }
}