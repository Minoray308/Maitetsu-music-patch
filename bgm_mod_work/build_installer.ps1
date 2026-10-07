$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
$compileArgs = @(
    '/nologo', '/target:winexe', '/optimize+',
    ('/out:' + (Join-Path $taskRoot 'BGM_Mod_Manager.exe')),
    '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll',
    ('/resource:' + (Join-Path $taskRoot 'patch.xp3') + ',patch.xp3'),
    ('/resource:' + (Join-Path $taskRoot 'bgm_music_mod\player.tjs') + ',bgm_music_mod/player.tjs'),
    ('/resource:' + (Join-Path $taskRoot 'bgm_music_mod\README.md') + ',bgm_music_mod/README.md'),
    ('/resource:' + (Join-Path $PSScriptRoot 'installer-legacy.txt') + ',legacy'),
    (Join-Path $PSScriptRoot 'ModManager.cs')
)
& $compiler @compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
