#region License
// The MIT License (MIT)
//
// Copyright (c) 2020 Wanzyee Studio
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
#endregion

using Newtonsoft.Json;
using System;
using System.Globalization;
using UnityEngine;

namespace AAM.UI;

public class ColorConverter : PartialConverter<Color>
{
    protected override void ReadValue(ref Color value, string name, JsonReader reader, JsonSerializer serializer)
    {
        switch (name)
        {
            case nameof(value.r):
                value.r = reader.ReadAsFloat() ?? 0f;
                break;
            case nameof(value.g):
                value.g = reader.ReadAsFloat() ?? 0f;
                break;
            case nameof(value.b):
                value.b = reader.ReadAsFloat() ?? 0f;
                break;
            case nameof(value.a):
                value.a = reader.ReadAsFloat() ?? 0f;
                break;
        }
    }

    protected override void WriteJsonProperties(JsonWriter writer, Color value, JsonSerializer serializer)
    {
        writer.WritePropertyName(nameof(value.r));
        writer.WriteValue(value.r);
        writer.WritePropertyName(nameof(value.g));
        writer.WriteValue(value.g);
        writer.WritePropertyName(nameof(value.b));
        writer.WriteValue(value.b);
        writer.WritePropertyName(nameof(value.a));
        writer.WriteValue(value.a);
    }
}

/// <summary>
/// Custom base <c>Newtonsoft.Json.JsonConverter</c> to filter serialized properties.
/// </summary>
public abstract class PartialConverter<T> : JsonConverter
    where T : new()
{
    protected abstract void ReadValue(ref T value, string name, JsonReader reader, JsonSerializer serializer);

    protected abstract void WriteJsonProperties(JsonWriter writer, T value, JsonSerializer serializer);

    /// <summary>
    /// Determine if the object type is <typeparamref name="T"/>
    /// </summary>
    /// <param name="objectType">Type of the object.</param>
    /// <returns><c>true</c> if this can convert the specified type; otherwise, <c>false</c>.</returns>
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(T)
               || (objectType.IsGenericType
                   && objectType.GetGenericTypeDefinition() == typeof(Nullable<>)
                   && objectType.GenericTypeArguments[0] == typeof(T));
    }

    /// <summary>
    /// Read the specified properties to the object.
    /// </summary>
    /// <returns>The object value.</returns>
    /// <param name="reader">The <c>Newtonsoft.Json.JsonReader</c> to read from.</param>
    /// <param name="objectType">Type of the object.</param>
    /// <param name="existingValue">The existing value of object being read.</param>
    /// <param name="serializer">The calling serializer.</param>
    public override object ReadJson(
        JsonReader reader,
        Type objectType,
        object existingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            bool isNullableStruct = objectType.IsGenericType
                                    && objectType.GetGenericTypeDefinition() == typeof(Nullable<>);

            return isNullableStruct ? null : (object)default(T);
        }

        return InternalReadJson(reader, serializer, existingValue);
    }

    private T InternalReadJson(JsonReader reader, JsonSerializer serializer, object existingValue)
    {
        if (reader.TokenType != JsonToken.StartObject)
        {
            throw new Exception($"Failed to read type '{typeof(T).Name}'. Expected object start, got '{reader.TokenType}' <{reader.Value}>");
        }

        reader.Read();

        if (!(existingValue is T value))
        {
            value = new T();
        }

        string previousName = null;

        while (reader.TokenType == JsonToken.PropertyName)
        {
            if (reader.Value is string name)
            {
                if (name == previousName)
                {
                    throw new Exception($"Failed to read type '{typeof(T).Name}'. Possible loop when reading property '{name}'");
                }

                previousName = name;
                ReadValue(ref value, name, reader, serializer);
            }
            else
            {
                reader.Skip();
            }

            reader.Read();
        }

        return value;
    }

    /// <summary>
    /// Write the specified properties of the object.
    /// </summary>
    /// <param name="writer">The <c>Newtonsoft.Json.JsonWriter</c> to write to.</param>
    /// <param name="value">The value.</param>
    /// <param name="serializer">The calling serializer.</param>
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();

        var typed = (T)value;
        WriteJsonProperties(writer, typed, serializer);

        writer.WriteEndObject();
    }
}

internal static class JsonHelperExtensions
{
    public static float? ReadAsFloat(this JsonReader reader)
    {
        // https://github.com/jilleJr/Newtonsoft.Json-for-Unity.Converters/issues/46

        var str = reader.ReadAsString();

        if (string.IsNullOrEmpty(str))
        {
            return null;
        }
        else if (float.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var valueParsed))
        {
            return valueParsed;
        }
        else
        {
            return 0f;
        }
    }
}