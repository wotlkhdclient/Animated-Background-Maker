using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ABGM
{
    public partial class MainForm : Form
    {
        private string _videoPath = "";
        private string _outputDir = "";
        private CancellationTokenSource _cts;

        private GroupBox grpInput = null;
        private TextBox txtVideo = null;
        private Button btnBrowseVideo = null;
        private Label lblVideoInfo = null;

        private GroupBox grpOutput = null;
        private TextBox txtOutput = null;
        private Button btnBrowseOutput = null;

        private GroupBox grpOptions = null;
        private CheckBox chkMipmaps = null;
        private CheckBox chkAllFrames = null;
        private Label lblFps = null;
        private NumericUpDown nudFps = null;
        private Label lblSize = null;
        private ComboBox cmbSize = null;
        private ComboBox cmbFormat = null;

        private Button btnConvert = null;
        private Button btnCancel = null;

        private ProgressBar pbMain = null;
        private Label lblProgress = null;
        private Label lblResultHint = null;
        private TextBox txtResult = null;
        private Label lblLog = null;
        private RichTextBox rtbLog = null;
        private StatusStrip statusStrip = null;
        private ToolStripStatusLabel tsslFfmpeg = null;

        public MainForm()
        {
            InitializeComponent();
            BuildUI();
            CheckFfmpegStatus();
        }

        private void BuildUI()
        {
            this.Text = "Animated Background Maker";
            this.Size = new Size(600, 660);
            this.MinimumSize = new Size(560, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Tahoma", 8.25f);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.AllowDrop = true;
            this.DragEnter += MainForm_DragEnter;
            this.DragDrop += MainForm_DragDrop;

            int m = 12;
            int y = m;

            grpInput = new GroupBox
            {
                Text = "Input Video",
                Left = m,
                Top = y,
                Width = this.ClientSize.Width - m * 2,
                Height = 76,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };

            txtVideo = new TextBox
            {
                Left = 8,
                Top = 20,
                Width = grpInput.Width - 108,
                ReadOnly = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            btnBrowseVideo = new Button
            {
                Text = "Browse...",
                Left = txtVideo.Right + 6,
                Top = 18,
                Width = 80,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            lblVideoInfo = new Label
            {
                Left = 8,
                Top = 48,
                Width = grpInput.Width - 16,
                AutoSize = false,
                Height = 16,
                ForeColor = SystemColors.GrayText,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            txtVideo.TextChanged += (s, e) => UpdateVideoInfo();
            btnBrowseVideo.Click += BtnBrowseVideo_Click;
            grpInput.Controls.AddRange(new Control[] { txtVideo, btnBrowseVideo, lblVideoInfo });
            y += grpInput.Height + 8;

            grpOutput = new GroupBox
            {
                Text = "Output Folder for BLP Files",
                Left = m,
                Top = y,
                Width = grpInput.Width,
                Height = 56,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            txtOutput = new TextBox
            {
                Left = 8,
                Top = 20,
                Width = grpOutput.Width - 108,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            btnBrowseOutput = new Button
            {
                Text = "Browse...",
                Left = txtOutput.Right + 6,
                Top = 18,
                Width = 80,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            btnBrowseOutput.Click += BtnBrowseOutput_Click;
            grpOutput.Controls.AddRange(new Control[] { txtOutput, btnBrowseOutput });
            y += grpOutput.Height + 8;

            grpOptions = new GroupBox
            {
                Text = "Options",
                Left = m,
                Top = y,
                Width = grpInput.Width,
                Height = 126,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            chkMipmaps = new CheckBox
            {
                Text = "Generate mipmaps",
                Left = 8,
                Top = 20,
                AutoSize = true,
                Checked = true
            };
            chkAllFrames = new CheckBox
            {
                Text = "All frames (do not limit FPS)",
                Left = 8,
                Top = 44,
                AutoSize = true
            };
            chkAllFrames.CheckedChanged += (s, e) => nudFps.Enabled = !chkAllFrames.Checked;

            // Row: FPS | Format
            lblFps = new Label { Text = "Frames/sec:", Left = 8, Top = 74, AutoSize = true };
            nudFps = new NumericUpDown
            {
                Left = 86,
                Top = 70,
                Width = 60,
                Minimum = 1,
                Maximum = 120,
                Value = 30
            };
            var lblFormat = new Label { Text = "Format:", Left = 162, Top = 74, AutoSize = true };
            cmbFormat = new ComboBox
            {
                Left = 212,
                Top = 70,
                Width = 80,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbFormat.Items.AddRange(new object[] { "DXT1", "DXT5" });
            cmbFormat.SelectedIndex = 0;

            // Row: Texture size
            lblSize = new Label { Text = "Texture size:", Left = 8, Top = 102, AutoSize = true };
            cmbSize = new ComboBox
            {
                Left = 112,
                Top = 98,
                Width = 140,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbSize.Items.AddRange(new object[] { "1024x1024", "2048x1024" });
            cmbSize.SelectedIndex = 1;

            grpOptions.Controls.AddRange(new Control[]
                { chkMipmaps, chkAllFrames, lblFps, nudFps, lblFormat, cmbFormat, lblSize, cmbSize });
            y += grpOptions.Height + 10;

            btnConvert = new Button
            {
                Text = "Convert",
                Left = m,
                Top = y,
                Width = 130,
                Height = 28,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            btnConvert.Click += BtnConvert_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                Left = btnConvert.Right + 8,
                Top = y,
                Width = 80,
                Height = 28,
                Enabled = false,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            btnCancel.Click += BtnCancel_Click;
            y += btnConvert.Height + 10;

            pbMain = new ProgressBar
            {
                Left = m,
                Top = y,
                Width = this.ClientSize.Width - m * 2,
                Height = 20,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            y += pbMain.Height + 4;

            lblProgress = new Label
            {
                Left = m,
                Top = y,
                Width = pbMain.Width,
                Height = 16,
                AutoSize = false,
                Text = "Ready",
                ForeColor = SystemColors.GrayText,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            y += lblProgress.Height + 6;

            lblResultHint = new Label
            {
                Left = m,
                Top = y,
                Width = this.ClientSize.Width - m * 2,
                AutoSize = false,
                Height = 16,
                Text = "Paste the following line into Interface\\loginui.lua -> SceneList:",
                ForeColor = SystemColors.GrayText,
                Visible = false,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            y += lblResultHint.Height + 2;

            txtResult = new TextBox
            {
                Left = m,
                Top = y,
                Width = this.ClientSize.Width - m * 2,
                ReadOnly = true,
                BackColor = SystemColors.Info,
                ForeColor = SystemColors.InfoText,
                Font = new Font("Courier New", 9f, FontStyle.Bold),
                Visible = false,
                Cursor = Cursors.IBeam,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            txtResult.Click += (s, e) =>
            {
                txtResult.SelectAll();
                if (!string.IsNullOrEmpty(txtResult.Text))
                    Clipboard.SetText(txtResult.Text);
            };
            var toolTip = new ToolTip();
            toolTip.SetToolTip(txtResult, "Нажмите, чтобы скопировать в буфер обмена");
            y += txtResult.Height + 8;

            lblLog = new Label
            {
                Text = "Log:",
                Left = m,
                Top = y,
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            y += lblLog.Height + 2;

            rtbLog = new RichTextBox
            {
                Left = m,
                Top = y,
                Width = this.ClientSize.Width - m * 2,
                Height = 120,
                ReadOnly = true,
                BackColor = SystemColors.Window,
                BorderStyle = BorderStyle.Fixed3D,
                Font = new Font("Courier New", 8f),
                ScrollBars = RichTextBoxScrollBars.Vertical,
                DetectUrls = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom
            };
            rtbLog.LinkClicked += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(e.LinkText) { UseShellExecute = true });
                }
                catch { }
            };

            statusStrip = new StatusStrip { SizingGrip = true };
            tsslFfmpeg = new ToolStripStatusLabel
            {
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            statusStrip.Items.Add(tsslFfmpeg);

            this.Controls.AddRange(new Control[]
            {
                grpInput, grpOutput, grpOptions,
                btnConvert, btnCancel,
                pbMain, lblProgress,
                lblResultHint, txtResult,
                lblLog, rtbLog,
                statusStrip
            });
        }

        private static string FindFfmpeg()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            if (File.Exists(path)) return path;
            throw new FileNotFoundException(
                "ffmpeg.exe not found next to the application.\n\n" +
                "Copy ffmpeg.exe into the same folder as ABGM.exe.\n\n" +
                "Download: https://github.com/btbn/ffmpeg-builds/releases");
        }

        private static string FindFfprobe()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffprobe.exe");
            return File.Exists(path) ? path : "";
        }

        private void CheckFfmpegStatus()
        {
            Task.Run(() =>
            {
                string text;
                bool ok = false;

                try
                {
                    string exe = FindFfmpeg();

                    var psi = new ProcessStartInfo(exe)
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    psi.Arguments = "-version";

                    using (var p = new Process { StartInfo = psi })
                    {
                        p.Start();
                        string stdout = p.StandardOutput.ReadToEnd();
                        p.StandardError.ReadToEnd();
                        p.WaitForExit();

                        if (p.ExitCode == 0)
                        {
                            string ver = "?";
                            foreach (var line in stdout.Split('\n'))
                            {
                                if (line.StartsWith("ffmpeg version"))
                                {
                                    var pts = line.Split(' ');
                                    if (pts.Length >= 3) ver = pts[2];
                                    break;
                                }
                            }
                            text = $"ffmpeg {ver}  —  {exe}";
                            ok = true;
                        }
                        else
                        {
                            text = p.ExitCode == -1073741515
                                ? "ffmpeg failed to start: missing DLLs. Download the full package from https://github.com/BtbN/FFmpeg-Builds/releases (*-win64-gpl.zip)"
                                : $"ffmpeg returned exit code {p.ExitCode}";
                        }
                    }
                }
                catch (FileNotFoundException ex)
                {
                    text = ex.Message.Split('\n')[0];
                }
                catch (Exception ex)
                {
                    text = "ffmpeg: " + ex.Message;
                }

                bool okCopy = ok;
                string textCopy = text;
                Invoke(new Action(() =>
                {
                    tsslFfmpeg.Text = textCopy;
                    tsslFfmpeg.ForeColor = okCopy ? Color.Empty : Color.Red;
                }));
            });
        }

        private void BtnBrowseVideo_Click(object s, EventArgs e)
        {
            using (var dlg = new OpenFileDialog
            {
                Title = "Select a video file",
                Filter = "Video|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.flv;*.webm;*.m4v|All files|*.*"
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                _videoPath = dlg.FileName;
                txtVideo.Text = dlg.FileName;
                if (string.IsNullOrEmpty(txtOutput.Text))
                {
                    _outputDir = Path.GetDirectoryName(dlg.FileName);
                    txtOutput.Text = _outputDir;
                }
            }
        }

        private void BtnBrowseOutput_Click(object s, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog { Description = "Output folder for BLP files" })
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    _outputDir = dlg.SelectedPath;
                    txtOutput.Text = dlg.SelectedPath;
                }
            }
        }

        private void MainForm_DragEnter(object s, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void MainForm_DragDrop(object s, DragEventArgs e)
        {
            var files = e.Data?.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0)
            {
                _videoPath = files[0];
                txtVideo.Text = files[0];
            }
        }

        private void UpdateVideoInfo()
        {
            lblVideoInfo.Text = "";
            if (!File.Exists(_videoPath)) return;
            string probe = FindFfprobe();
            if (string.IsNullOrEmpty(probe)) return;

            Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo(probe)
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        Arguments = $"-v error -select_streams v:0 -show_entries stream=width,height,r_frame_rate,duration -of default=noprint_wrappers=1 \"{_videoPath}\""
                    };

                    using (var p = Process.Start(psi))
                    {
                        string raw = p.StandardOutput.ReadToEnd();
                        p.WaitForExit();

                        var kv = new Dictionary<string, string>();
                        foreach (var line in raw.Split('\n'))
                        {
                            var parts = line.Trim().Split('=');
                            if (parts.Length == 2) kv[parts[0]] = parts[1];
                        }

                        string w = kv.ContainsKey("width") ? kv["width"] : "?";
                        string h = kv.ContainsKey("height") ? kv["height"] : "?";
                        string fps = kv.ContainsKey("r_frame_rate") ? kv["r_frame_rate"] : "?";
                        string dur = kv.ContainsKey("duration") ? kv["duration"] : "?";

                        if (fps.Contains("/"))
                        {
                            var fp = fps.Split('/');
                            if (double.TryParse(fp[0], out double n) &&
                                double.TryParse(fp[1], out double d) && d != 0)
                                fps = (n / d).ToString("F2");
                        }
                        double.TryParse(dur,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double sec);
                        string durStr = sec > 0 ? TimeSpan.FromSeconds(sec).ToString(@"h\:mm\:ss") : "?";

                        string info = $"{w}x{h}  |  {fps} fps  |  {durStr}";
                        Invoke(new Action(() => lblVideoInfo.Text = info));
                    }
                }
                catch { }
            });
        }

        private void BtnCancel_Click(object s, EventArgs e) => _cts?.Cancel();

        private async void BtnConvert_Click(object s, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtVideo.Text) || !File.Exists(txtVideo.Text))
            { MessageBox.Show("Please select a video file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (string.IsNullOrWhiteSpace(txtOutput.Text))
            { MessageBox.Show("Please specify an output folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            string ffmpegExe;
            try
            {
                ffmpegExe = FindFfmpeg();

                // Quick test: catch missing DLLs before starting
                var testPsi = new ProcessStartInfo(ffmpegExe)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    Arguments = "-version"
                };

                using (var testProc = new Process { StartInfo = testPsi })
                {
                    testProc.Start();
                    testProc.StandardOutput.ReadToEnd();
                    testProc.StandardError.ReadToEnd();
                    testProc.WaitForExit();

                    if (testProc.ExitCode != 0)
                    {
                        string hint = testProc.ExitCode == -1073741515
                            ? "ffmpeg.exe cannot start — missing DLLs.\n\n" +
                              "Download the FULL build with dependencies:\n" +
                              "https://github.com/BtbN/FFmpeg-Builds/releases\n\n" +
                              "You need a file like: ffmpeg-master-latest-win64-gpl.zip\n" +
                              "Extract the contents of the bin\\ folder next to ABGM.exe"
                            : $"ffmpeg returned exit code {testProc.ExitCode}";
                        MessageBox.Show(hint, "FFmpeg is not working", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
            }
            catch (FileNotFoundException ex)
            {
                MessageBox.Show(ex.Message, "FFmpeg not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to check ffmpeg:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _videoPath = txtVideo.Text;
            _outputDir = txtOutput.Text;

            SetRunning(true);
            rtbLog.Clear();
            txtResult.Visible = false;
            lblResultHint.Visible = false;
            pbMain.Value = 0;
            lblProgress.Text = "Starting...";
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            var sizeParts = cmbSize.Text.Split('x');
            int texW = int.Parse(sizeParts[0]);
            int texH = int.Parse(sizeParts[1]);
            bool mipmaps = chkMipmaps.Checked;
            bool dxt5 = cmbFormat.SelectedItem?.ToString() == "DXT5";
            bool allFps = chkAllFrames.Checked;
            int fps = (int)nudFps.Value;
            string tmpDir = Path.Combine(Path.GetTempPath(), "blp_" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(tmpDir);
                Log("FFmpeg: " + ffmpegExe);
                Log("Temp folder: " + tmpDir);

                Log($"Extracting frames ({texW}x{texH})...");
                lblProgress.Text = "Step 1/2 — extracting frames";

                string vfFilter = allFps
                    ? $"scale={texW}:{texH}:flags=lanczos"
                    : $"fps={fps},scale={texW}:{texH}:flags=lanczos";

                string outPattern = Path.Combine(tmpDir, "frame_%06d.png");
                var psi = new ProcessStartInfo(ffmpegExe)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = $"-y -i \"{_videoPath}\" -vf \"{vfFilter}\" \"{outPattern}\""
                };

                int exitCode = await RunProcessAsync(psi, ct, logStdErr: true);
                ct.ThrowIfCancellationRequested();

                if (exitCode != 0)
                    throw new Exception($"FFmpeg exited with code {exitCode}. See log for details.");

                var frames = Directory.GetFiles(tmpDir, "frame_*.png");
                Log($"Frames extracted: {frames.Length}");
                if (frames.Length == 0)
                    throw new Exception("FFmpeg produced no frames. Please check the video file.");

                Log($"Converting {frames.Length} frames to BLP2...");
                lblProgress.Text = "Step 2/2 — converting to BLP2";
                pbMain.Maximum = frames.Length;
                Directory.CreateDirectory(_outputDir);
                Array.Sort(frames);

                int done = 0, errors = 0;
                await Task.Run(() =>
                {
                    foreach (var frame in frames)
                    {
                        if (ct.IsCancellationRequested) break;
                        string frameName = Path.GetFileNameWithoutExtension(frame);
                        string blpPath = Path.Combine(_outputDir, $"{done + errors:D4}.blp");
                        try
                        {
                            using (var bmp = new System.Drawing.Bitmap(frame))
                                File.WriteAllBytes(blpPath, Blp2Encoder.Encode(bmp, mipmaps, dxt5));
                            done++;
                        }
                        catch (Exception ex)
                        {
                            errors++;
                            string fn = frameName;
                            string msg = ex.Message;
                            Invoke(new Action(() => Log($"  ERROR {fn}: {msg}")));
                        }
                        try { File.Delete(frame); } catch { }
                        int total = done + errors;
                        int totalCopy = total;
                        int frameCount = frames.Length;
                        Invoke(new Action(() =>
                        {
                            pbMain.Value = Math.Min(totalCopy, pbMain.Maximum);
                            lblProgress.Text = $"Processing: {totalCopy} / {frameCount}";
                        }));
                    }
                }, ct);

                Log($"Done. Converted: {done}  |  Errors: {errors}");
                Log("Output: " + _outputDir);
                lblProgress.Text = $"Completed: {done} BLP file(s)";

                string folderName = Path.GetFileName(_outputDir.TrimEnd(
                                                Path.DirectorySeparatorChar,
                                                Path.AltDirectorySeparatorChar));
                string resultStr = $"{folderName}|{done}";
                txtResult.Text = resultStr;
                txtResult.Visible = true;
                lblResultHint.Visible = true;
                Log($"SceneList entry: {resultStr}");
            }
            catch (OperationCanceledException)
            {
                Log("Cancelled by user.");
                lblProgress.Text = "Cancelled";
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex.Message);
                lblProgress.Text = "Error";
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                try { if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true); } catch { }
                SetRunning(false);
            }
        }

        private void SetRunning(bool running)
        {
            btnConvert.Enabled = !running;
            btnCancel.Enabled = running;
            grpInput.Enabled = !running;
            grpOutput.Enabled = !running;
            grpOptions.Enabled = !running;
        }

        private void Log(string msg)
        {
            if (InvokeRequired) { Invoke(new Action(() => Log(msg))); return; }
            rtbLog.AppendText(msg + "\r\n");
            rtbLog.ScrollToCaret();
        }

        private Task<int> RunProcessAsync(ProcessStartInfo psi, CancellationToken ct,
            bool logStdErr = false)
        {
            var tcs = new TaskCompletionSource<int>();
            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };

            if (logStdErr)
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) Log("  " + e.Data); };

            p.Exited += (s, e) => { tcs.TrySetResult(p.ExitCode); p.Dispose(); };

            ct.Register(() =>
            {
                try { if (!p.HasExited) p.Kill(); } catch { }
                tcs.TrySetCanceled();
            });

            try { p.Start(); }
            catch (Exception ex)
            {
                tcs.TrySetException(new Exception(
                    $"Failed to start '{psi.FileName}':\n{ex.Message}"));
                return tcs.Task;
            }

            if (logStdErr) p.BeginErrorReadLine();
            p.BeginOutputReadLine();
            return tcs.Task;
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources =
                new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.SuspendLayout();
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "MainForm";
            this.ResumeLayout(false);
        }
    }
}