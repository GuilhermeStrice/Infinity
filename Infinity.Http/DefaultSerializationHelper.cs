using System.Collections.Specialized;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infinity.Http
{
    /// <summary>
    /// Default serialization helper.
    /// </summary>
    public class DefaultSerializationHelper : ISerializationHelper
    {
        /// <summary>
        /// Deserialize JSON to an instance.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="json">JSON string.</param>
        /// <returns>Instance.</returns>
        public T DeserializeJson<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json);
        }

        /// <summary>
        /// Serialize object to JSON.
        /// </summary>
        /// <param name="_obj">Object.</param>
        /// <param name="_pretty">Pretty print.</param>
        /// <returns>JSON.</returns>
        public string SerializeJson(object _obj, bool _pretty = true)
        {
            if (_obj == null)
            {
                return null;
            }

            JsonSerializerOptions options = new JsonSerializerOptions();
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

            // see https://github.com/dotnet/runtime/issues/43026
            options.Converters.Add(new ExceptionConverter<Exception>());
            options.Converters.Add(new NameValueCollectionConverter());
            options.Converters.Add(new JsonStringEnumConverter());
            options.Converters.Add(new DateTimeConverter());
            options.Converters.Add(new IntPtrConverter());
            options.Converters.Add(new IPAddressConverter());

            if (!_pretty)
            {
                options.WriteIndented = false;
            }
            else
            {
                options.WriteIndented = true;
            }

            return JsonSerializer.Serialize(_obj, options);
        }

        private class ExceptionConverter<TExceptionType> : JsonConverter<TExceptionType>
        {
            public override bool CanConvert(Type _typeToConvert)
            {
                return typeof(Exception).IsAssignableFrom(_typeToConvert);
            }

            public override TExceptionType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotSupportedException("Deserializing exceptions is not allowed");
            }

            public override void Write(Utf8JsonWriter writer, TExceptionType value, JsonSerializerOptions options)
            {
                var serializable_properties = value.GetType()
                    .GetProperties()
                    .Select(uu => new { uu.Name, Value = uu.GetValue(value) })
                    .Where(uu => uu.Name != nameof(Exception.TargetSite));

                if (options.DefaultIgnoreCondition == JsonIgnoreCondition.WhenWritingNull)
                {
                    serializable_properties = serializable_properties.Where(uu => uu.Value != null);
                }

                var prop_list = serializable_properties.ToList();

                if (prop_list.Count == 0)
                {
                    // Nothing to write
                    return;
                }

                writer.WriteStartObject();

                foreach (var prop in prop_list)
                {
                    writer.WritePropertyName(prop.Name);
                    JsonSerializer.Serialize(writer, prop.Value, options);
                }

                writer.WriteEndObject();
            }
        }

        private class NameValueCollectionConverter : JsonConverter<NameValueCollection>
        {
            public override NameValueCollection Read(ref Utf8JsonReader _reader, Type _type_to_convert, JsonSerializerOptions _options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter _writer, NameValueCollection _value, JsonSerializerOptions _options)
            {
                var val = _value.Keys.Cast<string>()
                    .ToDictionary(k => k, k => string.Join(", ", _value.GetValues(k)));
                JsonSerializer.Serialize(_writer, val);
            }
        }

        private class DateTimeConverter : JsonConverter<DateTime>
        {
            public override DateTime Read(ref Utf8JsonReader _reader, Type _type_to_convert, JsonSerializerOptions _options)
            {
                string str = _reader.GetString();

                DateTime val;
                if (DateTime.TryParse(str, out val))
                {
                    return val;
                }

                throw new FormatException("The JSON value '" + str + "' could not be converted to System.DateTime.");
            }

            public override void Write(Utf8JsonWriter _writer, DateTime _date_time_value, JsonSerializerOptions _options)
            {
                _writer.WriteStringValue(_date_time_value.ToString(
                    "yyyy-MM-ddTHH:mm:ss.ffffffZ", CultureInfo.InvariantCulture));
            }

            private List<string> accepted_formats = new List<string>
            {
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-ddTHH:mm:ssK",
                "yyyy-MM-dd HH:mm:ss.ffffff",
                "yyyy-MM-ddTHH:mm:ss.ffffff",
                "yyyy-MM-ddTHH:mm:ss.fffffffK",
                "yyyy-MM-dd",
                "MM/dd/yyyy HH:mm",
                "MM/dd/yyyy hh:mm tt",
                "MM/dd/yyyy H:mm",
                "MM/dd/yyyy h:mm tt",
                "MM/dd/yyyy HH:mm:ss"
            };
        }

        private class IntPtrConverter : JsonConverter<IntPtr>
        {
            public override IntPtr Read(ref Utf8JsonReader _reader, Type _type_to_convert, JsonSerializerOptions _options)
            {
                throw new FormatException("IntPtr cannot be deserialized.");
            }

            public override void Write(Utf8JsonWriter _writer, IntPtr _int_ptr_value, JsonSerializerOptions _options)
            {
                _writer.WriteStringValue(_int_ptr_value.ToString());
            }
        }

        private class IPAddressConverter : JsonConverter<IPAddress>
        {
            public override IPAddress Read(ref Utf8JsonReader _reader, Type _type_to_convert, JsonSerializerOptions _options)
            {
                string str = _reader.GetString();
                return IPAddress.Parse(str);
            }

            public override void Write(Utf8JsonWriter _writer, IPAddress _value, JsonSerializerOptions _options)
            {
                _writer.WriteStringValue(_value.ToString());
            }
        }
    }
}