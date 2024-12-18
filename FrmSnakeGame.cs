/*
Copyright 2024 Allineed.Ru, Max Damascus

Permission is hereby granted, free of charge, to any person obtaining a copy of this software 
and associated documentation files (the "Software"), to deal in the Software without restriction, 
including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, 
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, 
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE 
OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SnakeGameExample {
    public partial class FrmSnakeGame : Form {        
        private const int CELL_SIZE_PIXELS = 30;                    // Размер клетки игрового поля, в пикселях
        private const int ROWS_NUMBER = 15;                         // Количество рядов в игровом поле
        private const int COLS_NUMBER = 15;                         // Количество столбцов в игровом поле
        private const int FIELD_LEFT_OFFSET_PIXELS = 40;            // Отступ в пикселях от левого края формы
        private const int FIELD_TOP_OFFSET_PIXELS = 15;             // Отступ в пикселях от правого края формы
        private const int INITIAL_SNAKE_SPEED_INTERVAL = 300;       // Задержка (свойство "Interval") для основного игрового таймера TimerGameLoop
        private const int SPEED_INCREMENT_BY = 5;                   // На сколько миллисекунд увеличить скорость "Змейки" при очередном поглощении змейкой "Еды"        
        private const bool ENABLE_KEYDOWN_DELAY = true;             // [Оптимизированный вариант] Разрешить функцию задержки при нажатии клавиш?
        private const int KEYDOWN_DELAY_MILLIS = 450;               // [Оптимизированный вариант] Минимальное количество миллисекунд, которое должно пройти между последовательными нажатиями клавиш
        private enum SnakeDirection {
            Left,
            Right,
            Up,
            Down
        }

        private SnakeDirection snakeDirection = SnakeDirection.Up;      // Текущее направление движения "Змейки"
        private LinkedList<Point> snake = new LinkedList<Point>();      // Список точек, содержащих координаты всего "тела Змейки"
        private Point food;                                             // Точка, содержащая координаты "Еды" для "Змейки"
        private Random rand = new Random();                             // Генератор псевдослучайных чисел. нужен для генерации очередной "Еды" в произвольном месте игрового поля
        private bool isGameEnded;                                       // Признак: игра завершена?
        
        private int foodAlpha = 255;                                    // [Оптимизированный вариант] Значение для Альфа-канала для цвета "Еды". Необходимо для эффекта "мерцания" еды на игровом поле
        private int foodAlphaInc = -25;                                 // [Оптимизированный вариант] Текущее значение, которое будет по таймеру прибавляться к значению foodAlpha
        private bool isGamePaused;                                      // [Оптимизированный вариант] Признак: поставлена ли игра на паузу?
        private Stopwatch keyPressSensivityStopwatch = new Stopwatch(); // [Оптимизированный вариант] Объект Stopwatch для подсчёта количества миллисекунд между нажатиями клавиш игроком
        private int points = 0;                                         // [Оптимизированный вариант] Статистика: количество очков, набранных игроком в игре
        private int foodEaten = 0;                                      // [Оптимизированный вариант] Статистика: количество "Еды", поглощённой "Змейкой"

        public FrmSnakeGame() {
            InitializeComponent();
        }

        public void InitializeSnake() {
            snakeDirection = SnakeDirection.Up;
            snake.Clear();
            snake.AddFirst(new Point(ROWS_NUMBER - 1, COLS_NUMBER / 2 - 1));
        }

        private void DrawGrid(Graphics g) {
            for (int row = 0; row <= ROWS_NUMBER; row++) {
                g.DrawLine(Pens.Cyan, 
                    new Point(FIELD_LEFT_OFFSET_PIXELS, FIELD_TOP_OFFSET_PIXELS + row * CELL_SIZE_PIXELS), 
                    new Point(FIELD_LEFT_OFFSET_PIXELS + CELL_SIZE_PIXELS * ROWS_NUMBER, FIELD_TOP_OFFSET_PIXELS + row * CELL_SIZE_PIXELS)
                );

                for (int col = 0; col <= COLS_NUMBER; col++) {
                    g.DrawLine(Pens.Cyan, new Point(FIELD_LEFT_OFFSET_PIXELS + col * CELL_SIZE_PIXELS, FIELD_TOP_OFFSET_PIXELS), 
                        new Point(FIELD_LEFT_OFFSET_PIXELS + col * CELL_SIZE_PIXELS, FIELD_TOP_OFFSET_PIXELS + CELL_SIZE_PIXELS * COLS_NUMBER));
                }
            }
        }

        private void GenerateFood() {
            bool isFoodClashWithSnake;
            do {
                food = new Point(rand.Next(0, ROWS_NUMBER), rand.Next(0, COLS_NUMBER));
                isFoodClashWithSnake = false;
                foreach (Point p in snake) {
                    if (p.X == food.X && p.Y == food.Y) {
                        isFoodClashWithSnake = true;
                        break;
                    }
                }
            } while (isFoodClashWithSnake);
            // увеличить скорость игры
            TimerGameLoop.Interval -= SPEED_INCREMENT_BY;
            foodAlpha = 255;
            foodAlphaInc = -25;
        }

        private void DrawSnake(Graphics g) {
            int snakePoint = 0;
            foreach (Point p in snake) {
                Rectangle snakeBodyRectangle = new Rectangle(
                    FIELD_LEFT_OFFSET_PIXELS + p.Y * CELL_SIZE_PIXELS + 1,
                    FIELD_TOP_OFFSET_PIXELS + p.X * CELL_SIZE_PIXELS + 1,
                    CELL_SIZE_PIXELS - 1,
                    CELL_SIZE_PIXELS - 1);
                Brush brushSnakeBodyGradient = new LinearGradientBrush(snakeBodyRectangle, snakePoint == 0 ? Color.Black : Color.DarkGreen, snakePoint == 0 ? Color.DarkGreen : Color.Lime, 100, true);

                g.FillRectangle(brushSnakeBodyGradient, snakeBodyRectangle);
                brushSnakeBodyGradient.Dispose();

                if (snakePoint == 0) {
                    // snakePoint == 0 - это голова. Нарисуем глаза "Змейки"
                    int offsetLeftEyeX = 0, offsetLeftEyeCircleX = 0;
                    int offsetLeftEyeY = 0, offsetLeftEyeCircleY = 0;
                    int offsetRightEyeX = 0, offsetRightEyeCircleX = 0;
                    int offsetRightEyeY = 0, offsetRightEyeCircleY = 0;
                    int eyeWidth = 0;
                    int eyeHeight = 0;
                    switch(snakeDirection) {
                        case SnakeDirection.Left:
                            eyeWidth = 10; eyeHeight = 3; offsetLeftEyeX = 5; offsetLeftEyeY = 22; offsetRightEyeX = 5; offsetRightEyeY = 5;
                            offsetLeftEyeCircleX = 7; offsetRightEyeCircleX = 7; offsetLeftEyeCircleY = 22; offsetRightEyeCircleY = 5;
                            break;
                        case SnakeDirection.Right:
                            eyeWidth = 10; eyeHeight = 3; offsetLeftEyeX = CELL_SIZE_PIXELS - eyeWidth - 5; offsetLeftEyeY = 5; offsetRightEyeX = CELL_SIZE_PIXELS - eyeWidth - 5; offsetRightEyeY = 22;
                            offsetLeftEyeCircleX = CELL_SIZE_PIXELS - eyeWidth - 5 + 5; 
                            offsetRightEyeCircleX = CELL_SIZE_PIXELS - eyeWidth - 5 + 5; 
                            offsetLeftEyeCircleY = 5; offsetRightEyeCircleY = 22;
                            break;
                        case SnakeDirection.Up:
                            eyeWidth = 3; eyeHeight = 10; offsetLeftEyeX = 5; offsetLeftEyeY = 5; offsetRightEyeX = 22; offsetRightEyeY = 5;
                            offsetLeftEyeCircleX = offsetLeftEyeX;
                            offsetRightEyeCircleX = offsetRightEyeX;
                            offsetLeftEyeCircleY = offsetLeftEyeY + 2; offsetRightEyeCircleY = offsetRightEyeY + 2;
                            break;
                        case SnakeDirection.Down:
                            eyeWidth = 3; eyeHeight = 10; offsetLeftEyeX = 22; offsetLeftEyeY = CELL_SIZE_PIXELS - eyeHeight - 5; offsetRightEyeX = 5; offsetRightEyeY = CELL_SIZE_PIXELS - eyeHeight - 5;
                            offsetLeftEyeCircleX = offsetLeftEyeX;
                            offsetRightEyeCircleX = offsetRightEyeX;
                            offsetLeftEyeCircleY = CELL_SIZE_PIXELS - eyeHeight - 5 + 5; offsetRightEyeCircleY = CELL_SIZE_PIXELS - eyeHeight - 5 + 5;
                            break;
                    }
                    // Рисуем правый глаз "Змейки"
                    g.FillEllipse(Brushes.Yellow, new Rectangle(
                        FIELD_LEFT_OFFSET_PIXELS + p.Y * CELL_SIZE_PIXELS + 1 + offsetRightEyeX,
                        FIELD_TOP_OFFSET_PIXELS + p.X * CELL_SIZE_PIXELS + 1 + offsetRightEyeY,
                        eyeWidth,
                        eyeHeight));

                    // Рисуем зрачок для правого глаза "Змейки"
                    g.FillEllipse(Brushes.Black, new Rectangle(
                        FIELD_LEFT_OFFSET_PIXELS + p.Y * CELL_SIZE_PIXELS + 1 + offsetRightEyeCircleX,
                        FIELD_TOP_OFFSET_PIXELS + p.X * CELL_SIZE_PIXELS + 1 + offsetRightEyeCircleY,
                        3,
                        3));

                    // Рисуем левый глаз "Змейки"
                    g.FillEllipse(Brushes.Yellow, new Rectangle(
                        FIELD_LEFT_OFFSET_PIXELS + p.Y * CELL_SIZE_PIXELS + 1 + offsetLeftEyeX,
                        FIELD_TOP_OFFSET_PIXELS + p.X * CELL_SIZE_PIXELS + 1 + offsetLeftEyeY,
                        eyeWidth,
                        eyeHeight));

                    // Рисуем зрачок для левого глаза "Змейки"
                    g.FillEllipse(Brushes.Black, new Rectangle(
                        FIELD_LEFT_OFFSET_PIXELS + p.Y * CELL_SIZE_PIXELS + 1 + offsetLeftEyeCircleX,
                        FIELD_TOP_OFFSET_PIXELS + p.X * CELL_SIZE_PIXELS + 1 + offsetLeftEyeCircleY,
                        3,
                        3));
                }
                snakePoint++;
            }
        }

        private void DrawFood(Graphics g) {
            Rectangle foodRectangle = new Rectangle(
                FIELD_LEFT_OFFSET_PIXELS + food.Y * CELL_SIZE_PIXELS + 1,
                FIELD_TOP_OFFSET_PIXELS + food.X * CELL_SIZE_PIXELS + 1,
                CELL_SIZE_PIXELS - 1,
                CELL_SIZE_PIXELS - 1);
            Brush brushFood = new LinearGradientBrush(foodRectangle, 
                Color.FromArgb(foodAlpha, Color.Crimson.R, Color.Crimson.G, Color.Crimson.B),
                Color.FromArgb(foodAlpha, Color.RosyBrown.R, Color.RosyBrown.G, Color.RosyBrown.B),
                100, true);
            g.FillEllipse(brushFood, foodRectangle);
            Brush brushFoodBorder = new SolidBrush(Color.FromArgb(foodAlpha, Color.Red.R, Color.Red.G, Color.Red.B));
            Pen penFoodBorder = new Pen(brushFoodBorder);
            g.DrawEllipse(penFoodBorder, foodRectangle);
            brushFood.Dispose();
            penFoodBorder.Dispose();
            brushFoodBorder.Dispose();
        }

        private void DrawStatsAndKeyboardHints(Graphics g) {
            Font fontStats = new Font("Consolas", 14);
            int statsLeftOffset = FIELD_LEFT_OFFSET_PIXELS + CELL_SIZE_PIXELS * COLS_NUMBER + 10;            
            g.DrawString(string.Format("Длина змейки: {0}", snake.Count), fontStats, Brushes.Lime, new Point(statsLeftOffset, 10));
            g.DrawString(string.Format("Скорость: {0}", INITIAL_SNAKE_SPEED_INTERVAL - TimerGameLoop.Interval + 5), fontStats, Brushes.Lime, new Point(statsLeftOffset, 30));
            g.DrawString(string.Format("Очки: {0}", points), fontStats, Brushes.Goldenrod, new Point(statsLeftOffset, 50));
            g.DrawString(string.Format("Еды съедено: {0}", foodEaten), fontStats, Brushes.Crimson, new Point(statsLeftOffset, 70));

            g.DrawString("Управление:", fontStats, Brushes.White, new Point(statsLeftOffset, 160));
            g.DrawString("Вверх: ↑ или W", fontStats, Brushes.White, new Point(statsLeftOffset, 190));
            g.DrawString("Вниз:  ↓ или S", fontStats, Brushes.White, new Point(statsLeftOffset, 210));
            g.DrawString("Влево: ← или A", fontStats, Brushes.White, new Point(statsLeftOffset, 230));
            g.DrawString("Влево: → или D", fontStats, Brushes.White, new Point(statsLeftOffset, 250));
            g.DrawString("Пауза: [Space]", fontStats, Brushes.White, new Point(statsLeftOffset, 270));
            g.DrawString("Старт: [Space]", fontStats, Brushes.White, new Point(statsLeftOffset, 290));
            g.DrawString("Выход: [Escape]", fontStats, Brushes.White, new Point(statsLeftOffset, 310));

            if (isGamePaused) {
                g.DrawString("Игра на паузе...", fontStats, Brushes.Yellow, new Point(statsLeftOffset, 350));
            }
            fontStats.Dispose();
        }

        private void MoveSnake() {
            LinkedListNode<Point> head = snake.First;
            Point newHead = new Point(0, 0);
            switch (snakeDirection) {
                case SnakeDirection.Left:
                    newHead = new Point(head.Value.X, head.Value.Y - 1);
                    break;
                case SnakeDirection.Right:                    
                    newHead = new Point(head.Value.X, head.Value.Y + 1);
                    break;
                case SnakeDirection.Down:                    
                    newHead = new Point(head.Value.X + 1, head.Value.Y);
                    break;
                case SnakeDirection.Up:             
                    newHead = new Point(head.Value.X - 1, head.Value.Y);
                    break;
            }

            foreach (Point p in snake) {
                if (p.X == newHead.X && p.Y == newHead.Y) {
                    // змейка съела сама себя! конец игры!
                    Invalidate();
                    GameOver();
                    return;
                }
            }

            snake.AddFirst(newHead);

            if (newHead.X == food.X && newHead.Y == food.Y) {
                // съели еду, вознаграждаем игрока очками
                AddPlayerPoints();

                // увеличиваем счётчик съеденной еды
                foodEaten++;

                // генерируем новую еду на поле
                GenerateFood();
            } else {                
                snake.RemoveLast();
            }            
        }

        private void AddPlayerPoints() {
            if (food.X == 0 && food.Y == 0 || food.X == ROWS_NUMBER - 1 && food.Y == 0 || food.X == ROWS_NUMBER - 1 && food.Y == COLS_NUMBER - 1 || food.X == 0 && food.Y == COLS_NUMBER - 1) {
                points += 1000;
            } else if (food.X == 0 || food.X == ROWS_NUMBER - 1 || food.Y == 0 || food.Y == COLS_NUMBER - 1) {
                points += 500;
            } else {
                points += 250;
            }
        }

        private void FrmSnakeGame_Paint(object sender, PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighQuality;
            DrawGrid(g);
            DrawFood(g);
            DrawSnake(g);
            DrawStatsAndKeyboardHints(g);
        }

        private void FrmSnakeGame_Load(object sender, EventArgs e) {
            DoubleBuffered = true;
            BackColor = Color.Black;
            StartGame();
        }

        private void StartGame() {
            GenerateFood();
            InitializeSnake();
            isGameEnded = false;
            isGamePaused = false;
            foodAlpha = 255; 
            foodAlphaInc = -25;
            points = 0;
            foodEaten = 0;
            TimerGameLoop.Start();
            TimerFoodBlink.Start();            
            TimerGameLoop.Interval = INITIAL_SNAKE_SPEED_INTERVAL;
        }

        private bool IsGameOver() {
            LinkedListNode<Point> head = snake.First;
            switch (snakeDirection) {
                case SnakeDirection.Left:
                    return head.Value.Y - 1 < 0;
                case SnakeDirection.Right:
                    return head.Value.Y + 1 >= COLS_NUMBER;
                case SnakeDirection.Down:
                    return head.Value.X + 1 >= ROWS_NUMBER;                    
                case SnakeDirection.Up:
                    return head.Value.X - 1 < 0;
            }
            return false;
        }

        private void GameOver() {
            isGameEnded = true;
            TimerGameLoop.Stop();
            TimerFoodBlink.Stop();  // [Оптимизированный вариант] Останавливаем таймер мерцания для "Еды"
            if (MessageBox.Show("Конец игры! Начать заново?", "Конец игры", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) {
                StartGame();
            }
        }

        private void TimerGameLoop_Tick(object sender, EventArgs e) {
            if (IsGameOver()) {
                GameOver();
            } else {
                MoveSnake();
                Invalidate();
            }            
        }

        private void FrmSnakeGame_KeyDown(object sender, KeyEventArgs e) {
            if (ENABLE_KEYDOWN_DELAY) {
                if (!keyPressSensivityStopwatch.IsRunning) {
                    keyPressSensivityStopwatch.Start();
                } else {
                    if (keyPressSensivityStopwatch.ElapsedMilliseconds < KEYDOWN_DELAY_MILLIS) {
                        return;
                    } else {
                        keyPressSensivityStopwatch.Restart();
                    }
                }
            }

            switch (e.KeyCode) {
                case Keys.Left:
                case Keys.A:
                    if (snakeDirection != SnakeDirection.Right) {
                        snakeDirection = SnakeDirection.Left;
                    }                    
                    break;
                case Keys.Right:
                case Keys.D:
                    if (snakeDirection != SnakeDirection.Left) {
                        snakeDirection = SnakeDirection.Right;
                    }                        
                    break;
                case Keys.Down:
                case Keys.S:
                    if (snakeDirection != SnakeDirection.Up) {
                        snakeDirection = SnakeDirection.Down;
                    }                    
                    break;
                case Keys.Up:
                case Keys.W:
                    if (snakeDirection != SnakeDirection.Down) {
                        snakeDirection = SnakeDirection.Up;
                    }                    
                    break;
                case Keys.Escape:
                    TimerGameLoop.Stop();
                    TimerFoodBlink.Stop();  // [Оптимизированный вариант] Перед выходом из игры также остановить таймер мерцания "Еды"
                    Close();
                    break;
                case Keys.Space:
                    if (isGameEnded && !TimerGameLoop.Enabled) {
                        StartGame();
                    } else {
                        PauseOrUnpauseGame();   // [Оптимизированный вариант] Поставить игру на паузу или снять с паузы
                    }
                    break;
            }
        }

        private void PauseOrUnpauseGame() {
            if (!isGamePaused) {
                TimerGameLoop.Stop();
                TimerFoodBlink.Stop();
                Invalidate();
            } else {
                TimerGameLoop.Start();
                TimerFoodBlink.Start();
            }
            isGamePaused = !isGamePaused;
        }

        private void TimerFoodBlink_Tick(object sender, EventArgs e) {            
            if (foodAlpha + 25 > 255) {
                foodAlphaInc = -25;
            } else if (foodAlpha - 25 < 0) {
                foodAlphaInc = 25;
            }
            foodAlpha += foodAlphaInc;
            Invalidate();
        }
    }
}
