$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$taskRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output = Join-Path $taskRoot 'outputs/steam-achievements'
$icons = Join-Path $output 'icons'
$sources = Join-Path $output 'sources'
New-Item -ItemType Directory -Force -Path $icons,$sources | Out-Null
$manifest = Get-Content -LiteralPath (Join-Path $output 'source-images.json') -Raw | ConvertFrom-Json
$catalog = Get-Content -LiteralPath (Join-Path $output 'achievement-catalog.json') -Raw | ConvertFrom-Json
$sheet = [System.Drawing.Bitmap]::new(6*256,5*288)
$sg = [System.Drawing.Graphics]::FromImage($sheet)
$sg.Clear([System.Drawing.Color]::FromArgb(25,25,29))
$font = [System.Drawing.Font]::new('Arial',11)
try {
    for($i=0;$i -lt $manifest.Count;$i++) {
        $entry = $manifest[$i]
        $source = $entry.source
        if(-not [System.IO.Path]::IsPathRooted($source)) { $source = Join-Path $taskRoot $source }
        Copy-Item -LiteralPath $source -Destination (Join-Path $sources ($entry.api_name+'.png'))
        $image = [System.Drawing.Image]::FromFile($source)
        $bitmap = [System.Drawing.Bitmap]::new(256,256)
        $g = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            if($image.Width -ne $image.Height) { throw ('Non-square source: '+$entry.api_name) }
            $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
            $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
            $g.DrawImage($image,[System.Drawing.Rectangle]::new(0,0,256,256),0,0,$image.Width,$image.Height,[System.Drawing.GraphicsUnit]::Pixel)
            $bitmap.Save((Join-Path $icons ($entry.api_name+'_ACHIEVED.png')),[System.Drawing.Imaging.ImageFormat]::Png)
            $x=($i%6)*256; $y=[math]::Floor($i/6)*288
            $sg.DrawImageUnscaled($bitmap,$x,$y)
            $sg.DrawString(('{0:D2}  {1}' -f [int]$catalog[$i].number,$catalog[$i].english_name),$font,[System.Drawing.Brushes]::White,[single]($x+4),[single]($y+260))
        } finally { $g.Dispose(); $bitmap.Dispose(); $image.Dispose() }
    }
    $sheet.Save((Join-Path $output 'all-26-preview.png'),[System.Drawing.Imaging.ImageFormat]::Png)
} finally { $font.Dispose(); $sg.Dispose(); $sheet.Dispose() }
Write-Output ('Exported '+$manifest.Count+' square 256px PNG icons; originals preserved in sources.')
