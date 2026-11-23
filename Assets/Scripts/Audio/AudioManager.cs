using System;
using UnityEngine;

// Singleton ligero de efectos de sonido.
// Si no se asignan clips en el Inspector, sintetiza clips de respaldo por código.
public class AudioManager : MonoBehaviour
{
    public enum Sfx { Click, DiceRoll, MinigameHit, TurnAlert, Victory }

    public static AudioManager Instance { get; private set; }

    const string VolumeKey = "RAParty.Volume";
    const string MutedKey = "RAParty.Muted";
    const int SampleRate = 44100;

    [Header("Clips opcionales (si están vacíos se generan por código)")]
    [SerializeField] AudioClip clickClip;
    [SerializeField] AudioClip diceRollClip;
    [SerializeField] AudioClip minigameHitClip;
    [SerializeField] AudioClip turnAlertClip;
    [SerializeField] AudioClip victoryClip;

    [Header("Salida")]
    [SerializeField] AudioSource source;
    [SerializeField, Range(0f, 1f)] float defaultVolume = 0.8f;

    public bool Muted { get; private set; }
    public float Volume { get; private set; }

    public event Action<bool> MuteChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (source == null) source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;

        Volume = PlayerPrefs.GetFloat(VolumeKey, defaultVolume);
        Muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
        ApplyVolume();

        if (clickClip == null) clickClip = SynthClick();
        if (diceRollClip == null) diceRollClip = SynthDiceRoll();
        if (minigameHitClip == null) minigameHitClip = SynthHit();
        if (turnAlertClip == null) turnAlertClip = SynthTurnAlert();
        if (victoryClip == null) victoryClip = SynthVictory();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------- API pública ----------
    public static void PlaySfx(Sfx sfx)
    {
        if (Instance != null) Instance.Play(sfx);
    }

    public void Play(Sfx sfx)
    {
        if (Muted || source == null) return;
        AudioClip clip = ClipFor(sfx);
        if (clip != null) source.PlayOneShot(clip);
    }

    public void SetMuted(bool muted)
    {
        if (Muted == muted) return;
        Muted = muted;
        PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
        PlayerPrefs.Save();
        ApplyVolume();
        MuteChanged?.Invoke(Muted);
    }

    public void ToggleMute()
    {
        SetMuted(!Muted);
        if (!Muted) Play(Sfx.Click);
    }

    public void SetVolume(float volume)
    {
        Volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(VolumeKey, Volume);
        PlayerPrefs.Save();
        ApplyVolume();
    }

    void ApplyVolume()
    {
        if (source != null) source.volume = Muted ? 0f : Volume;
    }

    AudioClip ClipFor(Sfx sfx)
    {
        switch (sfx)
        {
            case Sfx.Click: return clickClip;
            case Sfx.DiceRoll: return diceRollClip;
            case Sfx.MinigameHit: return minigameHitClip;
            case Sfx.TurnAlert: return turnAlertClip;
            default: return victoryClip;
        }
    }

    // ---------- Síntesis de respaldo ----------
    static AudioClip Build(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float[] Buffer(float seconds)
    {
        return new float[Mathf.CeilToInt(seconds * SampleRate)];
    }

    // "Tok" de madera corto
    static AudioClip SynthClick()
    {
        var d = Buffer(0.07f);
        double phase = 0;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / SampleRate;
            float f = Mathf.Lerp(1400f, 700f, t / 0.07f);
            phase += 2 * Math.PI * f / SampleRate;
            float env = Mathf.Exp(-t * 70f);
            d[i] = (float)Math.Sin(phase) * env * 0.5f;
        }
        return Build("sfx_click", d);
    }

    // Traqueteo de dado: golpes cortos de ruido filtrado
    static AudioClip SynthDiceRoll()
    {
        var d = Buffer(0.6f);
        var rng = new System.Random(7);
        float[] hits = { 0f, 0.07f, 0.15f, 0.21f, 0.30f, 0.37f, 0.47f };
        float lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / SampleRate;
            float s = 0f;
            for (int h = 0; h < hits.Length; h++)
            {
                float dt = t - hits[h];
                if (dt < 0f || dt > 0.05f) continue;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                float tone = Mathf.Sin(2 * Mathf.PI * (520f + h * 60f) * dt);
                s += (noise * 0.6f + tone * 0.4f) * Mathf.Exp(-dt * 90f) * (1f - h * 0.08f);
            }
            lp += (s - lp) * 0.35f; // filtro pasa bajos sencillo
            d[i] = lp * 0.7f;
        }
        return Build("sfx_dice", d);
    }

    // "Pop" ascendente para aciertos en minijuegos
    static AudioClip SynthHit()
    {
        var d = Buffer(0.12f);
        double phase = 0;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / SampleRate;
            float f = Mathf.Lerp(500f, 1100f, t / 0.12f);
            phase += 2 * Math.PI * f / SampleRate;
            float sq = Math.Sin(phase) >= 0 ? 1f : -1f;
            float env = Mathf.Exp(-t * 28f);
            d[i] = (sq * 0.25f + (float)Math.Sin(phase) * 0.35f) * env * 0.6f;
        }
        return Build("sfx_hit", d);
    }

    // Campanita de dos tonos: "¡es tu turno!"
    static AudioClip SynthTurnAlert()
    {
        var d = Buffer(0.5f);
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / SampleRate;
            float s = Bell(t, 880f) + Bell(t - 0.14f, 1318.5f);
            d[i] = s * 0.35f;
        }
        return Build("sfx_turn", d);
    }

    // Arpegio de victoria (Do-Mi-Sol-Do)
    static AudioClip SynthVictory()
    {
        var d = Buffer(1.3f);
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / SampleRate;
            float s = 0f;
            for (int n = 0; n < notes.Length; n++)
            {
                float start = n * 0.13f;
                float len = n == notes.Length - 1 ? 0.8f : 0.2f;
                float dt = t - start;
                if (dt < 0f || dt > len) continue;
                float tri = 2f * Mathf.Abs(2f * ((dt * notes[n]) % 1f) - 1f) - 1f;
                float env = Mathf.Min(1f, dt * 200f) * Mathf.Exp(-dt * (n == notes.Length - 1 ? 3f : 9f));
                s += tri * env;
            }
            d[i] = s * 0.3f;
        }
        return Build("sfx_victory", d);
    }

    static float Bell(float t, float f)
    {
        if (t < 0f) return 0f;
        float env = Mathf.Min(1f, t * 300f) * Mathf.Exp(-t * 7f);
        return (Mathf.Sin(2 * Mathf.PI * f * t) + 0.35f * Mathf.Sin(2 * Mathf.PI * f * 2.01f * t)) * env;
    }
}
