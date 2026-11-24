# UnduhFotoProdukSyncora.ps1
# Mengunduh satu gambar hasil pencarian Bing Images untuk setiap produk.
# Jalankan dari folder proyek TokoMusik.Web. Tidak mengubah database atau kode aplikasi.

$ErrorActionPreference = 'Stop'

$targetFolder = Join-Path (Get-Location) 'wwwroot\uploads\produk'
if (-not (Test-Path $targetFolder)) {
    New-Item -ItemType Directory -Path $targetFolder -Force | Out-Null
}

$products = @(
    @{ Code='GTR-001'; Name='Yamaha F310'; Query='Yamaha F310 acoustic guitar official product photo' },
    @{ Code='GTR-002'; Name='Yamaha F370'; Query='Yamaha F370 acoustic guitar official product photo' },
    @{ Code='GTR-003'; Name='Yamaha FS100C'; Query='Yamaha FS100C acoustic guitar official product photo' },
    @{ Code='GTR-004'; Name='Yamaha APX600'; Query='Yamaha APX600 electro acoustic guitar official product photo' },
    @{ Code='GTR-005'; Name='Yamaha C40 Classical'; Query='Yamaha C40 classical guitar official product photo' },
    @{ Code='GTR-006'; Name='Fender CD-60S'; Query='Fender CD-60S acoustic guitar official product photo' },
    @{ Code='GTR-007'; Name='Fender FA-125'; Query='Fender FA-125 acoustic guitar official product photo' },
    @{ Code='GTR-008'; Name='Squier SA-150'; Query='Squier SA-150 acoustic guitar product photo' },
    @{ Code='GTR-009'; Name='Fender Player Stratocaster'; Query='Fender Player Stratocaster electric guitar official product photo' },
    @{ Code='GTR-010'; Name='Squier Affinity Stratocaster'; Query='Squier Affinity Stratocaster electric guitar official product photo' },
    @{ Code='GTR-011'; Name='Ibanez GRX40'; Query='Ibanez GRX40 electric guitar official product photo' },
    @{ Code='GTR-012'; Name='Ibanez GRG121DX'; Query='Ibanez GRG121DX electric guitar official product photo' },
    @{ Code='GTR-013'; Name='Ibanez AW54'; Query='Ibanez AW54 acoustic guitar official product photo' },
    @{ Code='GTR-014'; Name='Ibanez AEG50'; Query='Ibanez AEG50 electro acoustic guitar official product photo' },
    @{ Code='GTR-015'; Name='Yamaha TRBX174 Bass'; Query='Yamaha TRBX174 bass guitar official product photo' },
    @{ Code='GTR-016'; Name='Squier Sonic Precision Bass'; Query='Squier Sonic Precision Bass official product photo' },
    @{ Code='KBD-001'; Name='Yamaha PSR-E373'; Query='Yamaha PSR-E373 keyboard official product photo' },
    @{ Code='KBD-002'; Name='Yamaha PSR-E473'; Query='Yamaha PSR-E473 keyboard official product photo' },
    @{ Code='KBD-003'; Name='Yamaha PSR-E283'; Query='Yamaha PSR-E283 keyboard official product photo' },
    @{ Code='KBD-004'; Name='Yamaha P-145'; Query='Yamaha P-145 digital piano official product photo' },
    @{ Code='KBD-005'; Name='Yamaha P-225'; Query='Yamaha P-225 digital piano official product photo' },
    @{ Code='KBD-006'; Name='Roland GO:KEYS 3'; Query='Roland GO KEYS 3 keyboard official product photo' },
    @{ Code='KBD-007'; Name='Roland E-X10'; Query='Roland E-X10 keyboard official product photo' },
    @{ Code='KBD-008'; Name='Roland FP-10'; Query='Roland FP-10 digital piano official product photo' },
    @{ Code='KBD-009'; Name='Roland JUNO-D6'; Query='Roland JUNO-D6 synthesizer official product photo' },
    @{ Code='KBD-010'; Name='Roland RD-08'; Query='Roland RD-08 stage piano official product photo' },
    @{ Code='DRM-001'; Name='Yamaha Rydeen Drum Set'; Query='Yamaha Rydeen acoustic drum set official product photo' },
    @{ Code='DRM-002'; Name='Yamaha Stage Custom Birch'; Query='Yamaha Stage Custom Birch drum set official product photo' },
    @{ Code='DRM-003'; Name='Yamaha DTX402K'; Query='Yamaha DTX402K electronic drum kit official product photo' },
    @{ Code='DRM-004'; Name='Yamaha DTX6K-X'; Query='Yamaha DTX6K-X electronic drum kit official product photo' },
    @{ Code='DRM-005'; Name='Roland TD-02K'; Query='Roland TD-02K electronic drum kit official product photo' },
    @{ Code='DRM-006'; Name='Roland TD-07KV'; Query='Roland TD-07KV electronic drum kit official product photo' },
    @{ Code='DRM-007'; Name='Roland SPD-SX'; Query='Roland SPD-SX sampling pad official product photo' },
    @{ Code='DRM-008'; Name='Roland SPD-30 Octapad'; Query='Roland SPD-30 Octapad official product photo' },
    @{ Code='ACC-001'; Name='Yamaha Guitar Strings'; Query='Yamaha guitar strings pack product photo' },
    @{ Code='ACC-002'; Name='Fender Acoustic Strings'; Query='Fender acoustic guitar strings pack product photo' },
    @{ Code='ACC-003'; Name='Ibanez Electric Strings'; Query='Ibanez electric guitar strings pack product photo' },
    @{ Code='ACC-004'; Name='Yamaha Guitar Tuner'; Query='Yamaha guitar tuner product photo' },
    @{ Code='ACC-005'; Name='Fender Guitar Picks Set'; Query='Fender guitar picks set product photo' },
    @{ Code='ACC-006'; Name='Ibanez Guitar Strap'; Query='Ibanez guitar strap product photo' },
    @{ Code='ACC-007'; Name='Yamaha Keyboard Stand'; Query='Yamaha keyboard stand product photo' },
    @{ Code='ACC-008'; Name='Roland Keyboard Pedal'; Query='Roland keyboard sustain pedal product photo' },
    @{ Code='ACC-009'; Name='Fender Guitar Cable 3m'; Query='Fender instrument guitar cable 3 meter product photo' },
    @{ Code='ACC-010'; Name='Roland Audio Cable'; Query='Roland audio cable product photo' },
    @{ Code='ACC-011'; Name='Yamaha Drumsticks'; Query='Yamaha drum sticks product photo' },
    @{ Code='ACC-012'; Name='Roland Drum Headphones'; Query='Roland electronic drum headphones product photo' },
    @{ Code='ACC-013'; Name='Fender Guitar Gig Bag'; Query='Fender guitar gig bag product photo' },
    @{ Code='ACC-014'; Name='Ibanez Guitar Hardcase'; Query='Ibanez guitar hardcase product photo' },
    @{ Code='ACC-015'; Name='Yamaha Microphone Stand'; Query='Yamaha microphone stand product photo' },
    @{ Code='ACC-016'; Name='Roland Expression Pedal'; Query='Roland expression pedal product photo' },
    @{ Code='ACC-017'; Name='Fender Guitar Wall Hanger'; Query='Fender guitar wall hanger product photo' },
    @{ Code='ACC-018'; Name='Ibanez Guitar Capo'; Query='Ibanez guitar capo product photo' }
)

