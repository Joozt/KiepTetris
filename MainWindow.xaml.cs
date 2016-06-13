using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Threading;
using System.Media;
using System.IO;
using System.Windows.Input;

namespace KiepTetris
{
    public partial class MainWindow : System.Windows.Window
    {
        private const string LOGFILE = "KiepTetris.log";
        private int[] CATCH_KEYCODES = { 107, 109, 106, 111 };  // Do not pass keys to other applications
        private bool keyUpPressed = false;
        private bool keyDownPressed = false;
        private bool mouseDownPressed = false;
        private int mouseDownIntervalTemp = 0;

        private Label[,] blocksControls;
        private Label[,] previewBlocks;

        private Point[] previewBlock;
        private Brush previewBlockBrush;
        private Point[] fallingBlock;
        private Point fallingBlockPosition;
        private Brush fallingBlockBrush;

        private Brush NO_BLOCK_BRUSH;
        private Brush FLASH_ROW_BRUSH;

        private Thickness NO_BLOCK_THICKNESS;
        private Thickness IN_BLOCK_THICKNESS;

        private int nextLevelScoreThreshold;
        private int currentScore;
        private int currentLevel;
        private bool gameOver;
        private const int ROW_REMOVED_POINTS = 100;
        private const int PRESS_DOWN_POINTS = 1;
        private const int PRESS_DOWN_SPEED = 100;

        private bool blockJustShowed;
        System.Windows.Forms.Timer blockStepTimer;

        Point previousMousePosition;

        #region Initialize
        public MainWindow()
        {
            InitializeComponent();

            // Cannot debug when application has topmost
#if !DEBUG
            this.Topmost = true;
#endif

            previousMousePosition = Mouse.GetPosition(this);
            this.MouseMove += new System.Windows.Input.MouseEventHandler(TetrisGame_MouseMove);

            // Log startup
            Log("Start");
        }

        void TetrisGame_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
#if !DEBUG
            try
            {
#endif
                Point currentMousePosition = Mouse.GetPosition(this);
                if (previousMousePosition.X - currentMousePosition.X > 50)
                {
                    Log("MouseMove\tLeft");
                    MoveBlockLeft();
                    previousMousePosition = currentMousePosition;
                }
                if (previousMousePosition.X - currentMousePosition.X < -50)
                {
                    Log("MouseMove\tRight");
                    MoveBlockRight();
                    previousMousePosition = currentMousePosition;
                }

                if (currentMousePosition.X == 0)
                {
                    previousMousePosition = new Point(100, currentMousePosition.Y);
                    System.Windows.Forms.Cursor.Position = new System.Drawing.Point(100, Convert.ToInt32(currentMousePosition.Y));
                }

                if (currentMousePosition.X >= this.Width)
                {
                    previousMousePosition = new Point(this.Width - 100, currentMousePosition.X);
                    System.Windows.Forms.Cursor.Position = new System.Drawing.Point(Convert.ToInt32(this.Width - 100), Convert.ToInt32(currentMousePosition.Y));
                }
#if !DEBUG
            }
            catch (Exception ex)
            {
                Log("Error during MouseMove\t" + ex.Message);
                ExitGame();
            }
#endif
        }

