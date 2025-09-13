using System.Text.Json;
using System.Text.Json.Nodes;

namespace BrineBlade.Infrastructure.Content;

public static class JsonSchemaValidator
{
    // Minimal JSON Schema evaluator supporting: type, properties, required, additionalProperties, enum, array.items
    public static IReadOnlyList<string> Validate(string json, string schemaJson, string? fileLabel = null)
    {
        var errors = new List<string>();
        var doc = JsonNode.Parse(json);
        var schema = JsonNode.Parse(schemaJson);
        if (doc is null || schema is null) { errors.Add("Invalid JSON or schema."); return errors; }
        ValidateNode(doc, schema, errors, fileLabel ?? "<memory>", "$");
        return errors;
    }

    private static void ValidateNode(JsonNode node, JsonNode schema, List<string> errors, string file, string path)
    {
        var type = schema?["type"]?.GetValue<string>();
        if (type != null)
        {
            if (!TypeMatches(node, type))
                errors.Add($"{file}:{path} expected type '{type}'.");
        }

        if (schema?["enum"] is JsonArray enumArr)
        {
            var allowed = enumArr.Select(e => e!.GetValue<string>()).ToHashSet();
            var val = (node as JsonValue)?.GetValue<string>();
            if (val != null && !allowed.Contains(val))
                errors.Add($"{file}:{path} value '{val}' not in enum.");
        }

        if (type == "object" && node is JsonObject obj)
        {
            // required
            if (schema?["required"] is JsonArray req)
            {
                foreach (var r in req)
                {
                    var key = r!.GetValue<string>();
                    if (!obj.ContainsKey(key))
                        errors.Add($"{file}:{path} missing required property '{key}'.");
                }
            }
            // properties
            var props = schema?["properties"] as JsonObject;
            if (props != null)
            {
                foreach (var kv in props)
                {
                    if (obj.TryGetPropertyValue(kv.Key, out var child) && child is not null)
                    {
                        ValidateNode(child, kv.Value!, errors, file, path + "." + kv.Key);
                    }
                }
            }
            // additionalProperties
            var addProps = schema?["additionalProperties"];
            if (addProps is JsonValue addVal && addVal.TryGetValue<bool>(out var allow) && !allow && props != null)
            {
                foreach (var key in obj.Select(p => p.Key))
                {
                    if (!props.ContainsKey(key))
                        errors.Add($"{file}:{path} unknown property '{key}'.");
                }
            }
            else if (addProps is JsonObject addSchemaObj)
            {
                foreach (var kv in obj)
                    ValidateNode(kv.Value!, addSchemaObj, errors, file, path + "." + kv.Key);
            }
        }
        else if (type == "array" && node is JsonArray arr)
        {
            var items = schema?["items"];
            if (items != null)
            {
                for (int i = 0; i < arr.Count; i++)
                    ValidateNode(arr[i]!, items, errors, file, path + $"[{i}]");
            }
        }
        else
        {
            // primitives: integer, number, string, boolean handled by TypeMatches
        }
    }

    private static bool TypeMatches(JsonNode node, string type)
    {
        return type switch
        {
            "object" => node is JsonObject,
            "array" => node is JsonArray,
            "string" => node is JsonValue v && v.TryGetValue<string>(out _),
            "integer" => node is JsonValue vi && vi.TryGetValue<int>(out _),
            "number" => node is JsonValue vn && (vn.TryGetValue<double>(out _) || vn.TryGetValue<int>(out _)),
            "boolean" => node is JsonValue vb && vb.TryGetValue<bool>(out _),
            _ => true
        };
    }
}