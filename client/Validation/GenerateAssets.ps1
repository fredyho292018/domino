$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../DominoGame'
$assets = Join-Path $project 'Assets/_Domino'
function GuidFor([string]$path) {
    $existingMeta = Join-Path $project ($path + '.meta')
    if (Test-Path -LiteralPath $existingMeta) {
        $existingGuid = [regex]::Match([IO.File]::ReadAllText($existingMeta), '(?m)^guid: ([a-f0-9]{32})')
        if ($existingGuid.Success) { return $existingGuid.Groups[1].Value }
    }
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($path.Replace('\','/'))
    $hash = [System.Security.Cryptography.MD5]::Create().ComputeHash($bytes)
    return ([System.BitConverter]::ToString($hash)).Replace('-','').ToLowerInvariant()
}
function WriteUtf8([string]$path, [string]$body) {
    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($path)) | Out-Null
    [System.IO.File]::WriteAllText($path, $body, [System.Text.UTF8Encoding]::new($false))
}
foreach ($folder in @('Art','Audio','Materials','Prefabs','Scenes','ScriptableObjects')) {
    [System.IO.Directory]::CreateDirectory((Join-Path $assets $folder)) | Out-Null
}
$tileGuid = GuidFor 'Assets/_Domino/Scripts/UI/DominoTileView.cs'
$playerGuid = GuidFor 'Assets/_Domino/Scripts/UI/PlayerView.cs'
$controllerGuid = GuidFor 'Assets/_Domino/Scripts/Client/DominoClientController.cs'
$tilePrefabGuid = GuidFor 'Assets/_Domino/Prefabs/DominoTile.prefab'
$playerPrefabGuid = GuidFor 'Assets/_Domino/Prefabs/Player.prefab'
function Prefab([string]$name, [string]$scriptGuid, [string]$extra) {
return @"
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &100000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 22400000}
  - component: {fileID: 11400000}
  m_Layer: 5
  m_Name: $name
  m_TagString: Untagged
  m_IsActive: 1
--- !u!224 &22400000
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 100000}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 0}
  m_AnchorMin: {x: 0.5, y: 0.5}
  m_AnchorMax: {x: 0.5, y: 0.5}
  m_AnchoredPosition: {x: 0, y: 0}
  m_SizeDelta: {x: 96, y: 46}
  m_Pivot: {x: 0.5, y: 0.5}
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 100000}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: $scriptGuid, type: 3}
  m_Name:
  m_EditorClassIdentifier:
$extra
"@
}
$tileAsset = Prefab 'DominoTile' $tileGuid "  previewA: 6`n  previewB: 4`n  faceUp: 1`n  vertical: 0"
$tileAsset = $tileAsset.Replace('  - component: {fileID: 11400000}', "  - component: {fileID: 11400000}`n  - component: {fileID: 22200000}")
$tileAsset += @"

--- !u!222 &22200000
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 100000}
  m_CullTransparentMesh: 1
"@
WriteUtf8 (Join-Path $assets 'Prefabs/DominoTile.prefab') $tileAsset
WriteUtf8 (Join-Path $assets 'Prefabs/Player.prefab') (Prefab 'Player' $playerGuid '')
$scene = Prefab 'DominoClient' $controllerGuid "  tilePrefab: {fileID: 11400000, guid: $tilePrefabGuid, type: 3}`n  playerPrefab: {fileID: 11400000, guid: $playerPrefabGuid, type: 3}"
$scene += @"

--- !u!1 &200000
GameObject:
  m_ObjectHideFlags: 0
  serializedVersion: 6
  m_Component:
  - component: {fileID: 400000}
  - component: {fileID: 2000000}
  m_Layer: 0
  m_Name: Main Camera
  m_TagString: MainCamera
  m_IsActive: 1
--- !u!4 &400000
Transform:
  m_ObjectHideFlags: 0
  m_GameObject: {fileID: 200000}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: -10}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_Children: []
  m_Father: {fileID: 0}
--- !u!20 &2000000
Camera:
  m_ObjectHideFlags: 0
  m_GameObject: {fileID: 200000}
  m_Enabled: 1
  serializedVersion: 2
  m_ClearFlags: 2
  m_BackGroundColor: {r: 0.03, g: 0.08, b: 0.08, a: 1}
  m_NormalizedViewPortRect:
    serializedVersion: 2
    x: 0
    y: 0
    width: 1
    height: 1
  near clip plane: 0.3
  far clip plane: 1000
  field of view: 60
  orthographic: 1
  orthographic size: 5
  m_Depth: -1
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_TargetTexture: {fileID: 0}
  m_TargetDisplay: 0
  m_HDR: 0
  m_AllowMSAA: 0
--- !u!1660057539 &9223372036854775807
SceneRoots:
  m_ObjectHideFlags: 0
  m_Roots:
  - {fileID: 22400000}
  - {fileID: 400000}
"@
WriteUtf8 (Join-Path $assets 'Scenes/DominoClient.unity') $scene
foreach ($file in Get-ChildItem (Join-Path $project 'Assets') -Recurse -File | Where-Object Extension -ne '.meta') {
    $relative = [System.IO.Path]::GetRelativePath($project, $file.FullName).Replace('\','/')
    $guid = GuidFor $relative
    $importer = switch ($file.Extension) { '.cs' { 'MonoImporter' } '.prefab' { 'PrefabImporter' } default { 'DefaultImporter' } }
    WriteUtf8 ($file.FullName + '.meta') "fileFormatVersion: 2`nguid: $guid`n${importer}:`n  externalObjects: {}`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n"
}
foreach ($folder in Get-ChildItem (Join-Path $project 'Assets') -Recurse -Directory) {
    $relative = [System.IO.Path]::GetRelativePath($project, $folder.FullName).Replace('\','/')
    $guid = GuidFor $relative
    WriteUtf8 ($folder.FullName + '.meta') "fileFormatVersion: 2`nguid: $guid`nfolderAsset: yes`nDefaultImporter:`n  externalObjects: {}`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n"
}
$sceneGuid = GuidFor 'Assets/_Domino/Scenes/DominoClient.unity'
WriteUtf8 (Join-Path $project 'ProjectSettings/EditorBuildSettings.asset') @"
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1045 &1
EditorBuildSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Scenes:
  - enabled: 1
    path: Assets/_Domino/Scenes/DominoClient.unity
    guid: $sceneGuid
  m_configObjects: {}
"@
