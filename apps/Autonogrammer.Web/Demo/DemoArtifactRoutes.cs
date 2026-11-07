using System.Globalization;
using System.Net;
using Autonogrammer.Web.Services.Artifacts;

namespace Autonogrammer.Web.Demo;

/// <summary>
/// The artifact read routes of the Demo handler: the list, one header, and the content of a version. They answer in the shapes of the API.
/// The Demo has no render route, so a render ticket is refused (the static view is shown instead), and a version without stored content
/// answers 404, which the client reports as sync is off.
/// </summary>
internal static class DemoArtifactRoutes
{
    /// <summary>Resolves an artifact read route to the function that answers it, or returns null when the request is not one of them.</summary>
    /// <param name="request">The request.</param>
    /// <param name="path">The unescaped path segments.</param>
    /// <returns>The function that answers the request, or null. The function runs only when the caller invokes it.</returns>
    internal static Func<HttpRequestMessage, HttpResponseMessage>? Resolve(HttpRequestMessage request, string[] path)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(path);
        if (request.Method != HttpMethod.Get)
        {
            return null;
        }

        if (DemoApiHandler.Match(path, "cowork", "artifacts"))
        {
            return req => DemoApiHandler.JsonResponse(req, DemoArtifactFixtures.List(), ArtifactWebJsonContext.Default.ArtifactListRecord);
        }

        if (DemoApiHandler.Match(path, "cowork", "artifacts", null))
        {
            var header = DemoArtifactFixtures.Find(path[2]);
            if (header is null)
            {
                return null;
            }

            return req => DemoApiHandler.JsonResponse(req, header, ArtifactWebJsonContext.Default.ArtifactSummaryRecord);
        }

        if (DemoApiHandler.Match(path, "cowork", "artifacts", null, "versions", null, "content")
            && int.TryParse(path[4], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            var bytes = DemoArtifactFixtures.Content(path[2], number);
            if (bytes is null)
            {
                return null;
            }

            return req =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes), RequestMessage = req };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(DemoArtifactFixtures.ContentMediaType);
                return response;
            };
        }

        return null;
    }
}