        void Tetris_Initialized(object sender, EventArgs e)
        {
            nextLevelScoreThreshold = 500;
            currentLevel = 0;
            currentScore = 0;
            gameOver = false;
            highscoreLabel.Content = Properties.Settings.Default.HighScore.ToString();

            NO_BLOCK_BRUSH = Brushes.Transparent;
            FLASH_ROW_BRUSH = Brushes.White;
            NO_BLOCK_THICKNESS = new Thickness(0, 0, 0, 0);
            IN_BLOCK_THICKNESS = new Thickness(2, 2, 2, 2);

            // Init blocks array
            int rowCount;
            int columnCount;
            rowCount = tetrisGrid.RowDefinitions.Count;
            columnCount = tetrisGrid.ColumnDefinitions.Count;

            // Init fallingBlock array
            fallingBlock = new Point[4];
            for (int i = 0; i < fallingBlock.Length; i++)
            {
                fallingBlock[i] = new Point(0, 0);
            }

            fallingBlockPosition = new Point(0, 0);
            previewBlock = GenerateRandomBlock();

            // Init game timer
            blockStepTimer = new System.Windows.Forms.Timer();
            blockStepTimer.Interval = 700;
            blockStepTimer.Tick += new EventHandler(blockStepTimer_Tick);
            blockStepTimer.Enabled = false;

            // Init game grid
            AddBlocksControls();

            // Block keys from being received by other applications
            List<int> blockedKeys = new List<int>(CATCH_KEYCODES);
            LowLevelKeyboardHook.Instance.SetBlockedKeys(blockedKeys);

            // Subscribe to low level keypress events
            LowLevelKeyboardHook.Instance.KeyDownEvent += LowLevelKeyDownEvent;
            LowLevelKeyboardHook.Instance.KeyUpEvent += LowLevelKeyUpEvent;

            // Start the game
            blockStepTimer.Start();
            ShowNextBlock();
        }

