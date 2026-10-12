using System.Text.Json;
using System.Text.Json.Serialization;
using Game.LocalHost;

var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
var session = new Session();
using var timer = new Timer(_ => session.Tick(), null, 100, 100);
// stdout is exclusively one response JSON per request. Diagnostics go to stderr.
while (Console.ReadLine() is { } line)
{
    try
    {
        if (line.Length > 65_536) { Console.WriteLine(JsonSerializer.Serialize(new Response("", false, "Request exceeds 64 KiB.", 0), options)); continue; }
        var request = JsonSerializer.Deserialize<Request>(line, options);
        var response = request == null ? new Response("", false, "Request is null.", 0) : session.Handle(request);
        Console.WriteLine(JsonSerializer.Serialize(response, options));
    }
    catch (JsonException)
    {
        Console.WriteLine(JsonSerializer.Serialize(new Response("", false, "Invalid request JSON.", 0), options));
    }
}
