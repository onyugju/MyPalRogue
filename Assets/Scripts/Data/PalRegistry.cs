using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PalRogue
{
    /// <summary>
    /// 게임에 나오는 모든 팰(PalData) 목록. Resources/PalRegistry.asset 하나로 저장된다.
    /// 에디터에서는 AssetDatabase로 바로 찾지만, 빌드(exe)에서는 그게 안 되므로 이 목록을 쓴다.
    /// 빌드할 때(메뉴 81 또는 일반 Build) 자동으로 최신 목록으로 채워진다.
    /// </summary>
    public class PalRegistry : ScriptableObject
    {
        public List<PalData> pals = new List<PalData>();

        static List<PalData> cached;

        /// <summary>모든 팰 (이름순). 목록 파일이 없으면 빈 목록</summary>
        public static List<PalData> All
        {
            get
            {
                if (cached == null)
                {
                    var reg = Resources.Load<PalRegistry>("PalRegistry");
                    cached = reg != null ? reg.pals.Where(p => p != null).OrderBy(p => p.name).ToList() : new List<PalData>();
                }
                return new List<PalData>(cached);
            }
        }
    }
}
