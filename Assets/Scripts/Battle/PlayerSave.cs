using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PalRogue
{
    /// <summary>
    /// 모험이 끝나도 남는 기록 (컴퓨터에 파일로 저장).
    ///  - 해금한 팰: 한 번이라도 잡은 팰은 다음 모험부터 스타팅 팰로 고를 수 있다. 스타팅 팰 5종은 처음부터 열려 있다
    ///  - 팰마다 잡은 횟수
    /// 파일: Application.persistentDataPath/palrogue_save.json  (타이틀 화면·도감·업적을 만들 때 여기에 더 붙이면 된다)
    /// </summary>
    public static class PlayerSave
    {
        [Serializable]
        class CaughtEntry
        {
            public string pal;
            public int count;
        }

        [Serializable]
        class SaveFile
        {
            public int version = 1;
            public List<string> unlocked = new List<string>();
            public List<CaughtEntry> caught = new List<CaughtEntry>();
        }

        static SaveFile data;

        public static string FilePath => Path.Combine(Application.persistentDataPath, "palrogue_save.json");

        static SaveFile Data
        {
            get
            {
                if (data != null) return data;
                try
                {
                    if (File.Exists(FilePath)) data = JsonUtility.FromJson<SaveFile>(File.ReadAllText(FilePath));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[저장] 저장 파일을 읽지 못해서 새로 시작합니다: {e.Message}");
                }
                data ??= new SaveFile();
                data.unlocked ??= new List<string>();
                data.caught ??= new List<CaughtEntry>();
                return data;
            }
        }

        public static void Save()
        {
            try { File.WriteAllText(FilePath, JsonUtility.ToJson(Data, true)); }
            catch (Exception e) { Debug.LogWarning($"[저장] 저장하지 못했습니다: {e.Message}"); }
        }

        /// <summary>스타팅 팰로 고를 수 있는지 (기본 스타팅 5종 + 한 번이라도 잡은 팰)</summary>
        public static bool IsUnlocked(PalData d) =>
            d != null && (GameConfig.IsStarterPal(d) || Data.unlocked.Contains(d.name));

        public static int CaughtCount(PalData d)
        {
            if (d == null) return 0;
            var e = Data.caught.Find(c => c.pal == d.name);
            return e != null ? e.count : 0;
        }

        /// <summary>팰을 잡았을 때 기록한다. 이번에 처음 해금됐으면 true (스타팅 팰 목록에 새로 추가)</summary>
        public static bool RecordCatch(PalData d)
        {
            if (d == null) return false;
            var e = Data.caught.Find(c => c.pal == d.name);
            if (e == null) Data.caught.Add(e = new CaughtEntry { pal = d.name });
            e.count++;
            bool isNew = !IsUnlocked(d) && GameConfig.CanBeStarter(d);
            if (isNew) Data.unlocked.Add(d.name);
            Save();
            return isNew;
        }

        /// <summary>(테스트용) 팰을 바로 해금한다</summary>
        public static void Unlock(IEnumerable<PalData> pals)
        {
            foreach (var d in pals)
                if (d != null && !Data.unlocked.Contains(d.name) && GameConfig.CanBeStarter(d)) Data.unlocked.Add(d.name);
            Save();
        }

        /// <summary>(테스트용) 모든 기록을 지운다</summary>
        public static void ResetAll()
        {
            data = new SaveFile();
            Save();
        }

        /// <summary>파일을 다시 읽게 한다 (에디터에서 파일을 직접 고쳤을 때)</summary>
        public static void Reload() => data = null;
    }
}
