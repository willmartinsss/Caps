using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.System;
using Microsoft.UI;

namespace SpaceInvaders;

public sealed partial class MainPage : Page
{
    private double _playerSpeed = 8;
    private bool isMovingLeft = false;
    private bool isMovingRight = false;
    private DispatcherTimer? gameLoopTimer;
    private Rectangle? playerProjectile = null;
    private List<Rectangle> enemies = new List<Rectangle>();
    private TextBlock? scoreText;
    private int score = 0;
    private Rectangle? specialEnemy;
    private DispatcherTimer? specialEnemyTimer;
    private double specialEnemySpeed = 3;


    public MainPage()
    {
        this.InitializeComponent();
        this.Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        await Task.Delay(100);
        this.Focus(FocusState.Programmatic);
        
        CreateScoreText();
        CreateEnemies();
        SetupSpecialEnemyTimer();

        gameLoopTimer = new DispatcherTimer();
        gameLoopTimer.Interval = TimeSpan.FromMilliseconds(16);
        gameLoopTimer.Tick += GameLoop;
        gameLoopTimer.Start();
    }

    private void GameLoop(object sender, object e)
    {
        // Movimento do jogador
        double left = Canvas.GetLeft(PlayerShip);
        if (isMovingLeft && left > 0)
        {
            Canvas.SetLeft(PlayerShip, left - _playerSpeed);
        }
        if (isMovingRight && (left + PlayerShip.Width < GameCanvas.ActualWidth))
        {
            Canvas.SetLeft(PlayerShip, left + _playerSpeed);
        }

        HandleProjectile();
        MoveSpecialEnemy();
    }
    private void CreateScoreText()
    {
        scoreText = new TextBlock
        {
            Text = "PONTOS: 0",
            Foreground = new SolidColorBrush(Colors.White),
            FontSize = 20,
        };
        Canvas.SetLeft(scoreText, 10);
        Canvas.SetTop(scoreText, 10);
        GameCanvas.Children.Add(scoreText);
    }
    private void CreateEnemies()
    {
        int enemyRows = 4;
        int enemyCols = 8;
        double enemyWidth = 30;
        double enemyHeight = 20;
        double enemySpacing = 10;
        
        double gridWidth = (enemyCols * enemyWidth) + ((enemyCols - 1) * enemySpacing);
        double startX = (this.ActualWidth - gridWidth) / 2;

        for (int row = 0; row < enemyRows; row++)
        {
            for (int col = 0; col < enemyCols; col++)
            {
                var enemy = new Rectangle
                {
                    Width = enemyWidth,
                    Height = enemyHeight,
                };

                if (row == 0)
                {
                    enemy.Fill = new SolidColorBrush(Colors.LightPink);
                    enemy.Tag = 40; 
                }
                else if (row < 3)
                {
                    enemy.Fill = new SolidColorBrush(Colors.LightBlue);
                    enemy.Tag = 20;
                }
                else
                {
                    enemy.Fill = new SolidColorBrush(Colors.White);
                    enemy.Tag = 10;
                }

                double left = startX + col * (enemyWidth + enemySpacing);
                double top = 70 + row * (enemyHeight + enemySpacing);

                Canvas.SetLeft(enemy, left);
                Canvas.SetTop(enemy, top);

                GameCanvas.Children.Add(enemy);
                enemies.Add(enemy);
            }
        }
    }
    private void HandleProjectile()
    {
        if (playerProjectile == null) return;

        double top = Canvas.GetTop(playerProjectile);
        Canvas.SetTop(playerProjectile, top - 10);

        if (top < 0)
        {
            RemoveProjectile();
            return;
        }

        Rect projectileRect = new Rect(Canvas.GetLeft(playerProjectile), top, playerProjectile.Width, playerProjectile.Height);
        
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            var enemy = enemies[i];
            Rect enemyRect = new Rect(Canvas.GetLeft(enemy), Canvas.GetTop(enemy), enemy.Width, enemy.Height);

            if (CheckCollision(projectileRect, enemyRect))
            {
                score += (int)enemy.Tag;
                UpdateScore();

                GameCanvas.Children.Remove(enemy);
                enemies.RemoveAt(i);
                RemoveProjectile();
                return;
            }
        }
        
        if (specialEnemy != null)
        {
            Rect specialEnemyRect = new Rect(Canvas.GetLeft(specialEnemy), Canvas.GetTop(specialEnemy), specialEnemy.Width, specialEnemy.Height);
            if (CheckCollision(projectileRect, specialEnemyRect))
            {
                score += (int)specialEnemy.Tag;
                UpdateScore();
                RemoveSpecialEnemy();
                RemoveProjectile();
            }
        }
    }
    private void SetupSpecialEnemyTimer()
    {
        specialEnemyTimer = new DispatcherTimer();
        specialEnemyTimer.Interval = TimeSpan.FromSeconds(15);
        specialEnemyTimer.Tick += (s, e) => CreateSpecialEnemy();
        specialEnemyTimer.Start();
    }

    private void CreateSpecialEnemy()
    {
        if (specialEnemy != null) return; 
        specialEnemy = new Rectangle
        {
            Width = 40,
            Height = 20,
            Fill = new SolidColorBrush(Colors.Red),
            Tag = 200
        };
        
        Canvas.SetTop(specialEnemy, 40);
        Canvas.SetLeft(specialEnemy, -specialEnemy.Width); 
        GameCanvas.Children.Add(specialEnemy);
    }

    private void MoveSpecialEnemy()
    {
        if (specialEnemy == null) return;
        
        double left = Canvas.GetLeft(specialEnemy);
        Canvas.SetLeft(specialEnemy, left + specialEnemySpeed);

        if (left > this.ActualWidth)
        {
            RemoveSpecialEnemy();
        }
    }

    private void RemoveSpecialEnemy()
    {
        if (specialEnemy == null) return;
        GameCanvas.Children.Remove(specialEnemy);
        specialEnemy = null;
    }
    private void UpdateScore()
    {
        if (scoreText != null)
        {
            scoreText.Text = $"PONTOS: {score}";
        }
    }
    
    private void RemoveProjectile()
    {
        if (playerProjectile == null) return;
        GameCanvas.Children.Remove(playerProjectile);
        playerProjectile = null;
    }

    private bool CheckCollision(Rect rect1, Rect rect2)
    {
        return rect1.X < rect2.X + rect2.Width &&
               rect1.X + rect1.Width > rect2.X &&
               rect1.Y < rect2.Y + rect2.Height &&
               rect1.Y + rect1.Height > rect2.Y;
    }
    private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Left: isMovingLeft = true; break;
            case VirtualKey.Right: isMovingRight = true; break;
            case VirtualKey.Space:
                if (playerProjectile == null)
                {
                    ShootPlayerProjectile();
                }
                break;
        }
        e.Handled = true;
    }
    
    private void Page_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Left: case VirtualKey.A: isMovingLeft = false; break;
            case VirtualKey.Right: case VirtualKey.D: isMovingRight = false; break;
        }
    }

    private void ShootPlayerProjectile()
    {
        playerProjectile = new Rectangle
        {
            Width = 5,
            Height = 15,
            Fill = new SolidColorBrush(Colors.Yellow)
        };
        double shipLeft = Canvas.GetLeft(PlayerShip);
        double shipTop = Canvas.GetTop(PlayerShip);
        Canvas.SetLeft(playerProjectile, shipLeft + PlayerShip.Width / 2 - 2.5);
        Canvas.SetTop(playerProjectile, shipTop - 15);
        GameCanvas.Children.Add(playerProjectile);
    }
}
