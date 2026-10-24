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
        [Tooltip("크기 미세 조정 (1 = 자동 크기 그대로). 바꾼 뒤 메뉴 PalRogue/7-2를 실행하면 반영된다")]
        [Range(0.5f, 2.5f)] public float sizeTune = 1f;
        [Tooltip("상대 자리(앞모습)에서 위아래 위치 보정 (유닛, -는 아래로). 화면을 꽉 채우는 보스처럼 발판 기준을 벗어나야 할 때만 쓴다. 메뉴 7-2가 자동으로 정한다")]
        public float frontYOffset = 0f;

        [Header("애니메이션 (선택)")]
        [Tooltip("있으면 정지 이미지 대신 이 애니메이터로 재생한다. 없으면 위 스프라이트를 그대로 사용")]
        public RuntimeAnimatorController frontAnimator;
        public RuntimeAnimatorController backAnimator;

        public bool HasBackVisual => backSprite != null;
        public RuntimeAnimatorController BackOrFrontAnimator => HasBackVisual ? backAnimator : frontAnimator;

        public Sprite BackOrFront => backSprite != null ? backSprite : frontSprite;
        /// <summary>뒷모습 그림이 없으면 앞모습을 내 자리에 쓰는데, 내 자리는 화면 앞쪽이라 조금 크게 (PalSizing 기준 3.9 / 3.0)</summary>
        public float BackOrFrontScale => backSprite != null || frontYOffset != 0f ? backScale : frontScale * 1.3f;   // 크기를 손으로 정한 팰(제로버스)은 backScale에 원래 크기 기준 값이 들어 있다

        [Header("속성")]
        public PalType primaryType;
        public bool hasSecondaryType;
        public PalType secondaryType;

        /// <summary>보조 속성이 없으면 null</summary>
        public PalType? SecondaryType => hasSecondaryType ? secondaryType : null;

        /// <summary>이 속성을 가지고 있는지 (주 속성 또는 보조 속성)</summary>
        public bool HasType(PalType t) => primaryType == t || (hasSecondaryType && secondaryType == t);

        [Header("기본 능력치")]
        [Min(1)] public int baseHP = 50;
        [Min(1)] public int baseAttack = 50;
        [Min(1)] public int baseDefense = 50;
        [Min(1)] public int baseSpeed = 50;

        [Header("스킬 / 성장")]
        public List<LearnsetEntry> learnset = new();

        [Header("포획")]
        [Tooltip("false면 팰 스피어로 포획할 수 없다 (제로버스 등 보스)")]
        public bool canBeCaught = true;
        [Range(1, 255)] public int catchRate = 100;
    }
}