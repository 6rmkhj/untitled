using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SignalHaul
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class ProximityVoiceChat : NetworkBehaviour
    {
        private const int SampleRate = 16000;
        private const int SamplesPerPacket = 320; // 20 ms at 16 kHz.
        private const int BytesPerPacket = SamplesPerPacket * 2; // Signed 16-bit PCM mono.
        private const int MicrophoneClipSeconds = 1;
        private const int PlaybackBufferSeconds = 2;
        private const int MaxPacketsPerFrame = 4;

        [Header("Input")]
        [SerializeField] private KeyCode pushToTalkKey = KeyCode.V;
        [SerializeField] private KeyCode toggleReceiveMuteKey = KeyCode.M;

        [Header("Proximity")]
        [SerializeField, Min(.1f)] private float minDistance = 1.5f;
        [SerializeField, Min(1f)] private float maxDistance = 14f;
        [SerializeField, Range(0f, 2f)] private float outputVolume = 1f;

        private static bool receiveMuted;

        private AudioSource voiceSource;
        private AudioClip playbackClip;
        private AudioClip microphoneClip;
        private string microphoneDevice;
        private bool microphoneReady;
        private string microphoneStatus = "initializing";
        private int microphoneReadPosition;
        private uint outgoingSequence;
        private uint lastReceivedSequence;
        private bool hasReceivedSequence;

        private readonly float[] captureSamples = new float[SamplesPerPacket];
        private readonly byte[] encodedPacket = new byte[BytesPerPacket];
        private readonly float[] playbackRing = new float[SampleRate * PlaybackBufferSeconds];
        private readonly object playbackLock = new object();
        private int playbackRead;
        private int playbackWrite;
        private int playbackCount;

        public bool IsMicrophoneReady => microphoneReady;
        public static bool ReceiveMuted => receiveMuted;

        private void Awake()
        {
            ConfigureSpatialAudioSource();
        }

        public override void OnNetworkSpawn()
        {
            ConfigureSpatialAudioSource();

            if (IsOwner)
            {
                voiceSource.enabled = false;
                StartCoroutine(StartMicrophoneCapture());
            }
            else
            {
                StartRemotePlayback();
            }
        }

        public override void OnNetworkDespawn()
        {
            StopMicrophoneCapture();

            if (voiceSource != null)
                voiceSource.Stop();
        }

        private void OnDestroy()
        {
            StopMicrophoneCapture();
        }

        private void Update()
        {
            if (!IsSpawned)
                return;

            if (IsOwner)
            {
                if (Input.GetKeyDown(toggleReceiveMuteKey))
                    receiveMuted = !receiveMuted;

                PumpMicrophone();
            }
            else if (voiceSource != null)
            {
                voiceSource.mute = receiveMuted;
            }
        }

        private void ConfigureSpatialAudioSource()
        {
            if (voiceSource == null)
                voiceSource = GetComponent<AudioSource>();
            if (voiceSource == null)
                voiceSource = gameObject.AddComponent<AudioSource>();

            voiceSource.playOnAwake = false;
            voiceSource.loop = true;
            voiceSource.spatialBlend = 1f;
            voiceSource.rolloffMode = AudioRolloffMode.Linear;
            voiceSource.minDistance = Mathf.Max(.1f, minDistance);
            voiceSource.maxDistance = Mathf.Max(voiceSource.minDistance + .1f, maxDistance);
            voiceSource.volume = outputVolume;
            voiceSource.dopplerLevel = 0f;
            voiceSource.spread = 0f;
        }

        private void StartRemotePlayback()
        {
            if (playbackClip == null)
            {
                playbackClip = AudioClip.Create(
                    $"Voice_{OwnerClientId}",
                    SampleRate * PlaybackBufferSeconds,
                    1,
                    SampleRate,
                    true,
                    OnAudioRead,
                    OnAudioSetPosition);
            }

            voiceSource.enabled = true;
            voiceSource.clip = playbackClip;
            voiceSource.mute = receiveMuted;
            if (!voiceSource.isPlaying)
                voiceSource.Play();
        }

        private IEnumerator StartMicrophoneCapture()
        {
            microphoneStatus = "requesting permission";

            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
                yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);

            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                microphoneStatus = "permission denied";
                yield break;
            }

            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                microphoneStatus = "no microphone";
                yield break;
            }

            microphoneDevice = Microphone.devices[0];
            microphoneClip = Microphone.Start(microphoneDevice, true, MicrophoneClipSeconds, SampleRate);
            if (microphoneClip == null)
            {
                microphoneStatus = "failed to start";
                yield break;
            }

            float timeoutAt = Time.realtimeSinceStartup + 3f;
            while (Microphone.GetPosition(microphoneDevice) <= 0 && Time.realtimeSinceStartup < timeoutAt)
                yield return null;

            int position = Microphone.GetPosition(microphoneDevice);
            if (position < 0)
            {
                microphoneStatus = "device unavailable";
                StopMicrophoneCapture();
                yield break;
            }

            microphoneReadPosition = position;
            microphoneReady = true;
            microphoneStatus = microphoneDevice;
        }

        private void StopMicrophoneCapture()
        {
            microphoneReady = false;

            if (!string.IsNullOrEmpty(microphoneDevice) && Microphone.IsRecording(microphoneDevice))
                Microphone.End(microphoneDevice);

            microphoneClip = null;
            microphoneDevice = null;
        }

        private void PumpMicrophone()
        {
            if (!microphoneReady || microphoneClip == null || string.IsNullOrEmpty(microphoneDevice))
                return;

            int currentPosition = Microphone.GetPosition(microphoneDevice);
            if (currentPosition < 0)
                return;

            int clipSamples = microphoneClip.samples;
            int available = currentPosition >= microphoneReadPosition
                ? currentPosition - microphoneReadPosition
                : clipSamples - microphoneReadPosition + currentPosition;

            bool transmitting = Input.GetKey(pushToTalkKey);
            int packetsProcessed = 0;

            while (available >= SamplesPerPacket && packetsProcessed < MaxPacketsPerFrame)
            {
                ReadMicrophonePacket(clipSamples);
                microphoneReadPosition = (microphoneReadPosition + SamplesPerPacket) % clipSamples;
                available -= SamplesPerPacket;
                packetsProcessed++;

                if (!transmitting)
                    continue;

                EncodePcm16(captureSamples, encodedPacket);
                SubmitVoiceRpc(++outgoingSequence, encodedPacket);
            }

            // If a frame stalls badly, discard old microphone audio instead of sending a burst of stale speech.
            if (available > SamplesPerPacket * MaxPacketsPerFrame)
                microphoneReadPosition = currentPosition;
        }

        private void ReadMicrophonePacket(int clipSamples)
        {
            int firstCount = Mathf.Min(SamplesPerPacket, clipSamples - microphoneReadPosition);
            if (firstCount == SamplesPerPacket)
            {
                microphoneClip.GetData(captureSamples, microphoneReadPosition);
                return;
            }

            float[] tail = new float[firstCount];
            float[] head = new float[SamplesPerPacket - firstCount];
            microphoneClip.GetData(tail, microphoneReadPosition);
            microphoneClip.GetData(head, 0);
            Array.Copy(tail, 0, captureSamples, 0, tail.Length);
            Array.Copy(head, 0, captureSamples, tail.Length, head.Length);
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        private void SubmitVoiceRpc(uint sequence, byte[] payload, RpcParams rpcParams = default)
        {
            if (!IsServer || payload == null || payload.Length != BytesPerPacket)
                return;

            if (rpcParams.Receive.SenderClientId != OwnerClientId)
                return;

            RelayVoiceRpc(sequence, payload);
        }

        [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Unreliable)]
        private void RelayVoiceRpc(uint sequence, byte[] payload)
        {
            if (IsOwner || receiveMuted || payload == null || payload.Length != BytesPerPacket)
                return;

            if (hasReceivedSequence && sequence <= lastReceivedSequence)
                return;

            hasReceivedSequence = true;
            lastReceivedSequence = sequence;
            EnqueuePcm16(payload);
        }

        private void EnqueuePcm16(byte[] payload)
        {
            lock (playbackLock)
            {
                for (int i = 0; i < payload.Length; i += 2)
                {
                    short sample = (short)(payload[i] | (payload[i + 1] << 8));
                    float value = sample / 32768f;

                    if (playbackCount == playbackRing.Length)
                    {
                        playbackRead = (playbackRead + 1) % playbackRing.Length;
                        playbackCount--;
                    }

                    playbackRing[playbackWrite] = value;
                    playbackWrite = (playbackWrite + 1) % playbackRing.Length;
                    playbackCount++;
                }
            }
        }

        private void OnAudioRead(float[] data)
        {
            lock (playbackLock)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    if (playbackCount > 0)
                    {
                        data[i] = playbackRing[playbackRead];
                        playbackRead = (playbackRead + 1) % playbackRing.Length;
                        playbackCount--;
                    }
                    else
                    {
                        data[i] = 0f;
                    }
                }
            }
        }

        private void OnAudioSetPosition(int newPosition)
        {
            // Streaming voice is driven by the ring buffer, not AudioClip seek position.
        }

        private static void EncodePcm16(float[] samples, byte[] destination)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                float clamped = Mathf.Clamp(samples[i], -1f, 1f);
                short value = (short)Mathf.RoundToInt(clamped * 32767f);
                int byteIndex = i * 2;
                destination[byteIndex] = (byte)(value & 0xff);
                destination[byteIndex + 1] = (byte)((value >> 8) & 0xff);
            }
        }

        private void OnGUI()
        {
            if (!IsSpawned || !IsOwner)
                return;

            const float width = 260f;
            const float height = 78f;
            float x = 18f;
            float y = Screen.height - 142f;

            GUI.Box(new Rect(x, y, width, height), string.Empty);

            string txStatus;
            if (!microphoneReady)
                txStatus = $"VOICE: {microphoneStatus}";
            else if (Input.GetKey(pushToTalkKey))
                txStatus = "VOICE: TRANSMITTING";
            else
                txStatus = $"VOICE: HOLD {pushToTalkKey} TO TALK";

            GUI.Label(new Rect(x + 12f, y + 10f, width - 24f, 24f), txStatus);
            GUI.Label(
                new Rect(x + 12f, y + 38f, width - 24f, 24f),
                $"{toggleReceiveMuteKey}: {(receiveMuted ? "UNMUTE" : "MUTE")} INCOMING VOICE");
        }
    }
}
