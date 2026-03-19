namespace Pc_Specs_with_screen_reader_
{
    using System.Management;
    using System.Runtime.InteropServices;
    using System.Linq;
    using System.Speech.Synthesis;
    using System.Net.Http;
    using System.IO.Compression;
    using System.Threading.Tasks;
    using LibreHardwareMonitor.Hardware;
    using System.Windows.Forms;
    using System.IO;
    using System.Collections.Generic;

    public partial class Form1 : Form
    {
        private SpeechSynthesizer? _synth;
        private bool _screenReaderEnabled = false;
        private Control? _lastHoveredControl;
        private string? _lastSpokenText;
        private LibreHardwareMonitor.Hardware.Computer? _computer;
        private System.Windows.Forms.Timer? _tempTimer;
        private bool _useFahrenheit = false;
        private float? _lastCpuTempC;
        private float? _lastGpuTempC;
        // removed logging fields (_logWriter/_logging/_logFilePath)
        private string _cpuVendor = "Unknown";
        private bool _showLoadStatus = false;
        private string? _baseInfoText;
        // Update settings
        private const string CurrentVersion = "1.0.0";
        private const string GitHubRawBase = "https://raw.githubusercontent.com/error0420/Pc-specs-with-screen-reader/main/";
        private const string GitHubZipUrl = "https://github.com/error0420/Pc-specs-with-screen-reader/archive/refs/heads/main.zip";
        
        
        public Form1()
        {
            InitializeComponent();
            // attach mouse move handlers so screen reader can announce hovered control
            this.MouseMove += Form_MouseMove;
            this.MouseEnter += Form_MouseEnter;
            this.MouseLeave += Form_MouseLeave;

            // attach handlers for existing controls and for controls added later
            foreach (Control c in this.Controls)
            {
                c.MouseEnter += Control_MouseEnter;
                c.MouseLeave += Control_MouseLeave;
                c.MouseMove += Control_MouseMove;
            }
            this.ControlAdded += Controls_ControlAdded;

            // Initialize hardware monitor and start temperature timer
            try
            {
                _computer = new LibreHardwareMonitor.Hardware.Computer()
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMotherboardEnabled = true,
                    IsMemoryEnabled = false,
                    IsStorageEnabled = false,
                };
                _computer.Open();
            }
            catch { _computer = null; }

            // Detect CPU vendor (Intel / AMD / Unknown) for vendor-aware labeling
            try { _cpuVendor = DetectCpuVendor(); } catch { _cpuVendor = "Unknown"; }

            _tempTimer = new System.Windows.Forms.Timer();
            _tempTimer.Interval = 5000; // 5 seconds - reduce polling frequency to save CPU
            _tempTimer.Tick += TempTimer_Tick;
            _tempTimer.Start();

            // Pause polling when minimized to save CPU
            this.Resize += Form1_Resize;

            this.FormClosing += Form1_FormClosing;
            // start background update check
            _ = CheckForUpdatesAsync();
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            try
            {
                if (_tempTimer != null)
                {
                    _tempTimer.Stop();
                    _tempTimer.Tick -= TempTimer_Tick;
                    _tempTimer.Dispose();
                    _tempTimer = null;
                }
            }
            catch { }

            try
            {
                if (_computer != null)
                {
                    _computer.Close();
                    _computer = null;
                }
            }
            catch { }
        }

        // tempLogButton and logging removed

        private void tempUnitButton_Click(object? sender, EventArgs e)
        {
            _useFahrenheit = !_useFahrenheit;
            tempUnitButton.Text = _useFahrenheit ? "Show °C" : "Show °F";
            UpdateTempLabels();
            RefreshInfoLabel();
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                using var http = new HttpClient();
                // increase timeout for slow networks
                http.Timeout = TimeSpan.FromSeconds(30);
                var versionUrl = GitHubRawBase + "version.txt";
                HttpResponseMessage resp;
                try
                {
                    resp = await http.GetAsync(versionUrl);
                }
                catch (Exception ex)
                {
                    AppendUpdateLog($"Version check failed: {ex}");
                    try { this.Invoke((MethodInvoker)(() => MessageBox.Show($"Failed to check for updates:\n{ex.Message}", "Update Check Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning))); } catch { }
                    return;
                }

                if (!resp.IsSuccessStatusCode)
                {
                    AppendUpdateLog($"Version check returned status {(int)resp.StatusCode} ({resp.ReasonPhrase}) for URL {versionUrl}");
                    return;
                }

                var remoteVersion = (await resp.Content.ReadAsStringAsync()).Trim();
                if (string.IsNullOrEmpty(remoteVersion))
                {
                    AppendUpdateLog("Version check returned empty version string.");
                    return;
                }

                if (!string.Equals(remoteVersion, CurrentVersion, StringComparison.OrdinalIgnoreCase))
                {
                    AppendUpdateLog($"Update available: local={CurrentVersion}, remote={remoteVersion}");
                    try { this.Invoke((MethodInvoker)(() => MessageBox.Show($"Update available: {remoteVersion}. Click 'Latest Source Code' to download.", "Update Available", MessageBoxButtons.OK, MessageBoxIcon.Information))); } catch { }
                }
                else
                {
                    AppendUpdateLog($"Up to date: {CurrentVersion}");
                    try { this.Invoke((MethodInvoker)(() => MessageBox.Show("You are on the latest version.", "Up to date", MessageBoxButtons.OK, MessageBoxIcon.Information))); } catch { }
                }
            }
            catch (Exception ex)
            {
                AppendUpdateLog($"Unexpected error during update check: {ex}");
            }
        }

        private async Task DownloadLatestZipAsync()
        {
            var updatesDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PcSpecsUpdates");
            Directory.CreateDirectory(updatesDir);
            var zipPath = Path.Combine(updatesDir, "update.zip");
            var extractDir = Path.Combine(updatesDir, "extracted_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));

            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromMinutes(5);
                var url = GitHubZipUrl;

                HttpResponseMessage resp;
                try
                {
                    resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                }
                catch (Exception ex)
                {
                    AppendUpdateLog($"Download failed (request): {ex}");
                    try { this.Invoke((MethodInvoker)(() => MessageBox.Show($"Failed to download update:\n{ex.Message}", "Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Error))); } catch { }
                    return;
                }

                if (!resp.IsSuccessStatusCode)
                {
                    AppendUpdateLog($"Download failed: {(int)resp.StatusCode} {resp.ReasonPhrase} for URL {url}");
                    try { this.Invoke((MethodInvoker)(() => MessageBox.Show($"Failed to download update. HTTP {(int)resp.StatusCode}: {resp.ReasonPhrase}", "Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Error))); } catch { }
                    return;
                }

                try
                {
                    using var stream = await resp.Content.ReadAsStreamAsync();
                    using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await stream.CopyToAsync(fs);
                }
                catch (Exception ex)
                {
                    AppendUpdateLog($"Download failed (save): {ex}");
                    try { this.Invoke((MethodInvoker)(() => MessageBox.Show($"Failed saving update:\n{ex.Message}", "Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Error))); } catch { }
                    return;
                }

                // Validate the downloaded file is a valid zip before extracting
                try
                {
                    using var testZip = ZipFile.OpenRead(zipPath);
                    // Access entry count to force reading the central directory
                    _ = testZip.Entries.Count;
                }
                catch (Exception ex)
                {
                    AppendUpdateLog($"Downloaded file is not a valid zip: {ex}");
                    try { this.Invoke((MethodInvoker)(() => MessageBox.Show($"Downloaded file is not a valid zip archive. The download may be corrupt or incomplete.\nPlease try again.\n\nFile: {zipPath}", "Invalid Download", MessageBoxButtons.OK, MessageBoxIcon.Error))); } catch { }
                    try { File.Delete(zipPath); } catch { }
                    return;
                }

                // Extract
                try
                {
                    Directory.CreateDirectory(extractDir);
                    ZipFile.ExtractToDirectory(zipPath, extractDir);
                    AppendUpdateLog($"Update downloaded and extracted to {extractDir}");
                    try { this.Invoke((MethodInvoker)(() => MessageBox.Show($"Downloaded the latest source code.\nSaved to:\n{extractDir}", "Download Complete", MessageBoxButtons.OK, MessageBoxIcon.Information))); } catch { }
                }
                catch (Exception ex)
                {
                    AppendUpdateLog($"Extraction failed: {ex}");
                    try { this.Invoke((MethodInvoker)(() => MessageBox.Show($"Downloaded update but extraction failed:\n{ex.Message}\nFiles are in: {zipPath}", "Extraction Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning))); } catch { }
                }
            }
            catch (Exception ex)
            {
                AppendUpdateLog($"Unexpected download error: {ex}");
                try { this.Invoke((MethodInvoker)(() => MessageBox.Show($"An unexpected error occurred while downloading the update:\n{ex.Message}", "Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error))); } catch { }
            }
        }

        private void AppendUpdateLog(string message)
        {
            try
            {
                var updatesDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PcSpecsUpdates");
                Directory.CreateDirectory(updatesDir);
                var logPath = Path.Combine(updatesDir, "update.log");
                File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] {message}" + Environment.NewLine);
            }
            catch { }
        }

        private void updateButton_Click(object? sender, EventArgs e)
        {
            // start download in background
            _ = DownloadLatestZipAsync();
        }

        private void RefreshInfoLabel()
        {
            try
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(_baseInfoText)) parts.Add(_baseInfoText!);
                // After base info, optionally append live sensor/status lines
                var extraLines = new List<string>();

                // If specs were loaded, show temps and utilization under the RAM line
                if (_showLoadStatus)
                {
                    // CPU temp
                    if (_lastCpuTempC.HasValue)
                    {
                        var cVal = _lastCpuTempC.Value;
                        var display = _useFahrenheit ? CToF(cVal) : cVal;
                        var unit = _useFahrenheit ? "°F" : "°C";
                        extraLines.Add($"CPU ({_cpuVendor}) Temp: {display:F1} {unit}");
                    }
                    else
                    {
                        extraLines.Add($"CPU ({_cpuVendor}) Temp: Unknown");
                    }

                    // GPU temp
                    if (_lastGpuTempC.HasValue)
                    {
                        var gVal = _lastGpuTempC.Value;
                        var display = _useFahrenheit ? CToF(gVal) : gVal;
                        var unit = _useFahrenheit ? "°F" : "°C";
                        extraLines.Add($"GPU Temp: {display:F1} {unit}");
                    }
                    else
                    {
                        extraLines.Add("GPU Temp: Unknown");
                    }

                    // CPU utilization removed to avoid extra WMI calls that may cause lag
                }

                if (extraLines.Count > 0) parts.Add(string.Join(Environment.NewLine, extraLines));

                infoLabel.Text = parts.Count > 0 ? string.Join(Environment.NewLine + Environment.NewLine, parts) : string.Empty;
            }
            catch { }
        }

        private void UpdateTempLabels()
        {
            try
            {
                // Dedicated cpu/gpu/util labels were removed; refresh the infoLabel which contains the live lines
                RefreshInfoLabel();
            }
            catch { }
        }

        private void Form1_Resize(object? sender, EventArgs e)
        {
            try
            {
                if (_tempTimer == null) return;
                if (this.WindowState == FormWindowState.Minimized)
                {
                    _tempTimer.Stop();
                }
                else
                {
                    // when restoring, restart timer
                    _tempTimer.Start();
                }
            }
            catch { }
        }

        private static float CToF(float c) => (c * 9f / 5f) + 32f;

        private void Controls_ControlAdded(object? sender, ControlEventArgs e)
        {
            var c = e.Control;
            c.MouseEnter += Control_MouseEnter;
            c.MouseLeave += Control_MouseLeave;
            c.MouseMove += Control_MouseMove;
        }

        private void loadButton_Click(object sender, EventArgs e)
        {
            // Toggle: if specs are showing, hide them
            if (_showLoadStatus)
            {
                _showLoadStatus = false;
                _baseInfoText = null;
                loadButton.Text = "Load Specs";
                RefreshInfoLabel();
                return;
            }

            // show immediate feedback
            infoLabel.Text = "Displaying specs...";
            infoLabel.Refresh();
            Application.DoEvents();

            var cpu = GetCpuInfo();
            var gpu = GetGpuInfo();
            var monitor = GetMonitorInfo(out string hertz, out string resolution, out string monitorBrand);
            GetRamInfo(out string ramAmountGb, out string ramUsageText);

            // Put monitor and motherboard info on the first lines, then CPU, GPU, RAM
            var mb = GetMotherboardInfo();
            _baseInfoText = string.Join(Environment.NewLine, new[]
            {
                $"Monitor: {monitorBrand} - {monitor} - {resolution} @ {hertz}Hz",
                string.Empty,
                $"Motherboard: {mb}",
                string.Empty,
                $"CPU: {cpu}",
                string.Empty,
                $"GPU: {gpu}",
                string.Empty,
                $"RAM: {ramAmountGb} GB{(string.IsNullOrEmpty(ramUsageText) ? string.Empty : " (" + ramUsageText + ")")}",
            });

            _showLoadStatus = true;
            loadButton.Text = "Hide Specs";
            RefreshInfoLabel();

            // Temperatures will be updated live by the timer
        }

        private void TempTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                if (_computer == null)
                {
                    _lastCpuTempC = null;
                    _lastGpuTempC = null;
                    UpdateTempLabels();
                    return;
                }

                // Update hardware readings
                foreach (var hw in _computer.Hardware)
                {
                    // single update per hardware node (including its subhardware) to reduce work
                    hw.Update();
                    foreach (var sub in hw.SubHardware ?? Enumerable.Empty<IHardware>())
                    {
                        sub.Update();
                    }
                }

                float? cpuTemp = null;
                float? gpuTemp = null;

                // collect highest CPU temperature available and first GPU temp
                foreach (var hw in _computer.Hardware)
                {
                    TraverseHardwareForTemps(hw, ref cpuTemp, ref gpuTemp);
                }

                // Fallback: if no CPU temp found, look for sensors whose name suggests CPU/package/core
                if (!cpuTemp.HasValue)
                {
                    try
                    {
                        foreach (var hw in _computer.Hardware)
                        {
                            // skip GPU hardware when searching for CPU
                            if (hw.HardwareType == HardwareType.GpuAmd || hw.HardwareType == HardwareType.GpuIntel || hw.HardwareType == HardwareType.GpuNvidia)
                                continue;

                            foreach (var sensor in hw.Sensors)
                            {
                                if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
                                {
                                    var name = (sensor.Name ?? string.Empty).ToLowerInvariant();
                                    if (name.Contains("cpu") || name.Contains("package") || name.Contains("core") || name.Contains("pkg") || name.Contains("tm0"))
                                    {
                                        if (!cpuTemp.HasValue || sensor.Value.Value > cpuTemp.Value) cpuTemp = sensor.Value.Value;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                // Last resort: take the highest non-GPU temperature sensor available
                if (!cpuTemp.HasValue)
                {
                    try
                    {
                        foreach (var hw in _computer.Hardware)
                        {
                            if (hw.HardwareType == HardwareType.GpuAmd || hw.HardwareType == HardwareType.GpuIntel || hw.HardwareType == HardwareType.GpuNvidia)
                                continue;

                            foreach (var sensor in hw.Sensors)
                            {
                                if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
                                {
                                    if (!cpuTemp.HasValue || sensor.Value.Value > cpuTemp.Value) cpuTemp = sensor.Value.Value;
                                }
                            }
                        }
                    }
                    catch { }
                }

                _lastCpuTempC = cpuTemp;
                _lastGpuTempC = gpuTemp;

                // CPU utilization polling removed to reduce WMI calls and avoid potential lag
                UpdateTempLabels();
            }
            catch { }
        }

        private void TraverseHardwareForTemps(IHardware hw, ref float? cpuTemp, ref float? gpuTemp)
        {
            try
            {
                foreach (var sensor in hw.Sensors)
                {
                    if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
                    {
                        var name = (sensor.Name ?? string.Empty).ToLowerInvariant();
                        // Primary: sensors reported under CPU hardware or hardware whose name indicates CPU vendor
                        var hwName = (hw.Name ?? string.Empty).ToLowerInvariant();
                        if (hw.HardwareType == HardwareType.Cpu || hwName.Contains("intel") || hwName.Contains("amd") || hwName.Contains("ryzen"))
                        {
                            if (!cpuTemp.HasValue || sensor.Value.Value > cpuTemp.Value)
                                cpuTemp = sensor.Value.Value;
                        }
                        // Motherboard may expose CPU/package/core temps -> treat as CPU
                        else if (hw.HardwareType == HardwareType.Motherboard)
                        {
                            if (name.Contains("cpu") || name.Contains("package") || name.Contains("core"))
                            {
                                if (!cpuTemp.HasValue || sensor.Value.Value > cpuTemp.Value)
                                    cpuTemp = sensor.Value.Value;
                            }
                        }
                        // GPUs
                        else if (hw.HardwareType == HardwareType.GpuAmd || hw.HardwareType == HardwareType.GpuIntel || hw.HardwareType == HardwareType.GpuNvidia)
                        {
                            if (!gpuTemp.HasValue) gpuTemp = sensor.Value.Value;
                        }
                        else
                        {
                            // Fallback: if sensor name mentions CPU, consider it
                            if (name.Contains("cpu") || name.Contains("package") || name.Contains("core"))
                            {
                                if (!cpuTemp.HasValue || sensor.Value.Value > cpuTemp.Value)
                                    cpuTemp = sensor.Value.Value;
                            }
                        }
                    }
                }

                // recurse into subhardware if present
                if (hw.SubHardware != null)
                {
                    foreach (var sub in hw.SubHardware)
                    {
                        TraverseHardwareForTemps(sub, ref cpuTemp, ref gpuTemp);
                    }
                }
            }
            catch { }
        }

        // GetTemps is no longer used; live updates are provided by the timer using _computer

        private void GetRamInfo(out string amountGb, out string usageText)
        {
            amountGb = "Unknown";
            usageText = string.Empty;
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Capacity, SMBIOSMemoryType, MemoryType FROM Win32_PhysicalMemory");
                ulong totalBytes = 0;
                int detectedTypeCode = 0;
                foreach (ManagementObject mo in searcher.Get())
                {
                    var capObj = mo["Capacity"];
                    if (capObj != null && ulong.TryParse(capObj.ToString(), out var cap)) totalBytes += cap;

                    var smObj = mo["SMBIOSMemoryType"] ?? mo["MemoryType"];
                    if (smObj != null)
                    {
                        try { detectedTypeCode = Convert.ToInt32(smObj); }
                        catch { }
                    }
                }

                if (totalBytes > 0)
                {
                    amountGb = Math.Round(totalBytes / 1024.0 / 1024.0 / 1024.0, 2).ToString();
                }
                // now gather usage via Win32_OperatingSystem
                try
                {
                    using var osSearcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
                    foreach (ManagementObject mo in osSearcher.Get())
                    {
                        var totalKbObj = mo["TotalVisibleMemorySize"];
                        var freeKbObj = mo["FreePhysicalMemory"];
                        if (totalKbObj != null && freeKbObj != null && ulong.TryParse(totalKbObj.ToString(), out var totalKb) && ulong.TryParse(freeKbObj.ToString(), out var freeKb))
                        {
                            var usedKb = totalKb > freeKb ? totalKb - freeKb : 0UL;
                            var usedGb = Math.Round(usedKb / 1024.0 / 1024.0, 2);
                            var totalGb = Math.Round(totalKb / 1024.0 / 1024.0, 2);
                            var percent = totalKb > 0 ? Math.Round(usedKb * 100.0 / totalKb, 1) : 0.0;
                            usageText = $"{usedGb} GB used ({percent}% )";
                            break;
                        }
                    }
                }
                catch { }
            }
            catch { }
        }

        // removed MemoryTypeCodeToString - no longer needed

        private void screenReaderButton_Click(object? sender, EventArgs e)
        {
            _screenReaderEnabled = !_screenReaderEnabled;
            screenReaderButton.Text = _screenReaderEnabled ? "Screen Reader: On" : "Screen Reader: Off";
            if (_screenReaderEnabled)
            {
                if (_synth == null)
                {
                    _synth = new SpeechSynthesizer();
                    try
                    {
                        _synth.SelectVoiceByHints(VoiceGender.Female, VoiceAge.Adult);
                    }
                    catch { }
                }
            }
            else
            {
                try { _synth?.SpeakAsyncCancelAll(); } catch { }
            }
            // if user toggles screen reader while minimized, ensure polling remains paused
            if (this.WindowState == FormWindowState.Minimized && _tempTimer != null)
            {
                try { _tempTimer.Stop(); } catch { }
            }
        }

        private void Control_MouseMove(object? sender, MouseEventArgs e)
        {
            if (!_screenReaderEnabled) return;
            if (sender is Control c)
            {
                // MouseMove will update which line in a label is under cursor; avoid repeated speaks
                if (c == infoLabel)
                {
                    string? textToSpeak = GetLabelLineUnderCursor(infoLabel);
                    if (!string.IsNullOrWhiteSpace(textToSpeak) && textToSpeak != _lastSpokenText)
                    {
                        _lastSpokenText = textToSpeak;
                        SpeakText(textToSpeak);
                    }
                }
                else
                {
                    // for other controls, do nothing here - MouseEnter will handle immediate speak
                }
            }
        }

        private void Control_MouseEnter(object? sender, EventArgs e)
        {
            if (!_screenReaderEnabled) return;
            if (sender is Control c)
            {
                string? textToSpeak = null;
                if (c == infoLabel)
                {
                    textToSpeak = GetLabelLineUnderCursor(infoLabel);
                }
                else
                {
                    textToSpeak = !string.IsNullOrWhiteSpace(c.Text) ? c.Text : c.Name;
                }

                if (!string.IsNullOrWhiteSpace(textToSpeak) && textToSpeak != _lastSpokenText)
                {
                    _lastSpokenText = textToSpeak;
                    SpeakText(textToSpeak);
                }
            }
        }

        private void Control_MouseLeave(object? sender, EventArgs e)
        {
            if (!_screenReaderEnabled) return;
            _lastSpokenText = null;
            try { _synth?.SpeakAsyncCancelAll(); } catch { }
        }

        private void Form_MouseEnter(object? sender, EventArgs e)
        {
            // reset when entering form background
            if (!_screenReaderEnabled) return;
            _lastSpokenText = null;
        }

        private void Form_MouseLeave(object? sender, EventArgs e)
        {
            if (!_screenReaderEnabled) return;
            _lastSpokenText = null;
            try { _synth?.SpeakAsyncCancelAll(); } catch { }
        }

        private string? GetLabelLineUnderCursor(Label lbl)
        {
            try
            {
                var pt = lbl.PointToClient(Cursor.Position);
                var lines = lbl.Text?.Split(new[] { Environment.NewLine }, StringSplitOptions.None) ?? new string[0];
                if (lines.Length == 0) return null;
                int lineHeight = Math.Max(1, lbl.Font.Height);
                int idx = pt.Y / lineHeight;
                if (idx < 0) idx = 0;
                if (idx >= lines.Length) idx = lines.Length - 1;
                return lines[idx].Trim();
            }
            catch { return null; }
        }

        private void Form_MouseMove(object? sender, MouseEventArgs e)
        {
            // when hovering over form background, don't speak
            if (!_screenReaderEnabled) return;
            if (_lastHoveredControl != this)
            {
                _lastHoveredControl = this;
                _lastSpokenText = null;
                // silence on background
                _synth?.SpeakAsyncCancelAll();
            }
        }

        private void SpeakText(string text)
        {
            try
            {
                _synth?.SpeakAsyncCancelAll();
                _synth?.SpeakAsync(text);
            }
            catch { }
        }

        private string GetCpuInfo()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("select Name from Win32_Processor");
                foreach (ManagementObject mo in searcher.Get())
                {
                    var name = mo["Name"] as string;
                    if (!string.IsNullOrEmpty(name)) return name.Trim();
                }
            }
            catch { }
            return "Unknown CPU";
        }

        private string DetectCpuVendor()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("select Manufacturer, Name from Win32_Processor");
                foreach (ManagementObject mo in searcher.Get())
                {
                    var manu = (mo["Manufacturer"] as string) ?? string.Empty;
                    var name = (mo["Name"] as string) ?? string.Empty;
                    var combined = (manu + " " + name).ToLowerInvariant();
                    if (combined.Contains("intel")) return "Intel";
                    if (combined.Contains("amd") || combined.Contains("ryzen")) return "AMD";
                }
            }
            catch { }
            return "Unknown";
        }

        private string GetGpuInfo()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("select Name from Win32_VideoController");
                foreach (ManagementObject mo in searcher.Get())
                {
                    var name = mo["Name"] as string;
                    if (!string.IsNullOrEmpty(name)) return name.Trim();
                }
            }
            catch { }
            return "Unknown GPU";
        }

        private string GetMonitorInfo(out string hertz, out string resolution, out string monitorBrand)
        {
            hertz = "Unknown";
            resolution = "Unknown";
            monitorBrand = "Unknown";

            try
            {
                // Resolution from primary screen
                var scr = System.Windows.Forms.Screen.PrimaryScreen;
                resolution = $"{scr.Bounds.Width} x {scr.Bounds.Height}";

                // Refresh rate using EnumDisplaySettings
                var dev = new DEVMODE();
                dev.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                if (EnumDisplaySettings(scr.DeviceName, ENUM_CURRENT_SETTINGS, ref dev))
                {
                    hertz = dev.dmDisplayFrequency.ToString();
                }

                // Try WmiMonitorID for brand/name
                try
                {
                    var scope = new ManagementScope("\\\\.\\root\\wmi");
                    var query = new ObjectQuery("SELECT * FROM WmiMonitorID");
                    using var searcher = new ManagementObjectSearcher(scope, query);
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        var nameArr = mo["UserFriendlyName"] as ushort[];
                        var manuArr = mo["ManufacturerName"] as ushort[];
                        var name = DecodeUshortArray(nameArr);
                        var manu = DecodeUshortArray(manuArr);
                        if (!string.IsNullOrEmpty(name))
                        {
                            monitorBrand = name;
                            if (!string.IsNullOrEmpty(manu)) monitorBrand += $" ({manu})";
                            break;
                        }
                    }
                }
                catch { }
            }
            catch { }

            return System.Windows.Forms.Screen.PrimaryScreen.DeviceName;
        }

        

        private static string DecodeUshortArray(ushort[] arr)
        {
            if (arr == null) return string.Empty;
            try
            {
                var chars = arr.TakeWhile(v => v != 0).Select(v => (char)v).ToArray();
                return new string(chars).Trim();
            }
            catch { return string.Empty; }
        }

        private string GetMotherboardInfo()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard");
                foreach (ManagementObject mo in searcher.Get())
                {
                    var manu = (mo["Manufacturer"] as string) ?? string.Empty;
                    var prod = (mo["Product"] as string) ?? string.Empty;
                    var combined = (manu + " " + prod).Trim();
                    if (!string.IsNullOrEmpty(combined)) return combined;
                }
            }
            catch { }
            return "Unknown";
        }

        private const int ENUM_CURRENT_SETTINGS = -1;

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }
    }
}
