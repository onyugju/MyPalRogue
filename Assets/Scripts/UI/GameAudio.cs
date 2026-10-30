using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PalRogue
{
    /// <summary>
    /// 설정 (이 컴퓨터에 저장: PlayerPrefs). 볼륨은 0~10, 대사 속도는 0 느림 / 1 보통 / 2 빠름 / 3 즉시.
    /// </summary>
    public static class GameSettings
    {
        const string KeyMaster = "pr_vol_master", KeyBgm = "pr_vol_bgm", KeySe = "pr_vol_se", KeyText = "pr_text_speed";
        public const int MaxVolume = 10;
        public static readonly string[] TextSpeedNames = { "느림", "보통", "빠름", "즉시" };
        static readonly float[] TextSpeeds = { 25f, 45f, 90f, 10000f };

        // 기본 볼륨 (전체 6 / 배경음악 3 / 효과음 4). 예전 기본값으로 저장된 볼륨은 한 번 새 기본값으로 바꾼다
        const int DefaultMaster = 6, DefaultBgm = 3, DefaultSe = 4, VolumeVersion = 2;
        const string KeyVersion = "pr_vol_version";

        static GameSettings()
        {
            if (PlayerPrefs.GetInt(KeyVersion, 1) >= VolumeVersion) return;
            PlayerPrefs.DeleteKey(KeyMaster); PlayerPrefs.DeleteKey(KeyBgm); PlayerPrefs.DeleteKey(KeySe);
            PlayerPrefs.SetInt(KeyVersion, VolumeVersion);
            PlayerPrefs.Save();
        }

        public static int Master { get => PlayerPrefs.GetInt(KeyMaster, DefaultMaster); set { PlayerPrefs.SetInt(KeyMaster, Mathf.Clamp(value, 0, MaxVolume)); Changed(); } }
        public static int Bgm { get => PlayerPrefs.GetInt(KeyBgm, DefaultBgm); set { PlayerPrefs.SetInt(KeyBgm, Mathf.Clamp(value, 0, MaxVolume)); Changed(); } }
        public static int Se { get => PlayerPrefs.GetInt(KeySe, DefaultSe); set { PlayerPrefs.SetInt(KeySe, Mathf.Clamp(value, 0, MaxVolume)); Changed(); } }
        public static int TextSpeed { get => PlayerPrefs.GetInt(KeyText, 1); set { PlayerPrefs.SetInt(KeyText, Mathf.Clamp(value, 0, TextSpeedNames.Length - 1)); PlayerPrefs.Save(); } }

        /// <summary>메시지가 한 글자씩 나오는 속도 (초당 글자 수)</summary>
        public static float CharsPerSecond => TextSpeeds[Mathf.Clamp(TextSpeed, 0, TextSpeeds.Length - 1)];

        public static float BgmVolume => Master / (float)MaxVolume * Bgm / MaxVolume;
        public static float SeVolume => Master / (float)MaxVolume * Se / MaxVolume;

        static void Changed()
        {
            PlayerPrefs.Save();
            GameAudio.ApplyVolume();
        }
    }

    /// <summary>
    /// 배경음악 · 효과음 (포켓로그 audio 에셋, Resources/Audio 에 복사해서 쓴다 - 메뉴 PalRogue/36).
    ///  GameAudio.Bgm("plains")   배경음악 (같은 곡이면 그대로, 다른 곡이면 부드럽게 바꿈, 반복 재생)
    ///  GameAudio.Se("hit")       효과음 한 번 (se 폴더 → ui 폴더 → bgm 폴더 순서로 찾는다)
    /// 씬이 바뀌어도 이어지도록 처음 쓸 때 한 번 만들어 둔다.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        const string Root = "Audio/";
        const float CrossFade = 0.6f;

        static GameAudio instance;
        AudioListener listener;

        void OneListener()
        {
            if (listener == null) return;
            foreach (var l in FindObjectsByType<AudioListener>())
                if (l != listener) l.enabled = false;
        }
        AudioSource[] music;          // 두 개를 번갈아 써서 곡을 겹쳐 바꾼다
        int current;
        string currentName;
        AudioSource se;
        Coroutine fading;
        float musicLevel = 1f;        // 페이드 중 곡 음량 (0~1, 설정 볼륨에 곱한다)
        readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();

        static GameAudio I
        {
            get
            {
                if (instance != null) return instance;
                var go = new GameObject("GameAudio");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<GameAudio>();
                instance.music = new[] { go.AddComponent<AudioSource>(), go.AddComponent<AudioSource>() };
                foreach (var m in instance.music) { m.loop = true; m.playOnAwake = false; m.volume = 0f; }
                instance.se = go.AddComponent<AudioSource>();
                instance.se.playOnAwake = false;
                // 소리를 듣는 귀(AudioListener)는 이 오브젝트 하나만 쓴다 (씬마다 카메라에 있거나 없어서)
                instance.listener = go.AddComponent<AudioListener>();
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) => instance.OneListener();
                instance.OneListener();
                return instance;
            }
        }

        /// <summary>씬이 시작될 때 바로 만들어 둔다 (소리를 들을 귀가 처음부터 있게)</summary>
        public static void Ensure() { var _ = I; }

        /// <summary>배경음악 재생. null이나 빈 이름이면 멈춘다</summary>
        public static void Bgm(string name)
        {
            var a = I;
            if (string.IsNullOrEmpty(name)) { StopBgm(); return; }
            if (a.currentName == name && a.music[a.current].isPlaying) return;
            var clip = a.Load("bgm/" + name);
            if (clip == null) { Debug.LogWarning($"[사운드] 배경음악이 없음: Resources/{Root}bgm/{name} (메뉴 PalRogue/36)"); return; }
            a.currentName = name;
            if (a.fading != null) a.StopCoroutine(a.fading);
            a.fading = a.StartCoroutine(a.Switch(clip));
        }

        /// <summary>배경음악을 천천히 줄이며 멈춘다</summary>
        public static void StopBgm(float seconds = 0.5f)
        {
            var a = I;
            a.currentName = null;
            if (a.fading != null) a.StopCoroutine(a.fading);
            a.fading = a.StartCoroutine(a.FadeOut(seconds));
        }

        /// <summary>효과음 한 번. 같은 소리가 아주 짧은 사이에 겹치면 한 번만 낸다</summary>
        public static void Se(string name, float volume = 1f)
        {
            if (string.IsNullOrEmpty(name)) return;
            var a = I;
            if (a.lastPlayed.TryGetValue(name, out var t) && Time.unscaledTime - t < 0.04f) return;
            var clip = a.Load("se/" + name) ?? a.Load("ui/" + name) ?? a.Load("bgm/" + name);
            if (clip == null) return;
            a.lastPlayed[name] = Time.unscaledTime;
            a.se.PlayOneShot(clip, GameSettings.SeVolume * volume);
        }

        /// <summary>설정에서 볼륨을 바꿨을 때</summary>
        public static void ApplyVolume()
        {
            if (instance == null) return;
            instance.music[instance.current].volume = GameSettings.BgmVolume * instance.musicLevel;
        }

        AudioClip Load(string path)
        {
            if (cache.TryGetValue(path, out var c)) return c;
            c = Resources.Load<AudioClip>(Root + path);
            cache[path] = c;
            return c;
        }

        IEnumerator Switch(AudioClip clip)
        {
            var from = music[current];
            current = 1 - current;
            var to = music[current];
            to.clip = clip;
            to.volume = 0f;
            to.Play();
            float fromStart = from.volume;
            for (float t = 0; t < CrossFade; t += Time.unscaledDeltaTime)
            {
                float k = t / CrossFade;
                musicLevel = k;
                to.volume = GameSettings.BgmVolume * k;
                from.volume = fromStart * (1f - k);
                yield return null;
            }
            musicLevel = 1f;
            to.volume = GameSettings.BgmVolume;
            from.Stop();
            fading = null;
        }

        IEnumerator FadeOut(float seconds)
        {
            var m = music[current];
            float start = m.volume;
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                musicLevel = 1f - t / seconds;
                m.volume = start * musicLevel;
                yield return null;
            }
            m.Stop();
            musicLevel = 1f;
            fading = null;
        }

        /// <summary>지역(배경 이름)별 배경음악. 포켓로그 음악 이름과 같다</summary>
        public static string AreaBgm(int wave, BossKind kind) => kind switch
        {
            BossKind.Final => "battle_final",
            BossKind.Legend or BossKind.Boss => "battle_wild_strong",
            _ => GameConfig.AreaBiome(wave),
        };
    }
}
