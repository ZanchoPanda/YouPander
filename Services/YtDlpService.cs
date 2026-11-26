using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using YouPander.Models;
using YouPander.Resources.Localization;
using YouPander.ViewModels;

namespace YouPander.Services
{
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

        private readonly SemaphoreSlim _installationLock = new(1, 1);
        private static readonly SemaphoreSlim _ffmpegInstallLock = new(1, 1);

        private static readonly HttpClient _ffmpegHttpClient = new()
        {
            Timeout = TimeSpan.FromMinutes(30)
        };
        #endregion

        public YtDlpService(string path)
        {
            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacCatalyst())
            {
                throw new PlatformNotSupportedException(Strings.UnsupportedPlatform);
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("La ruta de yt-dlp no puede estar vacía.", nameof(path));
            }

            string directory = Path.GetDirectoryName(path) ?? throw new ArgumentException("La ruta debe incluir un directorio.", nameof(path));

            Directory.CreateDirectory(directory);

            string executableName = GetYtDlpAssetName();

            _path = Path.Combine(directory, executableName);

            _ffmpegPath = Path.Combine(directory, OperatingSystem.IsWindows()
                ? "ffmpeg.exe"
                : "ffmpeg");
        }

        #region yt-dlp

        /// <summary>
        /// Descarga yt-dlp.exe si no existe todavía.
        /// </summary>
        public async Task EnsureInstalledAsync()
        {
            #region Version con MAc
            await _installationLock.WaitAsync();

            string tempPath = _path + ".download";

            try
            {
                if (File.Exists(_path))
                {
                    return;
                }

                string? directory = Path.GetDirectoryName(_path);

                if (string.IsNullOrEmpty(directory))
                {
                    throw new InvalidOperationException("No se pudo determinar el directorio de yt-dlp.");
                }

                Directory.CreateDirectory(directory);

                using HttpClient client = new();

                client.DefaultRequestHeaders.UserAgent.ParseAdd("YouPander/1.0");

                using HttpResponseMessage apiResponse = await client.GetAsync(ReleasesApi);

                apiResponse.EnsureSuccessStatusCode();

                string json = await apiResponse.Content.ReadAsStringAsync();

                using JsonDocument doc = JsonDocument.Parse(json);

                JsonElement root = doc.RootElement;

                string? latestTag = root.GetProperty("tag_name").GetString();

                string assetName = GetYtDlpAssetName();

                JsonElement asset = root.GetProperty("assets")
                    .EnumerateArray()
                    .FirstOrDefault(a =>
                        a.GetProperty("name").GetString() == assetName);

                if (asset.ValueKind == JsonValueKind.Undefined)
                {
                    throw new FileNotFoundException($"No se encontró el ejecutable oficial {assetName}.");
                }

                string? downloadUrl = asset
                    .GetProperty("browser_download_url")
                    .GetString();

                string? digest = asset.TryGetProperty("digest", out var digestElement)
                    ? digestElement.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(downloadUrl))
                {
                    throw new InvalidOperationException("GitHub no proporcionó una URL de descarga válida.");
                }

                if (string.IsNullOrWhiteSpace(digest) || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("GitHub no proporcionó un SHA-256 verificable para yt-dlp.");
                }

                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                using HttpResponseMessage downloadResponse = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);

                downloadResponse.EnsureSuccessStatusCode();

                await using (Stream stream = await downloadResponse.Content.ReadAsStreamAsync())
                await using (FileStream file = new(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    await stream.CopyToAsync(file);
                }

                // Verificar integridad SHA-256.
                string expectedHash = digest["sha256:".Length..];

                await using (FileStream file = File.OpenRead(tempPath))
                {
                    byte[] hash = await SHA256.HashDataAsync(file);

                    string actualHash = Convert.ToHexString(hash);

                    if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("La comprobación SHA-256 de yt-dlp ha fallado.");
                    }
                }

                // macOS necesita permiso de ejecución.
                if (OperatingSystem.IsMacCatalyst())
                {
                    File.SetUnixFileMode(
                        tempPath,
                        UnixFileMode.UserRead |
                        UnixFileMode.UserWrite |
                        UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead |
                        UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead |
                        UnixFileMode.OtherExecute);
                }

                // Instalar únicamente después de verificar el archivo.
                File.Move(tempPath, _path, overwrite: true);

                Preferences.Set("ytdlp_version", latestTag ?? "");
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                _installationLock.Release();
            }
            #endregion
        }

        private static string GetYtDlpAssetName()
        {
            if (OperatingSystem.IsWindows())
            {
                return "yt-dlp.exe";
            }

            if (OperatingSystem.IsMacCatalyst())
            {
                return "yt-dlp_macos";
            }

            throw new PlatformNotSupportedException();
        }

        #endregion

        #region FFmpeg (Mac Catalyst y Windows)

        private sealed record FfmpegSource(string ArchiveUrl, string ChecksumUrl, string ArchiveFileName);

        private static FfmpegSource GetFfmpegSource()
        {
            if (OperatingSystem.IsWindows())
            {
                if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
                {
                    throw new PlatformNotSupportedException("YouPander requiere FFmpeg para Windows x64.");
                }

                const string baseUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/";

                const string archiveName = "ffmpeg-master-latest-win64-lgpl.zip";

                return new FfmpegSource(baseUrl + archiveName, baseUrl + "checksums.sha256", archiveName);
            }

            if (OperatingSystem.IsMacCatalyst())
            {
                string architecture = RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X64 => "amd64",
                    Architecture.Arm64 => "arm64",

                    _ => throw new PlatformNotSupportedException($"Arquitectura no compatible: {RuntimeInformation.ProcessArchitecture}")
                };

                const string baseUrl = "https://ffmpeg.martin-riedl.de/redirect/latest/";

                string archiveUrl = $"{baseUrl}macos/{architecture}/release/ffmpeg.zip";

                return new FfmpegSource(archiveUrl, archiveUrl + ".sha256", "ffmpeg.zip");
            }

            throw new PlatformNotSupportedException("FFmpeg solo está configurado para Windows y Mac Catalyst.");
        }

        /// <summary>
        /// Comprueba e instala FFmpeg si es necesario.
        /// La instalación se realiza una sola vez aunque haya
        /// varias llamadas simultáneas.
        /// </summary>
        public async Task EnsureFfmpegInstalledAsync(CancellationToken cancellationToken = default)
        {
            await _ffmpegInstallLock.WaitAsync(cancellationToken);

            string? zipPath = null;
            string? stagedPath = null;

            try
            {
                string directory = Path.GetDirectoryName(_ffmpegPath)!;

                Directory.CreateDirectory(directory);

                // Comprobar si la instalación existente funciona.
                if (File.Exists(_ffmpegPath))
                {
                    try
                    {
                        await ValidateFfmpegAsync(_ffmpegPath, cancellationToken);
                        return;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        // Instalación corrupta o no ejecutable.
                        // Continuamos con una instalación nueva.
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                FfmpegSource source = GetFfmpegSource();

                string id = Guid.NewGuid().ToString("N");

                zipPath = Path.Combine(directory, $"ffmpeg-{id}.zip");

                stagedPath = Path.Combine(directory, $"ffmpeg-{id}.tmp");

                // 1. Descargar el archivo comprimido.
                await DownloadFileAsync(source.ArchiveUrl, zipPath, cancellationToken);

                // 2. Obtener el SHA-256 publicado por el proveedor.
                string checksumText = await _ffmpegHttpClient.GetStringAsync(source.ChecksumUrl, cancellationToken);

                string expectedHash = ExtractExpectedSha256(checksumText, source.ArchiveFileName);

                // 3. Comprobar la integridad del ZIP.
                await using (var archiveStream = new FileStream(
                    zipPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    byte[] hash = await SHA256.HashDataAsync(archiveStream, cancellationToken);

                    string actualHash = Convert.ToHexString(hash);

                    if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            "La comprobación SHA-256 de FFmpeg ha fallado. " +
                            "El archivo descargado no es válido.");
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // 4. Extraer únicamente el ejecutable.
                await ExtractFfmpegAsync(zipPath, stagedPath, cancellationToken);

                // 5. Dar permisos de ejecución en macOS.
                if (OperatingSystem.IsMacCatalyst())
                {
                    File.SetUnixFileMode(
                        stagedPath,
                        UnixFileMode.UserRead |
                        UnixFileMode.UserWrite |
                        UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead |
                        UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead |
                        UnixFileMode.OtherExecute);
                }

                // 6. Verificar que el ejecutable funciona.
                await ValidateFfmpegAsync(stagedPath, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                // 7. Sustitución del ejecutable.
                // Ambos archivos están en el mismo directorio.
                // No eliminamos previamente el ejecutable anterior.
                File.Move(stagedPath, _ffmpegPath, overwrite: true);

                stagedPath = null;

                // 8. Verificación final.
                await ValidateFfmpegAsync(_ffmpegPath, cancellationToken);
            }
            finally
            {
                // Limpiar archivos temporales.
                TryDeleteFile(zipPath);
                TryDeleteFile(stagedPath);

                _ffmpegInstallLock.Release();
            }
        }

        /// <summary>
        /// Descarga un archivo a una ruta temporal o de destino.
        /// Si falla, elimina el archivo incompleto.
        /// </summary>
        private async Task DownloadFileAsync(string url, string destinationPath, CancellationToken cancellationToken = default)
        {
            string? directory = Path.GetDirectoryName(destinationPath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            try
            {
                using HttpResponseMessage response = await _ffmpegHttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                response.EnsureSuccessStatusCode();

                await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken);

                await using var destination = new FileStream(
                    destinationPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);

                await source.CopyToAsync(destination, 81920, cancellationToken);

                await destination.FlushAsync(cancellationToken);
            }
            catch
            {
                TryDeleteFile(destinationPath);
                throw;
            }
        }

        /// <summary>
        /// Extrae únicamente ffmpeg.exe o ffmpeg.
        /// No extrae rutas arbitrarias del ZIP.
        /// </summary>
        private async Task ExtractFfmpegAsync(string zipPath, string destinationPath, CancellationToken cancellationToken = default)
        {
            string executableName = OperatingSystem.IsWindows()
                ? "ffmpeg.exe"
                : "ffmpeg";

            using var archive = ZipFile.OpenRead(zipPath);

            var entry = archive.Entries.FirstOrDefault(e => e.Name.Equals(executableName, StringComparison.OrdinalIgnoreCase));

            if (entry == null || entry.Length <= 0)
            {
                throw new FileNotFoundException($"No se encontró un ejecutable válido de {executableName} en el ZIP.");
            }

            await using Stream input = entry.Open();

            await using var output = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            await input.CopyToAsync(output, 81920, cancellationToken);

            await output.FlushAsync(cancellationToken);
        }

        /// <summary>
        /// Obtiene el SHA-256 del archivo esperado desde el manifiesto.
        /// </summary>
        private static string ExtractExpectedSha256(string checksumText, string archiveFileName)
        {
            if (string.IsNullOrWhiteSpace(checksumText))
            {
                throw new InvalidDataException("El manifiesto SHA-256 está vacío.");
            }

            if (string.IsNullOrWhiteSpace(archiveFileName))
            {
                throw new ArgumentException("El nombre del archivo no puede estar vacío.", nameof(archiveFileName));
            }

            var namedHashes = new List<string>();
            var unnamedHashes = new List<string>();

            string[] lines = checksumText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string originalLine in lines)
            {
                string line = originalLine.Trim();

                if (line.Length == 0)
                {
                    continue;
                }

                // Formato BSD:
                // SHA256 (archivo.zip) = HASH
                Match bsdMatch = Regex.Match(line, @"^SHA256\s*\((?<name>.+)\)\s*=\s*(?<hash>[a-fA-F0-9]{64})$", RegexOptions.IgnoreCase);

                if (bsdMatch.Success)
                {
                    string name = bsdMatch.Groups["name"].Value.Trim();
                    string hash = bsdMatch.Groups["hash"].Value;

                    namedHashes.Add(IsExpectedArchive(name, archiveFileName)
                            ? hash
                            : string.Empty);

                    continue;
                }

                // Formato GNU:
                // HASH  archivo.zip
                // HASH *archivo.zip
                Match gnuMatch = Regex.Match(line, @"^(?<hash>[a-fA-F0-9]{64})(?:\s+[* ](?<name>.+))?$");

                if (!gnuMatch.Success)
                {
                    throw new InvalidDataException($"Línea SHA-256 no reconocida: {line}");
                }

                string hashValue = gnuMatch.Groups["hash"].Value;

                if (gnuMatch.Groups["name"].Success)
                {
                    string name = gnuMatch.Groups["name"].Value.Trim();

                    namedHashes.Add(IsExpectedArchive(name, archiveFileName)
                            ? hashValue
                            : string.Empty);
                }
                else
                {
                    unnamedHashes.Add(hashValue);
                }
            }

            // Si existen entradas con nombre, exigimos coincidencia exacta.
            if (namedHashes.Count > 0)
            {
                string[] matches = namedHashes
                    .Where(hash => !string.IsNullOrEmpty(hash))
                    .ToArray();

                if (matches.Length != 1)
                {
                    throw new InvalidDataException($"El manifiesto no contiene una única entrada SHA-256 " +
                        $"válida para {archiveFileName}.");
                }

                return matches[0].ToUpperInvariant();
            }

            // Solo aceptamos un manifiesto sin nombre cuando
            // contiene exactamente un hash.
            if (unnamedHashes.Count == 1)
            {
                return unnamedHashes[0].ToUpperInvariant();
            }

            throw new InvalidDataException(
                $"El manifiesto SHA-256 es ambiguo o no contiene " +
                $"el hash esperado para {archiveFileName}.");
        }

        private static bool IsExpectedArchive(string manifestFileName, string expectedFileName)
        {
            string normalizedName = manifestFileName
                .Replace('\\', '/')
                .Trim();

            string actualName = normalizedName
                .Split('/')
                .Last();

            return string.Equals(actualName, expectedFileName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ejecuta ffmpeg -version para comprobar que el binario
        /// puede ejecutarse y corresponde a una compilación válida.
        /// </summary>
        private static async Task ValidateFfmpegAsync(string executablePath, CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("-version");

            using var process = new Process
            {
                StartInfo = startInfo
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("No se pudo iniciar FFmpeg.");
            }

            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // El proceso puede haber terminado.
                }
            });

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();

            Task<string> stderrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            string[] output = await Task.WhenAll(stdoutTask, stderrTask);

            string combinedOutput = string.Join(Environment.NewLine, output);

            if (process.ExitCode != 0 || !combinedOutput.Contains("ffmpeg version", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"FFmpeg no ha superado la validación. " +
                    $"Código: {process.ExitCode}. " +
                    $"Salida: {combinedOutput}");
            }
        }

        private static void TryDeleteFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // La limpieza no debe ocultar el error original.
            }
        }

        #endregion

        private ProcessStartInfo CreateYtDlpStartInfo(IEnumerable<string> arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _path,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            return startInfo;
        }

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
            Directory.CreateDirectory(output);

            List<string> arguments = BuildArguments(
                url,
                output,
                format,
                formatID,
                TargetExtension);

            using Process process = new()
            {
                StartInfo = CreateYtDlpStartInfo(arguments),
                EnableRaisingEvents = true
            };

            currentProcess = process;

            lock (_processLock)
            {
                _activeProcesses.Add(process);
            }

            var errorLog = new StringBuilder();

            try
            {
                token.ThrowIfCancellationRequested();

                if (!process.Start())
                {
                    throw new InvalidOperationException("No se pudo iniciar yt-dlp.");
                }

                // Si se cancela, detener yt-dlp y sus procesos secundarios.
                using var registration = token.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(entireProcessTree: true);
                        }
                    }
                    catch
                    {
                        // El proceso puede haber terminado.
                    }
                });

                // Leer ambas salidas simultáneamente.
                Task stdoutTask = ReadDownloadOutputAsync(process.StandardOutput, progress, null, token);

                Task stderrTask = ReadDownloadOutputAsync(process.StandardError, progress, errorLog, token);

                await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync(token));

                token.ThrowIfCancellationRequested();

                // Comprobar que yt-dlp realmente ha terminado correctamente.
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"La descarga ha fallado. Código de salida: {process.ExitCode}\n" +
                        errorLog);
                }

                // Solo comunicamos que ha terminado cuando el proceso
                // ha finalizado correctamente.
                progress?.Report(Strings.CompletedDownload);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error descargando: {ex}");

                progress?.Report($"Error: {ex.Message}");

                throw;
            }
            finally
            {
                lock (_processLock)
                {
                    _activeProcesses.Remove(process);
                }

                if (ReferenceEquals(currentProcess, process))
                {
                    currentProcess = null;
                }
            }
        }

        private async Task ReadDownloadOutputAsync(StreamReader reader, IProgress<string>? progress, StringBuilder? errorLog, CancellationToken token)
        {
            string? line;

            while ((line = await reader.ReadLineAsync(token)) != null)
            {
                token.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                // Registrar errores para poder mostrarlos si yt-dlp falla.
                if (errorLog != null && (line.Contains("ERROR", StringComparison.OrdinalIgnoreCase)))
                {
                    errorLog.AppendLine(line);
                }

                // Interpretar el progreso independientemente
                // de si viene de stdout o stderr.
                string parsed = ParseProgress(line);

                if (!string.IsNullOrWhiteSpace(parsed))
                {
                    progress?.Report(parsed);
                }
            }
        }


        public Task DownloadAsync(string url, string output, string format, string? formatID = null)
        {
            return DownloadAsync(url, output, format, null!, CancellationToken.None, formatID);
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
            {
                return string.Empty;
            }

            if (line.Contains("[download]"))
            {
                var match = Regex.Match(line,
                    @"(\d{1,3}(?:[\.,]\d+)?)%.*?(?:at\s+([\d\.,]+\s*\w+/s))?.*?(?:ETA\s+([\d:]+))?"
                );

                if (match.Success)
                {
                    string percent = match.Groups[1].Value;
                    string speed = match.Groups[2].Value;

                    if (!string.IsNullOrWhiteSpace(speed))
                    {
                        return $"{Strings.Downloading}... {percent}% ({speed})";
                    }

                    return $"{Strings.Downloading}... {percent}%";
                }

                return $"{Strings.Downloading}...";
            }

            if (line.Contains("[Merger]", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Merging formats", StringComparison.OrdinalIgnoreCase))
            {
                return Strings.MergingAudioVideo;
            }

            if (line.Contains("[ExtractAudio]", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Extracting audio", StringComparison.OrdinalIgnoreCase))
            {
                return Strings.ExtractingAudio;
            }

            if (line.Contains("[Metadata]", StringComparison.OrdinalIgnoreCase))
            {
                return Strings.GettingInfo;
            }

            return string.Empty;
        }

        public async Task<List<FormatOption>> FetchFormatsAsync(string url, CancellationToken ct = default)
        {
            #region Version 3

            var arguments = new List<string>();

            if (IsPlaylist(url))
            {
                arguments.AddRange(new[]
                {
                    "-J",
                    "--playlist-items", "1",
                    url
                });
            }
            else
            {
                arguments.AddRange(new[]
                {
                    "-J",
                    url
                });
            }

            var raw = await Task.Run(() => RunAndCaptureAsync(arguments, ct), ct);

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
                {
                    return new List<FormatOption>();
                }
                target = first;
            }

            if (!target.TryGetProperty("formats", out var formats))
            {
                return new List<FormatOption>();
            }

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

                int height = f.TryGetProperty("height", out var h) && h.ValueKind == JsonValueKind.Number
                    ? (int)h.GetDouble() : 0;
                var tbr = f.TryGetProperty("tbr", out var tb) && tb.ValueKind == JsonValueKind.Number
                    ? (int)tb.GetDouble() : 0;
                var abr = f.TryGetProperty("abr", out var ab) && ab.ValueKind == JsonValueKind.Number
                    ? (int)ab.GetDouble() : 0;
                var fps = f.TryGetProperty("fps", out var fp) && fp.ValueKind == JsonValueKind.Number
                    ? (int)Math.Round(fp.GetDouble()) : 0;

                int displayHeight = NormalizeResolution(height);
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
                            ? $"Video {ext.ToUpper()} — {displayHeight}p{(fps >= 60 ? $" {fps}fps" : "")}"
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
                    .GroupBy(v => NormalizeResolution(v.ResolutionInt)) // una entrada por resolución
                    .Select(g => g.OrderByDescending(v => v.Tbr).First()) // mejor tbr por resolución
                    .OrderByDescending(v => NormalizeResolution(v.ResolutionInt))
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
            var arguments = new List<string>
            {
                "--no-warnings",
                "--flat-playlist",
                "-J",
                url
            };

            var raw = await RunAndCaptureAsync(arguments, ct);

            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new Exception("yt-dlp no devolvió ningún contenido.");
            }

            // Localizar el inicio del JSON.
            int jsonStart = FindJsonStart(raw);

            if (jsonStart < 0)
            {
                throw new Exception($"yt-dlp no devolvió JSON válido.\nRespuesta:\n{raw}");
            }

            // Extraer únicamente el JSON.
            string jsonOnly = raw.Substring(jsonStart).Trim();

            using var doc = JsonDocument.Parse(jsonOnly);

            var root = doc.RootElement;

            var results = new List<VideoInfo>();

            if (root.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in entries.EnumerateArray())
                {
                    ct.ThrowIfCancellationRequested();

                    var info = ParseEntry(entry);

                    if (string.IsNullOrWhiteSpace(info.Url))
                    {
                        continue;
                    }

                    if (info.Title == "[Private video]" || info.Title == "[Deleted video]")
                    {
                        continue;
                    }

                    results.Add(info);
                }
            }
            else
            {
                results.Add(ParseEntry(root));
            }

            return results;
        }


        private static int NormalizeResolution(int height)
        {
            if (height <= 0)
                return 0;

            int[] standardResolutions =
                {
                    144, 240, 360, 480, 540,
                    720, 1080, 1440, 2160, 4320
                };

            // Buscar la resolución estándar más cercana,
            // siempre que la diferencia no supere un margen.
            int closest = standardResolutions
                .OrderBy(r => Math.Abs(r - height))
                .First();

            return Math.Abs(closest - height) <= 30
                ? closest
                : height;
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

        #region Version 1
        //public async Task<string> RunAndCaptureAsync(string arguments, CancellationToken ct = default)
        //{
        //    var sb = new StringBuilder();

        //    using var process = new Process
        //    {
        //        StartInfo = new ProcessStartInfo
        //        {
        //            FileName = _path,
        //            Arguments = arguments,
        //            RedirectStandardOutput = true,
        //            RedirectStandardError = true,
        //            UseShellExecute = false,
        //            CreateNoWindow = true
        //        }
        //    };

        //    process.OutputDataReceived += (_, e) =>
        //    {
        //        if (e.Data != null)
        //            sb.AppendLine(e.Data);
        //    };

        //    process.ErrorDataReceived += (_, e) =>
        //    {
        //        if (e.Data != null)
        //            sb.AppendLine(e.Data);
        //    };

        //    process.Start();
        //    process.BeginOutputReadLine();
        //    process.BeginErrorReadLine();

        //    await process.WaitForExitAsync(ct);

        //    if (process.ExitCode != 0)
        //        throw new Exception($"yt-dlp error (code {process.ExitCode}):\n{sb}");

        //    return sb.ToString();
        //}


        public async Task<string> RunAndCaptureAsync(IEnumerable<string> arguments, CancellationToken ct = default)
        {
            using var process = new Process
            {
                StartInfo = CreateYtDlpStartInfo(arguments)
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("No se pudo iniciar yt-dlp.");
            }

            using var registration = ct.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // El proceso puede haber terminado.
                }
            });

            // Leer ambas salidas simultáneamente para evitar bloqueos.
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();

            Task<string> stderrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(ct);

            string stdout = await stdoutTask;
            string stderr = await stderrTask;

            ct.ThrowIfCancellationRequested();

            // Los errores reales se notifican, pero nunca se mezclan
            // con el JSON de StandardOutput.
            if (process.ExitCode != 0)
            {
                throw new Exception($"yt-dlp error ({process.ExitCode}):\n{stderr}");
            }

            // Las advertencias no deben contaminar el JSON.
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                Debug.WriteLine($"yt-dlp STDERR:\n{stderr}");
            }

            return stdout;
        }


        #endregion

        #endregion

        #region Args Constructor

        //private string[] BuildArguments(string url, string output, string format, string? formatId = null, string? targetExt = null)
        //{
        //    string[] commonArgs =
        //    [
        //        $"-o \"{output}/%(title)s.%(ext)s\"",
        //        $"--ffmpeg-location \"{_ffmpegPath}\"",
        //        "--newline",
        //        "--embed-metadata",
        //        "--embed-thumbnail"
        //    ];

        //    string[] formatArgs;

        //    if (formatId == "mp3")
        //    {
        //        formatArgs =
        //        [
        //            "-x",
        //            "--audio-format mp3",
        //            "--audio-quality 0",
        //];
        //    }
        //    else if (!string.IsNullOrEmpty(formatId))
        //    {
        //        // Determinar el contenedor de salida según la extensión del formato elegido
        //        string mergeExt = targetExt?.ToLowerInvariant() switch
        //        {
        //            "webm" => "webm",
        //            "mkv" => "mkv",
        //            _ => "mp4"   // mp4 por defecto para cualquier otro
        //        };

        //        // El audio compatible según el contenedor
        //        string audioSelector = mergeExt == "webm"
        //            ? $"{formatId}+bestaudio[ext=webm]/bestaudio"
        //            : $"{formatId}+bestaudio[ext=m4a]/bestaudio";

        //        // Códec de audio compatible con el contenedor
        //        string audioCodec = mergeExt == "webm"
        //            ? "ffmpeg:-c:a libopus -b:a 192k"
        //            : "ffmpeg:-c:a aac -b:a 192k";

        //        formatArgs =
        //        [
        //            $"-f \"{audioSelector}\"",
        //            $"--merge-output-format {mergeExt}",
        //            $"--postprocessor-args \"{audioCodec}\"",
        //];
        //    }
        //    else if (format.Contains("Audio"))
        //    {
        //        formatArgs =
        //        [
        //            "-x",
        //            "--audio-format mp3",
        //            "--audio-quality 0",
        //];
        //    }
        //    else
        //    {
        //        formatArgs =
        //        [
        //            "-f \"bestvideo+bestaudio/best\"",
        //            "--merge-output-format mp4",
        //            "--postprocessor-args \"ffmpeg:-c:a aac -b:a 192k\"",
        //];
        //    }

        //    return [.. commonArgs, .. formatArgs, url];
        //}

        private List<string> BuildArguments(string url, string output, string format, string? formatId = null, string? targetExt = null)
        {
            var args = new List<string>
            {
                "-o",
                Path.Combine(output, "%(title)s.%(ext)s"),

                "--ffmpeg-location",
                _ffmpegPath,

                "--newline",
                "--embed-metadata",
                "--embed-thumbnail"
            };

            if (formatId == "mp3")
            {
                args.AddRange(new[]
                {
                    "-x",
                    "--audio-format", "mp3",
                    "--audio-quality", "0"
                });
            }
            else if (!string.IsNullOrEmpty(formatId))
            {
                string mergeExt = targetExt?.ToLowerInvariant() switch
                {
                    "webm" => "webm",
                    "mkv" => "mkv",
                    _ => "mp4"
                };

                string audioSelector = mergeExt == "webm"
                    ? $"{formatId}+bestaudio[ext=webm]/bestaudio"
                    : $"{formatId}+bestaudio[ext=m4a]/bestaudio";

                string audioCodec = mergeExt == "webm"
                    ? "ffmpeg:-c:a libopus -b:a 192k"
                    : "ffmpeg:-c:a aac -b:a 192k";

                args.AddRange(new[]
                {
                    "-f", audioSelector,
                    "--merge-output-format", mergeExt,
                    "--postprocessor-args", audioCodec
                });
            }
            else if (format.Contains("Audio", StringComparison.OrdinalIgnoreCase))
            {
                args.AddRange(new[]
                {
                    "-x",
                    "--audio-format", "mp3",
                    "--audio-quality", "0"
                });
            }
            else
            {
                args.AddRange(new[]
                {
                    "-f", "bestvideo+bestaudio/best",
                    "--merge-output-format", "mp4",
                    "--postprocessor-args",
                    "ffmpeg:-c:a aac -b:a 192k"
                });
            }

            args.Add(url);

            return args;
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
                string assetName = GetYtDlpAssetName();
                var assets = doc.RootElement.GetProperty("assets");

                var asset = assets.EnumerateArray().FirstOrDefault(a => a.GetProperty("name").GetString() == assetName);

                if (asset.ValueKind == JsonValueKind.Undefined)
                {
                    throw new FileNotFoundException($"No se encontró {assetName}.");
                }

                string? downloadUrl = asset.GetProperty("browser_download_url").GetString();

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
                // Cerrar el archivo descargado
                fs.Close();

                // Dar permisos de ejecución en Mac Catalyst
                if (OperatingSystem.IsMacCatalyst())
                {
                    File.SetUnixFileMode(
                        tempPath,
                        UnixFileMode.UserRead |
                        UnixFileMode.UserWrite |
                        UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead |
                        UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead |
                        UnixFileMode.OtherExecute);
                }

                // Sustituir el ejecutable anterior
                File.Move(tempPath, _path, overwrite: true);

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

