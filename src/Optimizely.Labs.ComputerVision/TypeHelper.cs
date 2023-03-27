using Episerver.Labs.Cognitive.Attributes;
using EPiServer.Core;
using System.Reflection;

namespace Episerver.Labs.Cognitive
{
    public static class TypeHelper
    {
        public static IEnumerable<PropertyInfo> PropertiesWithVisionAttributes(this Type t)
        {
            return t.GetProperties().Where(prop =>
                Attribute.IsDefined(prop, typeof(SmartThumbnailAttribute), true) ||
                Attribute.IsDefined(prop, typeof(VisionAttribute), true));
        }

        public static IEnumerable<PropertyInfo> GetPropertiesWithAttribute(this IContent c, Type a)
        {
            return c.GetType().GetProperties().Where(prop => Attribute.IsDefined(prop, a, true));
        }

        public static IEnumerable<PropertyInfo> GetEmptyPropertiesWithAttribute(this IContent c, Type a)
        {
            return c.GetType().GetProperties().Where(prop => Attribute.IsDefined(prop, a, true)).Where(prop => prop.GetValue(c) == null);
        }

        public static IEnumerable<SmartThumbnailAttribute> GetThumbnailAttributes(this IContent c)
        {
            return c.GetType().PropertiesWithVisionAttributes().SelectMany(pi => pi.GetCustomAttributes(true)).Where(ca => ca is SmartThumbnailAttribute).Cast<SmartThumbnailAttribute>();
        }

        public static IEnumerable<VisionAttribute> GetVisionAttributes(this IContent c)
        {
            return c.GetType().PropertiesWithVisionAttributes().SelectMany(pi => pi.GetCustomAttributes(true)).Where(ca => ca is VisionAttribute).Cast<VisionAttribute>();
        }
    }
}