        private void AddBlocksControls()
        {
            int rowCount;
            int columnCount;

            rowCount = tetrisGrid.RowDefinitions.Count;
            columnCount = tetrisGrid.ColumnDefinitions.Count;
            blocksControls = new Label[rowCount, columnCount];

            for (int i = 0; i < rowCount; i++)
            {
                for (int j = 0; j < columnCount; j++)
                {
                    Label blockLabel = new Label();
                    blockLabel.Background = NO_BLOCK_BRUSH;
                    blocksControls[i, j] = blockLabel;
                    Border blockBorder = new Border();
                    blockBorder.BorderBrush = Brushes.Black;
                    blockBorder.Child = blockLabel;
                    Grid.SetRow(blockBorder, i);
                    Grid.SetColumn(blockBorder, j);
                    tetrisGrid.Children.Add(blockBorder);
                }
            }

            // Preview blocks
            previewBlocks = new Label[3, 4];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    Label blockLabel = new Label();
                    blockLabel.Background = NO_BLOCK_BRUSH;
                    previewBlocks[i, j] = blockLabel;
                    Border blockBorder = new Border();
                    blockBorder.BorderBrush = Brushes.Black;
                    blockBorder.Child = blockLabel;
                    Grid.SetRow(blockBorder, i);
                    Grid.SetColumn(blockBorder, j);
                    previewGrid.Children.Add(blockBorder);
                }
            }
        }
        #endregion

        #region Game timer
        private void blockStepTimer_Tick(object sender, EventArgs e)
        {
#if !DEBUG
            try
            {
#endif
                MoveBlockDown();
#if !DEBUG
            }
            catch (Exception ex)
            {
                Log("Error during TimerTick\t" + ex.Message);
                ExitGame();
            }
#endif
        }
        #endregion

        #region Keyboard and mouse handling
        private void LowLevelKeyDownEvent(int keycode)
        {
#if !DEBUG
            try
            {
#endif
                switch (keycode)
                {
                    // Left and Numpad -
                    case 37:
                    case 109:
                        MoveBlockLeft();
                        break;

                    // Right and Numpad *
                    case 39:
                    case 106:
                        MoveBlockRight();
                        break;

                    // Down and Numpad +
                    case 40:
                    case 107:
                        keyDownPressed = true;


                        // Extra score when pressing down
                        if (!gameOver)
                        {
                            currentScore += PRESS_DOWN_POINTS;
                            scoreLabel.Content = "" + currentScore;
                        }

                        MoveBlockDown();
                        break;

                    // Up and Numpad /
                    case 38:
                    case 111:
                        keyUpPressed = true;
                        RotateBlockClockwise();
                        break;

                    default:
                        break;
                }
#if !DEBUG
            }
            catch (Exception ex)
            {
                Log("Error during KeyDown\t" + ex.Message);
                ExitGame();
            }
#endif
        }

        void LowLevelKeyUpEvent(int keycode)
        {
#if !DEBUG
            try
            {
#endif
                // Exit when both up and down are pressed simultanuously
                if (keyDownPressed && keyUpPressed)
                {
                    ExitGame();
                }


                switch (keycode)
                {
                    // Down and Numpad +
                    case 40:
                    case 107:
                        keyDownPressed = false;
                        break;

                    // Up and Numpad /
                    case 38:
                    case 111:
                        keyUpPressed = false;
                        break;

                    default:
                        break;
                }
#if !DEBUG
            }
            catch (Exception ex)
            {
                Log("Error during KeyUp\t" + ex.Message);
                ExitGame();
            }
#endif
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            
            if (gameOver)
            {
                Log("MouseClick\tExit");
                ExitGame();
            }

            if (e.RightButton == MouseButtonState.Pressed)
            {
                Log("MouseClick\tDown");
                if (!mouseDownPressed)
                {
                    mouseDownPressed = true;
                    mouseDownIntervalTemp = blockStepTimer.Interval;
                    blockStepTimer.Interval = PRESS_DOWN_SPEED;
                    MoveBlockDown();
                }
            }
            
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Log("MouseClick\tRotate");
                RotateBlockClockwise();
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.RightButton == MouseButtonState.Released && mouseDownPressed)
            {
                mouseDownPressed = false;
                blockStepTimer.Interval = mouseDownIntervalTemp;
            }
        }

        /// <summary>
        /// Save the game and exit
        /// </summary>
        private void ExitGame()
        {
            Log("Exit\tScore: " + currentScore);
            // Save highscore
            if (currentScore > Properties.Settings.Default.HighScore)
            {
                Properties.Settings.Default.HighScore = currentScore;
                Properties.Settings.Default.Save();
            }

            LowLevelKeyboardHook.Instance.Dispose();
            Application.Current.Shutdown();
        }
        #endregion

        #region Rotate and move blocks
        private void RotateBlockClockwise()
        {
            if (gameOver)
            {
                return;
            }

            ClearBlock();

            // Using regular for loop beacuse foreach causes iterator problems
            for (int i = 0; i < fallingBlock.Length; i++)
            {
                int x = (int)fallingBlock[i].X;
                fallingBlock[i].X = fallingBlock[i].Y;
                fallingBlock[i].Y = -1 * x;
            }

            PaintBlock();
        }

        private void MoveBlockRight()
        {
            if (gameOver)
            {
                return;
            }

            LinkedList<Point> rightestPoints = new LinkedList<Point>();
            int rightestX = int.MinValue;  // The highestValue, which is the lowest row

            foreach (Point p in fallingBlock)
            {
                if (rightestX == p.X)
                {
                    rightestPoints.AddFirst(p);
                }
                else if (rightestX < p.X)
                {
                    rightestX = (int)p.X;
                    rightestPoints.Clear();
                    rightestPoints.AddFirst(p);
                }
            }

            int baseX = (int)(fallingBlockPosition.X);
            int baseY = (int)(fallingBlockPosition.Y);

            int gridColumnCount = tetrisGrid.ColumnDefinitions.Count;

            // Check that it can move down
            bool canMoveRight = true;
            foreach (Point p in rightestPoints)
            {
                if (p.X < 0 || p.Y < 0)
                {
                    continue;
                }

                // -1 to check if the next possible value goes out of bounds
                if ((baseX + p.X + 1) >= gridColumnCount)
                {
                    canMoveRight = false;
                    break;
                }
                else if (blocksControls[(int)(baseY + p.Y), (int)(p.X + baseX + 1)].Background != NO_BLOCK_BRUSH)
                {
                    canMoveRight = false;
                    break;
                }
            }

            if (!canMoveRight)
            {
                return;
            }

            // Move right
            ClearBlock();
            fallingBlockPosition.X++;
            PaintBlock();
        }

        private void MoveBlockLeft()
        {
            if (gameOver)
            {
                return;
            }

            LinkedList<Point> leftestPoints = new LinkedList<Point>();
            int leftestX = int.MaxValue;  // The highestValue, which is the lowest row

            foreach (Point p in fallingBlock)
            {
                if (leftestX == p.X)
                {
                    leftestPoints.AddFirst(p);
                }
                else if (leftestX > p.X)
                {
                    leftestX = (int)p.X;
                    leftestPoints.Clear();
                    leftestPoints.AddFirst(p);
                }
            }

            int baseX = (int)(fallingBlockPosition.X);
            int baseY = (int)(fallingBlockPosition.Y);

            // Check that it can move down
            bool canMoveLeft = true;
            foreach (Point p in leftestPoints)
            {
                // -1 to check if the next possible value goes out of bounds
                if ((baseX + p.X - 1) < 0)
                {
                    canMoveLeft = false;
                    break;
                }

                if ((baseY + p.Y) < 0)
                {
                    continue;
                }
                else if (blocksControls[(int)(baseY + p.Y), (int)(p.X + baseX - 1)].Background != NO_BLOCK_BRUSH)
                {
                    canMoveLeft = false;
                    break;
                }
            }

            if (!canMoveLeft)
            {
                return;
            }

            // Move left
            ClearBlock();
            fallingBlockPosition.X--;
            PaintBlock();
        }

        private void MoveBlockDown()
        {
            if (gameOver)
            {
                return;
            }

            if (mouseDownPressed)
            {
                currentScore += PRESS_DOWN_POINTS;
                scoreLabel.Content = "" + currentScore;
            }

            LinkedList<Point> hitDetectionPoints = new LinkedList<Point>();
            foreach (Point p in fallingBlock)
            {
                // Check if there is a point under the current point
                bool found = false;
                foreach (Point q in fallingBlock)
                {
                    if (p.X == q.X && (p.Y + 1) == q.Y)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    hitDetectionPoints.AddFirst(p);
                }
            }

            int baseX = (int)(fallingBlockPosition.X);
            int baseY = (int)(fallingBlockPosition.Y);

            int gridRowCount = tetrisGrid.RowDefinitions.Count;
            int gridColCount = tetrisGrid.ColumnDefinitions.Count;

            // Check that it can move down
            bool canMoveDown = true;
            foreach (Point p in hitDetectionPoints)
            {
                if ((baseY + p.Y + 1) < 0 || (p.X + baseX) < 0)
                {
                    continue;
                }

                // +1 to check if the next possible value goes out of bounds
                if ((baseY + p.Y + 1) >= gridRowCount)
                {
                    canMoveDown = false;
                    break;
                }

                if ((p.X + baseX) >= gridColCount)
                {
                    continue;
                }

                if (blocksControls[(int)(baseY + p.Y + 1), (int)(p.X + baseX)].Background != NO_BLOCK_BRUSH)
                {
                    if (blockJustShowed)
                    {
                        LooseGame();
                        return;
                    }

                    canMoveDown = false;
                    break;
                }
            }

            if (!canMoveDown)
            {
                CheckForRowCompleted();
                ShowNextBlock();
                return;
            }

            // Move down
            ClearBlock();
            fallingBlockPosition.Y++;
            blockJustShowed = false;
            PaintBlock();
        }
        #endregion

        #region Row completed
        private void CheckForRowCompleted()
        {
            int rowCount = tetrisGrid.RowDefinitions.Count;
            int columnCount = tetrisGrid.ColumnDefinitions.Count;

            int completedRow = -1;
            for (int row = rowCount - 1; row >= 0; row--)
            {
                bool completed = true;
                for (int col = 0; col < columnCount; col++)
                {
                    if (blocksControls[row, col].Background == NO_BLOCK_BRUSH)
                    {
                        completed = false;
                        break;
                    }
                }

                if (completed)
                {
                    completedRow = row;
                    break;
                }
            }

            if (completedRow != -1)
            {
                RemoveTetrisRow(completedRow);
                IncrementScore(ROW_REMOVED_POINTS);
                Log("Row completed\tScore: " + currentScore);
                CheckForRowCompleted(); // And remove another one if exists
            }
        }

        private void RemoveTetrisRow(int rowToRemove)
        {
            int columnCount = tetrisGrid.ColumnDefinitions.Count;

            for (int row = rowToRemove; row > 0; row--)
            {
                for (int col = 0; col < columnCount; col++)
                {
                    blocksControls[row, col].Background = blocksControls[row - 1, col].Background;

                    Border currentRowBorder = ((Border)blocksControls[row, col].Parent);
                    Border upperRowBorder = ((Border)blocksControls[row - 1, col].Parent);
                    currentRowBorder.BorderThickness = upperRowBorder.BorderThickness;
                }
            }

            // Clear topmost row
            for (int col = 0; col < columnCount; col++)
            {
                PaintRow(0, NO_BLOCK_BRUSH);

                Border blockBorder = (Border)blocksControls[0, col].Parent;
                blockBorder.BorderThickness = NO_BLOCK_THICKNESS;
            }
        }

        public void IncrementScore(int amount)
        {
            currentScore += amount;
            scoreLabel.Content = "" + currentScore;

            if (currentScore >= nextLevelScoreThreshold)
            {
                currentLevel++;
                levelLabel.Content = "" + currentLevel;
                Log("Next level: " + currentLevel + "\tScore: " + currentScore);

                nextLevelScoreThreshold += 10 * ROW_REMOVED_POINTS;

                blockStepTimer.Interval = (int)(.85 * blockStepTimer.Interval);
                mouseDownIntervalTemp = (int)(.85 * mouseDownIntervalTemp);
            }

            scoreLabel.Content = "" + currentScore;
        }

        private void PaintRow(int row, Brush brush)
        {
            int columnCount = tetrisGrid.ColumnDefinitions.Count;

            for (int col = 0; col < columnCount; col++)
            {
                blocksControls[row, col].Background = brush;
            }
        }
        #endregion

        #region Paint and clear blocks on screen
        /// <summary>
        /// Generate a new block (random) and paint it on screen
        /// </summary>
        private void ShowNextBlock()
        {
            int columnCount = tetrisGrid.ColumnDefinitions.Count;

            fallingBlock = previewBlock;
            fallingBlockBrush = previewBlockBrush;
            previewBlock = GenerateRandomBlock();

            fallingBlockPosition = new Point(columnCount / 2, 1);
            PaintBlock();
            PaintPreviewBlock();
            blockJustShowed = true;
        }

        /// <summary>
        /// Clear the given block from the screen (when moving)
        /// </summary>
        private void ClearBlock()
        {
            foreach (Point p in fallingBlock)
            {
                int i = (int)(fallingBlockPosition.X + p.X);
                int j = (int)(fallingBlockPosition.Y + p.Y);

                if (i < 0 || j < 0 || j >= blocksControls.GetLength(0) || i >= blocksControls.GetLength(1))
                {
                    continue;
                }

                blocksControls[j, i].Background = NO_BLOCK_BRUSH;

                Border blockBorder = (Border)blocksControls[j, i].Parent;
                blockBorder.BorderThickness = NO_BLOCK_THICKNESS;
            }
        }

        /// <summary>
        /// Paint the block on the screen (when moving)
        /// </summary>
        private void PaintBlock()
        {
            foreach (Point p in fallingBlock)
            {
                int i = (int)(fallingBlockPosition.X + p.X);
                int j = (int)(fallingBlockPosition.Y + p.Y);

                if (i < 0 || j < 0 || j >= blocksControls.GetLength(0) || i >= blocksControls.GetLength(1))
                {
                    continue;
                }

                blocksControls[j, i].Background = fallingBlockBrush;

                Border blockBorder = (Border)blocksControls[j, i].Parent;
                blockBorder.BorderThickness = IN_BLOCK_THICKNESS;
            }
        }

        /// <summary>
        /// Paint the preview block on the screen
        /// </summary>
        private void PaintPreviewBlock()
        {
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    previewBlocks[i, j].Background = NO_BLOCK_BRUSH;

                    Border blockBorder = (Border)previewBlocks[i, j].Parent;
                    blockBorder.BorderThickness = NO_BLOCK_THICKNESS;
                }
            }

            foreach (Point p in previewBlock)
            {
                int i = (int)p.X + 1;
                int j = (int)p.Y + 1;

                previewBlocks[j, i].Background = previewBlockBrush;

                Border blockBorder = (Border)previewBlocks[j, i].Parent;
                blockBorder.BorderThickness = IN_BLOCK_THICKNESS;
            }
        }


        /// <summary>
        /// Generate a new block (random)
        /// </summary>
        /// <returns></returns>
        private Point[] GenerateRandomBlock()
        {
            Random rand = new Random();

            switch (rand.Next() % 7)
            {
                case 0: // T
                    previewBlockBrush = Brushes.Fuchsia;
                    return new Point[]{
                        new Point(0,0),
                        new Point(-1,0),
                        new Point(0,-1),
                        new Point(1,0),
                    };

                case 1: // L
                    previewBlockBrush = Brushes.Red;
                    return new Point[]{
                        new Point(0,0),
                        new Point(0,-1),
                        new Point(0,1),
                        new Point(1,1),
                    };

                case 2: // _
                    previewBlockBrush = Brushes.Lime;
                    return new Point[]{
                        new Point(0,0),
                        new Point(-1,0),
                        new Point(1,0),
                        new Point(2,0),
                    };

                case 3: // L inverted
                    previewBlockBrush = Brushes.Orange;
                    return new Point[]{
                        new Point(0,0),
                        new Point(0,-1),
                        new Point(0,1),
                        new Point(-1,1),
                    };


                case 4: // Z
                    previewBlockBrush = Brushes.Yellow;
                    return new Point[]{
                        new Point(0,0),
                        new Point(-1,0),
                        new Point(0,1),
                        new Point(1,1),
                    };


                case 5: // Z inverted
                    previewBlockBrush = Brushes.Turquoise;
                    return new Point[]{
                        new Point(0,0),
                        new Point(1,0),
                        new Point(0,1),
                        new Point(-1,1),
                    };

                case 6: // Cube
                    previewBlockBrush = Brushes.White;
                    return new Point[]{
                        new Point(0,0),
                        new Point(0,1),
                        new Point(1,0),
                        new Point(1,1),
                    };

                default:
                    return null;
            }
        }
        #endregion

        #region Game over
        /// <summary>
        /// When game over
        /// </summary>
        private void LooseGame()
        {
            blockStepTimer.Stop();
            previewGrid.Visibility = System.Windows.Visibility.Hidden;
            gameOverLabel.Visibility = System.Windows.Visibility.Visible;

            // Play Tetris theme song whistle
            if (!gameOver)
            {
                SoundPlayer player = new SoundPlayer(Properties.Resources.GameOverSound);
                player.PlayLooping();
            }

            gameOver = true;
        }
        #endregion

        private void Log(string text)
        {
            try
            {
                if (text != "")
                {
                    string baseDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
                    StreamWriter cachefile = File.AppendText(baseDir + "\\" + LOGFILE);
                    cachefile.WriteLine(DateTime.Now + "\t" + text);
                    cachefile.Close();
                }
            }
            catch (Exception) { }
        }
    }
}