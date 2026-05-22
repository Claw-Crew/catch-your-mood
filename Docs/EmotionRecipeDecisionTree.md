<!--
Changed: Added a source-grounded decision tree for result-card option 1.
Why: Document the implemented EmotionRecipeUI flow without modifying gameplay code.
-->

# Emotion Recipe Result-Card Decision Tree

## Status

- Document status: implemented behavior first, with missing/planned branches clearly separated.
- Runtime scope: result option 1, the dominant-emotion recipe card.
- Gameplay code status: no gameplay code changes are required by this document.
- Primary implemented path: `GameManager.EndGame()` finds `EmotionRecipeUI` first, then calls `EmotionRecipeUI.ShowResult()`.
- Important gap: tie handling is not explicitly implemented.
- Important gap: the current lighting effect dims one found `Light` to 40%, not every surrounding light.

## Inspected Source Skeleton

- `Assets/00.Main/Code/Scripts/Core/GameManager.cs`
  - `GameManager`
  - `Start()` lines 12-15
  - `Update()` lines 17-35
  - `EndGame()` lines 37-66
- `Assets/00.Main/Code/Scripts/UI/EmotionRecipeUI.cs`
  - `EmotionRecipeUI`
  - recipe/message/name dictionaries lines 52-84
  - `GetAmountUnit()` lines 90-100
  - Korean font helpers lines 102-201
  - `ShowResult()` lines 207-265
  - result-card anchor placement lines 267-309
  - `ShowResultCoroutine()` lines 317-403
  - `TypeText()` lines 408-421
- `Assets/00.Main/Code/Scripts/System/GameResultManager.cs`
  - `GameResultManager`
  - singleton assignment lines 20-23
  - `RegisterCatch()` lines 25-34
  - `RegisterTry()` lines 36-42
  - `GetResults()` lines 44-47
  - `GetTryCount()` lines 49-52
  - `GetEmotionCount()` lines 56-59
- `Assets/00.Main/Code/Scripts/System/CatchDetector.cs`
  - `CatchDetector`
  - `OnTriggerEnter()` lines 33-56
  - `TryGetDollInfo()` lines 60-81
  - duplicate catch gate lines 85-100
- `Assets/01.Test/SJM/ClawTestController.cs`
  - `ClawTestController`
  - input/state-machine `Update()` lines 339-451
  - `RegisterAcceptedTry()` lines 454-465
- `Assets/Editor/SceneSetup.cs`
  - `Build()` lines 76-109
  - `BuildMachine()` catch zone and machine light setup lines 338-578
  - `BuildLighting()` lines 590-614
  - `BuildDolls()` lines 617-646
  - `BuildResultCanvas()` lines 754-919
  - `BuildResultCardAnchor()` lines 921-928
  - scoreboard fallback wiring lines 948-1202
  - `BuildModelDoll()` emotion assignment lines 1290-1338
- `Assets/00.Main/Code/Scripts/System/EmotionType.cs`
  - `EmotionType` enum values lines 1-9

## Source-Grounded Decision Tree

### 1. Trigger

`[Original Text/Data]` `GameManager.Update()` checks Spacebar at `Assets/00.Main/Code/Scripts/Core/GameManager.cs:21-26`: if `Keyboard.current.spaceKey.wasPressedThisFrame`, it calls `EndGame()` and returns.
`→ [Exact Interpretation]` Pressing Spacebar triggers the end-result flow immediately, provided `isGameOver` is still false.
`→ [Detailed Explanation/Example]` After several dolls have been caught, pressing Spacebar enters the same result path as the timer ending.

`[Original Text/Data]` `GameManager.Update()` also subtracts `Time.deltaTime` and calls `EndGame()` when `timer <= 0` at `Assets/00.Main/Code/Scripts/Core/GameManager.cs:29-34`.
`→ [Exact Interpretation]` The recipe-card flow is not Spacebar-only; normal timer completion also triggers it.
`→ [Detailed Explanation/Example]` In a full session, the player can wait for the 180-second timer. In a demo, Spacebar skips the wait.