function Get-ImageExtensionFromBytes([byte[]]$Bytes, [string]$ContentType, [string]$Url) {
    if ($Bytes.Length -ge 3 -and $Bytes[0] -eq 0xFF -and $Bytes[1] -eq 0xD8 -and $Bytes[2] -eq 0xFF) { return '.jpg' }
    if ($Bytes.Length -ge 8 -and $Bytes[0] -eq 0x89 -and $Bytes[1] -eq 0x50 -and $Bytes[2] -eq 0x4E -and $Bytes[3] -eq 0x47) { return '.png' }
    if ($Bytes.Length -ge 12 -and [Text.Encoding]::ASCII.GetString($Bytes,0,4) -eq 'RIFF' -and [Text.Encoding]::ASCII.GetString($Bytes,8,4) -eq 'WEBP') { return '.webp' }
    if ($Bytes.Length -ge 6 -and [Text.Encoding]::ASCII.GetString($Bytes,0,3) -eq 'GIF') { return '.gif' }
    return $null
}

$manifest = [System.Collections.Generic.List[object]]::new()
$success = 0
$failed = 0

Write-Host "Target foto: $targetFolder" -ForegroundColor Cyan
Write-Host "Jumlah produk: $($products.Count)" -ForegroundColor Cyan

foreach ($product in $products) {
    Write-Host "`n[$($product.Code)] Mencari $($product.Name)..." -ForegroundColor Yellow
    $saved = $false
    try {
        $searchUrl = 'https://www.bing.com/images/search?q=' + [uri]::EscapeDataString($product.Query) + '&form=HDRSC3'
        $page = Invoke-WebRequest -Uri $searchUrl -Headers @{ 'User-Agent' = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36' } -TimeoutSec 30
        $html = [string]$page.Content

        $matches = [regex]::Matches($html, '"murl"\s*:\s*"(?<url>https?[^"\\]+)"')
        if ($matches.Count -eq 0) {
            $matches = [regex]::Matches($html, 'murl&quot;\s*:\s*&quot;(?<url>https?[^&]+)')
        }

        $seen = @{}
        foreach ($match in $matches) {
            $imageUrl = $match.Groups['url'].Value.Replace('\u0026','&').Replace('\/','/')
            if ($seen.ContainsKey($imageUrl)) { continue }
            $seen[$imageUrl] = $true
            try {
                $response = Invoke-WebRequest -Uri $imageUrl -Headers @{ 'User-Agent' = 'Mozilla/5.0' } -TimeoutSec 20
                [byte[]]$bytes = $response.Content
                $extension = Get-ImageExtensionFromBytes -Bytes $bytes -ContentType ([string]$response.Headers['Content-Type']) -Url $imageUrl
                if ($null -eq $extension -or $bytes.Length -lt 5000) { continue }

                $fileName = $product.Code + $extension
                $filePath = Join-Path $targetFolder $fileName
                [System.IO.File]::WriteAllBytes($filePath, $bytes)
                $manifest.Add([pscustomobject]@{ Kode=$product.Code; Produk=$product.Name; File=$fileName; Sumber=$imageUrl; Status='Terunduh - periksa kecocokan foto' })
                Write-Host "  OK: $fileName" -ForegroundColor Green
                $success++
                $saved = $true
                break
            }
            catch {
                continue
            }
        }
        if (-not $saved) { throw 'Tidak berhasil menemukan/mengunduh gambar yang dapat dibaca.' }
    }
    catch {
        Write-Host "  GAGAL: $($_.Exception.Message)" -ForegroundColor Red
        $manifest.Add([pscustomobject]@{ Kode=$product.Code; Produk=$product.Name; File=''; Sumber=''; Status='Gagal - perlu pencarian manual' })
        $failed++
    }
    Start-Sleep -Milliseconds 500
}

$manifestPath = Join-Path $targetFolder 'manifest-foto-produk.csv'
$manifest | Export-Csv -Path $manifestPath -NoTypeInformation -Encoding UTF8

Write-Host "`nSelesai. Berhasil: $success | Gagal: $failed | Total: $($products.Count)" -ForegroundColor Cyan
Write-Host "Daftar sumber dan status: $manifestPath" -ForegroundColor Cyan
Write-Host 'PENTING: periksa hasil foto satu per satu sebelum dipakai di toko. Pencarian gambar otomatis bisa memilih varian atau model yang mirip.' -ForegroundColor Yellow
