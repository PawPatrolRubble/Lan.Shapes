using System;
using System.Windows.Media;
using Newtonsoft.Json;

namespace Lan.Shapes.Converters
{
    public class BrushToHexConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is Brush brush)
            {
                var hex = brush.ToString();
                writer.WriteValue(hex);
            }
            else
            {
                throw new InvalidOperationException("Expected a brush object.");
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.Value != null)
            {
                BrushConverter converter = new BrushConverter();
                Brush brush = (Brush)converter.ConvertFromString(reader.Value.ToString());
                return brush;
            }

            return null;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Brush);
        }
    }
}
