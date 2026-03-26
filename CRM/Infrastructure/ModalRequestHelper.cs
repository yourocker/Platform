using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace CRM.Infrastructure;

public static class ModalRequestHelper
{
    public const string ModalParameterName = "modal";

    public static bool IsModalRequest(HttpRequest request)
    {
        if (request == null)
        {
            return false;
        }

        if (IsTruthy(request.Query[ModalParameterName].ToString()))
        {
            return true;
        }

        return request.HasFormContentType && IsTruthy(request.Form[ModalParameterName].ToString());
    }

    public static string EnsureModalFlag(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
        {
            var builder = new UriBuilder(absoluteUri);
            var query = QueryHelpers.ParseQuery(builder.Query);
            var items = query
                .SelectMany(entry => entry.Value, (entry, value) => new KeyValuePair<string, string?>(entry.Key, value))
                .Where(item => !string.Equals(item.Key, ModalParameterName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            items.Add(new KeyValuePair<string, string?>(ModalParameterName, "true"));
            builder.Query = string.Join("&", items.Select(item =>
                $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value ?? string.Empty)}"));

            return builder.Uri.ToString();
        }

        var fragment = string.Empty;
        var fragmentIndex = url.IndexOf('#');
        if (fragmentIndex >= 0)
        {
            fragment = url[fragmentIndex..];
            url = url[..fragmentIndex];
        }

        var path = url ?? string.Empty;
        var queryIndex = path.IndexOf('?');
        var pathPart = queryIndex >= 0 ? path[..queryIndex] : path;
        var queryPart = queryIndex >= 0 ? path[queryIndex..] : string.Empty;
        var localQuery = QueryHelpers.ParseQuery(queryPart);
        var localItems = localQuery
            .SelectMany(entry => entry.Value, (entry, value) => new KeyValuePair<string, string?>(entry.Key, value))
            .Where(item => !string.Equals(item.Key, ModalParameterName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        localItems.Add(new KeyValuePair<string, string?>(ModalParameterName, "true"));

        var rebuiltQuery = string.Join("&", localItems.Select(item =>
            $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value ?? string.Empty)}"));

        return string.IsNullOrEmpty(rebuiltQuery)
            ? pathPart + fragment
            : $"{pathPart}?{rebuiltQuery}{fragment}";
    }

    public static ContentResult BuildEntityCreatedContent(string entityCode, Guid id, string? name)
    {
        return BuildParentMessageContent(new
        {
            type = "crm-entity-created",
            entityCode,
            id,
            name = name ?? string.Empty
        });
    }

    public static ContentResult BuildEntityUpdatedContent(string entityCode, Guid id, string? name)
    {
        return BuildParentMessageContent(new
        {
            type = "crm-entity-updated",
            entityCode,
            id,
            name = name ?? string.Empty
        });
    }

    public static ContentResult BuildRedirectContent(string? url)
    {
        return BuildParentMessageContent(new
        {
            type = "crm-modal-navigate",
            url = EnsureModalFlag(url)
        });
    }

    private static ContentResult BuildParentMessageContent(object payload)
    {
        var payloadJson = JsonSerializer.Serialize(payload);
        var html = $"""
                    <!DOCTYPE html>
                    <html lang="ru">
                    <head>
                        <meta charset="utf-8" />
                        <title>Modal bridge</title>
                    </head>
                    <body>
                        <script>
                            window.parent.postMessage({payloadJson}, window.location.origin);
                        </script>
                    </body>
                    </html>
                    """;

        return new ContentResult
        {
            Content = html,
            ContentType = "text/html; charset=utf-8"
        };
    }

    private static bool IsTruthy(string? value)
    {
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
    }
}
