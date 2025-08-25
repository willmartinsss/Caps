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
using Windows.Media.Playback;
using Windows.UI;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Media.Core;
using Windows.UI.Text;
using Microsoft.UI.Text;
using System.Linq;
using Windows.Storage;
using NAudio.Wave;

namespace SpaceInvaders;

public sealed partial class MainPage : Page
{
    // Enum para gerenciar os diferentes estados do jogo
    private enum GameState
    {
        Menu,
        Playing,
        GameOver,
        HighScores,
        Controls
    }

    private GameState currentState;

    // --- Variáveis do Jogador ---
    private double _playerSpeed = 8;
    private bool isMovingLeft = false;
    private bool isMovingRight = false;
    private Rectangle? playerProjectile = null;
    private int playerLives;
    private const int maxLives = 6;
    private const int initialLives = 3;
    private bool canShoot = true;

    // --- Variáveis dos Inimigos ---
    private List<Rectangle> enemies = new List<Rectangle>();
    private List<Rectangle> enemyProjectiles = new List<Rectangle>();
    private double enemySpeed = 1.5;
    private int enemyDirection = 1; // 1 para direita, -1 para esquerda
    private double enemyFireRate = 1000; // ms
    private int waveNumber = 1;

    // --- Inimigo Especial (OVNI) ---
    private Rectangle? specialEnemy;
    private double specialEnemySpeed = 3;

    // --- Escudos ---
    private List<Rectangle> shieldParts = new List<Rectangle>();

    // --- Timers do Jogo ---
    private DispatcherTimer? gameLoopTimer;
    private DispatcherTimer? specialEnemyTimer;
    private DispatcherTimer? enemyFireTimer;

    // --- Elementos da UI ---
    private TextBlock? scoreText;
    private StackPanel? livesPanel;
    private int score = 0;

    // Painéis para cada estado do jogo
    private StackPanel? menuPanel;
    private StackPanel? gameOverPanel;
    private StackPanel? highScoresPanel;
    private StackPanel? controlsPanel;

    // --- Recursos Visuais e de Áudio ---
    private ImageBrush? alienSkin10, alienSkin20, alienSkin40, specialAlienSkin, playerSkin;
    private MediaPlayer? mediaPlayer; // Usaremos MediaPlayer para os sons

    public MainPage()
    {
        this.InitializeComponent();
        this.Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        // Garante que o Canvas tenha foco para receber eventos do teclado
        await Task.Delay(100);
        this.Focus(FocusState.Programmatic);

        // Carrega recursos e inicializa a UI
        LoadSkins();
        SetupSound();
        SetupUI();
        ShowMenu(); // Começa mostrando o menu principal

        // Inicia o loop principal do jogo
        gameLoopTimer = new DispatcherTimer();
        gameLoopTimer.Interval = TimeSpan.FromMilliseconds(16); // Aproximadamente 60 FPS
        gameLoopTimer.Tick += GameLoop;
        gameLoopTimer.Start();
    }

    #region Setup e Inicialização

