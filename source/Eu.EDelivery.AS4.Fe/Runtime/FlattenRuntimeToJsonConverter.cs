using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Eu.EDelivery.AS4.Fe.Runtime;

public class FlattenRuntimeToJsonConverter : JsonConverter
{
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        var mainObj = new JObject();
        if (value is IEnumerable<ItemType> list)
        {
            foreach (var itemType in list)
            {
                WriteItem(itemType, mainObj);
            }
        }
        else if (value is ItemType itemType)
        {
            WriteItem(itemType, mainObj);
        }

        mainObj.WriteTo(writer);
    }

    private static void WriteItem(ItemType itemType, JObject rootJson)
    {
        // Add all properties to the current JObject
        foreach (var prop in itemType.Properties)
        {
            AddChild(prop, rootJson);
        }
    }

    private static void AddChild(Property property, JObject root)
    {
        AddProperty(property, root);
        //root.Add(new JProperty(property.Path, PropertyToJobject(property.Description, property)));
        if (property.Properties == null) return;
        foreach (var childProp in property.Properties)
        {
            AddChild(childProp, root);
        }
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }

    public override bool CanConvert(Type objectType)
    {
        return true;
    }

    private static void AddProperty(Property property, JObject root)
    {
        if (string.IsNullOrEmpty(property.Description) && property.DefaultValue == null) return;
        root.Add(new JProperty(property.Path, new JObject(
            new JProperty("description", property.Description),
            new JProperty("defaultvalue", property.DefaultValue)
        )));
    }

    private static JObject PropertyToJobject(string description, Property property)
    {
        return new JObject(
            new JProperty("description", description),
            new JProperty("defaultvalue", property.DefaultValue)
        );
    }
}
