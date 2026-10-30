#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// 메뉴 PalRogue/35. 씬 3개 만들기 (타이틀 → 팰 선택·저장 슬롯 → 본게임)
    ///  ① 지금의 전투 씬(SampleScene)을 GameScene으로 이름을 바꾼다
    ///  ② TitleScene, StarterScene을 새로 만든다 (카메라 + EventSystem + 화면 컴포넌트)
    ///  ③ 빌드 설정의 씬 순서를 TitleScene(0) → StarterScene(1) → GameScene(2)로
    ///  ④ TitleScene을 열어 둔다 (플레이를 누르면 타이틀부터)
    /// 여러 번 실행해도 된다 (이미 있는 씬은 안의 화면 오브젝트만 다시 맞춘다).
    /// 글꼴은 GameScene 전투 화면의 메시지 글꼴을 그대로 쓴다.
    /// </summary>
    public static class SceneSetup
    {
        const string Dir = "Assets/Scenes";
        static string PathOf(string scene) => $"{Dir}/{scene}.unity";

        [MenuItem("PalRogue/35. 씬 3개 만들기 (타이틀 → 팰 선택 → 본게임)")]
        static void Build()
        {
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("플레이 중", "플레이를 멈춘 뒤에 실행해 주세요.", "확인"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // ① 전투 씬 이름 바꾸기
            string gamePath = PathOf(GameSession.GameScene);
            if (!File.Exists(gamePath))
            {
                string old = PathOf("SampleScene");
                if (!File.Exists(old)) { EditorUtility.DisplayDialog("전투 씬 없음", $"{old} 를 찾지 못했습니다.", "확인"); return; }
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);   // 열려 있는 씬을 닫고 이름 바꾸기
                string err = AssetDatabase.RenameAsset(old, GameSession.GameScene);
                if (!string.IsNullOrEmpty(err)) { Debug.LogError("[씬] 이름 바꾸기 실패: " + err); return; }
            }

            // 전투 씬에서 글꼴·입력 모듈 종류를 가져온다
            var game = EditorSceneManager.OpenScene(gamePath, OpenSceneMode.Single);
            var battle = Object.FindAnyObjectByType<BattleScreen>();
            TMP_FontAsset font = battle != null && battle.messageText != null ? battle.messageText.font : null;
            if (font == null) font = AssetDatabase.FindAssets("t:TMP_FontAsset").Select(g => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g)))
                                                  .FirstOrDefault(f => f != null && !f.name.Contains("Liberation"));
            var es = Object.FindAnyObjectByType<EventSystem>();
            var moduleType = es != null ? es.GetComponent<BaseInputModule>()?.GetType() : null;
            var pals = AssetDatabase.FindAssets("t:PalData").Select(g => AssetDatabase.LoadAssetAtPath<PalData>(AssetDatabase.GUIDToAssetPath(g)))
                                    .Where(p => p != null).OrderBy(p => p.name).ToList();
            if (battle != null)
            {
                battle.wildPals = pals;                      // 빌드에서도 모든 팰이 나오게 채워서 저장
                EditorUtility.SetDirty(battle);
                EditorSceneManager.MarkSceneDirty(game);
                EditorSceneManager.SaveScene(game);
            }

            // ② 타이틀 · 팰 선택 씬
            MakeScene(GameSession.TitleScene, font, moduleType, root =>
            {
                var title = root.AddComponent<TitleScreen>();
                title.font = font;
                title.allPals = pals;
            });
            MakeScene(GameSession.StarterScene, font, moduleType, root =>
            {
                var ctrl = root.AddComponent<StarterSceneController>();
                ctrl.font = font;
                ctrl.allPals = pals;
            });

            // ③ 빌드 설정
            var list = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(PathOf(GameSession.TitleScene), true),
                new EditorBuildSettingsScene(PathOf(GameSession.StarterScene), true),
                new EditorBuildSettingsScene(gamePath, true),
            };
            list.AddRange(EditorBuildSettings.scenes.Where(s => list.All(l => l.path != s.path) && File.Exists(s.path) && !s.path.EndsWith("SampleScene.unity")));
            EditorBuildSettings.scenes = list.ToArray();

            // ④ 타이틀 씬 열기
            EditorSceneManager.OpenScene(PathOf(GameSession.TitleScene), OpenSceneMode.Single);
            Debug.Log($"[씬] 완료: TitleScene → StarterScene → GameScene (빌드 순서 0, 1, 2). 글꼴: {(font != null ? font.name : "없음")}. " +
                      "플레이를 누르면 타이틀부터 시작해요. (GameScene을 열고 플레이하면 예전처럼 전투 씬 안에서 팰을 고르고 저장하지 않아요)");
        }

        static void MakeScene(string name, TMP_FontAsset font, System.Type moduleType, System.Action<GameObject> setup)
        {
            string path = PathOf(name);
            var scene = File.Exists(path) ? EditorSceneManager.OpenScene(path, OpenSceneMode.Single)
                                          : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 카메라 (화면은 캔버스가 다 그리지만, 카메라가 없으면 "No cameras rendering"이 뜬다)
            var cam = Object.FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.transform.position = new Vector3(0, 0, -10);

            // 키보드·마우스 입력
            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem", typeof(EventSystem));
                if (moduleType != null) esGo.AddComponent(moduleType);
                else esGo.AddComponent<StandaloneInputModule>();
            }

            // 화면 오브젝트 (있으면 지우고 다시)
            var old = GameObject.Find(name == GameSession.TitleScene ? "Title" : "StarterSelect");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(name == GameSession.TitleScene ? "Title" : "StarterSelect");
            setup(root);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
        }
    }
}
#endif
