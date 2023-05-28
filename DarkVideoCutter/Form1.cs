using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using WMPLib;
using System.Diagnostics;
using System.Threading;
namespace VideoCutter
{
    public partial class Form1 : Form
    {

        string fileOpened = "", cutCommand = "", outputFile = "", fileName = "";
        long previousFileLenght = 0;
        int waitFileTimeout = 0;

        public Form1()
        {
            InitializeComponent();
            this.AllowDrop = true;
            this.DragEnter += new DragEventHandler(Form1_DragEnter);
            this.DragDrop += new DragEventHandler(Form1_DragDrop);
        }

        public void ControlsEnable(bool status)
        {
             buttonCut.Enabled = trackBar1.Enabled = trackBar2.Enabled = textBox1.Enabled = textBox2.Enabled = status;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            axWindowsMediaPlayer1.uiMode = "none";

            // 检查ffmpeg路径，未找到则打开窗口添加ffmpeg路径
            if (Properties.Settings.Default.FfmpegLocation == "" || System.IO.File.Exists(Properties.Settings.Default.FfmpegLocation) == false)
            {
                this.Hide();
                ffmpegConf ff = new ffmpegConf();
                ff.textBox1.Text = Properties.Settings.Default.FfmpegLocation;
                if (ff.ShowDialog() != DialogResult.OK)
                    Application.Exit();
            }

            SetStatus("通过菜单打开文件或者拖动文件到窗口内.", WorkingStatus.Idle);
        }

        public enum WorkingStatus
        {
            Idle,
            Busy,
            Error,
            Success
        }

        public void SetStatus(string newStatus, WorkingStatus workingStatus)
        {
            

            /*if (workingStatus == WorkingStatus.Idle) pictureBoxStatus.Image = VideoCutter.Properties.Resources.logo;
            else if (workingStatus == WorkingStatus.Busy) pictureBoxStatus.Image = VideoCutter.Properties.Resources.load;
            else if (workingStatus == WorkingStatus.Error) pictureBoxStatus.Image = VideoCutter.Properties.Resources.error;
            else if (workingStatus == WorkingStatus.Success) pictureBoxStatus.Image = VideoCutter.Properties.Resources.success;*/
        }


        private void OpenVideo(string videoPath)
        {
            fileOpened = videoPath;
            fileName = Path.GetFileName(videoPath);

            SetStatus(Path.GetFileName(videoPath), WorkingStatus.Idle);

            // videoplayer显示
            axWindowsMediaPlayer1.URL = videoPath;
            axWindowsMediaPlayer1.settings.volume = 0;

            // 进度条使能
            trackBar1.Enabled = true;
            trackBar2.Enabled = true;
            textBox1.Enabled = true;
            textBox2.Enabled = true;
            labelVideoPosition.Visible = true;

            // 获取视频长度，进度条初始化
            var player = new WindowsMediaPlayer();
            var clip = player.newMedia(videoPath);
            textBox1.Text = labelVideoPosition.Text = "00:00:00.0";

            string vidLenght = TimeSpan.FromSeconds(clip.duration).ToString();

            if (vidLenght.Contains(".") == false) vidLenght += ".0";

            textBox2.Text = vidLenght.Substring(0, 10);
            trackBar1.Maximum = Convert.ToInt32(textBox2.Text.Substring(0, 2)) * 3600 + Convert.ToInt32(textBox2.Text.Substring(3, 2)) * 60 + Convert.ToInt32(textBox2.Text.Substring(6, 2));
            trackBar2.Maximum = trackBar1.Maximum;
            trackBar1.Value = 0;
            trackBar2.Value = trackBar2.Maximum;

            // reset media player
            axWindowsMediaPlayer1.Ctlcontrols.currentPosition = 0;
            axWindowsMediaPlayer1.Ctlcontrols.play();

            // 启动计时器，更新视频位置
            timerCheckVideoEnd.Start();
        }

        private void OpenVideoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog o = new OpenFileDialog();
            o.Filter = "Video Files(*.mp4;*.mov;*.avi;*.wmv;*.mkv)|*.mp4;*.mov;*.avi;*.wmv;*.mkv|All files (*.*)|*.*";

            if (o.ShowDialog() == DialogResult.OK)
                OpenVideo(o.FileName);

        }

