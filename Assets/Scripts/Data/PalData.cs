using System;
using System.Collections.Generic;
using UnityEngine;

namespace PalRogue
{
    [Serializable]
    public struct LearnsetEntry
    {
        public int level;
        public SkillData skill;
    }

    [CreateAssetMenu(fileName = "NewPal", menuName = "PalRogue/Pal Data")]
    public class PalData : ScriptableObject
    {
        [Header("기본 정보")]
        public string palId;        // 도감 번호 (예: "001"). 보스 2종은 둘 다 "000"이므로 식별은 에셋 이름으로
        public string palName;      // 한글 이름
        public string englishName;

        [Header("스프라이트")]
        public Sprite frontSprite;  // 적 쪽(앞모습)
        public Sprite backSprite;   // 내 팰(뒷모습), 없으면 null → frontSprite 사용
        [Tooltip("원본 이미지 크기가 제각각이라 배치 시 곱해주는 보정 배율")]
        public float frontScale = 1f;
        public float backScale = 1f;

        public Sprite BackOrFront => backSprite != null ? backSprite : frontSprite;
        public float BackOrFrontScale => backSprite != null ? backScale : frontScale;

        [Header("속성")]
        public PalType primaryType;
        public bool hasSecondaryType;
        public PalType secondaryType;

        /// <summary>보조 속성이 없으면 null</summary>
        public PalType? SecondaryType => hasSecondaryType ? secondaryType : null;

        [Header("기본 능력치")]
        [Min(1)] public int baseHP = 50;
        [Min(1)] public int baseAttack = 50;
        [Min(1)] public int baseDefense = 50;
        [Min(1)] public int baseSpAttack = 50;
        [Min(1)] public int baseSpDefense = 50;
        [Min(1)] public int baseSpeed = 50;

        [Header("스킬 / 성장")]
        public List<LearnsetEntry> learnset = new();

        [Header("포획")]
        [Tooltip("false면 팰 스피어로 포획할 수 없다 (제로버스 등 보스)")]
        public bool canBeCaught = true;
        [Range(1, 255)] public int catchRate = 100;
    }
}