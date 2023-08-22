using System.Net.Http.Headers;
using System.Text.Json;

var transcriber = new Transcriber();

string audioFilePath;
if (args.Length > 0)
{
    audioFilePath = args[0];
}
else
{
    System.Console.Write("File to transcribe: ");
    audioFilePath = System.Console.ReadLine()!;
}

if (!File.Exists(audioFilePath))
{
    throw new FileNotFoundException("File not found", audioFilePath);
}

System.Console.WriteLine("Transcribing...");
var str = await transcriber.Transcribe(audioFilePath);
System.Console.WriteLine();
System.Console.WriteLine("Transcription: " + str);

class Transcriber
{
    string _apiKey;
    private const string TranscriptionUrl = "https://api.openai.com/v1/audio/transcriptions";
    private const string ModelName = "whisper-1";

    public Transcriber() : this(Environment.GetEnvironmentVariable("OPENAI_API_KEY")!)
    {
    }

    public Transcriber(string apiKey)
    {
        if (String.IsNullOrWhiteSpace(apiKey))
        {
            Environment.FailFast("OPENAI_API_KEY is not set");
        }

        _apiKey = apiKey;
    }

    public async Task<string> Transcribe(string audioFilePath)
    {
        using (var httpClient = new HttpClient())
        {
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);

            var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(File.ReadAllBytes(audioFilePath));
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/m4a");
            content.Add(fileContent, "file", Path.GetFileName(audioFilePath));
            content.Add(new StringContent(ModelName), "model");

            var response = await httpClient.PostAsync(
                TranscriptionUrl,
                content);

            var jsonText = await response.Content.ReadAsStringAsync();
            var transcriptionResponse = JsonSerializer.Deserialize<TrascriptionResponse>(jsonText)!;

            return transcriptionResponse.text;
        }
    }

    record TrascriptionResponse(string text);

}