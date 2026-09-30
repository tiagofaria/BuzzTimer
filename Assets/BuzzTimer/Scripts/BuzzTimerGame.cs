using System.Collections.Generic;
using Buzz.Input;
using UnityEngine;

namespace BuzzTimer.Game
{
    public sealed class BuzzTimerGame : MonoBehaviour
    {
        private static readonly float[] VictoryNotes = { 523.25f, 659.25f, 783.99f, 1046.50f, 783.99f, 880f, 987.77f, 1046.50f };

        private enum GameState { PlayerCount, PlayerNames, Binding, Ready, TurnReady, Timing, RoundResult, GameOver }
        private enum Language { Portuguese, English }

        private sealed class PlayerProfile
        {
            public string Name = string.Empty;
            public int Buzzer;
        }

        private readonly PlayerProfile[] players = new PlayerProfile[4];
        private readonly List<int> activePlayers = new List<int>();
        private GameState state = GameState.PlayerCount;
        private Language language = Language.Portuguese;
        private int selectedPlayerCount = 2;
        private int bindingIndex;
        private string bindingMessage = string.Empty;
        private int totalRounds;
        private int roundNumber;
        private int currentIndex;
        private int previousPlayer = -1;
        private int eliminatedPlayer;
        private int pendingNextStarter;
        private int lastChallenger;
        private int lastChallenged;
        private bool lastChallengeExceeded;
        private float targetTime;
        private float elapsedTime;
        private float nextStateAt;
        private float inputEnabledAt;
        private float ledOffAt = -1f;

        private AudioSource audioSource;
        private AudioClip startSound;
        private AudioClip stopSound;
        private AudioClip challengeSound;
        private AudioClip victorySound;