Decision:

```text
Runtime update
├─ isGameOver == true
│  └─ Do nothing.
├─ Spacebar pressed this frame
│  └─ EndGame()
└─ timer <= 0
   └─ EndGame()
```

### 2. Result UI Selection

`[Original Text/Data]` `GameManager.EndGame()` first calls `FindAnyObjectByType<EmotionRecipeUI>()`; if found, it calls `recipeUI.ShowResult()` and returns at `Assets/00.Main/Code/Scripts/Core/GameManager.cs:44-50`.
`→ [Exact Interpretation]` Result option 1, `EmotionRecipeUI`, has highest priority.
`→ [Detailed Explanation/Example]` If both `EmotionRecipeUI` and `ResultPanelUI` exist in the scene, the recipe card wins.

`[Original Text/Data]` `GameManager.EndGame()` falls back to `ResultPanelUI` at `Assets/00.Main/Code/Scripts/Core/GameManager.cs:52-58`, then to `ResultUI` at lines 60-65.
`→ [Exact Interpretation]` The scoreboard ending and plain text result are fallback paths, not the primary recipe-card branch.
`→ [Detailed Explanation/Example]` Removing `EmotionRecipeUI` from a scene will cause the result to use `ResultPanelUI` if present.

Decision:

```text
EndGame()
├─ EmotionRecipeUI exists
│  └─ Option 1: show dominant-emotion recipe card.
├─ ResultPanelUI exists
│  └─ Option 2: switch scoreboard into result mode.
└─ ResultUI exists
   └─ Fallback: print plain text result.
```

### 3. Prerequisites For Option 1

`[Original Text/Data]` `SceneSetup.Build()` creates `GameResultManager`, `GameManager`, and calls `BuildResultCanvas()` at `Assets/Editor/SceneSetup.cs:93-103`.
`→ [Exact Interpretation]` The generated main scene is intended to contain both the result data manager and the recipe-card UI.
`→ [Detailed Explanation/Example]` The main scene generated through `Claw Crew > Build Main Scene` should make option 1 available automatically.

`[Original Text/Data]` `SceneSetup.BuildResultCanvas()` creates `EmotionRecipeCanvas`, sets `Canvas.renderMode = WorldSpace`, initializes `CanvasGroup.alpha = 0f`, creates the title/recipe/ingredients/message/count fields, then adds `EmotionRecipeUI` and assigns references at `Assets/Editor/SceneSetup.cs:765-918`.
`→ [Exact Interpretation]` The card exists during gameplay but is visually hidden until result time.
`→ [Detailed Explanation/Example]` `FindAnyObjectByType<EmotionRecipeUI>()` can find it because the object remains active; only alpha hides it.

`[Original Text/Data]` `SceneSetup.BuildResultCanvas()` sets `cg.blocksRaycasts = false` and `cg.interactable = false` at `Assets/Editor/SceneSetup.cs:912-918`.
`→ [Exact Interpretation]` The hidden result card is not an interactive gameplay UI.
`→ [Detailed Explanation/Example]` It should not block raycasts while the player is catching dolls.

Decision:

```text
Option 1 prerequisites
├─ GameResultManager.Instance exists
├─ GameManager exists
├─ EmotionRecipeUI exists and is active
├─ EmotionRecipeUI has TMP fields assigned
└─ CanvasGroup starts at alpha 0
```

### 4. Data Source

`[Original Text/Data]` `CatchDetector.OnTriggerEnter()` routes caught dolls through `DollInfo.emotionType` into `GameResultManager.Instance.RegisterCatch(info.emotionType)` at `Assets/00.Main/Code/Scripts/System/CatchDetector.cs:33-56`.
`→ [Exact Interpretation]` Emotion counts come from dolls that enter `CatchZone` and pass the doll/tag/info checks.
`→ [Detailed Explanation/Example]` A caught Happy doll increments the Happy count by one.

