using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Pv;

var transcriber = new Transcriber();
var interactive = !String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("INTERACTIVE"));

if (interactive)
{
    System.Console.WriteLine("Interactive mode");
    System.Console.WriteLine("Type \"exit\" to end the program.");
    System.Console.WriteLine();
    System.Console.WriteLine("Start speaking now!");

    while (true)
    {
        var outputWavPath = Path.GetTempFileName() + ".wav";

        var wavFile = new WavFile(outputWavPath);

        var _ = wavFile.StartRecording();

        var input = Console.ReadLine();

        if (input == "exit")
        {
            return;
        }

        wavFile.StopRecording();

        System.Console.WriteLine("Transcribing...");
        var str = await transcriber.Transcribe(outputWavPath);
        System.Console.WriteLine();
        System.Console.WriteLine("Transcription: " + str);

        File.Delete(outputWavPath);
    }
}
else
{
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
}

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
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
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

class WavFile
{
    string _path;
    PvRecorder _pvRecorder;
    BinaryWriter _outputFileWriter;
    int _totalSamplesWritten;
    bool _open = false;

    object _lock = new object();

    public WavFile(string path)
    {
        _path = path;
        _pvRecorder = PvRecorder.Create(frameLength: 512);
        _outputFileWriter = new BinaryWriter(new FileStream(_path, FileMode.OpenOrCreate, FileAccess.Write));
        WriteHeader(1, 16);
    }

    public async Task StartRecording()
    {
        if (_pvRecorder.IsRecording) return;

        _open = true;
        _pvRecorder.Start();
        System.Console.WriteLine("Recording...");

        while (_pvRecorder.IsRecording)
        {
            lock (_lock)
            {
                if (_open)
                {
                    short[] frame = _pvRecorder.Read();
                    
                    foreach (short sample in frame)
                    {
                        _outputFileWriter.Write(sample);
                    }
                    _totalSamplesWritten += frame.Length;
                }
            }

            await Task.Yield();
        }

    }

    public void StopRecording()
    {
        if (_pvRecorder.IsRecording)
        {
            lock (_lock)
            {
                _pvRecorder.Stop();
                _pvRecorder.Dispose();

                //WriteHeader(_outputFileWriter, 1, 16);
                _outputFileWriter.Flush();
                _outputFileWriter.Dispose();
                _open = false;
            }
        }
    }

    private void WriteHeader(ushort channelCount, ushort bitDepth)
    {
        lock (_lock)
        {
            _outputFileWriter.Seek(0, SeekOrigin.Begin);
            _outputFileWriter.Write(Encoding.ASCII.GetBytes("RIFF"));
            _outputFileWriter.Write((bitDepth / 8 * _totalSamplesWritten) + 36);
            _outputFileWriter.Write(Encoding.ASCII.GetBytes("WAVE"));
            _outputFileWriter.Write(Encoding.ASCII.GetBytes("fmt "));
            _outputFileWriter.Write(16);
            _outputFileWriter.Write((ushort)1);
            _outputFileWriter.Write(channelCount);
            _outputFileWriter.Write(_pvRecorder.SampleRate);
            _outputFileWriter.Write(_pvRecorder.SampleRate * channelCount * bitDepth / 8);
            _outputFileWriter.Write((ushort)(channelCount * bitDepth / 8));
            _outputFileWriter.Write(bitDepth);
            _outputFileWriter.Write(Encoding.ASCII.GetBytes("data"));
            _outputFileWriter.Write(bitDepth / 8 * _totalSamplesWritten);
        }
    }
}