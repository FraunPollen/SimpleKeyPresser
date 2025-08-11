using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;

namespace KeyPresser
{
    public partial class MainForm : Form
    {
        // Import Windows API functions for simulating key presses
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        static extern ushort MapVirtualKey(uint uCode, uint uMapType);

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public int type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        const int INPUT_KEYBOARD = 1;
        const int KEYEVENTF_KEYUP = 0x0002;
        const int KEYEVENTF_SCANCODE = 0x0008;

        readonly Timer keyPressTimer;
        readonly Timer keyReleaseTimer;
        readonly Timer timeTrackingTimer; // New timer for tracking elapsed time
        bool isRunning = false;
        int keyPressCount = 0;
        DateTime startTime; // Track when the simulation started
        TimeSpan timePassed = new(0);
        bool keyCurrentlyPressed = false;
        readonly Random random = new();

        // UI Controls
        TextBox keyTextBox;
        char[] keysToPress; // Array to store multiple keys
        NumericUpDown intervalMinNumeric;
        NumericUpDown intervalMaxNumeric;
        NumericUpDown holdMinNumeric;
        NumericUpDown holdMaxNumeric;
        Button startButton;
        Button stopButton;
        Label statusLabel;
        Label counterLabel;
        Label timeLabel;

        public MainForm()
        {
            InitializeComponent();
            keyPressTimer = new Timer();
            keyPressTimer.Tick += KeyPressTimer_Tick;
            keyReleaseTimer = new Timer();
            keyReleaseTimer.Tick += KeyReleaseTimer_Tick;
            
            // Initialize time tracking timer (updates every second)
            timeTrackingTimer = new Timer();
            timeTrackingTimer.Interval = 1000; // 1 second
            timeTrackingTimer.Tick += TimeTrackingTimer_Tick;
        }