`[Original Text/Data]` `GameResultManager.RegisterCatch()` increments `emotionCounts[emotion]` and invokes `OnCatch` at `Assets/00.Main/Code/Scripts/System/GameResultManager.cs:25-34`.
`→ [Exact Interpretation]` The stored result is a dictionary from `EmotionType` to caught count.
`→ [Detailed Explanation/Example]` If Happy is caught three times, `emotionCounts[EmotionType.Happy]` becomes `3`.

`[Original Text/Data]` `ClawTestController.Update()` calls `RegisterAcceptedTry()` when a drop input is accepted in `S.Idle`, and `RegisterAcceptedTry()` calls `GameResultManager.Instance.RegisterTry()` at `Assets/01.Test/SJM/ClawTestController.cs:398-405` and `454-465`.
`→ [Exact Interpretation]` Try count increases once per accepted Idle-to-Drop claw cycle.
`→ [Detailed Explanation/Example]` Multiple input sources merge into one `drop` boolean, then one accepted try is recorded.

`[Original Text/Data]` `EmotionRecipeUI.ShowResult()` reads `GameResultManager.Instance.GetResults()` and `GetTryCount()` at `Assets/00.Main/Code/Scripts/UI/EmotionRecipeUI.cs:211-212`.
`→ [Exact Interpretation]` The recipe card uses the stored catch counts and try count as its source of truth.
`→ [Detailed Explanation/Example]` The recipe card does not recalculate catches from scene objects at result time.

Decision:

```text
Caught doll enters CatchZone
└─ CatchDetector
   └─ DollInfo.emotionType
      └─ GameResultManager.RegisterCatch(emotion)
         └─ emotionCounts[emotion]++

Accepted claw drop
└─ ClawTestController.RegisterAcceptedTry()
   └─ GameResultManager.RegisterTry()
      └─ tryCount++

Show result
└─ EmotionRecipeUI.ShowResult()
   ├─ results = GameResultManager.GetResults()
   └─ tryCount = GameResultManager.GetTryCount()
```

### 5. Dominant Emotion Selection

`[Original Text/Data]` `EmotionRecipeUI.ShowResult()` filters counts to `kv.Value > 0`, orders them descending by value, and stores them in `sorted` at `Assets/00.Main/Code/Scripts/UI/EmotionRecipeUI.cs:214-218`.
`→ [Exact Interpretation]` Only positive catch counts participate in the recipe ingredients and dominant-emotion selection.
`→ [Detailed Explanation/Example]` Emotions with zero catches are omitted from `ingredients`.

`[Original Text/Data]` `EmotionRecipeUI.ShowResult()` sets `dominant = sorted.Count > 0 ? sorted[0].Key : EmotionType.Happy` at `Assets/00.Main/Code/Scripts/UI/EmotionRecipeUI.cs:220-221`.
`→ [Exact Interpretation]` The dominant emotion is the first entry after descending count sort; if there are no catches, Happy is used as the fallback dominant emotion.
`→ [Detailed Explanation/Example]` Happy=3 and Sad=1 makes Happy dominant. No catches makes Happy dominant even though no Happy doll was caught.

Decision:

```text
results dictionary
└─ keep only counts > 0
   ├─ sorted.Count > 0
   │  └─ dominant = first highest-count emotion
   └─ sorted.Count == 0
      └─ dominant = Happy fallback
```

### 6. Tie Handling

`[Original Text/Data]` The only implemented ordering call is `.OrderByDescending(kv => kv.Value)` at `Assets/00.Main/Code/Scripts/UI/EmotionRecipeUI.cs:215-218`; no secondary sort key or explicit tie branch appears before `dominant = sorted[0].Key` at lines 220-221.
`→ [Exact Interpretation]` There is no explicit implemented tie rule.
`→ [Detailed Explanation/Example]` Happy=2 and Sad=2 is not documented in code as "Happy wins", "first caught wins", or "show mixed recipe"; the implementation simply takes the first entry produced by the current sorted sequence.

