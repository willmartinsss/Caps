using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Diagnostics;
using Windows.System;

namespace SpaceInvaders;

public sealed partial class MainPage : Page
{
    private double _playerSpeed = 10;
    private bool isMovingLeft = false;
    private bool isMovingRight = false;
    private DispatcherTimer gameLoopTimer;
    private Rectangle? playerProjectile = null;

    public MainPage()
    {
        this.InitializeComponent();
        this.Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        await Task.Delay(100);
        this.Focus(FocusState.Programmatic);

        gameLoopTimer = new DispatcherTimer();
        gameLoopTimer.Interval = TimeSpan.FromMilliseconds(16);
        gameLoopTimer.Tick += GameLoop;
        gameLoopTimer.Start();
    }

    private void GameLoop(object sender, object e)
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

        if (playerProjectile != null)
        {
            double top = Canvas.GetTop(playerProjectile);
            Canvas.SetTop(playerProjectile, top - 10);

            if (top < 0)
            {
                GameCanvas.Children.Remove(playerProjectile);
                playerProjectile = null;
            }
        }
    }

    private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Left:
                isMovingLeft = true;
                Debug.WriteLine("Clicou");
                break;

            case Windows.System.VirtualKey.Right:
                isMovingRight = true;
                Debug.WriteLine("Clicou");

                break;

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

    private void ShootPlayerProjectile()
    {
        playerProjectile = new Rectangle
        {
            Width = 5,
            Height = 15,
        };

        double shipLeft = Canvas.GetLeft(PlayerShip);
        double shipTop = Canvas.GetTop(PlayerShip);

        Canvas.SetLeft(playerProjectile, shipLeft + PlayerShip.Width / 2 - 2.5);
        Canvas.SetTop(playerProjectile, shipTop - 15);

        GameCanvas.Children.Add(playerProjectile);
    }
}
