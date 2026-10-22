namespace PalRogue
{
    /// <summary>
    /// 한국어 조사 붙이기: 이름 마지막 글자의 받침을 보고 은/는, 이/가, 을/를, 으로/로를 고른다.
    /// 예) Josa.Eun("파이호") → "파이호는",  Josa.Eun("큐룰리스") → "큐룰리스는",  Josa.I("찌릿도치") → "찌릿도치가"
    /// 한글이나 숫자로 끝나지 않는 이름(영어 등)은 "은(는)"처럼 둘 다 적는다.
    /// </summary>
    public static class Josa
    {
        public static string Eun(object word) => Attach(word, "은", "는");
        public static string I(object word) => Attach(word, "이", "가");
        public static string Eul(object word) => Attach(word, "을", "를");
        public static string Wa(object word) => Attach(word, "과", "와");

        /// <summary>으로/로: ㄹ 받침은 "로"를 쓴다 (예: "Lv.7로", "Lv.10으로")</summary>
        public static string Ro(object word)
        {
            string s = word?.ToString() ?? "";
            int f = FinalConsonant(s);
            if (f < 0) return s + "(으)로";
            return s + (f == 0 || f == Rieul ? "로" : "으로");
        }

        const int Rieul = 8;   // 종성 ㄹ 번호

        static string Attach(object word, string withFinal, string withoutFinal)
        {
            string s = word?.ToString() ?? "";
            int f = FinalConsonant(s);
            if (f < 0) return $"{s}{withFinal}({withoutFinal})";
            return s + (f > 0 ? withFinal : withoutFinal);
        }

        /// <summary>마지막 글자의 종성 번호 (0 = 받침 없음, -1 = 판단 불가)</summary>
        static int FinalConsonant(string s)
        {
            for (int i = s.Length - 1; i >= 0; i--)
            {
                char c = s[i];
                if (c == ' ' || c == ')' || c == ']' || c == '!' || c == '.') continue;   // 끝의 괄호·문장부호는 건너뜀
                if (c >= '가' && c <= '힣') return (c - '가') % 28;
                if (c >= '0' && c <= '9') return DigitFinal(s, i);
                return -1;
            }
            return -1;
        }

        /// <summary>숫자를 한국어로 읽었을 때의 받침 (10 = 십, 100 = 백, 1000 = 천 …)</summary>
        static int DigitFinal(string s, int last)
        {
            int zeros = 0;
            int i = last;
            while (i >= 0 && s[i] == '0') { zeros++; i--; }
            bool allZero = i < 0 || s[i] < '0' || s[i] > '9';
            if (allZero) return 21;                    // 0 = 영(ㅇ)
            switch (zeros)
            {
                case 0:
                    // 일 이 삼 사 오 육 칠 팔 구
                    int[] f = { 21, 8, 0, 16, 0, 0, 1, 8, 8, 0 };
                    return f[s[i] - '0'];
                case 1: return 17;                     // 십(ㅂ)
                case 2: return 1;                      // 백(ㄱ)
                case 3: return 4;                      // 천(ㄴ)
                default: return 4;                     // 만(ㄴ)
            }
        }
    }
}