    /// <summary>
    /// Carrega todas as imagens (skins) para os elementos do jogo.
    /// </summary>
    private void LoadSkins()
    {
        alienSkin40 = new ImageBrush { ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/images/alien3.png")) };
        alienSkin20 = new ImageBrush { ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/images/alien2.png")) };
        alienSkin10 = new ImageBrush { ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/images/alien1.png")) };
        specialAlienSkin = new ImageBrush { ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/images/alienespecial.png")) };
        playerSkin = new ImageBrush { ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/images/player.png")) };
    }

    /// <summary>
    /// Inicializa o MediaPlayer para tocar os efeitos sonoros.
    /// </summary>
    private void SetupSound()
    {
        mediaPlayer = new MediaPlayer();
    }

    /// <summary>
    /// Cria todos os elementos da interface do usuário (UI) que serão usados no jogo.
    /// </summary>
    private void SetupUI()
    {
        // --- UI do Jogo (visível durante o gameplay) ---
        scoreText = new TextBlock
        {
            Text = "PONTOS: 0",
            Foreground = new SolidColorBrush(Colors.White),
            FontSize = 20,
        };
        Canvas.SetLeft(scoreText, 10);
        Canvas.SetTop(scoreText, 10);
        GameCanvas.Children.Add(scoreText);

        livesPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        Canvas.SetLeft(livesPanel, this.Width - 150); // Posiciona no canto superior direito
        Canvas.SetTop(livesPanel, 10);
        GameCanvas.Children.Add(livesPanel);

        // --- Painel do Menu Principal ---
        menuPanel = CreateMenuPanel();
        GameCanvas.Children.Add(menuPanel);

        // --- Painel de Fim de Jogo ---
        gameOverPanel = CreateGameOverPanel();
        GameCanvas.Children.Add(gameOverPanel);

        // --- Painel de Controles ---
        controlsPanel = CreateControlsPanel();
        GameCanvas.Children.Add(controlsPanel);

        // --- Painel de Pontuações ---
        highScoresPanel = CreateHighScoresPanel();
        GameCanvas.Children.Add(highScoresPanel);
    }

    #endregion

    #region Lógica do Loop Principal do Jogo

    /// <summary>
    /// O coração do jogo, executado a cada frame.
    /// </summary>
    private void GameLoop(object sender, object e)
    {
        if (currentState != GameState.Playing) return;

        MovePlayer();
        MovePlayerProjectile();
        MoveEnemies();
        MoveEnemyProjectiles();
        MoveSpecialEnemy();
        CheckCollisions();
        CheckGameState();
    }

    /// <summary>
    /// Move a nave do jogador com base nas teclas pressionadas.
    /// </summary>
    private void MovePlayer()
    {
        double left = Canvas.GetLeft(PlayerShip);
        if (isMovingLeft && left > 0)
        {
            Canvas.SetLeft(PlayerShip, left - _playerSpeed);
        }
        if (isMovingRight && (left + PlayerShip.Width < GameCanvas.ActualWidth))
        {
            Canvas.SetLeft(PlayerShip, left + _playerSpeed);
        }
    }

    /// <summary>
    /// Movimenta o projétil do jogador e verifica se saiu da tela.
    /// </summary>
    private void MovePlayerProjectile()
    {
        if (playerProjectile == null) return;

        double top = Canvas.GetTop(playerProjectile);
        Canvas.SetTop(playerProjectile, top - 15); // Velocidade do projétil

        if (top < 0)
        {
            RemovePlayerProjectile();
        }
    }

    /// <summary>
    /// Movimenta o bloco de inimigos.
    /// </summary>
    private void MoveEnemies()
    {
        if (!enemies.Any()) return;

        bool edgeReached = false;
        foreach (var enemy in enemies)
        {
            double left = Canvas.GetLeft(enemy);
            Canvas.SetLeft(enemy, left + (enemySpeed * enemyDirection));

            if ((left <= 0 && enemyDirection == -1) || (left + enemy.Width >= GameCanvas.ActualWidth && enemyDirection == 1))
            {
                edgeReached = true;
            }
        }

        if (edgeReached)
        {
            enemyDirection *= -1; // Inverte a direção
            enemySpeed += 0.1; // Aumenta a velocidade
            if (enemyFireRate > 300) enemyFireRate -= 50; // Aumenta a cadência de tiro
            UpdateEnemyFireTimer();

            foreach (var enemy in enemies)
            {
                Canvas.SetTop(enemy, Canvas.GetTop(enemy) + 20); // Move para baixo
            }
        }
    }

    /// <summary>
    /// Move os projéteis dos inimigos e verifica se saíram da tela.
    /// </summary>
    private void MoveEnemyProjectiles()
    {
        for (int i = enemyProjectiles.Count - 1; i >= 0; i--)
        {
            var projectile = enemyProjectiles[i];
            double top = Canvas.GetTop(projectile);
            Canvas.SetTop(projectile, top + 8); // Velocidade do projétil inimigo

            if (top > GameCanvas.ActualHeight)
            {
                GameCanvas.Children.Remove(projectile);
                enemyProjectiles.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Movimenta o inimigo especial (OVNI).
    /// </summary>
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

    #endregion

    #region Geração de Elementos do Jogo

    /// <summary>
    /// Cria uma nova onda de inimigos.
    /// </summary>
    private void CreateEnemies()
    {
        int enemyRows = 5;
        int enemyCols = 11;
        double enemyWidth = 40;
        double enemyHeight = 30;
        double enemySpacing = 15;
        double startY = 70 + ((waveNumber - 1) * 20); // Ondas começam mais baixo

        double gridWidth = (enemyCols * enemyWidth) + ((enemyCols - 1) * enemySpacing);
        double startX = (this.ActualWidth - gridWidth) / 2;

        for (int row = 0; row < enemyRows; row++)
        {
            for (int col = 0; col < enemyCols; col++)
            {
                var enemy = new Rectangle { Width = enemyWidth, Height = enemyHeight };
                ImageBrush skin;
                int points;

                if (row == 0) { skin = alienSkin40; points = 40; }
                else if (row < 3) { skin = alienSkin20; points = 20; }
                else { skin = alienSkin10; points = 10; }

                enemy.Fill = skin;
                enemy.Tag = points;

                Canvas.SetLeft(enemy, startX + col * (enemyWidth + enemySpacing));
                Canvas.SetTop(enemy, startY + row * (enemyHeight + enemySpacing));

                GameCanvas.Children.Add(enemy);
                enemies.Add(enemy);
            }
        }
    }

    /// <summary>
    /// Cria as barreiras de proteção (escudos).
    /// </summary>
    private void CreateShields()
    {
        int shieldBlockSize = 8;
        int numberOfShields = 4;
        int shieldGridWidth = 7;
        int shieldGridHeight = 5;

        double shieldPixelWidth = shieldGridWidth * shieldBlockSize;
        double totalShieldsArea = (this.ActualWidth * 0.8);
        double shieldSpacing = (totalShieldsArea - (numberOfShields * shieldPixelWidth)) / (numberOfShields - 1);
        double startX = (this.ActualWidth * 0.1);
        double shieldBaseY = 720; // Posição Y ajustada para os escudos

        for (int i = 0; i < numberOfShields; i++)
        {
            double shieldBaseX = startX + i * (shieldPixelWidth + shieldSpacing);
            for (int row = 0; row < shieldGridHeight; row++)
            {
                for (int col = 0; col < shieldGridWidth; col++)
                {
                    // Cria uma forma de 'U' invertido para o escudo
                    if (row == 0 && (col == 0 || col == shieldGridWidth - 1)) continue;
                    if (row == shieldGridHeight - 1 && (col > 1 && col < shieldGridWidth - 2)) continue;

                    var shieldPart = new Rectangle
                    {
                        Width = shieldBlockSize,
                        Height = shieldBlockSize,
                        Fill = new SolidColorBrush(Colors.LightGreen),
                        Tag = 3 // Vida do bloco do escudo
                    };
                    Canvas.SetLeft(shieldPart, shieldBaseX + col * shieldBlockSize);
                    Canvas.SetTop(shieldPart, shieldBaseY + row * shieldBlockSize);
                    GameCanvas.Children.Add(shieldPart);
                    shieldParts.Add(shieldPart);
                }
            }
        }
    }

    /// <summary>
    /// Cria o inimigo especial (OVNI) que cruza a tela.
    /// </summary>
    private void CreateSpecialEnemy()
    {
        if (specialEnemy != null) return;
        PlaySound("ufo_lowpitch.wav");
        specialEnemy = new Rectangle
        {
            Width = 50,
            Height = 22,
            Fill = specialAlienSkin,
            Tag = new Random().Next(5, 16) * 10 // Pontos aleatórios: 50, 60, ..., 150
        };
        Canvas.SetTop(specialEnemy, 40);
        Canvas.SetLeft(specialEnemy, -specialEnemy.Width);
        GameCanvas.Children.Add(specialEnemy);
    }

    #endregion

    #region Lógica de Disparo

    /// <summary>
    /// Cria um projétil na posição da nave do jogador.
    /// </summary>
    private void ShootPlayerProjectile()
    {
        if (!canShoot) return;

        PlaySound("shoot.wav");
        canShoot = false;

        playerProjectile = new Rectangle
        {
            Width = 4,
            Height = 15,
            Fill = new SolidColorBrush(Colors.White)
        };
        double shipLeft = Canvas.GetLeft(PlayerShip);
        double shipTop = Canvas.GetTop(PlayerShip);
        Canvas.SetLeft(playerProjectile, shipLeft + PlayerShip.Width / 2 - 2);
        Canvas.SetTop(playerProjectile, shipTop - 15);
        GameCanvas.Children.Add(playerProjectile);
    }

    /// <summary>
    /// Lógica para os inimigos dispararem.
    /// </summary>
    private void EnemyShoot(object? sender, object e)
    {
        if (currentState != GameState.Playing || !enemies.Any()) return;

        // Encontra todos os inimigos na coluna mais baixa
        var shooters = enemies
            .GroupBy(enemy => Canvas.GetLeft(enemy))
            .Select(group => group.OrderByDescending(enemy => Canvas.GetTop(enemy)).First())
            .ToList();

        if (!shooters.Any()) return;

        // Escolhe um atirador aleatório
        var shooter = shooters[new Random().Next(shooters.Count)];
        double left = Canvas.GetLeft(shooter);
        double top = Canvas.GetTop(shooter);

        var projectile = new Rectangle
        {
            Width = 4,
            Height = 15,
            Fill = new SolidColorBrush(Colors.LightGreen)
        };

        Canvas.SetLeft(projectile, left + shooter.Width / 2 - 2);
        Canvas.SetTop(projectile, top + shooter.Height);
        GameCanvas.Children.Add(projectile);
        enemyProjectiles.Add(projectile);
    }

    #endregion

    #region Detecção de Colisão

    /// <summary>
    /// Verifica todas as possíveis colisões no jogo.
    /// </summary>
    private void CheckCollisions()
    {
        // Projétil do jogador com elementos do jogo
        if (playerProjectile != null)
        {
            Rect projectileRect = new Rect(Canvas.GetLeft(playerProjectile), Canvas.GetTop(playerProjectile), playerProjectile.Width, playerProjectile.Height);

            // Colisão com inimigos
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var enemy = enemies[i];
                Rect enemyRect = new Rect(Canvas.GetLeft(enemy), Canvas.GetTop(enemy), enemy.Width, enemy.Height);
                if (CheckRectCollision(projectileRect, enemyRect))
                {
                    PlaySound("invaderkilled.wav");
                    score += (int)enemy.Tag;
                    UpdateScoreDisplay();
                    GameCanvas.Children.Remove(enemy);
                    enemies.RemoveAt(i);
                    RemovePlayerProjectile();
                    return; // Sai para evitar colisão com múltiplos alvos
                }
            }

            // Colisão com inimigo especial
            if (specialEnemy != null)
            {
                Rect specialEnemyRect = new Rect(Canvas.GetLeft(specialEnemy), Canvas.GetTop(specialEnemy), specialEnemy.Width, specialEnemy.Height);
                if (CheckRectCollision(projectileRect, specialEnemyRect))
                {
                    PlaySound("invaderkilled.wav");
                    score += (int)specialEnemy.Tag;
                    UpdateScoreDisplay();
                    RemoveSpecialEnemy();
                    RemovePlayerProjectile();
                    return;
                }
            }
        }

        // Projéteis (jogador e inimigo) com escudos
        CheckProjectileShieldCollisions(playerProjectile, true);
        foreach (var proj in enemyProjectiles.ToList())
        {
            CheckProjectileShieldCollisions(proj, false);
        }

        // Projéteis inimigos com o jogador
        for (int i = enemyProjectiles.Count - 1; i >= 0; i--)
        {
            var projectile = enemyProjectiles[i];
            Rect projectileRect = new Rect(Canvas.GetLeft(projectile), Canvas.GetTop(projectile), projectile.Width, projectile.Height);
            Rect playerRect = new Rect(Canvas.GetLeft(PlayerShip), Canvas.GetTop(PlayerShip), PlayerShip.Width, PlayerShip.Height);

            if (CheckRectCollision(projectileRect, playerRect))
            {
                GameCanvas.Children.Remove(projectile);
                enemyProjectiles.RemoveAt(i);
                PlayerHit();
                return;
            }
        }
    }

    /// <summary>
    /// Verifica colisão entre um projétil e os escudos.
    /// </summary>
    private void CheckProjectileShieldCollisions(Rectangle? projectile, bool isPlayerProjectile)
    {
        if (projectile == null) return;

        Rect projectileRect = new Rect(Canvas.GetLeft(projectile), Canvas.GetTop(projectile), projectile.Width, projectile.Height);

        for (int i = shieldParts.Count - 1; i >= 0; i--)
        {
            var part = shieldParts[i];
            Rect shieldPartRect = new Rect(Canvas.GetLeft(part), Canvas.GetTop(part), part.Width, part.Height);

            if (CheckRectCollision(projectileRect, shieldPartRect))
            {
                DamageShield(part);
                if (isPlayerProjectile)
                {
                    RemovePlayerProjectile();
                }
                else
                {
                    GameCanvas.Children.Remove(projectile);
                    enemyProjectiles.Remove(projectile);
                }
                return;
            }
        }
    }

    /// <summary>
    /// Função auxiliar para verificar a interseção de dois retângulos.
    /// </summary>
    private bool CheckRectCollision(Rect rect1, Rect rect2)
    {
        rect1.Intersect(rect2);
        return !rect1.IsEmpty;
    }

    #endregion

    #region Gerenciamento de Estado do Jogo

    /// <summary>
    /// Inicia um novo jogo, resetando todas as variáveis.
    /// </summary>
    private void StartNewGame()
    {
        // Limpa o estado anterior
        CleanUpGameElements();

        // Reseta variáveis
        score = 0;
        playerLives = initialLives;
        waveNumber = 1;
        enemySpeed = 1.5;
        enemyDirection = 1;
        enemyFireRate = 1000;

        // Configura o jogo
        CreateEnemies();
        CreateShields();
        UpdateScoreDisplay();
        UpdateLivesDisplay();
        PlayerShip.Fill = playerSkin;
        Canvas.SetLeft(PlayerShip, (this.ActualWidth - PlayerShip.Width) / 2);
        Canvas.SetTop(PlayerShip, this.ActualHeight - PlayerShip.Height - 40); // Posiciona a nave perto da parte inferior

        // Inicia os timers
        SetupTimers();

        // Muda o estado e a UI
        SwitchGameState(GameState.Playing);
    }

    /// <summary>
    /// Verifica as condições de vitória ou derrota.
    /// </summary>
    private void CheckGameState()
    {
        // Condição de derrota: Inimigos alcançam o jogador
        if (enemies.Any(enemy => Canvas.GetTop(enemy) + enemy.Height >= Canvas.GetTop(PlayerShip)))
        {
            GameOver(false);
            return;
        }

        // Condição de vitória de onda: Todos os inimigos destruídos
        if (!enemies.Any() && currentState == GameState.Playing)
        {
            waveNumber++;
            // Aumenta a dificuldade para a próxima onda
            if (enemySpeed < 4) enemySpeed += 0.5;
            if (enemyFireRate > 250) enemyFireRate -= 100;

            // Limpa projéteis restantes e cria a nova onda
            CleanUpProjectiles();
            CreateEnemies();
            UpdateEnemyFireTimer();
        }
    }

    /// <summary>
    /// Chamado quando o jogador é atingido.
    /// </summary>
    private async void PlayerHit()
    {
        PlaySound("explosion.wav");
        playerLives--;
        UpdateLivesDisplay();

        if (playerLives <= 0)
        {
            GameOver(false);
        }
        else
        {
            // Efeito de piscar e invencibilidade temporária
            PlayerShip.Opacity = 0.5;
            await Task.Delay(1500);
            PlayerShip.Opacity = 1.0;
        }
    }

    /// <summary>
    /// Finaliza o jogo.
    /// </summary>
    private void GameOver(bool playerWon)
    {
        StopTimers();
        // A vitória não é uma condição de fim de jogo em Space Invaders clássico,
        // mas a lógica está aqui caso seja necessária.
        // Por enquanto, apenas a derrota é tratada.
        (gameOverPanel.FindName("GameOverTitle") as TextBlock).Text = "FIM DE JOGO";
        (gameOverPanel.FindName("FinalScoreText") as TextBlock).Text = $"SUA PONTUAÇÃO: {score}";
        SwitchGameState(GameState.GameOver);
    }

    /// <summary>
    /// Muda o estado do jogo e atualiza a visibilidade dos painéis da UI.
    /// </summary>
    private void SwitchGameState(GameState newState)
    {
        currentState = newState;

        // Esconde todos os painéis e elementos de UI específicos de estado
        menuPanel.Visibility = Visibility.Collapsed;
        gameOverPanel.Visibility = Visibility.Collapsed;
        highScoresPanel.Visibility = Visibility.Collapsed;
        controlsPanel.Visibility = Visibility.Collapsed;

        PlayerShip.Visibility = Visibility.Collapsed;
        scoreText.Visibility = Visibility.Collapsed;
        livesPanel.Visibility = Visibility.Collapsed;

        // Mostra os elementos do novo estado
        switch (newState)
        {
            case GameState.Menu:
                menuPanel.Visibility = Visibility.Visible;
                break;
            case GameState.Playing:
                PlayerShip.Visibility = Visibility.Visible;
                scoreText.Visibility = Visibility.Visible;
                livesPanel.Visibility = Visibility.Visible;
                break;
            case GameState.GameOver:
                gameOverPanel.Visibility = Visibility.Visible;
                (gameOverPanel.FindName("NicknameInput") as TextBox).Text = ""; // Limpa o campo
                break;
            case GameState.HighScores:
                highScoresPanel.Visibility = Visibility.Visible;
                LoadAndDisplayScores();
                break;
            case GameState.Controls:
                controlsPanel.Visibility = Visibility.Visible;
                break;
        }
    }

    #endregion

    #region Gerenciamento de UI e Pontuação

    /// <summary>
    /// Atualiza o texto da pontuação e verifica se o jogador ganhou uma vida extra.
    /// </summary>
    private void UpdateScoreDisplay()
    {
        int previousScore = Int32.Parse(scoreText.Text.Split(':')[1].Trim());
        scoreText.Text = $"PONTOS: {score}";

        // Adiciona uma vida a cada 1000 pontos
        if (score / 1000 > previousScore / 1000)
        {
            if (playerLives < maxLives)
            {
                playerLives++;
                UpdateLivesDisplay();
                PlaySound("extraShip.wav"); // Som para vida extra
            }
        }
    }

    /// <summary>
    /// Atualiza os ícones de vida na tela.
    /// </summary>
    private void UpdateLivesDisplay()
    {
        livesPanel.Children.Clear();
        for (int i = 0; i < playerLives; i++)
        {
            var lifeIcon = new Rectangle
            {
                Width = 30,
                Height = 20,
                Fill = playerSkin
            };
            livesPanel.Children.Add(lifeIcon);
        }
    }

    /// <summary>
    /// Aplica dano a um bloco do escudo.
    /// </summary>
    private void DamageShield(Rectangle shieldPart)
    {
        int health = (int)shieldPart.Tag;
        health--;
        shieldPart.Tag = health;

        if (health <= 0)
        {
            GameCanvas.Children.Remove(shieldPart);
            shieldParts.Remove(shieldPart);
        }
        else
        {
            // Muda a cor para indicar dano
            switch (health)
            {
                case 2: shieldPart.Fill = new SolidColorBrush(Colors.YellowGreen); break;
                case 1: shieldPart.Fill = new SolidColorBrush(Colors.Orange); break;
            }
        }
    }

    #endregion

    #region Limpeza e Remoção de Elementos

    /// <summary>
    /// Remove todos os elementos de jogo da tela.
    /// </summary>
    private void CleanUpGameElements()
    {
        CleanUpProjectiles();
        RemoveSpecialEnemy();

        foreach (var enemy in enemies) GameCanvas.Children.Remove(enemy);
        enemies.Clear();

        foreach (var part in shieldParts) GameCanvas.Children.Remove(part);
        shieldParts.Clear();
    }

    private void CleanUpProjectiles()
    {
        RemovePlayerProjectile();
        foreach (var proj in enemyProjectiles) GameCanvas.Children.Remove(proj);
        enemyProjectiles.Clear();
    }

    private void RemovePlayerProjectile()
    {
        if (playerProjectile == null) return;
        GameCanvas.Children.Remove(playerProjectile);
        playerProjectile = null;
        canShoot = true; // Permite atirar novamente
    }

    private void RemoveSpecialEnemy()
    {
        if (specialEnemy == null) return;
        GameCanvas.Children.Remove(specialEnemy);
        specialEnemy = null;
    }

    #endregion

    #region Gerenciamento de Timers

    /// <summary>
    /// Inicia todos os timers necessários para o jogo.
    /// </summary>
    private void SetupTimers()
    {
        // Timer do inimigo especial (OVNI)
        specialEnemyTimer = new DispatcherTimer();
        specialEnemyTimer.Interval = TimeSpan.FromSeconds(20); // Aparece a cada 20 segundos
        specialEnemyTimer.Tick += (s, e) => CreateSpecialEnemy();
        specialEnemyTimer.Start();

        // Timer de disparo dos inimigos
        enemyFireTimer = new DispatcherTimer();
        UpdateEnemyFireTimer();
        enemyFireTimer.Tick += EnemyShoot;
        enemyFireTimer.Start();
    }

    /// <summary>
    /// Para todos os timers do jogo.
    /// </summary>
    private void StopTimers()
    {
        specialEnemyTimer?.Stop();
        enemyFireTimer?.Stop();
    }

    /// <summary>
    /// Atualiza o intervalo do timer de disparo dos inimigos.
    /// </summary>
    private void UpdateEnemyFireTimer()
    {
        if (enemyFireTimer != null)
        {
            enemyFireTimer.Interval = TimeSpan.FromMilliseconds(enemyFireRate);
        }
    }

    #endregion

    #region Eventos de Teclado

    private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (currentState != GameState.Playing) return;

        switch (e.Key)
        {
            case VirtualKey.Left:
            case VirtualKey.A:
                isMovingLeft = true;
                break;
            case VirtualKey.Right:
            case VirtualKey.D:
                isMovingRight = true;
                break;
            case VirtualKey.Space:
                ShootPlayerProjectile();
                break;
        }
    }

    private void Page_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Left:
            case VirtualKey.A:
                isMovingLeft = false;
                break;
            case VirtualKey.Right:
            case VirtualKey.D:
                isMovingRight = false;
                break;
        }
    }

    #endregion

    #region Áudio

    /// <summary>
    /// Toca um efeito sonoro.
    /// </summary>
    private async void PlaySound(string soundFileName)
    {
        try
        {
            var fileUri = new Uri($"ms-appx:///Assets/sounds/{soundFileName}");
            var storageFile = await StorageFile.GetFileFromApplicationUriAsync(fileUri);
            var stream = await storageFile.OpenAsync(FileAccessMode.Read);

            var waveReader = new WaveFileReader(stream.AsStreamForRead());
            var waveOut = new WaveOutEvent();

            waveOut.PlaybackStopped += (s, a) =>
            {
                waveReader.Dispose();
                stream.Dispose();
                waveOut.Dispose();
            };

            waveOut.Init(waveReader);
            waveOut.Play();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Erro ao tocar som '{soundFileName}': {ex.Message}");
        }
    }

    #endregion

    #region Persistência de Pontuação (Arquivo)

    private const string HighScoreFileName = "highscores.txt";

    /// <summary>
    /// Salva a pontuação do jogador em um arquivo de texto.
    /// </summary>
    private async void SaveScoreToFile(string nickname, int finalScore)
    {
        StorageFolder localFolder = ApplicationData.Current.LocalFolder;
        StorageFile scoreFile = await localFolder.CreateFileAsync(HighScoreFileName, CreationCollisionOption.OpenIfExists);

        // Limita o nickname para evitar entradas muito longas
        if (nickname.Length > 10) nickname = nickname.Substring(0, 10);
        if (string.IsNullOrWhiteSpace(nickname)) nickname = "JOGADOR";

        string scoreEntry = $"{nickname.ToUpper()}:{finalScore}\n";
        await FileIO.AppendTextAsync(scoreFile, scoreEntry);
    }

    /// <summary>
    /// Carrega as pontuações do arquivo e as exibe na tela.
    /// </summary>
    private async void LoadAndDisplayScores()
    {
        var scoreListPanel = highScoresPanel.Children.OfType<StackPanel>().First();
        scoreListPanel.Children.Clear(); // Limpa a lista antiga

        try
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            StorageFile scoreFile = await localFolder.GetFileAsync(HighScoreFileName);
            var lines = await FileIO.ReadLinesAsync(scoreFile);

            var scores = lines
                .Select(line => line.Split(':'))
                .Where(parts => parts.Length == 2 && int.TryParse(parts[1], out _))
                .Select(parts => new { Nickname = parts[0], Score = int.Parse(parts[1]) })
                .OrderByDescending(s => s.Score)
                .Take(10) // Pega os 10 melhores
                .ToList();

            if (scores.Any())
            {
                foreach (var score in scores)
                {
                    var scoreEntryText = new TextBlock
                    {
                        Text = $"{score.Nickname.PadRight(12)} {score.Score}",
                        Foreground = new SolidColorBrush(Colors.White),
                        FontSize = 24,
                        FontFamily = new FontFamily("Consolas")
                    };
                    scoreListPanel.Children.Add(scoreEntryText);
                }
            }
            else
            {
                scoreListPanel.Children.Add(new TextBlock { Text = "NENHUMA PONTUAÇÃO REGISTRADA", Foreground = new SolidColorBrush(Colors.White), FontSize = 24 });
            }
        }
        catch (System.IO.FileNotFoundException)
        {
            scoreListPanel.Children.Add(new TextBlock { Text = "NENHUMA PONTUAÇÃO REGISTRADA", Foreground = new SolidColorBrush(Colors.White), FontSize = 24 });
        }
    }

    #endregion

    #region Criação de Painéis da UI

    private void CenterPanel(StackPanel panel)
    {
        panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(panel, (this.ActualWidth - panel.DesiredSize.Width) / 2);
        Canvas.SetTop(panel, (this.ActualHeight - panel.DesiredSize.Height) / 2);
    }

    private StackPanel CreateMenuPanel()
    {
        var panel = new StackPanel { Spacing = 20, HorizontalAlignment = HorizontalAlignment.Center };

        var title1 = new TextBlock { Text = "SPACE", Foreground = new SolidColorBrush(Colors.White), FontSize = 80, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
        var title2 = new TextBlock { Text = "INVADERS", Foreground = new SolidColorBrush(Colors.LightGreen), FontSize = 80, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, -30, 0, 20) };
        
        var scoreLegendPanel = new StackPanel { Spacing = 10, Margin = new Thickness(0, 20, 0, 20) };
        scoreLegendPanel.Children.Add(CreateLegendLine(specialAlienSkin, "= ??? PONTOS"));
        scoreLegendPanel.Children.Add(CreateLegendLine(alienSkin40, "= 40 PONTOS"));
        scoreLegendPanel.Children.Add(CreateLegendLine(alienSkin20, "= 20 PONTOS"));
        scoreLegendPanel.Children.Add(CreateLegendLine(alienSkin10, "= 10 PONTOS"));

        var startButton = new Button { Content = "INICIAR JOGO", FontSize = 24, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
        startButton.Click += (s, e) => StartNewGame();

        var scoresButton = new Button { Content = "PONTUAÇÕES", FontSize = 24, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
        scoresButton.Click += (s, e) => SwitchGameState(GameState.HighScores);

        var controlsButton = new Button { Content = "CONTROLES", FontSize = 24, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
        controlsButton.Click += (s, e) => SwitchGameState(GameState.Controls);

        panel.Children.Add(title1);
        panel.Children.Add(title2);
        panel.Children.Add(scoreLegendPanel);
        panel.Children.Add(startButton);
        panel.Children.Add(scoresButton);
        panel.Children.Add(controlsButton);

        CenterPanel(panel);
        return panel;
    }

    private StackPanel CreateLegendLine(ImageBrush? alienSkin, string text)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 15,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var alienIcon = new Rectangle
        {
            Width = 40,
            Height = 30,
            Fill = alienSkin
        };

        var pointsText = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Colors.White),
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };

        panel.Children.Add(alienIcon);
        panel.Children.Add(pointsText);
        return panel;
    }

    private StackPanel CreateGameOverPanel()
    {
        var panel = new StackPanel { Spacing = 20, HorizontalAlignment = HorizontalAlignment.Center };
        var title = new TextBlock { Name = "GameOverTitle", Text = "FIM DE JOGO", Foreground = new SolidColorBrush(Colors.Red), FontSize = 60, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
        var finalScore = new TextBlock { Name = "FinalScoreText", Foreground = new SolidColorBrush(Colors.White), FontSize = 30, HorizontalAlignment = HorizontalAlignment.Center };
        var nicknameLabel = new TextBlock { Text = "DIGITE SEU NOME:", Foreground = new SolidColorBrush(Colors.White), FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center };
        var nicknameInput = new TextBox { Name = "NicknameInput", Width = 200, HorizontalAlignment = HorizontalAlignment.Center };
        var saveButton = new Button { Content = "SALVAR PONTUAÇÃO", FontSize = 20 };
        saveButton.Click += (s, e) =>
        {
            SaveScoreToFile(nicknameInput.Text, score);
            SwitchGameState(GameState.HighScores);
        };
        var menuButton = new Button { Content = "VOLTAR AO MENU", FontSize = 20 };
        menuButton.Click += (s, e) => SwitchGameState(GameState.Menu);

        panel.Children.Add(title);
        panel.Children.Add(finalScore);
        panel.Children.Add(nicknameLabel);
        panel.Children.Add(nicknameInput);
        panel.Children.Add(saveButton);
        panel.Children.Add(menuButton);

        CenterPanel(panel);
        return panel;
    }

    private StackPanel CreateHighScoresPanel()
    {
        var panel = new StackPanel { Spacing = 15, HorizontalAlignment = HorizontalAlignment.Center };
        var title = new TextBlock { Text = "MELHORES PONTUAÇÕES", Foreground = new SolidColorBrush(Colors.White), FontSize = 40, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
        var scoreListPanel = new StackPanel { Spacing = 10 };
        var menuButton = new Button { Content = "VOLTAR AO MENU", FontSize = 20, Margin = new Thickness(0, 20, 0, 0) };
        menuButton.Click += (s, e) => SwitchGameState(GameState.Menu);

        panel.Children.Add(title);
        panel.Children.Add(scoreListPanel);
        panel.Children.Add(menuButton);

        CenterPanel(panel);
        return panel;
    }

    private StackPanel CreateControlsPanel()
    {
        var panel = new StackPanel { Spacing = 15, HorizontalAlignment = HorizontalAlignment.Center };
        var title = new TextBlock { Text = "CONTROLES", Foreground = new SolidColorBrush(Colors.White), FontSize = 40, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
        var moveText = new TextBlock { Text = "← → / A D : Mover Nave", Foreground = new SolidColorBrush(Colors.White), FontSize = 24 };
        var shootText = new TextBlock { Text = "ESPAÇO : Atirar", Foreground = new SolidColorBrush(Colors.White), FontSize = 24 };
        var menuButton = new Button { Content = "VOLTAR AO MENU", FontSize = 20, Margin = new Thickness(0, 20, 0, 0) };
        menuButton.Click += (s, e) => SwitchGameState(GameState.Menu);

        panel.Children.Add(title);
        panel.Children.Add(moveText);
        panel.Children.Add(shootText);
        panel.Children.Add(menuButton);

        CenterPanel(panel);
        return panel;
    }

    // Método para ser chamado quando o menu principal é exibido
    private void ShowMenu()
    {
        CleanUpGameElements();
        StopTimers();
        SwitchGameState(GameState.Menu);
    }

    #endregion
}