        void InitializeComponent()
        {
            var labelColX = 50;
            var colWidth = 150;
            var row1Y = 50;
            var row2Y = 85;
            var row3Y = 120;

            SuspendLayout();

            // Form properties
            Text = "Key Press Simulator";
            Size = new System.Drawing.Size(900, 500);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            // Build inputs rows
            BuildKeyPressRow(labelColX, row1Y, colWidth);
            BuildHoldRangeRow(labelColX, row2Y, colWidth);
            BuildIntervalRangeRow(labelColX, row3Y, colWidth);
            
            // Start button
            startButton = new Button
            {
                Text = "Start",
                Location = new System.Drawing.Point(50, 260),
                Size = new System.Drawing.Size(120, 40),
                Font = new System.Drawing.Font("Microsoft Sans Serif", 11F, System.Drawing.FontStyle.Bold),
                BackColor = System.Drawing.Color.LightGreen
            };
            startButton.Click += StartButton_Click;
            Controls.Add(startButton);

            // Stop button
            stopButton = new Button
            {
                Text = "Stop",
                Location = new System.Drawing.Point(200, 260),
                Size = new System.Drawing.Size(120, 40),
                Enabled = false,
                Font = new System.Drawing.Font("Microsoft Sans Serif", 11F, System.Drawing.FontStyle.Bold),
                BackColor = System.Drawing.Color.LightCoral
            };
            stopButton.Click += StopButton_Click;
            Controls.Add(stopButton);

            // Status label
            statusLabel = new Label
            {
                Text = "Ready",
                Location = new System.Drawing.Point(50, 350),
                Size = new System.Drawing.Size(500, 40),
                ForeColor = System.Drawing.Color.Green,
                Font = new System.Drawing.Font("Microsoft Sans Serif", 10F)
            };
            Controls.Add(statusLabel);

            // Counter label
            counterLabel = new Label
            {
                Text = "Keys pressed: 0",
                Location = new System.Drawing.Point(580, 350),
                Size = new System.Drawing.Size(150, 25),
                ForeColor = System.Drawing.Color.Black,
                Font = new System.Drawing.Font("Microsoft Sans Serif", 11F, System.Drawing.FontStyle.Bold)
            };
            Controls.Add(counterLabel);

            // Time label - positioned below counter label
            timeLabel = new Label
            {
                Text = "Active time: 00:00:00",
                Location = new System.Drawing.Point(580, 375),
                Size = new System.Drawing.Size(200, 25),
                ForeColor = System.Drawing.Color.Black,
                Font = new System.Drawing.Font("Microsoft Sans Serif", 11F, System.Drawing.FontStyle.Bold)
            };
            Controls.Add(timeLabel);

            // Instructions label
            var instructionsLabel = new Label
            {
                Text = "Instructions:\n" +
                       "• Enter multiple keys to simulate (A-Z, 0-9, space) - e.g., \"WASD\" or \"123\"\n" +
                       "• Each key press will randomly select from your entered keys\n" +
                       "• Set randomized ranges for both interval and hold duration\n" +
                       "• Hold: how long each key is pressed down (makes it more realistic)\n" +
                       "• Interval: time between key presses (randomized to avoid detection)\n" +
                       "• Random timing and key selection helps avoid spam detection\n" +
                       "• Click Start to begin simulation, Stop to end",
                Location = new System.Drawing.Point(500, 50),
                Size = new System.Drawing.Size(350, 200),
                Font = new System.Drawing.Font("Microsoft Sans Serif", 9F),
                BackColor = System.Drawing.Color.LightYellow,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(instructionsLabel);

            ResumeLayout(false);
        }

        void BuildKeyPressRow(int rowX, int colY, int colWidth) {
            var keyLabel = new Label
            {
                Text = "Key(s) to press:",
                Location = new System.Drawing.Point(rowX, colY),
                Size = new System.Drawing.Size(120, 25),
                Font = new System.Drawing.Font("Microsoft Sans Serif", 10F)
            };
            Controls.Add(keyLabel);

            var inputsX = rowX + colWidth;
            keyTextBox = new TextBox
            {
                Location = new System.Drawing.Point(inputsX, colY - 3),
                Size = new System.Drawing.Size(150, 30),
                MaxLength = 50, // Allow multiple characters
                Text = "wasd",
                Font = new System.Drawing.Font("Microsoft Sans Serif", 10F)
            };
            Controls.Add(keyTextBox);
        }

        void BuildHoldRangeRow(int rowX, int colY, int colWidth) {
            var intervalLabel = new Label
            {
                Text = "Hold range (ms):",
                Location = new System.Drawing.Point(rowX, colY),
                Size = new System.Drawing.Size(140, 25),
                Font = new System.Drawing.Font("Microsoft Sans Serif", 10F),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            Controls.Add(intervalLabel);

            var inputsX = rowX + colWidth;
            holdMinNumeric = new NumericUpDown
            {
                Location = new System.Drawing.Point(inputsX, colY - 3),
                Size = new System.Drawing.Size(100, 30),
                Minimum = 10,
                Maximum = 2000,
                Value = 200,
                Font = new System.Drawing.Font("Microsoft Sans Serif", 10F)
            };
            Controls.Add(holdMinNumeric);

            var toLabel1 = new Label
            {
                Text = "to",
                Location = new System.Drawing.Point(inputsX + 120, colY),
                Size = new System.Drawing.Size(30, 25),
                Font = new System.Drawing.Font("Microsoft Sans Serif", 10F),
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };
            Controls.Add(toLabel1);

            holdMaxNumeric = new NumericUpDown
            {
                Location = new System.Drawing.Point(inputsX + 160, colY - 3),
                Size = new System.Drawing.Size(100, 30),
                Minimum = 10,
                Maximum = 2000,
                Value = 500,
                Font = new System.Drawing.Font("Microsoft Sans Serif", 10F)
            };
            Controls.Add(holdMaxNumeric);
        }

        void BuildIntervalRangeRow(int rowX, int colY, int colWidth) {
            var intervalLabel = new Label
            {
                Text = "Interval range (ms):",
                Location = new System.Drawing.Point(rowX, colY),
                Size = new System.Drawing.Size(140, 25),
                Font = new System.Drawing.Font("Microsoft Sans Serif", 10F),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            Controls.Add(intervalLabel);

            // Interval min numeric input
            var inputsX = rowX + colWidth;
            intervalMinNumeric = new NumericUpDown
            {
                Location = new System.Drawing.Point(inputsX, colY - 3),
                Size = new System.Drawing.Size(100, 30),
                Minimum = 200,
                Maximum = 30000,
                Value = 800,
                Font = new System.Drawing.Font("Microsoft Sans Serif", 10F)
            };
            Controls.Add(intervalMinNumeric);

            // "to" label
            var toLabel1 = new Label
            {
                Text = "to",
                Location = new System.Drawing.Point(inputsX + 120, colY),
                Size = new System.Drawing.Size(30, 25),
                Font = new System.Drawing.Font("Microsoft Sans Serif", 10F),
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };
            Controls.Add(toLabel1);

            // Interval max numeric input
            intervalMaxNumeric = new NumericUpDown
            {
                Location = new System.Drawing.Point(inputsX + 160, colY - 3),
                Size = new System.Drawing.Size(100, 30),
                Minimum = 200,
                Maximum = 30000,
                Value = 1200,
                Font = new System.Drawing.Font("Microsoft Sans Serif", 10F)
            };
            Controls.Add(intervalMaxNumeric);
        }
        
        void StartButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(keyTextBox.Text))
            {
                MessageBox.Show("Please enter keys to press.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Parse and validate keys
            string inputKeys = keyTextBox.Text.ToUpper().Replace(" ", "").Trim();
            var validKeys = new List<char>();
            var invalidChars = new List<char>();

            foreach (char c in inputKeys)
            {
                if (IsValidKey(c))
                {
                    if (!validKeys.Contains(c)) // Avoid duplicates
                        validKeys.Add(c);
                }
                else
                {
                    if (!invalidChars.Contains(c))
                        invalidChars.Add(c);
                }
            }

            if (validKeys.Count == 0)
            {
                MessageBox.Show("Please enter at least one valid key (A-Z, 0-9, or space).", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (invalidChars.Count > 0)
            {
                string invalidCharsStr = string.Join(", ", invalidChars.Select(c => c == ' ' ? "space" : c.ToString()));
                MessageBox.Show($"Invalid characters found and will be ignored: {invalidCharsStr}\n\nValid characters: A-Z, 0-9, space", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            keysToPress = validKeys.ToArray();

            // Validate ranges
            if (intervalMinNumeric.Value > intervalMaxNumeric.Value)
            {
                MessageBox.Show("Interval minimum cannot be greater than maximum.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (holdMinNumeric.Value > holdMaxNumeric.Value)
            {
                MessageBox.Show("Hold minimum cannot be greater than maximum.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            isRunning = true;
            keyPressCount = 0; // Reset counter when starting new session
            startTime = DateTime.Now; // Record start time
            timePassed = new(0);
            
            // Set initial random interval
            int randomInterval = random.Next((int)intervalMinNumeric.Value, (int)intervalMaxNumeric.Value + 1);
            keyPressTimer.Interval = randomInterval;
            keyPressTimer.Start();
            
            // Start time tracking timer
            timeTrackingTimer.Start();

            startButton.Enabled = false;
            stopButton.Enabled = true;
            keyTextBox.Enabled = false;
            intervalMinNumeric.Enabled = false;
            intervalMaxNumeric.Enabled = false;
            holdMinNumeric.Enabled = false;
            holdMaxNumeric.Enabled = false;

            string keysDisplay = string.Join(", ", keysToPress.Select(k => k == ' ' ? "SPACE" : k.ToString()));
            statusLabel.Text = $"Simulating keys: [{keysDisplay}] - Interval: {intervalMinNumeric.Value}-{intervalMaxNumeric.Value}ms, Hold: {holdMinNumeric.Value}-{holdMaxNumeric.Value}ms";
            statusLabel.ForeColor = System.Drawing.Color.Blue;
            UpdateCounter();
            UpdateTime(); // Initialize time display
        }

        void StopButton_Click(object sender, EventArgs e)
        {
            StopSimulation();
        }

        void StopSimulation()
        {
            isRunning = false;
            keyPressTimer.Stop();
            keyReleaseTimer.Stop();
            timeTrackingTimer.Stop(); // Stop time tracking
            
            // Release any currently pressed keys
            if (keyCurrentlyPressed && keysToPress != null)
            {
                foreach (char key in keysToPress)
                {
                    ReleaseKey(key);
                }
                keyCurrentlyPressed = false;
            }

            startButton.Enabled = true;
            stopButton.Enabled = false;
            keyTextBox.Enabled = true;
            intervalMinNumeric.Enabled = true;
            intervalMaxNumeric.Enabled = true;
            holdMinNumeric.Enabled = true;
            holdMaxNumeric.Enabled = true;

            statusLabel.Text = "Stopped";
            statusLabel.ForeColor = System.Drawing.Color.Red;
        }

        void KeyPressTimer_Tick(object sender, EventArgs e)
        {
            if (isRunning && !keyCurrentlyPressed)
            {
                // Randomly select a key from the available keys
                char keyChar = keysToPress[random.Next(keysToPress.Length)];
                
                PressKey(keyChar);
                keyCurrentlyPressed = true;
                
                // Set random hold duration
                int randomHoldTime = random.Next((int)holdMinNumeric.Value, (int)holdMaxNumeric.Value + 1);
                keyReleaseTimer.Interval = randomHoldTime;
                keyReleaseTimer.Start();
                
                // Set random interval for next key press
                int randomInterval = random.Next((int)intervalMinNumeric.Value, (int)intervalMaxNumeric.Value + 1);
                keyPressTimer.Interval = randomInterval;
                
                // Increment counter and update display
                keyPressCount++;
                UpdateCounter();
            }
        }

        void KeyReleaseTimer_Tick(object sender, EventArgs e)
        {
            keyReleaseTimer.Stop();
            if (keyCurrentlyPressed)
            {
                // Release the currently pressed key (we need to track which key is pressed)
                // For now, we'll release all possible keys to ensure clean state
                foreach (char key in keysToPress)
                {
                    ReleaseKey(key);
                }
                keyCurrentlyPressed = false;
            }
        }

        // New timer event handler for tracking elapsed time
        void TimeTrackingTimer_Tick(object sender, EventArgs e)
        {
            if (isRunning)
            {
                timePassed = DateTime.Now - startTime;
                UpdateTime();
            }
        }

        static void PressKey(char key)
        {
            ushort virtualKey = GetVirtualKeyCode(key);
            if (virtualKey != 0)
            {
                INPUT input = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = virtualKey,
                            wScan = MapVirtualKey(virtualKey, 0),
                            dwFlags = KEYEVENTF_SCANCODE,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };

                _ = SendInput(1, new INPUT[] { input }, Marshal.SizeOf(typeof(INPUT)));
            }
        }

        static void ReleaseKey(char key)
        {
            ushort virtualKey = GetVirtualKeyCode(key);
            if (virtualKey != 0)
            {
                INPUT input = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = virtualKey,
                            wScan = MapVirtualKey(virtualKey, 0),
                            dwFlags = KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };

                _ = SendInput(1, new INPUT[] { input }, Marshal.SizeOf(typeof(INPUT)));
            }
        }

        void UpdateCounter()
        {
            counterLabel.Text = $"Keys pressed: {keyPressCount}";
        }

        void UpdateTime()
        {
            timeLabel.Text = $"Active time: {timePassed:hh\\:mm\\:ss}";
        }

        static ushort GetVirtualKeyCode(char key)
        {
            if (key >= 'A' && key <= 'Z')
                return key;
            if (key >= '0' && key <= '9')
                return key;
            if (key == ' ')
                return 0x20; // VK_SPACE

            return 0;
        }

        static bool IsValidKey(char key)
        {
            return (key >= 'A' && key <= 'Z') ||
                   (key >= '0' && key <= '9') ||
                   key == ' ';
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopSimulation();
            base.OnFormClosing(e);
        }
    }
}