Implemented branch:

```text
Two or more emotions share the highest count
└─ No explicit tie rule in current code
   └─ dominant = sorted[0].Key after count-only descending sort
```

Planned/proposed branch, not implemented:

```text
Two or more emotions share the highest count
├─ Proposed option A: deterministic enum priority
├─ Proposed option B: first-caught emotion wins
├─ Proposed option C: mixed-emotion recipe card
└─ Required code change: add explicit tie branch before selecting dominant
```

### 7. Zero-Catch Fallback

`[Original Text/Data]` If `sorted.Count == 0`, `dominant` becomes `EmotionType.Happy` at `Assets/00.Main/Code/Scripts/UI/EmotionRecipeUI.cs:220-221`; if `sorted.Count == 0`, `ingredients = "아직 재료가 없어요..."` at lines 235-239.
`→ [Exact Interpretation]` Zero catches show the Happy recipe/message but a no-ingredients line.
`→ [Detailed Explanation/Example]` With `tryCount = 3` and zero caught dolls, the card uses recipe name `햇살 가득 바닐라 라떼`, ingredients `아직 재료가 없어요...`, Happy message, and count line `시도 3회 / 성공 0마리`.

Implemented branch:

```text
No caught dolls
├─ dominant = Happy
├─ recipeName = 햇살 가득 바닐라 라떼
├─ ingredients = 아직 재료가 없어요...
├─ message = 오늘 당신의 하루는 반짝반짝 빛나고 있어요!
└─ countLine = 시도 {tryCount}회 / 성공 0마리
```

### 8. Ingredient Text

`[Original Text/Data]` `EmotionRecipeUI.GetAmountUnit()` maps count `1` to `한 꼬집`, count `2` to `두 방울`, and all other counts to `{count}스푼` at `Assets/00.Main/Code/Scripts/UI/EmotionRecipeUI.cs:90-100`.
`→ [Exact Interpretation]` Ingredient units are count-dependent Korean recipe metaphors.
`→ [Detailed Explanation/Example]` `Happy=3` becomes `기쁨 3스푼`; `Sad=1` becomes `슬픔 한 꼬집`.

`[Original Text/Data]` `EmotionRecipeUI.ShowResult()` builds ingredients by iterating `sorted`, mapping each emotion through `EmotionKoreanNames`, and joining parts with `" + "` at `Assets/00.Main/Code/Scripts/UI/EmotionRecipeUI.cs:232-249`.
`→ [Exact Interpretation]` Ingredients include every caught emotion with positive count, ordered by descending count.
`→ [Detailed Explanation/Example]` Happy=3, Sad=1 produces `기쁨 3스푼 + 슬픔 한 꼬집`.

Implemented ingredient names:

| EmotionType | Korean ingredient name | Source |
| --- | --- | --- |
| `Happy` | `기쁨` | `EmotionRecipeUI.cs:76-84` |
| `Sad` | `슬픔` | `EmotionRecipeUI.cs:76-84` |
| `Angry` | `분노` | `EmotionRecipeUI.cs:76-84` |
| `Sleepy` | `졸림` | `EmotionRecipeUI.cs:76-84` |
| `Scared` | `두려움` | `EmotionRecipeUI.cs:76-84` |
| `Serene` | `평온` | `EmotionRecipeUI.cs:76-84` |

### 9. Per-Emotion Recipe Branches

`[Original Text/Data]` `RecipeNames` and `Messages` dictionaries define every implemented recipe branch at `Assets/00.Main/Code/Scripts/UI/EmotionRecipeUI.cs:52-72`.
`→ [Exact Interpretation]` The project already implements exact Korean recipe names and messages for all six enum values.
`→ [Detailed Explanation/Example]` These are not proposed copy; they are current runtime data used by `ShowResult()`.

