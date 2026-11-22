| Variant | Default: brotli MiB | Default: raw MiB | Trim: brotli MiB | Trim: raw MiB |
|---|---|---|---|---|
| None | 1.47 | 4.62 | 1.47 | 4.61 |
| Own | 1.49 | 4.67 | 1.48 | 4.64 |
| Resampler | 22.48 | 71.00 | 11.88 | 18.16 |
| Wav | 22.48 | 71.00 | 11.88 | 18.16 |
| Fft | 22.48 | 71.01 | 11.88 | 18.16 |
| Other | 22.48 | 71.00 | 11.88 | 18.16 |
| All | 22.49 | 71.01 | 11.89 | 18.19 |

All-Default: files with HartsyInference/Tokenizers/Protobuf/dotnet.native in the name (brotli MiB)
  Google.Protobuf.wasm.br: 0.093
  HartsyInference.Audio.wasm.br: 11.326
  HartsyInference.Core.wasm.br: 0.329
  HartsyInference.LLM.wasm.br: 0.605
  HartsyInference.ModelAssets.wasm.br: 8.125
  Microsoft.ML.Tokenizers.wasm.br: 0.097
  O9.Wasm.wasm.br: 0.016
  dotnet.native.js.br: 0.041
  dotnet.native.wasm.br: 0.457
All-Trim: files with HartsyInference/Tokenizers/Protobuf/dotnet.native in the name (brotli MiB)
  HartsyInference.Audio.wasm.br: 10.406
  O9.Wasm.wasm.br: 0.016
  dotnet.native.js.br: 0.041
  dotnet.native.wasm.br: 0.446

| Variant | AOT: brotli MiB | AOT: raw MiB |
|---|---|---|
| Own | 3.22 | 14.23 |
| All | 13.66 | 27.93 |
Own AOT warning/error codes: ['IL2065', 'IL2072', 'IL2111'] 6
All AOT warning/error codes: ['IL2065', 'IL2072', 'IL2111'] 6
