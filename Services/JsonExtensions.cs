using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace YouPander.Services
{
    public static class JsonExtensions
    {

        public static string GetStringOrEmpty(this JsonElement el, string property) 
            => el.TryGetProperty(property, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString() ?? string.Empty
            : string.Empty;

    }
}