| Dominant emotion | Recipe name | Message | Source |
| --- | --- | --- | --- |
| `Happy` | `햇살 가득 바닐라 라떼` | `오늘 당신의 하루는 반짝반짝 빛나고 있어요!` | `EmotionRecipeUI.cs:52-72` |
| `Sad` | `비 오는 날의 카모마일 티` | `가끔은 눈물도 좋은 양념이 되어요. 괜찮아요.` | `EmotionRecipeUI.cs:52-72` |
| `Angry` | `불꽃 시나몬 에스프레소` | `뜨거운 에너지가 가득한 하루! 그 열정을 응원해요.` | `EmotionRecipeUI.cs:52-72` |
| `Sleepy` | `달빛 라벤더 핫초코` | `포근한 꿈에 빠질 시간. 오늘도 수고했어요.` | `EmotionRecipeUI.cs:52-72` |
| `Scared` | `안개 속 민트 모카` | `용기는 두려움을 넘는 거예요. 당신은 이미 충분히 용감해요.` | `EmotionRecipeUI.cs:52-72` |
| `Serene` | `고요한 오후의 말차 라떼` | `평온한 당신의 하루가 주변을 따뜻하게 해요.` | `EmotionRecipeUI.cs:52-72` |

Branch tree:

```text
dominant == Happy
└─ 햇살 가득 바닐라 라떼

dominant == Sad
└─ 비 오는 날의 카모마일 티

dominant == Angry
└─ 불꽃 시나몬 에스프레소

dominant == Sleepy
└─ 달빛 라벤더 핫초코

dominant == Scared
└─ 안개 속 민트 모카

dominant == Serene
└─ 고요한 오후의 말차 라떼
```

### 10. UI Output Fields

`[Original Text/Data]` `EmotionRecipeUI.ShowResult()` computes `recipeName`, `ingredients`, `message`, `totalCaught`, and `countLine = $"시도 {tryCount}회 / 성공 {totalCaught}마리"` at `Assets/00.Main/Code/Scripts/UI/EmotionRecipeUI.cs:227-261`.
`→ [Exact Interpretation]` The card output has four dynamic fields plus a fixed title.
`→ [Detailed Explanation/Example]` With `tryCount=4`, Happy=3, Sad=1, the count line is `시도 4회 / 성공 4마리`.

`[Original Text/Data]` `ShowResultCoroutine()` types title, recipe name, ingredients, and message, then writes count text immediately at `Assets/00.Main/Code/Scripts/UI/EmotionRecipeUI.cs:380-402`.
`→ [Exact Interpretation]` The display sequence is animated text first, instant count line last.
`→ [Detailed Explanation/Example]` The title `오늘의 감정 레시피` appears via typewriter effect before the recipe name.

Output tree:

```text
Recipe card output
├─ titleText
│  └─ 오늘의 감정 레시피
├─ recipeNameText
│  └─ recipe name for dominant emotion
├─ ingredientsText
│  └─ positive emotion counts joined with " + "
├─ messageText
│  └─ message for dominant emotion
└─ countText
   └─ 시도 {tryCount}회 / 성공 {totalCaught}마리
```

### 11. Placement

`[Original Text/Data]` `SceneSetup.BuildResultCardAnchor()` creates `ResultCardAnchor` at `ResultCardAnchorPosition` and rotates it with `GetUprightLookRotation()` at `Assets/Editor/SceneSetup.cs:921-928`.
`→ [Exact Interpretation]` The generated scene has a fixed world anchor for the result card.
`→ [Detailed Explanation/Example]` The card is placed at the intended stable reading pose instead of directly following the current HMD pitch/roll.

`[Original Text/Data]` `EmotionRecipeUI.ApplyResultCardPlacement()` uses the serialized anchor, falls back to `GameObject.Find("ResultCardAnchor")`, and then falls back to a hardcoded pose at `Assets/00.Main/Code/Scripts/UI/EmotionRecipeUI.cs:267-297`.
`→ [Exact Interpretation]` Missing serialized anchor does not block result display.
`→ [Detailed Explanation/Example]` Older scenes can still show the card if a `ResultCardAnchor` object exists by name, or else at fallback position `(1.08, 1.45, -0.46)`.

