using System;
using System.Collections.Generic;
using System.Linq;

namespace PalRogue
{
    /// <summary>
    /// 능력치 단계(-6 ~ +6) 바꾸기와 그 메시지 (포켓몬과 같은 말투).
    ///  +1 올라갔다 / +2 크게 올라갔다 / +3 이상 매우 크게 올라갔다
    ///  -1 떨어졌다 / -2 크게 떨어졌다 / -3 이하 매우 크게 떨어졌다
    ///  더 바꿀 수 없으면 "더 이상 올라가지 않는다!" / "더 이상 떨어지지 않는다!"
    /// 단계 배율: +1 = 1.5배, +2 = 2배, … / -1 = 0.67배, -2 = 0.5배, … (Pal.GetStat)
    /// </summary>
    public static class StatStages
    {
        /// <summary>단계를 바꾸고 메시지를 남긴다. 메시지를 남겼으면(바뀌었거나 한계라서 실패) true</summary>
        public static bool Change(Pal target, StatType stat, int delta, Action<string> log)
        {
            if (target == null || delta == 0 || target.IsFainted) return false;
            int changed = target.ModifyStage(stat, delta);
            string who = $"{target.Data.palName}의 {Josa.I(StatNames.Korean(stat))}";
            log?.Invoke(changed == 0 ? $"{who} 더 이상 {(delta > 0 ? "올라가지" : "떨어지지")} 않는다!" : $"{who} {Verb(changed)}");
            return true;
        }

        /// <summary>여러 능력치를 한꺼번에 (같은 쪽으로 같은 양이면 "공격과 방어가 올라갔다!"처럼 한 줄로)</summary>
        public static bool ChangeMany(Pal target, IList<(StatType stat, int delta)> changes, Action<string> log)
        {
            if (target == null || target.IsFainted || changes == null || changes.Count == 0) return false;
            if (changes.Count == 1) return Change(target, changes[0].stat, changes[0].delta, log);

            // 같은 양만큼 바뀐 능력치끼리 묶어서 한 줄로
            var results = new List<(StatType stat, int asked, int changed)>();
            foreach (var (stat, delta) in changes) results.Add((stat, delta, target.ModifyStage(stat, delta)));
            foreach (var group in results.GroupBy(r => r.changed == 0 ? (r.asked > 0 ? 100 : -100) : r.changed))
            {
                string names = JoinNames(group.Select(r => r.stat).ToList());
                string who = $"{target.Data.palName}의 {Josa.I(names)}";
                log?.Invoke(group.Key == 100 ? $"{who} 더 이상 올라가지 않는다!"
                          : group.Key == -100 ? $"{who} 더 이상 떨어지지 않는다!"
                          : $"{who} {Verb(group.Key)}");
            }
            return true;
        }

        static string JoinNames(List<StatType> stats)
        {
            if (stats.Count == 1) return StatNames.Korean(stats[0]);
            if (stats.Count == (int)Enum.GetValues(typeof(StatType)).Length) return "모든 능력";
            return string.Join("", stats.Take(stats.Count - 1).Select(s => Josa.Wa(StatNames.Korean(s)) + " ")) + StatNames.Korean(stats[stats.Count - 1]);
        }

        public static string Verb(int changed) => changed switch
        {
            >= 3 => "매우 크게 올라갔다!",
            2 => "크게 올라갔다!",
            1 => "올라갔다!",
            -1 => "떨어졌다!",
            -2 => "크게 떨어졌다!",
            _ => "매우 크게 떨어졌다!",
        };

        /// <summary>단계 배율 (Pal.GetStat과 같은 식)</summary>
        public static float Multiplier(int stage) => stage >= 0 ? (2f + stage) / 2f : 2f / (2f - stage);

        /// <summary>정보창에 쓰는 짧은 이름</summary>
        public static string Short(StatType s) => s switch
        {
            StatType.Attack => "공격",
            StatType.Defense => "방어",
            _ => "스피드",
        };
    }
}
