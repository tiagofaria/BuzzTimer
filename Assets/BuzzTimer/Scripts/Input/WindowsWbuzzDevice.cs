#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using HidSharp;

namespace Buzz.Input
{
    /// <summary>HID transport for the Sony Wireless Buzz receiver (VID 054C, PID 1000).</summary>
    internal sealed class WindowsWbuzzDevice : IDisposable
    {
        private const int VendorId = 0x054c;
        private const int ProductId = 0x1000;

        private readonly object writeLock = new object();
        private readonly HidDevice device;
        private HidStream stream;
        private Thread readThread;
        private volatile bool running;

        private WindowsWbuzzDevice(HidDevice device, HidStream stream)
        {
            this.device = device;
            this.stream = stream;
            stream.ReadTimeout = 250;
            stream.WriteTimeout = 1000;
        }

        public event Action<byte[]> ReportReceived;
        public event Action<string> Disconnected;

        public string ProductName
        {
            get
            {
                try { return device.GetProductName(); }
                catch { return "Wbuzz"; }
            }
        }

        public static WindowsWbuzzDevice Open()
        {
            var matches = new List<HidDevice>(DeviceList.Local.GetHidDevices(VendorId, ProductId));
            if (matches.Count == 0)
                throw new InvalidOperationException("Recetor Wbuzz (VID 054C / PID 1000) não encontrado.");

            foreach (var match in matches)
            {
                if (match.TryOpen(out var openedStream))
                    return new WindowsWbuzzDevice(match, openedStream);
            }

            throw new IOException($"O Wbuzz foi encontrado ({matches.Count} interface(s)), mas não foi possível abrir a ligação HID.");
        }

        public void InitialiseWirelessReceiver()
        {
            // The wireless model stays visible to Windows but sends no button data until
            // this zero output report is written. The one-byte write is intentional:
            // HidSharp expands it to the report size advertised by this receiver.
            lock (writeLock)
                stream.Write(new byte[] { 0x00 }, 0, 1);
        }

        public void StartReading()
        {
            if (running)
                return;

            running = true;
            readThread = new Thread(ReadLoop) { IsBackground = true, Name = "Wbuzz HID reader" };
            readThread.Start();
        }

        /// <summary>
        /// Stops the HID reader cooperatively and waits for its bounded read timeout.
        /// This must happen before closing the stream; disposing a stream while HidSharp
        /// is inside Read can leave Mono waiting for the reader thread during shutdown.
        /// </summary>
        public bool StopReading(int timeoutMilliseconds = 1000)
        {
            running = false;
            var thread = readThread;
            if (thread == null || thread == Thread.CurrentThread)
                return true;

            var stopped = thread.Join(timeoutMilliseconds);
            if (stopped)
                readThread = null;
            return stopped;
        }

        public void SetLeds(bool player1, bool player2, bool player3, bool player4)
        {
            var length = Math.Max(6, device.GetMaxOutputReportLength());
            var report = new byte[length];
            report[2] = player1 ? (byte)0xff : (byte)0x00;
            report[3] = player2 ? (byte)0xff : (byte)0x00;
            report[4] = player3 ? (byte)0xff : (byte)0x00;
            report[5] = player4 ? (byte)0xff : (byte)0x00;

            lock (writeLock)
                stream.Write(report, 0, report.Length);
        }

        private void ReadLoop()
        {
            var reportLength = Math.Max(5, device.GetMaxInputReportLength());
            var buffer = new byte[reportLength];

            while (running)
            {
                try
                {
                    var read = stream.Read(buffer, 0, buffer.Length);
                    if (read < 5)
                        continue;

                    var copy = new byte[read];
                    Buffer.BlockCopy(buffer, 0, copy, 0, read);
                    ReportReceived?.Invoke(copy);
                }
                catch (TimeoutException)
                {
                    // No button changed during this interval.
                }
                catch (Exception exception)
                {
                    if (running)
                        Disconnected?.Invoke(exception.Message);
                    break;
                }
            }
        }

        public void Dispose()
        {
            StopReading();
            var oldStream = stream;
            stream = null;
            oldStream?.Dispose();
        }
    }
}
#endif