Placement tree:

```text
ShowResultCoroutine()
└─ ApplyResultCardPlacement()
   ├─ serialized resultCardAnchor exists
   │  └─ use anchor position/rotation
   ├─ GameObject named ResultCardAnchor exists
   │  └─ cache and use that transform
   └─ no anchor exists
      └─ use fallback fixed world pose and log warning
```

### 12. Lighting Effect

`[Original Text/Data]` `ShowResultCoroutine()` calls `FindAnyObjectByType<Light>()`, stores one `mainLight`, and interpolates `mainLight.intensity` to `originalIntensity * 0.4f` at `Assets/00.Main/Code/Scripts/UI/EmotionRecipeUI.cs:346-365`.
`→ [Exact Interpretation]` The implemented result-card branch dims one found light to 40% of its original intensity.
`→ [Detailed Explanation/Example]` If Unity returns `L_Main`, `L_Main` dims from `1.45` to `0.58`; other lights are not explicitly dimmed by this code path.

`[Original Text/Data]` `SceneSetup.BuildLighting()` creates multiple scene lights at `Assets/Editor/SceneSetup.cs:590-614`, and `SceneSetup.BuildMachine()` creates an `InteriorSpot` light at `Assets/Editor/SceneSetup.cs:563-572`.
`→ [Exact Interpretation]` The scene contains multiple lights, but the recipe-card coroutine targets only one.
`→ [Detailed Explanation/Example]` The user-facing requirement "surrounding lights darken to 40%" is only partially represented by the current implementation.

Implemented branch:

```text
Result card begins
└─ FindAnyObjectByType<Light>()
   ├─ Light found
   │  └─ intensity lerps to originalIntensity * 0.4
   └─ No Light found
      └─ skip dimming
```

Planned/proposed branch, not implemented:

```text
Result card begins
└─ Find all surrounding/environment lights
   └─ lerp every selected light to 40% of its own original intensity
```

### 13. Fallback Result Modes

`[Original Text/Data]` `ResultPanelUI.ResultSequence()` independently sorts results, selects dominant as `sorted[0].Key` or Happy, then displays English scoreboard-style messages at `Assets/00.Main/Code/Scripts/UI/ResultPanelUI.cs:81-172`.
`→ [Exact Interpretation]` The fallback result panel is not the Korean recipe-card flow.
`→ [Detailed Explanation/Example]` If option 1 is missing, a player may see `YOUR MOOD`, `Happy (75%)`, and English messages instead of `오늘의 감정 레시피`.

`[Original Text/Data]` `ResultUI.ShowResult()` prints `=== Result ===`, `Tried:`, and each raw result pair at `Assets/00.Main/Code/Scripts/UI/ResultUI.cs:10-26`.
`→ [Exact Interpretation]` The final fallback is plain diagnostic text.
`→ [Detailed Explanation/Example]` This path has no recipe names, no Korean message branch, and no lighting dim effect.

## End-To-End Implemented Flow

```text
Player catches dolls
└─ CatchZone trigger
   └─ CatchDetector reads DollInfo.emotionType
      └─ GameResultManager.RegisterCatch(emotion)
         └─ emotionCounts updated

Player attempts claw drops
└─ ClawTestController accepts drop in Idle
   └─ GameResultManager.RegisterTry()
      └─ tryCount updated

Player presses Spacebar, or timer reaches zero
└─ GameManager.EndGame()
   └─ EmotionRecipeUI found
      └─ EmotionRecipeUI.ShowResult()
         ├─ read emotionCounts and tryCount
         ├─ sort positive counts descending
         ├─ choose dominant emotion, or Happy for zero catches
         ├─ generate recipeName, ingredients, message, countLine
         └─ start ShowResultCoroutine()
            ├─ place card at ResultCardAnchor/fallback
            ├─ wait fadeInDelay
            ├─ dim one found Light to 40%
            ├─ fade CanvasGroup from 0 to 1
            ├─ type title
            ├─ type recipe name
            ├─ type ingredients
            ├─ type message
            └─ write count line
```

