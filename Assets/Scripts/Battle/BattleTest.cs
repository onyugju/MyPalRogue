using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PalRogue
{
    /// <summary>
    /// 화면 없이 Console 창에서 전투를 자동으로 돌려보는 테스트용 컴포넌트.
    /// 씬에 빈 오브젝트를 만들어 붙이고, 두 팰(PalData)을 끌어다 넣으면 시작할 때 한 번 실행된다.
    /// (우클릭 메뉴 "Run Test Battle"로 다시 돌릴 수도 있다)
    /// </summary>
    public class BattleTest : MonoBehaviour
    {
        public PalData playerPal;
        public PalData enemyPal;
        [Range(1, 50)] public int playerLevel = 10;
        [Range(1, 50)] public int enemyLevel = 10;
        [Tooltip("-1이면 매번 다른 결과, 0 이상이면 같은 결과를 다시 볼 수 있다")]
        public int seed = -1;
        public int maxTurns = 100;

        void Start() => Run();

        [ContextMenu("Run Test Battle")]
        public void Run()
        {
            if (playerPal == null || enemyPal == null)
            {
                Debug.LogWarning("[BattleTest] playerPal / enemyPal 에 PalData 에셋을 넣어주세요.");
                return;
            }

            var rng = seed >= 0 ? new System.Random(seed) : new System.Random();
            var log = new StringBuilder();
            var battle = new Battle(
                new List<Pal> { new Pal(playerPal, playerLevel) },
                new List<Pal> { new Pal(enemyPal, enemyLevel) },
                rng, line => log.AppendLine(line));

            int turns = 0;
            while (battle.State == BattleState.Ongoing && turns++ < maxTurns)
                battle.ExecuteTurn(PickPlayerAction(battle.Player, rng));

            log.AppendLine($"결과: {battle.State} ({turns}턴)");
            Debug.Log(log.ToString());
        }

        // 테스트용 플레이어: PP가 남은 공격 스킬 중 무작위, 없으면 아무 스킬, 그것도 없으면 발버둥(-1)
        static BattleAction PickPlayerAction(Pal pal, System.Random rng)
        {
            var usable = pal.Skills.Select((s, i) => (s, i)).Where(t => t.s.HasPP).ToList();
            if (usable.Count == 0) return BattleAction.Skill(-1);
            var attacks = usable.Where(t => t.s.Data.power > 0).ToList();
            var pool = attacks.Count > 0 ? attacks : usable;
            return BattleAction.Skill(pool[rng.Next(pool.Count)].i);
        }
    }
}
