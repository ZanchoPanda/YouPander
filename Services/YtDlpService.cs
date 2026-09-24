using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using YouPander.Models;
using YouPander.Resources.Localization;
using YouPander.ViewModels;

namespace YouPander.Services
{
    [SupportedOSPlatform("windows")]
    public class YtDlpService : BaseViewModel
    {
        #region Params

        public string InstalledVersion => Preferences.Get("ytdlp_version", "Desconocida");

        private readonly string _path;
        private readonly string _ffmpegPath;

        private Process? _currentProcess;
        public Process? currentProcess
        {
            get => _currentProcess;
            private set // <- private: solo se asigna internamente
            {
                if (value != _currentProcess)
                {
                    _currentProcess = value;
                    OnPropertyChanged(nameof(currentProcess));
                }
            }
        }

        #endregion

        public YtDlpService(string path)
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("YtDlpService solo es compatible en Windows.");

            _path = path;
            _ffmpegPath = Path.Combine(Path.GetDirectoryName(path)!, "ffmpeg.exe");
        }

        #region yt-dlp

        /// <summary>
        /// Descarga yt-dlp.exe si no existe todavía.
        /// </summary>
        public async Task EnsureInstalledAsync()
        {
            #region V1
            if (File.Exists(_path))
            {
                return;
            }

            string? dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Obtener la última versión disponible
            using HttpClient client = new();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("YouPander/1.0");

            var json = await client.GetStringAsync(ReleasesApi);
            using var doc = JsonDocument.Parse(json);
            var latestTag = doc.RootElement.GetProperty("tag_name").GetString();

            string url = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";

            using HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using Stream stream = await response.Content.ReadAsStreamAsync();
            using var fs = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fs);

