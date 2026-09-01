# 未使用资源分析报告

生成时间：2026-09-01 16:24
根资源数：68　依赖图资源数：715

> 注意：本报告只统计资源引用关系。
> 通过 Resources.Load / Addressables / 字符串路径动态加载的资源无法被检测，
> 删除前请再确认一次。

## Assets/ThirdParty

| 子目录 | 已使用 | 未使用 | 未使用体积 | 可否整包删除 |
|---|---:|---:|---:|---|
| Blink | 21 | 27 | 71.5 MB | 部分使用，逐个确认 |
| Kevin Iglesias | 184 | 48 | 18.3 MB | 部分使用，逐个确认 |
| Materials | 1 | 0 | 0 B | 部分使用，逐个确认 |
| SazenGames | 50 | 1 | 37 KB | 部分使用，逐个确认 |
| SimpleNaturePack | 55 | 2 | 1.3 MB | 部分使用，逐个确认 |

<details><summary>未使用文件清单（78 个）</summary>

```
Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Combat/BlockingLoop.fbx
Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Combat/BowShot.fbx
Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Combat/Buff.fbx
Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Combat/CastingLoop.fbx
Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Combat/Death.fbx
Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Combat/GetHit.fbx
Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Combat/IdleCombat.fbx
Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Combat/MeleeAttack_OneHanded.fbx
Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Combat/MeleeAttack_TwoHanded.fbx
Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Combat/PunchLeft.fbx
Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Combat/PunchRight.fbx
Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Combat/SpellCast.fbx
Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Combat/StunnedLoop.fbx
Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Gathering/Gathering.fbx
Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Gathering/MiningLoop.fbx
Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Movement/JumpWhileRunning.fbx
Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Movement/RollBackward.fbx
Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Movement/RollForward.fbx
Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Movement/RollLeft.fbx
Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Movement/RollRight.fbx
Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Movement/RunBackwardLeft.fbx
Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Movement/RunBackwardRight.fbx
Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Movement/RunLeft.fbx
Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Movement/RunRight.fbx
Assets/ThirdParty/Blink/Art/Characters/LowPoly/Demo_FREE_LowPolyCharacter/PPProfile.asset
Assets/ThirdParty/Blink/Art/Characters/LowPoly/Editor/MaterialTilingOffset.cs
Assets/ThirdParty/Blink/Art/Characters/LowPoly/FREE_HumanLowPoly/Textures_Humans/LowPolyArmorTexture.png
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Female/Combat/HumanF@Death01.fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Female/Movement/Run/RootMotion/HumanF@Run01_Backward [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Female/Movement/Run/RootMotion/HumanF@Run01_BackwardLeft [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Female/Movement/Run/RootMotion/HumanF@Run01_BackwardRight [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Female/Movement/Run/RootMotion/HumanF@Run01_Forward [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Female/Movement/Run/RootMotion/HumanF@Run01_ForwardLeft [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Female/Movement/Run/RootMotion/HumanF@Run01_ForwardRight [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Female/Movement/Run/RootMotion/HumanF@Run01_Left [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Female/Movement/Run/RootMotion/HumanF@Run01_Right [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Female/Movement/Strafe/StrafeRun/RootMotion/HumanF@StrafeRun01_BackwardLeft [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Female/Movement/Strafe/StrafeRun/RootMotion/HumanF@StrafeRun01_BackwardRight [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Female/Movement/Strafe/StrafeRun/RootMotion/HumanF@StrafeRun01_ForwardLeft [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Female/Movement/Strafe/StrafeRun/RootMotion/HumanF@StrafeRun01_ForwardRight [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Female/Movement/Strafe/StrafeRun/RootMotion/HumanF@StrafeRun01_Left [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Female/Movement/Strafe/StrafeRun/RootMotion/HumanF@StrafeRun01_Right [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/HumanMeleeAnimationsFREE_BlenderFiles.zip
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/RootMotion/HumanM@Run01_Backward [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/RootMotion/HumanM@Run01_BackwardLeft [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/RootMotion/HumanM@Run01_BackwardRight [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/RootMotion/HumanM@Run01_Forward [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/RootMotion/HumanM@Run01_ForwardLeft [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/RootMotion/HumanM@Run01_ForwardRight [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/RootMotion/HumanM@Run01_Left [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/RootMotion/HumanM@Run01_Right [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Movement/Strafe/StrafeRun/RootMotion/HumanM@StrafeRun01_BackwardLeft [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Movement/Strafe/StrafeRun/RootMotion/HumanM@StrafeRun01_BackwardRight [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Movement/Strafe/StrafeRun/RootMotion/HumanM@StrafeRun01_ForwardLeft [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Movement/Strafe/StrafeRun/RootMotion/HumanM@StrafeRun01_ForwardRight [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Movement/Strafe/StrafeRun/RootMotion/HumanM@StrafeRun01_Left [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Male/Movement/Strafe/StrafeRun/RootMotion/HumanM@StrafeRun01_Right [RM].fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Masked Poses/Human@HandsClosed01.fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Masked Poses/HumanF@ObjectGripShoulder01_L.fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Masked Poses/HumanF@ObjectGripShoulder01_R.fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Masked Poses/HumanF@ObjectGripShoulder02_L.fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Masked Poses/HumanF@ObjectGripShoulder02_R.fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Masked Poses/HumanM@ObjectGripShoulder01_L.fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Masked Poses/HumanM@ObjectGripShoulder01_R.fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Masked Poses/HumanM@ObjectGripShoulder02_L.fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/Masked Poses/HumanM@ObjectGripShoulder02_R.fbx
Assets/ThirdParty/Kevin Iglesias/Human Animations/Human Melee Animations 2.0 FREE.pdf
Assets/ThirdParty/Kevin Iglesias/Human Animations/Models/Avatar Masks/Arms/Human Arm Left Mask.mask
Assets/ThirdParty/Kevin Iglesias/Human Animations/Models/Avatar Masks/Arms/Human Arm Right Mask.mask
Assets/ThirdParty/Kevin Iglesias/Human Animations/Models/Avatar Masks/Hands/Human Hand Left Mask.mask
Assets/ThirdParty/Kevin Iglesias/Human Animations/Models/Avatar Masks/Hands/Human Hand Right Mask.mask
Assets/ThirdParty/Kevin Iglesias/Human Animations/Models/Avatar Masks/Human Head Mask.mask
Assets/ThirdParty/Kevin Iglesias/Human Animations/Scripts/UpperBodyAnimations-SpineProxy.url
Assets/ThirdParty/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Melee Animations/Prefabs/Characters/HumanF_Dummy_Red.prefab
Assets/ThirdParty/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Melee Animations/Prefabs/Characters/HumanM_Dummy_Red.prefab
Assets/ThirdParty/SazenGames/Skeleton/Documentation/documentation.rtf
Assets/ThirdParty/SimpleNaturePack/SimpleNaturePack_2020.3_HDRP_v1.24.unitypackage
Assets/ThirdParty/SimpleNaturePack/SimpleNaturePack_2020.3_URP_v1.24.unitypackage
```