        private GUIStyle titleStyle, headingStyle, centerStyle, smallStyle, giantStyle;
        private GUIStyle panelStyle, buttonStyle, numberButtonStyle, selectedNumberButtonStyle, playerStyle, textFieldStyle;
        private Texture2D panelTexture, activePlayerTexture, inactivePlayerTexture, eliminatedTexture;
        private Texture2D portugalFlag, unitedKingdomFlag, selectedFrame, normalFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<BuzzTimerGame>() == null)
                new GameObject("Buzz Timer Game").AddComponent<BuzzTimerGame>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            for (var i = 0; i < players.Length; i++) players[i] = new PlayerProfile();
            InitialiseAudio();
        }

        private void Start() => SubscribeToInput();

        private void SubscribeToInput()
        {
            var input = BuzzInputManager.Instance;
            if (input == null)
            {
                Invoke(nameof(SubscribeToInput), 0.1f);
                return;
            }
            input.ButtonPressed -= OnButtonPressed;
            input.ButtonPressed += OnButtonPressed;
        }

        private void Update()
        {
            if (state == GameState.Timing) elapsedTime += Time.unscaledDeltaTime;
            if (state == GameState.RoundResult && Time.unscaledTime >= nextStateAt) BeginRound(pendingNextStarter);
            if (ledOffAt >= 0f && Time.unscaledTime >= ledOffAt)
            {
                ledOffAt = -1f;
                BuzzInputManager.Instance?.SetPlayerLeds(false, false, false, false);
            }
        }

        private void OnButtonPressed(int buzzer, BuzzButton button)
        {
            if (Time.unscaledTime < inputEnabledAt) return;

            if (state == GameState.Binding)
            {
                if (button == BuzzButton.Red) BindBuzzer(buzzer);
                return;
            }

            if (state != GameState.TurnReady && state != GameState.Timing) return;
            if (players[CurrentPlayer].Buzzer != buzzer) return;

            if (state == GameState.TurnReady)
            {
                if (button == BuzzButton.Green)
                {
                    PlaySound(startSound);
                    state = GameState.Timing;
                    inputEnabledAt = Time.unscaledTime + 0.15f;
                }
                else if (button == BuzzButton.Yellow && previousPlayer >= 0)
                {
                    PlaySound(challengeSound);
                    ResolveChallenge(CurrentPlayer, previousPlayer);
                }
            }
            else if (button == BuzzButton.Red)
            {
                PlaySound(stopSound);
                previousPlayer = CurrentPlayer;
                currentIndex = (currentIndex + 1) % activePlayers.Count;
                state = GameState.TurnReady;
                inputEnabledAt = Time.unscaledTime + 0.3f;
                FlashBuzzer(players[CurrentPlayer].Buzzer);
            }
        }

        private int CurrentPlayer => activePlayers[currentIndex];
        private string PlayerName(int index) => players[index].Name;
        private string T(string pt, string en) => language == Language.Portuguese ? pt : en;

        private void OpenNameSetup()
        {
            for (var i = 0; i < selectedPlayerCount; i++)
                if (string.IsNullOrWhiteSpace(players[i].Name)) players[i].Name = T("Jogador ", "Player ") + (i + 1);
            state = GameState.PlayerNames;
        }

        private void BeginBinding()
        {
            for (var i = 0; i < selectedPlayerCount; i++)
            {
                players[i].Name = players[i].Name.Trim();
                if (players[i].Name.Length == 0) players[i].Name = T("Jogador ", "Player ") + (i + 1);
                players[i].Buzzer = 0;
            }
            bindingIndex = 0;
            bindingMessage = string.Empty;
            state = GameState.Binding;
            inputEnabledAt = Time.unscaledTime + 0.4f;
            ClearLeds();
        }

        private void BindBuzzer(int buzzer)
        {
            for (var i = 0; i < bindingIndex; i++)
            {
                if (players[i].Buzzer != buzzer) continue;
                bindingMessage = T("Esse comando já pertence a ", "That controller is already assigned to ") + players[i].Name + ".";
                return;
            }

            players[bindingIndex].Buzzer = buzzer;
            FlashBuzzer(buzzer, 0.6f);
            bindingIndex++;
            bindingMessage = string.Empty;
            inputEnabledAt = Time.unscaledTime + 0.35f;
            if (bindingIndex >= selectedPlayerCount) state = GameState.Ready;
        }

        private void StartMatch()
        {
            activePlayers.Clear();
            for (var i = 0; i < selectedPlayerCount; i++) activePlayers.Add(i);
            totalRounds = selectedPlayerCount - 1;
            roundNumber = 0;
            BeginRound(0);
        }

        private void BeginRound(int startingPlayer)
        {
            roundNumber++;
            targetTime = Random.Range(3f, 10f);
            elapsedTime = 0f;
            previousPlayer = -1;
            currentIndex = Mathf.Max(0, activePlayers.IndexOf(startingPlayer));
            state = GameState.TurnReady;
            inputEnabledAt = Time.unscaledTime + 0.5f;
            FlashBuzzer(players[CurrentPlayer].Buzzer);
        }

        private void ResolveChallenge(int challenger, int challenged)
        {
            lastChallenger = challenger;
            lastChallenged = challenged;
            lastChallengeExceeded = elapsedTime > targetTime;
            eliminatedPlayer = lastChallengeExceeded ? challenged : challenger;
            var eliminatedIndex = activePlayers.IndexOf(eliminatedPlayer);
            activePlayers.RemoveAt(eliminatedIndex);
            inputEnabledAt = float.PositiveInfinity;

            if (activePlayers.Count == 1)
            {
                state = GameState.GameOver;
                Invoke(nameof(PlayVictorySound), 0.82f);
                FlashBuzzer(players[activePlayers[0]].Buzzer, 2f);
                return;
            }

            pendingNextStarter = activePlayers[eliminatedIndex % activePlayers.Count];
            state = GameState.RoundResult;
            nextStateAt = Time.unscaledTime + 4f;
            ClearLeds();
        }

        private void FlashBuzzer(int buzzer, float duration = 0.8f)
        {
            BuzzInputManager.Instance?.SetPlayerLeds(buzzer == 1, buzzer == 2, buzzer == 3, buzzer == 4);
            ledOffAt = Time.unscaledTime + duration;
        }

        private void ClearLeds()
        {
            BuzzInputManager.Instance?.SetPlayerLeds(false, false, false, false);
            ledOffAt = -1f;
        }

        private void InitialiseAudio()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;

            startSound = CreateStartSound();
            stopSound = CreateStopSound();
            challengeSound = CreateChallengeSound();
            victorySound = CreateVictorySound();
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioSource == null || clip == null) return;
            audioSource.Stop();
            audioSource.PlayOneShot(clip);
        }

        private void PlayVictorySound() => PlaySound(victorySound);

        private static AudioClip CreateStartSound()
        {
            return CreateSynthClip("Timer Start", 0.48f, (time, duration) =>
            {
                var frequency = time < 0.20f ? 660f : time < 0.36f ? 880f : 1100f;
                return Sine(time, frequency) * Envelope(time, duration, 0.012f, 0.15f) * 0.38f;
            });
        }

        private static AudioClip CreateStopSound()
        {
            return CreateSynthClip("Timer Stop", 0.62f, (time, duration) =>
            {
                var wobble = 1f + 0.035f * Mathf.Sin(2f * Mathf.PI * 28f * time);
                var tone = 0.70f * Sine(time, 145f * wobble) + 0.30f * Sine(time, 290f * wobble);
                return tone * Envelope(time, duration, 0.006f, 0.12f) * 0.42f;
            });
        }

        private static AudioClip CreateChallengeSound()
        {
            return CreateSynthClip("Challenge", 0.82f, (time, duration) =>
            {
                var frequency = time < 0.18f ? 440f : time < 0.36f ? 554.37f : time < 0.54f ? 659.25f : 880f;
                var pulse = 0.72f + 0.28f * Mathf.Sin(2f * Mathf.PI * 9f * time);
                return (0.82f * Sine(time, frequency) + 0.18f * Sine(time, frequency * 2f))
                    * pulse * Envelope(time, duration, 0.008f, 0.16f) * 0.34f;
            });
        }

        private static AudioClip CreateVictorySound()
        {
            return CreateSynthClip("Victory", 2.35f, (time, duration) =>
            {
                var beat = Mathf.Min(7, Mathf.FloorToInt(time / 0.25f));
                var localTime = time - beat * 0.25f;
                var noteLength = beat == 7 ? 0.60f : 0.23f;
                var noteEnvelope = Envelope(localTime, noteLength, 0.008f, beat == 7 ? 0.42f : 0.10f);
                var frequency = VictoryNotes[beat];
                var chord = Sine(time, frequency) + 0.32f * Sine(time, frequency * 1.5f) + 0.18f * Sine(time, frequency * 2f);
                return chord * noteEnvelope * Envelope(time, duration, 0.01f, 0.25f) * 0.25f;
            });
        }

        private static AudioClip CreateSynthClip(string name, float duration, System.Func<float, float, float> sample)
        {
            const int sampleRate = 44100;
            var samples = Mathf.CeilToInt(duration * sampleRate);
            var data = new float[samples];
            for (var i = 0; i < samples; i++) data[i] = Mathf.Clamp(sample(i / (float)sampleRate, duration), -1f, 1f);
            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Sine(float time, float frequency) => Mathf.Sin(2f * Mathf.PI * frequency * time);

        private static float Envelope(float time, float duration, float attack, float release)
        {
            var attackGain = Mathf.Clamp01(time / attack);
            var releaseGain = Mathf.Clamp01((duration - time) / release);
            return attackGain * releaseGain;
        }

        private void ChangePlayers()
        {
            activePlayers.Clear();
            state = GameState.PlayerCount;
            inputEnabledAt = Time.unscaledTime + 0.3f;
            ClearLeds();
        }

        private void OnGUI()
        {
            EnsureStyles();
            const float width = 1280f, height = 720f;
            var scale = Mathf.Min(Screen.width / width, Screen.height / height);
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - width * scale) * 0.5f, (Screen.height - height * scale) * 0.5f, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            GUI.backgroundColor = new Color(0.035f, 0.05f, 0.09f);
            GUI.Box(new Rect(0, 0, width, height), GUIContent.none);
            GUI.backgroundColor = Color.white;

            DrawLanguageButtons();
            switch (state)
            {
                case GameState.PlayerCount: DrawPlayerCount(); break;
                case GameState.PlayerNames: DrawPlayerNames(); break;
                case GameState.Binding: DrawBinding(); break;
                case GameState.Ready: DrawReady(); break;
                default: DrawGame(); break;
            }
            DrawConnectionStatus();
            GUI.matrix = oldMatrix;
        }

        private void DrawHeader(string subtitle)
        {
            GUI.Label(new Rect(0, 42, 1280, 66), "BUZZ TIMER", giantStyle);
            GUI.Label(new Rect(0, 108, 1280, 34), subtitle, centerStyle);
        }

        private void DrawLanguageButtons()
        {
            DrawFlag(new Rect(1110, 28, 58, 40), portugalFlag, Language.Portuguese, "Português");
            DrawFlag(new Rect(1180, 28, 58, 40), unitedKingdomFlag, Language.English, "English");
        }

        private void DrawFlag(Rect rect, Texture2D flag, Language target, string tooltip)
        {
            GUI.DrawTexture(rect, language == target ? selectedFrame : normalFrame);
            GUI.DrawTexture(new Rect(rect.x + 3, rect.y + 3, rect.width - 6, rect.height - 6), flag, ScaleMode.StretchToFill);
            if (GUI.Button(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none)) language = target;
        }

        private void DrawPlayerCount()
        {
            DrawHeader(T("Arrisca, passa o tempo e desafia antes que seja tarde.", "Take a risk, pass the timer and challenge before it is too late."));
            GUI.Box(new Rect(260, 180, 760, 420), GUIContent.none, panelStyle);
            GUI.Label(new Rect(0, 215, 1280, 42), T("QUANTOS JOGADORES?", "HOW MANY PLAYERS?"), headingStyle);
            for (var count = 2; count <= 4; count++)
            {
                var style = selectedPlayerCount == count ? selectedNumberButtonStyle : numberButtonStyle;
                var label = selectedPlayerCount == count ? count + "  ✓" : count.ToString();
                if (GUI.Button(new Rect(405 + (count - 2) * 165, 290, 140, 90), label, style)) selectedPlayerCount = count;
            }
            GUI.backgroundColor = new Color(0.86f, 0.14f, 0.20f);
            if (GUI.Button(new Rect(460, 445, 360, 72), T("CONTINUAR", "CONTINUE"), buttonStyle)) OpenNameSetup();
            GUI.backgroundColor = Color.white;
            GUI.Label(new Rect(0, 535, 1280, 30), T(
                $"{selectedPlayerCount - 1} ronda{(selectedPlayerCount - 1 == 1 ? string.Empty : "s")} • um jogador eliminado por ronda",
                $"{selectedPlayerCount - 1} round{(selectedPlayerCount - 1 == 1 ? string.Empty : "s")} • one player eliminated per round"), centerStyle);
        }

        private void DrawPlayerNames()
        {
            DrawHeader(T("Escreve o nome de cada jogador.", "Enter each player's name."));
            GUI.Box(new Rect(300, 165, 680, 455), GUIContent.none, panelStyle);
            GUI.Label(new Rect(0, 190, 1280, 42), T("NOMES DOS JOGADORES", "PLAYER NAMES"), headingStyle);
            for (var i = 0; i < selectedPlayerCount; i++)
            {
                var y = 260 + i * 68;
                GUI.Label(new Rect(360, y, 190, 46), T("JOGADOR ", "PLAYER ") + (i + 1), titleStyle);
                var value = GUI.TextField(new Rect(550, y, 365, 46), players[i].Name, 16, textFieldStyle);
                players[i].Name = value;
            }
            GUI.backgroundColor = new Color(0.16f, 0.30f, 0.62f);
            if (GUI.Button(new Rect(340, 535, 245, 60), T("VOLTAR", "BACK"), buttonStyle)) state = GameState.PlayerCount;
            GUI.backgroundColor = new Color(0.10f, 0.55f, 0.28f);
            if (GUI.Button(new Rect(610, 535, 330, 60), T("ASSOCIAR COMANDOS", "ASSIGN CONTROLLERS"), buttonStyle)) BeginBinding();
            GUI.backgroundColor = Color.white;
        }

        private void DrawBinding()
        {
            DrawHeader(T("Associa cada pessoa ao comando que tem na mão.", "Assign each person to the controller they are holding."));
            GUI.Box(new Rect(260, 175, 760, 430), GUIContent.none, panelStyle);
            GUI.Label(new Rect(0, 215, 1280, 38), T("AGORA:", "NOW:"), centerStyle);
            GUI.Label(new Rect(0, 258, 1280, 66), players[bindingIndex].Name, giantStyle);
            GUI.Label(new Rect(0, 345, 1280, 42), T("CARREGA NO BOTÃO BUZZ", "PRESS THE BUZZ BUTTON"), headingStyle);
            GUI.Label(new Rect(0, 400, 1280, 32), T("Usa o botão vermelho grande do teu comando.", "Use the large red button on your controller."), centerStyle);
            GUI.color = new Color(1f, 0.64f, 0.35f);
            GUI.Label(new Rect(0, 445, 1280, 32), bindingMessage, centerStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, 500, 1280, 30), T("Associados: ", "Assigned: ") + bindingIndex + " / " + selectedPlayerCount, centerStyle);
            GUI.backgroundColor = new Color(0.20f, 0.26f, 0.40f);
            if (GUI.Button(new Rect(500, 545, 280, 48), T("CANCELAR", "CANCEL"), buttonStyle)) state = GameState.PlayerNames;
            GUI.backgroundColor = Color.white;
        }

        private void DrawReady()
        {
            DrawHeader(T("Jogadores e comandos prontos.", "Players and controllers are ready."));
            GUI.Box(new Rect(300, 165, 680, 455), GUIContent.none, panelStyle);
            GUI.Label(new Rect(0, 195, 1280, 42), T("TUDO PRONTO", "READY TO PLAY"), headingStyle);
            for (var i = 0; i < selectedPlayerCount; i++)
                GUI.Label(new Rect(0, 255 + i * 52, 1280, 38), $"{players[i].Name}  •  {T("Comando ", "Controller ")}{players[i].Buzzer}", centerStyle);
            GUI.backgroundColor = new Color(0.20f, 0.26f, 0.40f);
            if (GUI.Button(new Rect(340, 535, 270, 60), T("REASSOCIAR", "ASSIGN AGAIN"), buttonStyle)) BeginBinding();
            GUI.backgroundColor = new Color(0.86f, 0.14f, 0.20f);
            if (GUI.Button(new Rect(635, 535, 305, 60), T("COMEÇAR JOGO", "START GAME"), buttonStyle)) StartMatch();
            GUI.backgroundColor = Color.white;
        }

        private void DrawGame()
        {
            GUI.Label(new Rect(42, 20, 330, 42), "BUZZ TIMER", titleStyle);
            GUI.Label(new Rect(790, 24, 285, 34), T("RONDA ", "ROUND ") + roundNumber + " / " + totalRounds, headingStyle);
            DrawPlayerStrip();
            if (state == GameState.RoundResult) DrawRoundResult();
            else if (state == GameState.GameOver) DrawGameOver();
            else DrawTurn();
        }

        private void DrawPlayerStrip()
        {
            var stripWidth = selectedPlayerCount * 205f + (selectedPlayerCount - 1) * 40f;
            var startX = (1280f - stripWidth) * 0.5f;
            for (var i = 0; i < selectedPlayerCount; i++)
            {
                var active = activePlayers.Contains(i);
                var current = active && (state == GameState.TurnReady || state == GameState.Timing) && i == CurrentPlayer;
                var style = new GUIStyle(playerStyle);
                style.normal.background = !active ? eliminatedTexture : current ? activePlayerTexture : inactivePlayerTexture;
                style.normal.textColor = !active ? new Color(0.55f, 0.58f, 0.65f) : Color.white;
                GUI.Box(new Rect(startX + i * 245, 82, 205, 64), active ? players[i].Name : players[i].Name + "  ✕", style);
            }
        }

        private void DrawTurn()
        {
            GUI.Label(new Rect(0, 170, 1280, 34), T("TEMPO-ALVO", "TARGET TIME"), centerStyle);
            GUI.Label(new Rect(0, 202, 1280, 100), $"{targetTime:0.000} s", giantStyle);
            GUI.Box(new Rect(310, 320, 660, 285), GUIContent.none, panelStyle);
            GUI.Label(new Rect(0, 345, 1280, 52), T("VEZ DE ", "TURN: ") + PlayerName(CurrentPlayer), headingStyle);

            if (state == GameState.Timing)
            {
                GUI.Label(new Rect(0, 410, 1280, 50), T("CRONÓMETRO A CONTAR…", "TIMER RUNNING…"), headingStyle);
                DrawAction(new Rect(465, 490, 350, 72), new Color(0.90f, 0.12f, 0.16f), T("VERMELHO", "RED"), T("PARAR E PASSAR", "STOP AND PASS"));
            }
            else if (previousPlayer < 0)
            {
                GUI.Label(new Rect(0, 405, 1280, 42), T("Inicia o tempo oculto quando estiveres pronto.", "Start the hidden timer when you are ready."), centerStyle);
                DrawAction(new Rect(465, 490, 350, 72), new Color(0.16f, 0.75f, 0.34f), T("VERDE", "GREEN"), T("INICIAR", "START"));
            }
            else
            {
                GUI.Label(new Rect(0, 395, 1280, 32), PlayerName(previousPlayer) + T(" parou o cronómetro.", " stopped the timer."), centerStyle);
                GUI.Label(new Rect(0, 430, 1280, 32), T("Continuas ou achas que o tempo já foi ultrapassado?", "Continue or challenge the previous player?"), centerStyle);
                DrawAction(new Rect(345, 500, 280, 72), new Color(0.16f, 0.75f, 0.34f), T("VERDE", "GREEN"), T("CONTINUAR", "CONTINUE"));
                DrawAction(new Rect(655, 500, 280, 72), new Color(0.98f, 0.82f, 0.10f), T("AMARELO", "YELLOW"), T("DESAFIAR", "CHALLENGE"));
            }
            GUI.Label(new Rect(0, 625, 1280, 28), T("O tempo acumulado está oculto.", "The accumulated time is hidden."), smallStyle);
        }

        private void DrawRoundResult()
        {
            GUI.Box(new Rect(245, 190, 790, 405), GUIContent.none, panelStyle);
            GUI.Label(new Rect(0, 225, 1280, 55), lastChallengeExceeded ? T("O TEMPO FOI ULTRAPASSADO!", "TIME EXCEEDED!") : T("DESAFIO DEMASIADO CEDO!", "CHALLENGED TOO EARLY!"), headingStyle);
            GUI.Label(new Rect(0, 315, 1280, 55), $"{T("TEMPO", "TIME")}: {elapsedTime:0.000} s  /  {T("ALVO", "TARGET")}: {targetTime:0.000} s", centerStyle);
            var detail = lastChallengeExceeded
                ? PlayerName(lastChallenged) + T(" é eliminado.", " is eliminated.")
                : PlayerName(lastChallenger) + T(" é eliminado.", " is eliminated.");
            GUI.Label(new Rect(0, 385, 1280, 55), detail, headingStyle);
            GUI.Label(new Rect(0, 500, 1280, 35), T("A próxima ronda começa com ", "The next round starts with ") + PlayerName(pendingNextStarter) + "…", centerStyle);
        }

        private void DrawGameOver()
        {
            GUI.Box(new Rect(225, 175, 830, 455), GUIContent.none, panelStyle);
            GUI.Label(new Rect(0, 210, 1280, 55), T("VENCEDOR", "WINNER"), centerStyle);
            GUI.Label(new Rect(0, 270, 1280, 90), PlayerName(activePlayers[0]), giantStyle);
            GUI.Label(new Rect(0, 365, 1280, 38),
                $"{T("TEMPO FINAL", "FINAL TIME")}: {elapsedTime:0.000} s  /  {T("ALVO", "TARGET")}: {targetTime:0.000} s", centerStyle);
            GUI.Label(new Rect(0, 420, 1280, 34), T("Queres jogar novamente com os mesmos jogadores?", "Play again with the same players?"), centerStyle);
            GUI.backgroundColor = new Color(0.10f, 0.55f, 0.28f);
            if (GUI.Button(new Rect(315, 490, 315, 72), T("MESMOS JOGADORES", "SAME PLAYERS"), buttonStyle)) StartMatch();
            GUI.backgroundColor = new Color(0.20f, 0.32f, 0.62f);
            if (GUI.Button(new Rect(650, 490, 315, 72), T("ALTERAR JOGADORES", "CHANGE PLAYERS"), buttonStyle)) ChangePlayers();
            GUI.backgroundColor = Color.white;
        }

        private void DrawAction(Rect rect, Color color, string button, string action)
        {
            GUI.backgroundColor = color;
            GUI.Box(rect, button + "\n" + action, buttonStyle);
            GUI.backgroundColor = Color.white;
        }

        private void DrawConnectionStatus()
        {
            var input = BuzzInputManager.Instance;
            if (input == null) return;
            GUI.color = input.IsConnected ? new Color(0.45f, 1f, 0.62f) : new Color(1f, 0.55f, 0.45f);
            GUI.Label(new Rect(42, 680, 1190, 24), LocaliseStatus(input.Status), smallStyle);
            GUI.color = Color.white;
        }

        private string LocaliseStatus(string status)
        {
            if (language == Language.Portuguese) return status;
            return status.Replace("A iniciar…", "Starting…")
                .Replace("A abrir o recetor Wbuzz…", "Opening the Wbuzz receiver…")
                .Replace(" inicializado. Carrega num botão.", " initialised. Press a button.")
                .Replace("Recetor Wbuzz (VID 054C / PID 1000) não encontrado.", "Wbuzz receiver (VID 054C / PID 1000) not found.")
                .Replace(" Nova tentativa automática…", " Retrying automatically…")
                .Replace("Ligação perdida: ", "Connection lost: ");
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            panelTexture = MakeTexture(new Color(0.075f, 0.10f, 0.17f));
            activePlayerTexture = MakeTexture(new Color(0.10f, 0.55f, 0.28f));
            inactivePlayerTexture = MakeTexture(new Color(0.12f, 0.16f, 0.25f));
            eliminatedTexture = MakeTexture(new Color(0.075f, 0.08f, 0.11f));
            titleStyle = LabelStyle(27, TextAnchor.MiddleLeft, FontStyle.Bold);
            headingStyle = LabelStyle(29, TextAnchor.MiddleCenter, FontStyle.Bold);
            centerStyle = LabelStyle(20, TextAnchor.MiddleCenter, FontStyle.Normal);
            smallStyle = LabelStyle(16, TextAnchor.MiddleCenter, FontStyle.Normal);
            giantStyle = LabelStyle(54, TextAnchor.MiddleCenter, FontStyle.Bold);
            panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = panelTexture } };
            buttonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, fontSize = 21, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, hover = { textColor = Color.white }, active = { textColor = Color.white } };
            numberButtonStyle = new GUIStyle(buttonStyle);
            numberButtonStyle.normal.background = MakeTexture(new Color(0.16f, 0.30f, 0.62f));
            numberButtonStyle.hover.background = MakeTexture(new Color(0.22f, 0.40f, 0.80f));
            numberButtonStyle.active.background = MakeTexture(new Color(0.12f, 0.24f, 0.52f));
            selectedNumberButtonStyle = new GUIStyle(buttonStyle);
            selectedNumberButtonStyle.normal.background = MakeTexture(new Color(0.10f, 0.55f, 0.28f));
            selectedNumberButtonStyle.hover.background = MakeTexture(new Color(0.14f, 0.68f, 0.35f));
            selectedNumberButtonStyle.active.background = MakeTexture(new Color(0.08f, 0.45f, 0.22f));
            playerStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold, wordWrap = true, normal = { textColor = Color.white } };
            textFieldStyle = new GUIStyle(GUI.skin.textField) { alignment = TextAnchor.MiddleLeft, fontSize = 22, padding = new RectOffset(14, 10, 6, 6), normal = { textColor = Color.white } };
            selectedFrame = MakeTexture(new Color(1f, 0.78f, 0.18f));
            normalFrame = MakeTexture(new Color(0.28f, 0.32f, 0.42f));
            portugalFlag = MakePortugalFlag();
            unitedKingdomFlag = MakeUnitedKingdomFlag();
        }

        private static GUIStyle LabelStyle(int size, TextAnchor alignment, FontStyle style) => new GUIStyle(GUI.skin.label) { fontSize = size, alignment = alignment, fontStyle = style, normal = { textColor = new Color(0.90f, 0.93f, 1f) } };
        private static Texture2D MakeTexture(Color color) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, color); t.Apply(); return t; }

        private static Texture2D MakePortugalFlag()
        {
            const int w = 90, h = 54; var t = new Texture2D(w, h);
            for (var y = 0; y < h; y++) for (var x = 0; x < w; x++)
            {
                var c = x < 36 ? new Color(0.02f, 0.40f, 0.20f) : new Color(0.84f, 0.08f, 0.12f);
                var dx = x - 36; var dy = y - h / 2; if (dx * dx + dy * dy < 121) c = new Color(0.98f, 0.78f, 0.08f);
                t.SetPixel(x, y, c);
            }
            t.Apply(); return t;
        }

        private static Texture2D MakeUnitedKingdomFlag()
        {
            const int w = 90, h = 54; var t = new Texture2D(w, h);
            for (var y = 0; y < h; y++) for (var x = 0; x < w; x++)
            {
                var a = Mathf.Abs(y - x * (h - 1f) / (w - 1f)); var b = Mathf.Abs(y - (h - 1f - x * (h - 1f) / (w - 1f)));
                var c = new Color(0.05f, 0.16f, 0.42f);
                if (a < 5f || b < 5f || Mathf.Abs(x - w / 2f) < 9f || Mathf.Abs(y - h / 2f) < 9f) c = Color.white;
                if (a < 2f || b < 2f || Mathf.Abs(x - w / 2f) < 4f || Mathf.Abs(y - h / 2f) < 4f) c = new Color(0.78f, 0.04f, 0.13f);
                t.SetPixel(x, y, c);
            }
            t.Apply(); return t;
        }

        private void OnDestroy()
        {
            CancelInvoke();
            if (BuzzInputManager.Instance != null) BuzzInputManager.Instance.ButtonPressed -= OnButtonPressed;
        }
    }
}
