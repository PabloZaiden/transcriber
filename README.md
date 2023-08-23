# Transcriber

Use the OpenAI Whisper model to transcribe audio files using dotnet.

## Usage

```bash
export OPENAI_API_KEY=<your key>

# To transcribe a single file
dotnet run "path/to/m4a/audio/file.m4a"

# To run interactively, caputuring audio from the default audio input
INTERACTIVE=1 dotnet run
```
