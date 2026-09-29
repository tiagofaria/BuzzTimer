using System;
using System.Threading;
using UnityEngine;

namespace Buzz.Input
{
    /// <summary>Game-facing API for four Buzz controllers with five buttons each.</summary>
    public sealed class BuzzInputManager : MonoBehaviour
    {
        public static BuzzInputManager Instance { get; private set; }

        public event Action<int, BuzzButton> ButtonPressed;
        public event Action<int, BuzzButton> ButtonReleased;

        public string Status { get; private set; } = "A iniciar…";
        public bool IsConnected { get; private set; }
        public string LastReportHex { get; private set; } = "—";
        public long ReportCount => Interlocked.Read(ref reportCount);

        private readonly bool[] buttons = new bool[20];
        private readonly bool[] pendingButtons = new bool[20];
        private readonly object stateLock = new object();
        private bool hasPendingReport;
        private long reportCount;
        private float nextReconnectAt = float.PositiveInfinity;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private WindowsWbuzzDevice device;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance == null)
                new GameObject("Buzz Input Manager").AddComponent<BuzzInputManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Connect();
        }

        public bool GetButton(int player, BuzzButton button)
        {
            if (player < 1 || player > 4)
                throw new ArgumentOutOfRangeException(nameof(player));
            return buttons[(player - 1) * 5 + (int)button];
        }

        public void Connect()
        {
            Disconnect();
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                Status = "A abrir o recetor Wbuzz…";
                device = WindowsWbuzzDevice.Open();
                device.ReportReceived += OnReportReceived;
                device.Disconnected += OnDeviceDisconnected;

                // The receiver enumerates normally but stays silent until this handshake.
                device.InitialiseWirelessReceiver();
                device.StartReading();
                IsConnected = true;
                nextReconnectAt = float.PositiveInfinity;
                Status = device.ProductName + " inicializado. Carrega num botão.";
            }
            catch (Exception exception)
            {
                IsConnected = false;
                Status = exception.Message + " Nova tentativa automática…";
                nextReconnectAt = Time.unscaledTime + 1.5f;
                Debug.LogWarning(exception);
            }
#else
            Status = "O teste Wbuzz está atualmente preparado para Windows.";
#endif
        }

        public void SetPlayerLeds(bool player1, bool player2, bool player3, bool player4)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                device?.SetLeds(player1, player2, player3, player4);
            }
            catch (Exception exception)
            {
                Status = exception.Message;
            }
#endif
        }

        private void Update()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (!IsConnected && Time.unscaledTime >= nextReconnectAt)
            {
                nextReconnectAt = float.PositiveInfinity;
                Connect();
            }
#endif

            lock (stateLock)
            {
                if (!hasPendingReport)
                    return;

                hasPendingReport = false;
                for (var i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i] == pendingButtons[i])
                        continue;

                    buttons[i] = pendingButtons[i];
                    var player = i / 5 + 1;
                    var button = (BuzzButton)(i % 5);
                    if (buttons[i])
                        ButtonPressed?.Invoke(player, button);
                    else
                        ButtonReleased?.Invoke(player, button);
                }
            }
        }

        private void OnReportReceived(byte[] report)
        {
            lock (stateLock)
            {
                BuzzReportParser.TryParse(report, pendingButtons);
                LastReportHex = BitConverter.ToString(report);
                Interlocked.Increment(ref reportCount);
                hasPendingReport = true;
            }
        }

        private void OnDeviceDisconnected(string reason)
        {
            IsConnected = false;
            Status = "Ligação perdida: " + reason;
            nextReconnectAt = 0f;
        }

        private void Disconnect()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (device != null)
            {
                // Let the timed HID read finish before writing or disposing the stream.
                // Closing HidSharp while its reader thread is active can hang Unity/Mono.
                device.StopReading();
                try
                {
                    device.SetLeds(false, false, false, false);
                }
                catch
                {
                    // The receiver may already have been removed; disposal must still continue.
                }
                device.ReportReceived -= OnReportReceived;
                device.Disconnected -= OnDeviceDisconnected;
                device.Dispose();
                device = null;
            }
#endif
            IsConnected = false;
        }

        private void OnDestroy()
        {
            Disconnect();
            if (Instance == this)
                Instance = null;
        }

        private void OnApplicationQuit() => Disconnect();
    }
}
