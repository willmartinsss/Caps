using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.Foundation;
using Windows.System;
using System.Threading.Tasks;
using Windows.UI;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media.Imaging;

namespace SpaceInvaders;

public sealed partial class MainPage : Page
{
    private enum GameState
    {
        Menu,
        Playing,
        GameOver
    }
    private GameState currentState;
    private double _playerSpeed = 8;
    private bool isMovingLeft = false;
    private bool isMovingRight = false;
    private DispatcherTimer? gameLoopTimer;
    private Rectangle? playerProjectile = null;
    
    private List<Rectangle> shieldParts = new List<Rectangle>();
    private List<Rectangle> enemies = new List<Rectangle>();
    
    private TextBlock? scoreText;
    private int score = 0;
    private Rectangle? specialEnemy;
    private DispatcherTimer? specialEnemyTimer;
    private double specialEnemySpeed = 3;
    private TextBlock? titleText;
    private Button? startButton;
    private TextBlock? gameOverText;
    
    
    private ImageBrush? alienSkin10;
    private ImageBrush? alienSkin20;
    private ImageBrush? alienSkin40;
    private ImageBrush? specialAlienSkin;

    public MainPage()
    {
        this.InitializeComponent();
        this.Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        await Task.Delay(100);
        this.Focus(FocusState.Programmatic);

        LoadEnemySkins();
        
        SetupUI();
        ShowMenu();

        gameLoopTimer = new DispatcherTimer();
        gameLoopTimer.Interval = TimeSpan.FromMilliseconds(16);
        gameLoopTimer.Tick += GameLoop;
        gameLoopTimer.Start();
    }

    private void GameLoop(object sender, object e)
    {
        if (currentState != GameState.Playing)
        {
            return;
        }

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
    
    private void SetupUI()
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

        titleText = new TextBlock
        {
            Text = "SPACE INVADERS",
            Foreground = new SolidColorBrush(Colors.White),
            FontSize = 60,
        };

        startButton = new Button
        {
            Content = "Iniciar Jogo",
            FontSize = 30,
            Width = 250,
            Height = 60,
        };
        startButton.Click += (s, e) => StartNewGame();

        gameOverText = new TextBlock
        {
            Text = "VOCÊ VENCEU!",
            Foreground = new SolidColorBrush(Colors.Green),
            FontSize = 60,
        };
    }

    private void LoadEnemySkins()
    {
        alienSkin40 = new ImageBrush { ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/images/alien3.png")) };
        alienSkin20 = new ImageBrush { ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/images/alien2.png")) };
        alienSkin10 = new ImageBrush { ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/images/alien1.png")) };
        specialAlienSkin = new ImageBrush { ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/images/alienespecial.png")) };
    }

    private void ShowMenu()
    {
        currentState = GameState.Menu;
        
        titleText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(titleText, (this.ActualWidth - titleText.DesiredSize.Width) / 2);
        Canvas.SetTop(titleText, 200);
        GameCanvas.Children.Add(titleText);

        Canvas.SetLeft(startButton, (this.ActualWidth - startButton.Width) / 2);
        Canvas.SetTop(startButton, 300);
        GameCanvas.Children.Add(startButton);

        PlayerShip.Visibility = Visibility.Collapsed;
        scoreText.Visibility = Visibility.Collapsed;
    }

    private void StartNewGame()
    {
        currentState = GameState.Playing;

        GameCanvas.Children.Remove(titleText);
        GameCanvas.Children.Remove(startButton);
        GameCanvas.Children.Remove(gameOverText);

        score = 0;
        UpdateScore();
        
        foreach(var enemy in enemies)
        {
            GameCanvas.Children.Remove(enemy);
        }
        enemies.Clear();
        
        foreach(var part in shieldParts) 
        {
            GameCanvas.Children.Remove(part);
        }
        shieldParts.Clear();

        RemoveSpecialEnemy();
        RemoveProjectile();
     
        CreateEnemies();
        CreateShields(); 
        
        PlayerShip.Visibility = Visibility.Visible;
        ApplyPlayerSkin();
        scoreText.Visibility = Visibility.Visible;

        SetupSpecialEnemyTimer();
    }
    
    private void ApplyPlayerSkin()
    {
        var playerSkin = new ImageBrush();
        playerSkin.ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/images/player.png"));
        PlayerShip.Fill = playerSkin;
    }

    private void ShowGameOver()
    {
        currentState = GameState.GameOver;
        specialEnemyTimer?.Stop();

        gameOverText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(gameOverText, (this.ActualWidth - gameOverText.DesiredSize.Width) / 2);
        Canvas.SetTop(gameOverText, 250);
        GameCanvas.Children.Add(gameOverText);
        
        startButton.Content = "Jogar Denovo";
        Canvas.SetLeft(startButton, (this.ActualWidth - startButton.Width) / 2);
        Canvas.SetTop(startButton, 350);
        GameCanvas.Children.Add(startButton);
    }

    private void CreateEnemies()
    {
        int enemyRows = 4;
        int enemyCols = 8;
        double enemyWidth = 35;
        double enemyHeight = 25;
        double enemySpacing = 13;
        
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
                    enemy.Fill = alienSkin40;
                    enemy.Tag = 40; 
                }
                else if (row < 3)
                {
                    enemy.Fill = alienSkin20;
                    enemy.Tag = 20;
                }
                else
                {
                    enemy.Fill = alienSkin10;
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

    private void CreateShields()
    {
        int shieldBlockSize = 10; 
        int numberOfShields = 3;
        double shieldWidth = 6 * shieldBlockSize; 
        double shieldSpacing = 210; 
        double shieldBaseY = 450; 

        double totalShieldsWidth = (numberOfShields * shieldWidth) + ((numberOfShields - 1) * shieldSpacing);
        double startX = (this.ActualWidth - totalShieldsWidth) / 2;

        for (int i = 0; i < numberOfShields; i++)
        {
            double shieldBaseX = startX + i * (shieldWidth + shieldSpacing);

            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 6; col++)
                {
                    var shieldPart = new Rectangle
                    {
                        Width = shieldBlockSize,
                        Height = shieldBlockSize,
                        Fill = new SolidColorBrush(Colors.LightGreen)
                    };

                    Canvas.SetLeft(shieldPart, shieldBaseX + col * shieldBlockSize);
                    Canvas.SetTop(shieldPart, shieldBaseY + row * shieldBlockSize);
                    
                    GameCanvas.Children.Add(shieldPart);
                    shieldParts.Add(shieldPart);
                }
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
        
        for (int i = shieldParts.Count - 1; i >= 0; i--)
        {
            var part = shieldParts[i];
            Rect shieldPartRect = new Rect(Canvas.GetLeft(part), Canvas.GetTop(part), part.Width, part.Height);

            if (CheckCollision(projectileRect, shieldPartRect))
            {
                GameCanvas.Children.Remove(part);
                shieldParts.RemoveAt(i);
                RemoveProjectile();
                return; 
            }
        }
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
        specialEnemyTimer?.Stop();
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
            Height = 25,
            Fill = specialAlienSkin,
            Tag = 100
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

        if (score >= 500)
        {
            ShowGameOver();
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
        if (currentState != GameState.Playing) return;

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
