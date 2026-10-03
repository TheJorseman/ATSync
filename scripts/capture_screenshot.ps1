# Captura screenshot de ATSync.App usando PrintWindow PW_RENDERFULLCONTENT
$exe = 'C:\Users\migue\OneDrive\Documentos\GitHub\ATSync\dist\ATSync.App.exe'
$out = 'C:\Users\migue\OneDrive\Documentos\GitHub\ATSync\docs\screenshot.png'

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

# Cerrar instancias previas
Get-Process -Name 'ATSync.App' -ErrorAction SilentlyContinue | Stop-Process -Force

# Arrancar
$proc = Start-Process -FilePath $exe -PassThru -WindowStyle Normal
Write-Host "Started PID $($proc.Id)"
Start-Sleep -Seconds 6  # esperar a que Avalonia renderice

$code = @'
using System;
using System.Runtime.InteropServices;
public class W {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
'@
Add-Type -TypeDefinition $code -Language CSharp

$emDash = [char]8212
$target = "ATSync ${emDash} ATS mod sync"

# Buscar nuestra ventana por PID
$found = [IntPtr]::Zero
$callback = [W+EnumWindowsProc]{
    param($h, $l)
    $pid_ = 0
    [void][W]::GetWindowThreadProcessId($h, [ref]$pid_)
    if ($pid_ -eq $script:proc.Id -and [W]::IsWindowVisible($h)) {
        $len = [W]::GetWindowTextLength($h)
        if ($len -gt 0) {
            $sb = New-Object System.Text.StringBuilder ($len + 1)
            [void][W]::GetWindowText($h, $sb, $sb.Capacity)
            $script:found = $h
            Write-Host "Found window: '$($sb.ToString())'"
            return $false  # stop enum
        }
    }
    return $true
}
$script:proc = $proc
[void][W]::EnumWindows($callback, [IntPtr]::Zero)

if ($found -eq [IntPtr]::Zero) {
    Write-Host "Window not found by PID, trying by title"
    $callback2 = [W+EnumWindowsProc]{
        param($h, $l)
        $len = [W]::GetWindowTextLength($h)
        if ($len -gt 0) {
            $sb = New-Object System.Text.StringBuilder ($len + 1)
            [void][W]::GetWindowText($h, $sb, $sb.Capacity)
            $t = $sb.ToString()
            if ($t -like "*ATSync*" -and [W]::IsWindowVisible($h)) {
                $script:found = $h
                Write-Host "Found by title: '$t'"
                return $false
            }
        }
        return $true
    }
    [void][W]::EnumWindows($callback2, [IntPtr]::Zero)
}

if ($found -eq [IntPtr]::Zero) {
    Write-Host "ERROR: ATSync window not found"
    Stop-Process -Id $proc.Id -Force
    exit 1
}

# Maximizar y traer a foreground
[W]::ShowWindow($found, 3) | Out-Null   # SW_MAXIMIZE
[W]::SetForegroundWindow($found) | Out-Null
Start-Sleep -Seconds 2

# Obtener rect
$rect = New-Object W+RECT
[void][W]::GetWindowRect($found, [ref]$rect)
$w = $rect.R - $rect.L
$h = $rect.B - $rect.T
Write-Host "Window: $w x $h at ($($rect.L),$($rect.T))"

# PrintWindow con PW_RENDERFULLCONTENT (2)
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
$ok = [W]::PrintWindow($found, $hdc, 2)
$g.ReleaseHdc($hdc)
$g.Dispose()
Write-Host "PrintWindow ok=$ok"

# Si falla, reintentar con BitBlt
if (-not $ok) {
    Write-Host "PrintWindow failed, fallback BitBlt"
    $bmp.Dispose()
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($rect.L, $rect.T, 0, 0, $bmp.Size)
    $g.Dispose()
}

$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Saved $out"

Stop-Process -Id $proc.Id -Force
Write-Host "Done"