        private void Button1_Click(object sender, EventArgs e)
        {
            if (axWindowsMediaPlayer1.URL == "")
                SetStatus("请先打开一个文件.", WorkingStatus.Idle);
            else
            {
                // 检查开始结束位置是否相同
                if (textBox1.Text == textBox2.Text)
                {
                    SetStatus("开始和结束位置不能在同一个位置.", WorkingStatus.Error);
                    return;
                }

                // 确定需要剪切的部分

                // 先确定开始和结束时间
                int endPointInSecs = Convert.ToInt32(textBox2.Text.Substring(0, 2)) * 3600 + Convert.ToInt32(textBox2.Text.Substring(3, 2)) * 60 + Convert.ToInt32(textBox2.Text.Substring(6, 2));
                int startPointInSecs = Convert.ToInt32(textBox1.Text.Substring(0, 2)) * 3600 + Convert.ToInt32(textBox1.Text.Substring(3, 2)) * 60 + Convert.ToInt32(textBox1.Text.Substring(6, 2));

                int duration = endPointInSecs - startPointInSecs;

                // 分为h,m,s
                int hour = (int)duration / 3600;
                int min = (int)(duration - hour * 3600) / 60;
                int sec = (int)duration - hour * 3600 - min * 60;

                // find ms
                int ms;

                if (Convert.ToInt32(textBox2.Text.Substring(9, 1)) >= Convert.ToInt32(textBox1.Text.Substring(9, 1)))
                    ms = Convert.ToInt32(textBox2.Text.Substring(9, 1)) - Convert.ToInt32(textBox1.Text.Substring(9, 1));
                else { 
                    ms = Convert.ToInt32(textBox2.Text.Substring(9, 1)) + 10 - Convert.ToInt32(textBox1.Text.Substring(9, 1));
                    sec--;
                }

                // 向ffmpeg发送命令
                outputFile = Path.GetDirectoryName(fileOpened) + "\\" + Path.GetFileNameWithoutExtension(fileOpened) + " " + textBox1.Text.Replace(":", ".").Replace(",", ".") + " - " + " " + textBox2.Text.Replace(":", ".").Replace(",", ".") + Path.GetExtension(fileOpened);
                cutCommand = "-ss " + textBox1.Text.Replace(",", ".") + " -i " + "\"" + fileOpened + "\" -to " + hour + ":"+ min + ":" + sec + "." + ms + " -c copy \"" + outputFile + "\"";

                if (System.IO.File.Exists(outputFile))
                {
                    SetStatus("存在相同名字文件，无法继续.", WorkingStatus.Error);
                    return;
                }
                if (!System.IO.File.Exists(Properties.Settings.Default.FfmpegLocation)) {
                    SetStatus("未找到ffmpeg，请点击菜单添加ffmpeg路径 \"Configure...\" to continue.", WorkingStatus.Error);
                    return;
                }

                // 所有按钮失能
                ControlsEnable(false);

                SetStatus("视频剪切中...", WorkingStatus.Busy );

                previousFileLenght = 0;
                waitFileTimeout = 0;

                backgroundWorker1.RunWorkerAsync();
                timerCheckCutCompleted.Start();
            }
        }


        private void TimerCheckVideoEnd_Tick(object sender, EventArgs e)
        {

            int hour = (int)axWindowsMediaPlayer1.Ctlcontrols.currentPosition / 3600;
            int min = (int)(axWindowsMediaPlayer1.Ctlcontrols.currentPosition - hour * 3600) / 60;
            int sec = ((int)axWindowsMediaPlayer1.Ctlcontrols.currentPosition - hour * 3600 - min * 60);
            labelVideoPosition.Text = hour.ToString("00") + ":" + min.ToString("00") + ":" + sec.ToString("00") + ".0";


            int last = Convert.ToInt32(textBox2.Text.Substring(0, 2)) * 3600 + Convert.ToInt32(textBox2.Text.Substring(3, 2)) * 60 + Convert.ToInt32(textBox2.Text.Substring(6, 2));

            if (axWindowsMediaPlayer1.Ctlcontrols.currentPosition.CompareTo(last) == 1)
            {
                axWindowsMediaPlayer1.Ctlcontrols.pause();
                timerCheckVideoEnd.Stop();
            }

        }

        public void PlayCuttedPart()
        {
            axWindowsMediaPlayer1.Ctlcontrols.currentPosition = Convert.ToInt32(textBox1.Text.Substring(0, 2)) * 3600 + Convert.ToInt32(textBox1.Text.Substring(3, 2)) * 60 + Convert.ToInt32(textBox1.Text.Substring(6, 2));
            axWindowsMediaPlayer1.Ctlcontrols.play();

            timerCheckVideoEnd.Start();
        }

        public void ShowTheLastSec()
        {
            axWindowsMediaPlayer1.Ctlcontrols.currentPosition = Convert.ToInt32(textBox2.Text.Substring(0, 2)) * 3600 + Convert.ToInt32(textBox2.Text.Substring(3, 2)) * 60 + Convert.ToInt32(textBox2.Text.Substring(6, 2));
            axWindowsMediaPlayer1.Ctlcontrols.play();

            timerCheckVideoEnd.Start();
        }

        private void TrackBar1_MouseUp(object sender, MouseEventArgs e)
        {
            PlayCuttedPart();
        }

