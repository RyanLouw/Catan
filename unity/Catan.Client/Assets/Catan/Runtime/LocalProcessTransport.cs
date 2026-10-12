#nullable disable
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Catan.Client
{
    /// <summary>Desktop/editor V1 transport. Replace IGameTransport with an authenticated online client for V2.</summary>
    public sealed class LocalProcessTransport : IGameTransport
    {
        private readonly Process process;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<HostResponse>> pending = new ConcurrentDictionary<string, TaskCompletionSource<HostResponse>>();
        private readonly object writeGate = new object();
        private bool disposed;
        public string LastDiagnostic { get; private set; }
        public LocalProcessTransport(string executable, string arguments, string workingDirectory)
        {
            process = new Process { StartInfo = new ProcessStartInfo(executable, arguments)
            {
                WorkingDirectory = workingDirectory, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
            }};
            process.StartInfo.EnvironmentVariables["DOTNET_NOLOGO"] = "1";
            process.StartInfo.EnvironmentVariables["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            if (!process.Start()) throw new InvalidOperationException("Could not start the local engine.");
            process.StandardInput.AutoFlush = true;
            _ = ReadResponses(); _ = ReadDiagnostics();
        }
        public async Task<HostResponse> RequestAsync(HostRequest request)
        {
            if (disposed) throw new ObjectDisposedException(nameof(LocalProcessTransport));
            request.Id = Guid.NewGuid().ToString("N");
            var completion = new TaskCompletionSource<HostResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!pending.TryAdd(request.Id, completion)) throw new InvalidOperationException("Duplicate correlation ID.");
            try
            {
                lock (writeGate) process.StandardInput.WriteLine(JsonConvert.SerializeObject(request));
                if (await Task.WhenAny(completion.Task, Task.Delay(10_000)) != completion.Task)
                    throw new TimeoutException("Local engine did not reply. Check its executable and runtime. " + LastDiagnostic);
                return await completion.Task;
            }
            finally { pending.TryRemove(request.Id, out _); }
        }
        private async Task ReadResponses()
        {
            Exception failure = new IOException("Local engine exited. " + LastDiagnostic);
            try
            {
                string line;
                while ((line = await process.StandardOutput.ReadLineAsync()) != null)
                {
                    var response = JsonConvert.DeserializeObject<HostResponse>(line);
                    if (response == null || response.Id == null) throw new IOException("Invalid local engine response.");
                    if (pending.TryGetValue(response.Id, out var completion)) completion.TrySetResult(response);
                }
            }
            catch (Exception e) { failure = e; }
            finally { foreach (var item in pending.Values) item.TrySetException(failure); }
        }
        private async Task ReadDiagnostics()
        {
            try { string line; while ((line = await process.StandardError.ReadLineAsync()) != null) LastDiagnostic = line; }
            catch (ObjectDisposedException) { }
            catch (IOException) { }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var completion in pending.Values) completion.TrySetException(new ObjectDisposedException(nameof(LocalProcessTransport)));
            try { process.StandardInput.Close(); if (!process.WaitForExit(500)) { process.Kill(); process.WaitForExit(500); } }
            finally { process.Dispose(); }
        }
    }
}