## Test Cases

| Case | Setup | Trigger | Implemented expected result |
| --- | --- | --- | --- |
| Happy dominant | `Happy=3`, `Sad=1`, `tryCount=4` | Press Spacebar | Title `오늘의 감정 레시피`; recipe `햇살 가득 바닐라 라떼`; ingredients `기쁨 3스푼 + 슬픔 한 꼬집`; message `오늘 당신의 하루는 반짝반짝 빛나고 있어요!`; count `시도 4회 / 성공 4마리`; one found `Light` dims to 40%. |
| Sad dominant with two catches | `Sad=2`, `Happy=1`, `tryCount=3` | Press Spacebar | Recipe `비 오는 날의 카모마일 티`; ingredients begin with `슬픔 두 방울`; count `시도 3회 / 성공 3마리`. |
| Angry dominant | `Angry=3`, `Serene=2`, `tryCount=5` | Timer reaches zero or Spacebar | Recipe `불꽃 시나몬 에스프레소`; message `뜨거운 에너지가 가득한 하루! 그 열정을 응원해요.` |
| Sleepy dominant | `Sleepy=1`, `tryCount=1` | Press Spacebar | Recipe `달빛 라벤더 핫초코`; ingredients `졸림 한 꼬집`; count `시도 1회 / 성공 1마리`. |
| Scared dominant | `Scared=4`, `Happy=1`, `tryCount=6` | Press Spacebar | Recipe `안개 속 민트 모카`; ingredients include `두려움 4스푼`; count `시도 6회 / 성공 5마리`. |
| Serene dominant | `Serene=2`, `Sad=1`, `tryCount=4` | Press Spacebar | Recipe `고요한 오후의 말차 라떼`; ingredients begin with `평온 두 방울`; count `시도 4회 / 성공 3마리`. |
| Zero catches | no positive emotion counts, `tryCount=0` or more | Press Spacebar | Dominant fallback is Happy; recipe `햇살 가득 바닐라 라떼`; ingredients `아직 재료가 없어요...`; Happy message; count uses actual try count and `성공 0마리`. |
| Tie for highest count | example `Happy=2`, `Sad=2`, `tryCount=4` | Press Spacebar | No explicit tie branch exists. Current code selects `sorted[0].Key` after count-only descending sort. Treat this as unspecified until a tie rule is implemented. |
| Missing `EmotionRecipeUI` | remove/disable recipe canvas but keep `ResultPanelUI` | End game | `GameManager.EndGame()` uses `ResultPanelUI.ShowResult()` instead of the recipe card. |
| Missing result-card anchor | remove serialized anchor and no object named `ResultCardAnchor` | End game | `EmotionRecipeUI` uses fallback pose `(1.08, 1.45, -0.46)` and logs a warning. |
| No scene light found | scene has no `Light` component | End game | Card still fades/types text; dimming branch is skipped. |

## Proposed Branches Not Implemented

### Explicit Tie Rule

`[Original Text/Data]` No secondary ordering or tie branch exists in `EmotionRecipeUI.ShowResult()` at `Assets/00.Main/Code/Scripts/UI/EmotionRecipeUI.cs:214-221`.
`→ [Exact Interpretation]` Tie behavior is not a designed result-card branch.
`→ [Detailed Explanation/Example]` Add a documented rule before `dominant` assignment if ties should be deterministic or mixed.

### Surrounding Lights At 40%

`[Original Text/Data]` The implemented dimming path stores a single `Light mainLight = FindAnyObjectByType<Light>()` at `Assets/00.Main/Code/Scripts/UI/EmotionRecipeUI.cs:349`.
`→ [Exact Interpretation]` The implementation does not iterate all scene lights.
`→ [Detailed Explanation/Example]` If the design requirement is that all surrounding lights darken to 40%, add a selected-light collection and dim each light independently.