            // Guardar versión instalada
            Preferences.Set("ytdlp_version", latestTag ?? "");
            #endregion

        }

        #endregion

        #region Ffmpeg

        public async Task EnsureFfmpegInstalledAsync()
        {
            if (File.Exists(_ffmpegPath)) return;

            // FFmpeg build estático para Windows (from github.com/BtbN/FFmpeg-Builds)
            string zipUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
            string zipPath = Path.Combine(Path.GetDirectoryName(_ffmpegPath)!, "ffmpeg.zip");

            await DownloadFileAsync(zipUrl, zipPath);
            await ExtractFfmpegAsync(zipPath);
        }

        private async Task ExtractFfmpegAsync(string zipPath)
        {
            string outputDir = Path.GetDirectoryName(_ffmpegPath)!;

            await Task.Run(() =>
            {
                using var zip = System.IO.Compression.ZipFile.OpenRead(zipPath);

                // El zip contiene una carpeta raíz, buscamos ffmpeg.exe dentro de /bin/
                var entry = zip.Entries.FirstOrDefault(e =>
                    e.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase) &&
                    e.FullName.Contains("bin"));

                if (entry == null)
                    throw new FileNotFoundException("No se encontró ffmpeg.exe en el zip.");

                entry.ExtractToFile(_ffmpegPath, overwrite: true);
            });

            File.Delete(zipPath); // Limpiamos el zip
        }

        private async Task DownloadFileAsync(string url, string destinationPath)
        {
            string? dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using HttpClient client = new HttpClient();
            using HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using Stream stream = await response.Content.ReadAsStreamAsync();
            using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fs);
        }
        #endregion

        private readonly List<Process> _activeProcesses = new();
        private readonly object _processLock = new();

        /// <summary>
        /// Mata el proceso en curso y limpia la referencia.
        /// </summary>
        public void KillCurrentProcess()
        {
            #region Version 1

            //try
            //{
            //    if (_currentProcess != null && !_currentProcess.HasExited)
            //        _currentProcess.Kill(entireProcessTree: true);
            //}
            //catch { /* El proceso ya terminó */ }
            //finally
            //{
            //    currentProcess = null;
            //}

            #endregion

            #region  Version 2

            lock (_processLock)
            {
                foreach (var p in _activeProcesses)
                {
                    try
                    {
                        if (!p.HasExited)
                        {
                            p.Kill(entireProcessTree: true);
                        }
                    }
                    catch { }
                }
                _activeProcesses.Clear();
            }

            #endregion

        }

        public async Task DownloadAsync(string url, string output, string format, IProgress<string> progress, CancellationToken token, string? formatID = null, string? TargetExtension = null)
        {

            string[] parts = BuildArguments(url, output, format, formatID, TargetExtension);
            string args = string.Join(" ", parts);

            using Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _path,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            currentProcess = process;

            //process.Start();
            lock (_processLock) _activeProcesses.Add(process);

            try
            {
                process.Start();

                // Leer stderr en paralelo para evitar que el buffer se llene y bloquee el proceso
                var stderrTask = ConsumeStreamAsync(process.StandardError, progress, token);

                string? line;
                while ((line = await process.StandardOutput.ReadLineAsync(token)) != null)
                {
                    token.ThrowIfCancellationRequested();

                    var parsed = ParseProgress(line);
                    if (!string.IsNullOrEmpty(parsed))
                        progress?.Report(parsed);
                }

                await Task.WhenAll(
                    process.WaitForExitAsync(token),
                    stderrTask
                );
            }
            finally
            {
                lock (_processLock) _activeProcesses.Remove(process);
                currentProcess = null;
            }
        }

        public async Task DownloadAsync(string url, string output, string format, string? formatID = null)
        {
            string[] parts = BuildArguments(url, output, format, formatID);
            string args = string.Join(" ", parts);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _path,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            // Drenar stdout y stderr para evitar deadlock por buffer lleno
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync());
        }

        // Consume el stream sin bloquearlo, opcionalmente loguea errores
        private async Task ConsumeStreamAsync(StreamReader reader, IProgress<string>? progress, CancellationToken token)
        {
            try
            {
                string? line;
                while ((line = await reader.ReadLineAsync(token)) != null)
                {
                    token.ThrowIfCancellationRequested();

                    // Reportar errores reales de yt-dlp
                    if (line.Contains("ERROR") || line.Contains("error"))
                        progress?.Report($"⚠️ {line}");
                }
            }
            catch (OperationCanceledException) { }
        }

        public string ParseProgress(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return string.Empty;

            if (line.Contains("[download]"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    line,
                    @"(\d{1,3}[\.,]\d+)%.*?at\s+([\d\.]+\w+/s).*?ETA\s+([\d:]+)"
                );

                if (match.Success)
                {
                    var percent = match.Groups[1].Value;
                    var speed = match.Groups[2].Value;
                    return $"{Strings.Downloading}... {percent}% ( {speed} )";
                }

                return $"{Strings.Downloading}...";
            }

            if (line.Contains("[info]") || line.Contains("[youtube]"))
            {
                // Solo mostrar info relevante, no cada metadata intermedia
                if (line.Contains("Downloading") || line.Contains("Extracting URL"))
                    return $"{Strings.GettingInfo}...";

                if (line.Contains("Extracting audio"))
                    return $"{Strings.ExtractingAudio}...";

                if (line.Contains("Merging formats"))
                    return $"{Strings.MergingAudioVideo}...";

                if (line.Contains("[PostProcess]") || line.Contains("post-process"))
                    return $"{Strings.ExtractingAudio}...";

                if (line.Contains("100%"))
                    return $"{Strings.CompletedDownload}";

                return string.Empty; // Ignorar el resto de líneas [info]
            }

            return string.Empty;
        }

        public async Task<List<FormatOption>> FetchFormatsAsync(string url, CancellationToken ct = default)
        {
            #region Version 3

            string args = IsPlaylist(url)
                ? $"-J --playlist-items 1 \"{url}\""
                : $"-J \"{url}\"";

            var raw = await Task.Run(() => RunAndCaptureAsync(args, ct), ct);

            int jsonIndex = FindJsonStart(raw);
            if (jsonIndex < 0)
            {
                return new List<FormatOption>();
            }

            using var doc = JsonDocument.Parse(raw.AsMemory(jsonIndex));
            var root = doc.RootElement;

            JsonElement target = root;
            if (root.TryGetProperty("entries", out var entries))
            {
                var first = entries.EnumerateArray().FirstOrDefault();
                if (first.ValueKind == JsonValueKind.Undefined)
                    return new List<FormatOption>();
                target = first;
            }

            if (!target.TryGetProperty("formats", out var formats))
                return new List<FormatOption>();

            return ParseBestFormats(formats);

            #endregion
        }

        private static bool IsPlaylist(string url) =>
            url.Contains("list=", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("/playlist", StringComparison.OrdinalIgnoreCase);

        private static int FindJsonStart(string raw)
        {
            int i = 0;
            while (i < raw.Length)
            {
                int nl = raw.IndexOf('\n', i);
                int end = nl < 0 ? raw.Length : nl;
                var slice = raw.AsSpan(i, end - i).TrimStart();
                if (slice.StartsWith("{") || slice.StartsWith("["))
                    return i + (end - i - slice.Length);
                i = end + 1;
            }
            return -1;
        }

        private static List<FormatOption> ParseBestFormats(JsonElement formats, int topPerFormat = 2)
        {
            // Siempre ofrecemos MP3 como opción de extracción de audio
            var audioMp3 = new FormatOption
            {
                FormatId = "mp3",
                Extension = "mp3",
                IsVideo = false,
                Abr = 320,
                Label = "Audio MP3"
            };

            // Recopilar todos los candidatos de vídeo válidos
            var allVideoCandidates = new List<FormatOption>();

            // Para audio nativo (m4a, opus, webm audio...)
            // clave: ext, valor: mejor candidato por bitrate
            var audioCandidates = new Dictionary<string, FormatOption>(StringComparer.OrdinalIgnoreCase);

            foreach (var f in formats.EnumerateArray())
            {
                string vcodec = f.GetStringOrEmpty("vcodec") ?? "none";
                string acodec = f.GetStringOrEmpty("acodec") ?? "none";
                string ext = f.GetStringOrEmpty("ext") ?? "";
                string fmtId = f.GetStringOrEmpty("format_id") ?? "";

                if ((vcodec == "none" && acodec == "none")
                    || ext is "mhtml" or "3gp" or "flv" or "json")
                    continue;

                bool hasVideo = vcodec != "none";
                bool hasAudio = acodec != "none";

                var height = f.TryGetProperty("height", out var h) && h.ValueKind == JsonValueKind.Number
                    ? (int)h.GetDouble() : 0;
                var tbr = f.TryGetProperty("tbr", out var tb) && tb.ValueKind == JsonValueKind.Number
                    ? (int)tb.GetDouble() : 0;
                var abr = f.TryGetProperty("abr", out var ab) && ab.ValueKind == JsonValueKind.Number
                    ? (int)ab.GetDouble() : 0;
                var fps = f.TryGetProperty("fps", out var fp) && fp.ValueKind == JsonValueKind.Number
                    ? (int)Math.Round(fp.GetDouble()) : 0;

                // ── Vídeo ────────────────────────────────────────────────────
                if (hasVideo && (height > 0 || tbr > 0))
                {
                    allVideoCandidates.Add(new FormatOption
                    {
                        FormatId = fmtId,
                        Extension = ext,
                        ResolutionInt = height,
                        Fps = fps,
                        Tbr = tbr,
                        IsVideo = true,
                        Label = height > 0
                            ? $"Video {ext.ToUpper()} — {height}p{(fps >= 60 ? $" {fps}fps" : "")}"
                            : $"Video {ext.ToUpper()} — ~{tbr:F0}kbps"
                    });
                }

                // ── Audio nativo (solo audio, sin vídeo) ─────────────────────
                if (!hasVideo && hasAudio && ext is "m4a" or "webm" or "opus" or "ogg" or "mp3")
                {
                    double bitrate = abr > 0 ? abr : tbr;
                    if (bitrate <= 0) continue;

                    var candidate = new FormatOption
                    {
                        FormatId = fmtId,
                        Extension = ext,
                        IsVideo = false,
                        Abr = bitrate,
                        Label = $"Audio {ext.ToUpper()} — ~{bitrate:F0}kbps"
                    };

                    // Guardar solo si es mejor que el anterior del mismo formato
                    if (!audioCandidates.TryGetValue(ext, out var existing)
                        || bitrate > existing.Abr)
                    {
                        audioCandidates[ext] = candidate;
                    }
                }
            }

            var result = new List<FormatOption>();

            // ── Top N vídeos por extensión, ordenados por resolución desc ────
            var videoGroups = allVideoCandidates
                .GroupBy(v => v.Extension, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key); // mp4, webm...

            foreach (var group in videoGroups)
            {
                var top = group
                    .GroupBy(v => v.ResolutionInt) // una entrada por resolución
                    .Select(g => g.OrderByDescending(v => v.Tbr).First()) // mejor tbr por resolución
                    .OrderByDescending(v => v.ResolutionInt)
                    .ThenByDescending(v => v.Tbr)
                    .Take(topPerFormat);

                result.AddRange(top);
            }

            // ── Audio nativo (el mejor de cada formato) ───────────────────────
            result.AddRange(
                audioCandidates.Values
                    .OrderByDescending(a => a.Abr));

            // ── MP3 siempre al final como opción de extracción ────────────────
            result.Add(audioMp3);

            return result;
        }

        #region Clean URL (no list/playlists)

        public string CleanYouTubeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return url;

            var query = HttpUtility.ParseQueryString(uri.Query);

            // Parámetros que queremos eliminar
            string[] keysToRemove = { "list", "index", "start_radio", "pp" };

            foreach (var key in keysToRemove)
            {
                query.Remove(key);
            }

            // Reconstruir query limpia
            var newQuery = string.Join("&",
                query.AllKeys
                     .Where(k => !string.IsNullOrEmpty(k))
                     .Select(k => $"{k}={query[k]}"));

            var uriBuilder = new UriBuilder(uri)
            {
                Query = newQuery
            };

            return uriBuilder.Uri.ToString();
        }

        #endregion

        #region Get Info from URL

        public async Task<List<VideoInfo>> FetchInfoAsync(string url, CancellationToken ct = default, bool fullMetadata = false)
        {
            var raw = await RunAndCaptureAsync($"--flat-playlist -J \"{url}\"", ct);

            var jsonStart = raw.Split('\n').FirstOrDefault(line => line.TrimStart().StartsWith("{") || line.TrimStart().StartsWith("["));

            if (jsonStart == null)
                throw new Exception("yt-dlp no devolvió JSON válido.");

            var jsonOnly = raw.Substring(raw.IndexOf(jsonStart, StringComparison.Ordinal));

            using var doc = JsonDocument.Parse(jsonOnly);
            var root = doc.RootElement;

            var results = new List<VideoInfo>();

            if (root.TryGetProperty("entries", out var entries))
            {
                foreach (var entry in entries.EnumerateArray())
                {
                    var info = ParseEntry(entry);

                    if (string.IsNullOrWhiteSpace(info.Url)) continue;
                    if (info.Title == "[Private video]" || info.Title == "[Deleted video]") continue;

                    results.Add(info);
                }
            }
            else
            {
                results.Add(ParseEntry(root));
            }

            return results;
        }

        private static VideoInfo ParseEntry(JsonElement el)
        {
            try
            {

                double? DurationSeconds = null;
                string Duration = string.Empty;

                if (el.TryGetProperty("duration", out var d) &&
                    d.ValueKind == JsonValueKind.Number &&
                    d.TryGetDouble(out double seconds))
                {
                    DurationSeconds = seconds;

                    Duration = TimeSpan.FromSeconds(seconds).ToString(@"m\:ss");
                }

                string thumbnail = string.Empty;

                if (el.TryGetProperty("thumbnails", out var thumbs) && thumbs.ValueKind == JsonValueKind.Array)
                {
                    thumbnail = thumbs.EnumerateArray()
                        .Where(t => t.TryGetProperty("url", out _))
                        .OrderByDescending(t => t.TryGetProperty("width", out var w) && w.ValueKind == JsonValueKind.Number
                        ? w.GetInt32() : 0)
                        .Select(t => t.GetProperty("url").GetString())
                        .FirstOrDefault() ?? string.Empty;
                }

                var id = el.GetStringOrEmpty("id");

                var url = !string.IsNullOrWhiteSpace(el.GetStringOrEmpty("webpage_url")) ? el.GetStringOrEmpty("webpage_url") :
                    !string.IsNullOrWhiteSpace(el.GetStringOrEmpty("url")) ? el.GetStringOrEmpty("url") :
                    !string.IsNullOrWhiteSpace(el.GetStringOrEmpty("original_url")) ? el.GetStringOrEmpty("original_url") :
                    BuildUrlFromId(el, id);

                return new VideoInfo
                {
                    Id = id,
                    Title = el.GetStringOrEmpty("title"),
                    Channel = el.GetStringOrEmpty("channel") ?? el.GetStringOrEmpty("uploader"),
                    Thumbnail = thumbnail,
                    Url = url,
                    Duration = Duration,
                    DurationSeconds = DurationSeconds,

                    // Metadata musical
                    Artist = el.GetStringOrEmpty("artist"),
                    Artists = GetStringArray(el, "artists"),
                    Album = el.GetStringOrEmpty("album"),
                    AlbumArtist = el.GetStringOrEmpty("album_artist"),
                    TrackNumber = GetInt32(el, "track_number"),
                    DiscNumber = GetInt32(el, "disc_number"),
                    Genre = el.GetStringOrEmpty("genre"),
                    ReleaseDate = el.GetStringOrEmpty("release_date"),
                    Description = el.GetStringOrEmpty("description"),

                };
            }
            catch (Exception ex)
            {
                var aux = ex;
                throw;
            }
        }

        private static int? GetInt32(JsonElement el, string property)
        {
            if (!el.TryGetProperty(property, out var value))
                return null;

            if (value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt32(out var result))
            {
                return result;
            }

            return null;
        }
        private static List<string>? GetStringArray(JsonElement el, string property)
        {
            if (!el.TryGetProperty(property, out var value) ||
                value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var result = value.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .ToList();

            return result.Count > 0 ? result : null;
        }

        private static string BuildUrlFromId(JsonElement el, string id)
        {
            if (string.IsNullOrEmpty(id))
                return string.Empty;

            var extractor = el.GetStringOrEmpty("ie_key")
                         ?? el.GetStringOrEmpty("extractor")
                         ?? string.Empty;

            return extractor?.ToLowerInvariant() switch
            {
                "youtube" or "youtubetab" => $"https://www.youtube.com/watch?v={id}",
                "soundcloud" => string.Empty,
                "twitch:vod" => $"https://www.twitch.tv/videos/{id}",
                _ => $"https://www.youtube.com/watch?v={id}"
            };
        }

        public async Task<string> RunAndCaptureAsync(string arguments, CancellationToken ct = default)
        {
            var sb = new StringBuilder();

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _path,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    sb.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    sb.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
                throw new Exception($"yt-dlp error (code {process.ExitCode}):\n{sb}");

            return sb.ToString();
        }

        #endregion

        #region Args Constructor

        //private string[] BuildArguments(string url, string output, string format)
        //{
        //    string[] commonArgs =
        //    [
        //        $"-o \"{output}/%(title)s.%(ext)s\"",
        //        $"--ffmpeg-location \"{_ffmpegPath}\"",
        //        "--newline"
        //    ];

        //    string[] formatArgs;

        //    if (format.Contains("Audio"))
        //    {
        //        formatArgs =
        //        [
        //            "-x",
        //            "--audio-format mp3",
        //            "--audio-quality 0",
        //            "--embed-metadata",
        //            "--embed-thumbnail"
        //        ];
        //    }
        //    else
        //    {
        //        formatArgs =
        //        [
        //            "-f \"bestvideo+bestaudio/best\"",
        //            "--merge-output-format mkv",
        //        ];
        //    }

        //    //Return the combined arguments with the URL at the end
        //    return [.. commonArgs, .. formatArgs, url];
        //}

        private string[] BuildArguments(string url, string output, string format, string? formatId = null, string? targetExt = null)
        {
            string[] commonArgs =
            [
                $"-o \"{output}/%(title)s.%(ext)s\"",
                $"--ffmpeg-location \"{_ffmpegPath}\"",
                "--newline",
                "--embed-metadata",
                "--embed-thumbnail"
            ];

            string[] formatArgs;

            if (formatId == "mp3")
            {
                formatArgs =
                [
                    "-x",
                    "--audio-format mp3",
                    "--audio-quality 0",
        ];
            }
            else if (!string.IsNullOrEmpty(formatId))
            {
                // Determinar el contenedor de salida según la extensión del formato elegido
                string mergeExt = targetExt?.ToLowerInvariant() switch
                {
                    "webm" => "webm",
                    "mkv" => "mkv",
                    _ => "mp4"   // mp4 por defecto para cualquier otro
                };

                // El audio compatible según el contenedor
                string audioSelector = mergeExt == "webm"
                    ? $"{formatId}+bestaudio[ext=webm]/bestaudio"
                    : $"{formatId}+bestaudio[ext=m4a]/bestaudio";

                // Códec de audio compatible con el contenedor
                string audioCodec = mergeExt == "webm"
                    ? "ffmpeg:-c:a libopus -b:a 192k"
                    : "ffmpeg:-c:a aac -b:a 192k";

                formatArgs =
                [
                    $"-f \"{audioSelector}\"", 
                    $"--merge-output-format {mergeExt}",
                    $"--postprocessor-args \"{audioCodec}\"",
        ];
            }
            else if (format.Contains("Audio"))
            {
                formatArgs =
                [
                    "-x",
                    "--audio-format mp3",
                    "--audio-quality 0",
        ];
            }
            else
            {
                formatArgs =
                [
                    "-f \"bestvideo+bestaudio/best\"",
                    "--merge-output-format mp4",
                    "--postprocessor-args \"ffmpeg:-c:a aac -b:a 192k\"",
        ];
            }

            return [.. commonArgs, .. formatArgs, url];
        }
        #endregion


        #region Auto-Actualizacion

        private const string ReleasesApi = "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest";

        public async Task<string?> CheckAndUpdateAsync(IProgress<string>? progress = null)
        {
            try
            {
                progress?.Report("Comprobando actualizaciones de yt-dlp...");

                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.ParseAdd("YouPander/1.0");

                var json = await http.GetStringAsync(ReleasesApi);
                using var doc = JsonDocument.Parse(json);
                var latestTag = doc.RootElement.GetProperty("tag_name").GetString();
                var currentTag = Preferences.Get("ytdlp_version", "");

                if (latestTag == currentTag)
                {
                    progress?.Report("yt-dlp ya está actualizado.");
                    return null;
                }

                progress?.Report($"Nueva versión encontrada: {latestTag}. Descargando...");

                // Buscar la URL del asset correcto (yt-dlp.exe en Windows)
                var assets = doc.RootElement.GetProperty("assets");
                var downloadUrl = assets.EnumerateArray()
                    .FirstOrDefault(a => a.GetProperty("name").GetString() == "yt-dlp.exe")
                    .GetProperty("browser_download_url").GetString();

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    progress?.Report("No se encontró el asset de descarga.");
                    return null;
                }

                // Descargar a un fichero temporal primero — si falla, el binario actual queda intacto
                var tempPath = _path + ".tmp";

                using var response = await http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                var downloadedBytes = 0L;

                await using var stream = await response.Content.ReadAsStreamAsync();
                await using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[81920]; // 80 KB por chunk
                int read;
                while ((read = await stream.ReadAsync(buffer)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, read));
                    downloadedBytes += read;

                    if (totalBytes > 0)
                    {
                        var pct = (int)(downloadedBytes * 100 / totalBytes);
                        progress?.Report($"Descargando yt-dlp... {pct}%");
                    }
                }

                // Reemplazar el binario actual con el nuevo
                fs.Close();
                if (File.Exists(_path)) File.Delete(_path);
                File.Move(tempPath, _path);

                Preferences.Set("ytdlp_version", latestTag ?? "");
                progress?.Report($"yt-dlp actualizado a {latestTag}.");
                return latestTag;
            }
            catch (Exception ex)
            {
                progress?.Report($"Error al actualizar yt-dlp: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Obtencion de MetaData

        public static SongMetadata FromVideoInfo(VideoInfo info)
        {
            return new SongMetadata
            {
                Title = info.Title,

                Artist =
                    info.Artist ??
                    (info.Artists?.Count > 0
                        ? string.Join(", ", info.Artists)
                        : null),

                Album = info.Album,
                AlbumArtist = info.AlbumArtist,

                TrackNumber = info.TrackNumber,
                DiscNumber = info.DiscNumber,

                Genre = info.Genre,

                CoverUrl = info.Thumbnail,

                Year = ParseYear(info.ReleaseDate)
            };
        }

        private static int? ParseYear(string? date)
        {
            if (string.IsNullOrWhiteSpace(date))
                return null;

            if (date.Length >= 4 &&
                int.TryParse(date[..4], out var year))
            {
                return year;
            }

            return null;
        }

        public static SongCandidate CreateSongCandidate(VideoInfo info)
        {
            string? artist = info.Artist;

            if (string.IsNullOrWhiteSpace(artist))
            {
                if (info.Artists?.Count > 0)
                {
                    artist = string.Join(", ", info.Artists);
                }
                else
                {
                    artist = info.Channel;
                }
            }

            var title = CleanYoutubeTitle(
                info.Title,
                artist);

            return new SongCandidate
            {
                Title = title,
                Artist = artist,
                DurationSeconds = info.DurationSeconds,
                CoverUrl = info.Thumbnail
            };
        }

        public static SongCandidate CreateCandidate(VideoInfo info)
        {
            var title = CleanYoutubeTitle(
                info.Title,
                info.Channel);

            var artist =
                !string.IsNullOrWhiteSpace(info.Artist)
                    ? info.Artist
                    : info.Channel;

            return new SongCandidate
            {
                Title = title,
                Artist = artist,
                DurationSeconds = info.DurationSeconds,
                CoverUrl = info.Thumbnail
            };
        }
        private static string CleanYoutubeTitle(string? title, string? channel)
        {
            if (string.IsNullOrWhiteSpace(title))
                return string.Empty;

            var result = title.Trim();

            // Eliminar contenido entre corchetes
            result = Regex.Replace(result,
                @"\s*\[[^\]]*\]",
                "");

            // Eliminar contenido entre paréntesis
            result = Regex.Replace(result,
                @"\s*\([^)]*\)",
                "");

            // Si termina en "- Linkin Park", quitarlo
            if (!string.IsNullOrWhiteSpace(channel))
            {
                result = Regex.Replace(result,
                    $@"\s*[-–—]\s*{Regex.Escape(channel)}\s*$",
                    "",
                    RegexOptions.IgnoreCase);
            }

            return result.Trim();
        }

        #endregion

    }
}