</details>

## Assets/Rowlan

| 子目录 | 已使用 | 未使用 | 未使用体积 | 可否整包删除 |
|---|---:|---:|---:|---|
| Fullscreen | 0 | 12 | 207 KB | **可整包删除** |

<details><summary>未使用文件清单（12 个）</summary>

```
Assets/Rowlan/Fullscreen/Documentation/Fullscreen Editor Play Mode.pdf
Assets/Rowlan/Fullscreen/Editor/FullscreenGameView.cs
Assets/Rowlan/Fullscreen/Editor/FullscreenSettings.cs
Assets/Rowlan/Fullscreen/Editor/FullscreenStateGuard.cs
Assets/Rowlan/Fullscreen/Editor/Platform/FullscreenPlatform.cs
Assets/Rowlan/Fullscreen/Editor/Platform/FullscreenPlatformMac.cs
Assets/Rowlan/Fullscreen/Editor/Platform/FullscreenPlatformWindows.cs
Assets/Rowlan/Fullscreen/Editor/Rowlan.Fullscreen.Editor.asmdef
Assets/Rowlan/Fullscreen/package.json
Assets/Rowlan/Fullscreen/Runtime/FullscreenKeybinds.cs
Assets/Rowlan/Fullscreen/Runtime/FullscreenKeyListener.cs
Assets/Rowlan/Fullscreen/Runtime/Rowlan.Fullscreen.Runtime.asmdef
```

</details>

## Assets/Fonts

| 子目录 | 已使用 | 未使用 | 未使用体积 | 可否整包删除 |
|---|---:|---:|---:|---|
| 07_SourceHanSansJ | 4 | 6 | 78.4 MB | 部分使用，逐个确认 |
| 09_SourceHanSansSC | 2 | 7 | 94.4 MB | 部分使用，逐个确认 |

<details><summary>未使用文件清单（13 个）</summary>

```
Assets/Fonts/07_SourceHanSansJ/LICENSE.txt
Assets/Fonts/07_SourceHanSansJ/OTF/Japanese/SourceHanSansJP-ExtraLight.otf
Assets/Fonts/07_SourceHanSansJ/OTF/Japanese/SourceHanSansJP-Heavy.otf
Assets/Fonts/07_SourceHanSansJ/OTF/Japanese/SourceHanSansJP-Light.otf
Assets/Fonts/07_SourceHanSansJ/OTF/Japanese/SourceHanSansJP-Medium.otf
Assets/Fonts/07_SourceHanSansJ/OTF/Japanese/SourceHanSansJP-Normal.otf
Assets/Fonts/09_SourceHanSansSC/LICENSE.txt
Assets/Fonts/09_SourceHanSansSC/OTF/SimplifiedChinese/SourceHanSansSC-Bold.otf
Assets/Fonts/09_SourceHanSansSC/OTF/SimplifiedChinese/SourceHanSansSC-ExtraLight.otf
Assets/Fonts/09_SourceHanSansSC/OTF/SimplifiedChinese/SourceHanSansSC-Heavy.otf
Assets/Fonts/09_SourceHanSansSC/OTF/SimplifiedChinese/SourceHanSansSC-Light.otf
Assets/Fonts/09_SourceHanSansSC/OTF/SimplifiedChinese/SourceHanSansSC-Normal.otf
Assets/Fonts/09_SourceHanSansSC/OTF/SimplifiedChinese/SourceHanSansSC-Regular.otf
```

</details>

## Assets/Art

| 子目录 | 已使用 | 未使用 | 未使用体积 | 可否整包删除 |
|---|---:|---:|---:|---|
| (根目录文件) | 0 | 2 | 29 KB | **可整包删除** |
| UI | 13 | 2 | 276 KB | 部分使用，逐个确认 |

<details><summary>未使用文件清单（4 个）</summary>

```
Assets/Art/2DUIpngpreset.preset
Assets/Art/UI/RPG/UI_RPG_Button_Blank_Normal.png
Assets/Art/UI/TeaShop/Tag2.png
Assets/Art/背包九宫格预设.preset
```

</details>

## Assets/Audio

| 子目录 | 已使用 | 未使用 | 未使用体积 | 可否整包删除 |
|---|---:|---:|---:|---|
| SFX | 2 | 0 | 0 B | 部分使用，逐个确认 |

---

**已使用合计：239.1 MB　未使用合计：264.5 MB**