        private void TrackBar2_MouseUp(object sender, MouseEventArgs e)
        {
            ShowTheLastSec();
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void SoundOffToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void TrackBar1_ValueChanged(object sender, EventArgs e)
        {
            if (trackBar1.Value > trackBar2.Value)
            {
                trackBar1.Value = trackBar2.Value;
            }
            else
            {
                int hour = trackBar1.Value / 3600;
                int min = (trackBar1.Value - hour * 3600) / 60;
                int sec = (trackBar1.Value - hour * 3600 - min * 60);
                textBox1.Text = hour.ToString("00") + ":" + min.ToString("00") + ":" + sec.ToString("00") + "." + textBox1.Text.Substring(textBox1.Text.Length - 1, 1);
            }

            SetStatus(fileName, WorkingStatus.Idle);
        }

        private void TrackBar2_ValueChanged(object sender, EventArgs e)
        {
            if (trackBar2.Value < trackBar1.Value)
            {
                trackBar2.Value = trackBar1.Value;
            }
            else
            {
                int hour = trackBar2.Value / 3600;
                int min = (trackBar2.Value - hour * 3600) / 60;
                int sec = (trackBar2.Value - hour * 3600 - min * 60);
                textBox2.Text = hour.ToString("00") + ":" + min.ToString("00") + ":" + sec.ToString("00") + "." + textBox2.Text.Substring(textBox2.Text.Length - 1, 1);
            }

            SetStatus(fileName, WorkingStatus.Idle);
        }

        private void TextBox1_TextChanged(object sender, EventArgs e)
        {
            try
            {
                int newValue = Convert.ToInt32(textBox1.Text.Substring(0, 2)) * 3600 + Convert.ToInt32(textBox1.Text.Substring(3, 2)) * 60 + Convert.ToInt32(textBox1.Text.Substring(6, 2));

                if (newValue > trackBar1.Maximum) trackBar1.Value = trackBar1.Maximum; else trackBar1.Value = newValue;

                SetStatus(fileName, WorkingStatus.Idle);
            }
            catch
            {
   
            }
        }

        private void TextBox2_TextChanged(object sender, EventArgs e)
        {
            try
            {
                int newValue = Convert.ToInt32(textBox2.Text.Substring(0, 2)) * 3600 + Convert.ToInt32(textBox2.Text.Substring(3, 2)) * 60 + Convert.ToInt32(textBox2.Text.Substring(6, 2));

                if (newValue > trackBar2.Maximum) trackBar2.Value = trackBar2.Maximum; else trackBar2.Value = newValue;

                SetStatus(fileName, WorkingStatus.Idle);
            }
            catch
            {
  
            }
        }

        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            About ab = new About();
            ab.ShowDialog();
        }

        private void CheckForUpdatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/bveyseloglu/Dark-Video-Cutter");
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Link;
        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (Path.GetExtension(files[0]) == ".mp4" || Path.GetExtension(files[0]) == ".mov" || Path.GetExtension(files[0]) == ".avi" || Path.GetExtension(files[0]) == ".wmv" || Path.GetExtension(files[0]) == ".mkv")
                OpenVideo(files[0]);
        }

        private void buttonMenu_Click(object sender, EventArgs e)
        {

        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {

        }

        private void darkContextMenu1_Opening(object sender, CancelEventArgs e)
        {

        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            OpenFileDialog o = new OpenFileDialog();
            o.Filter = "Video Files(*.mp4;*.mov;*.avi;*.wmv;*.mkv)|*.mp4;*.mov;*.avi;*.wmv;*.mkv|All files (*.*)|*.*";

            if (o.ShowDialog() == DialogResult.OK)
                 OpenVideo(o.FileName);
        }

        private void ConfigureFFMPEGLocationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ffmpegConf ff = new ffmpegConf();
            ff.labelTitle.Text = "Configure";
            ff.labelDesc.Text = ":";
            ff.buttonOK.Text = "OK";
            ff.buttonOK.Enabled = true;
            ff.labelSelected.Visible = false;
            ff.textBox1.Text = Properties.Settings.Default.FfmpegLocation;
            ff.ShowDialog();
        }

        private void BackgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            Process p = new Process();
            ProcessStartInfo psi = new ProcessStartInfo(Properties.Settings.Default.FfmpegLocation, cutCommand);
/*            psi.WindowStyle = ProcessWindowStyle.Hidden;*/
            p.StartInfo = psi;
            p.Start();
            p.WaitForExit();
        }

        private void TimerCheckCutCompleted_Tick(object sender, EventArgs e)
        {
            if (System.IO.File.Exists(outputFile) == true)
            {
                FileInfo fi = new FileInfo(outputFile);
                if (previousFileLenght == fi.Length)
                {
                    SetStatus("视频剪切成功",WorkingStatus.Success);
                    timerCheckCutCompleted.Stop();
                    ControlsEnable(true);
                }
                else
                {
                    previousFileLenght = fi.Length;
                }
            }
            else
            {
               
                if (waitFileTimeout > 5)
                {
                    SetStatus("出现错误，剪切失败",WorkingStatus.Error);
                    timerCheckCutCompleted.Stop();
                    ControlsEnable(true);
                }
                else
                    waitFileTimeout++;
            }

        }
    }
}
