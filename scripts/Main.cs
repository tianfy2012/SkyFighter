using System;
using Godot;

/// <summary>
/// 主场景控制器：
/// - 把各实体容器节点注册到分组（enemies/obstacles/player_bullets/enemy_bullets/
///   health_packs/bullet_packs/effects），供实体在运行时查找目标容器；
/// - 定时在屏幕顶部随机位置生成敌机、陨石、血包、子弹包；
/// - 开场显示开始界面（暂停等待开始），玩家死亡显示结束界面，
///   点击"再来一局"重载场景重开；
/// - 监听玩家 Died 信号：停止生成并暂停游戏树。
/// 所有可调参数均为 @export，在 Inspector 中可视化调整。
/// </summary>
public partial class Main : Node2D
{
    [Export] public PackedScene EnemyScene;
    [Export] public PackedScene ObstacleScene;
    [Export] public PackedScene HealthPackScene;
    [Export] public PackedScene BulletPackScene;

    [Export] public float EnemySpawnInterval = 2.2f;
    [Export] public float ObstacleSpawnInterval = 1.3f;
    [Export] public float HealthPackSpawnInterval = 9f;
    [Export] public float BulletPackSpawnInterval = 4.5f;
    [Export] public float SpawnMarginX = 40f;
    [Export] public float SpawnY = -48f;

    /// <summary>屏幕上同时存在的敌机数量上限，超过则不生成。</summary>
    [Export] public int MaxEnemies = 5;

    private Label _hud;
    private Label _ammoLabel;
    private Control _startScreen;
    private Control _endScreen;
    private Button _startButton;
    private Button _restartButton;
    private Label _endResult;
    private Timer _enemyTimer;
    private Timer _obstacleTimer;
    private Timer _healthTimer;
    private Timer _bulletPackTimer;

    public override void _Ready()
    {
        GetNode("Enemies").AddToGroup("enemies");
        GetNode("Obstacles").AddToGroup("obstacles");
        GetNode("PlayerBullets").AddToGroup("player_bullets");
        GetNode("EnemyBullets").AddToGroup("enemy_bullets");
        GetNode("HealthPacks").AddToGroup("health_packs");
        GetNode("BulletPacks").AddToGroup("bullet_packs");
        GetNode("Effects").AddToGroup("effects");

        _enemyTimer = CreateSpawnTimer(EnemySpawnInterval, SpawnEnemy);
        _obstacleTimer = CreateSpawnTimer(ObstacleSpawnInterval, SpawnObstacle);
        _healthTimer = CreateSpawnTimer(HealthPackSpawnInterval, SpawnHealthPack);
        _bulletPackTimer = CreateSpawnTimer(BulletPackSpawnInterval, SpawnBulletPack);

        _hud = GetNode<Label>("HUD");
        _ammoLabel = GetNode<Label>("AmmoHUD");

        var player = GetNode<Player>("Player");
        player.Died += OnPlayerDied;

        _startScreen = GetNode<Control>("UI/StartScreen");
        _endScreen = GetNode<Control>("UI/EndScreen");
        _startButton = GetNode<Button>("UI/StartScreen/StartButton");
        _restartButton = GetNode<Button>("UI/EndScreen/RestartButton");
        _endResult = GetNode<Label>("UI/EndScreen/Result");
        _startButton.Pressed += OnStartPressed;
        _restartButton.Pressed += OnRestartPressed;

        ShowStartScreen();
    }

    private void ShowStartScreen()
    {
        _startScreen.Visible = true;
        _endScreen.Visible = false;
        GetTree().Paused = true;
    }

    private void OnStartPressed()
    {
        _startScreen.Visible = false;
        GetTree().Paused = false;
    }

    private void OnRestartPressed()
    {
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }

    public override void _Process(double delta)
    {
        UpdateHud();
    }

    /// <summary>每帧刷新 HUD 上的剩余血量与弹药。</summary>
    private void UpdateHud()
    {
        var player = GetNodeOrNull<Player>("Player");
        if (player == null || !IsInstanceValid(player))
            return;

        if (_hud != null)
            _hud.Text = $"HP: {player.Health}/{player.MaxHealth}";
        if (_ammoLabel != null)
            _ammoLabel.Text = $"子弹: {player.Ammo}";
    }

    private Timer CreateSpawnTimer(double interval, Action onTimeout)
    {
        var timer = new Timer();
        timer.WaitTime = interval;
        timer.OneShot = false;
        timer.Autostart = true;
        timer.Timeout += onTimeout;
        AddChild(timer);
        return timer;
    }

    private void SpawnEnemy()
    {
        if (EnemyScene == null)
            return;
        if (GetNode("Enemies").GetChildCount() >= MaxEnemies)
            return;
        var enemy = EnemyScene.Instantiate<Enemy>();
        enemy.Position = RandomTopSpawnClearOfEnemies();
        GetNode("Enemies").AddChild(enemy);
    }

    /// <summary>顶部出生点：横向避开已有敌机，避免刚出生就与队友重叠。</summary>
    private Vector2 RandomTopSpawnClearOfEnemies()
    {
        var vp = GetViewportRect();
        for (int i = 0; i < 8; i++)
        {
            float x = (float)GD.RandRange(SpawnMarginX, vp.Size.X - SpawnMarginX);
            if (SpawnXIsClear(x))
                return new Vector2(x, SpawnY);
        }
        return new Vector2((float)GD.RandRange(SpawnMarginX, vp.Size.X - SpawnMarginX), SpawnY);
    }

    private bool SpawnXIsClear(float x)
    {
        const float margin = 70f;
        foreach (Node node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node is not Enemy enemy)
                continue;
            if (Mathf.Abs(enemy.Position.X - x) < margin)
                return false;
        }
        return true;
    }

    private void SpawnObstacle()
    {
        if (ObstacleScene == null)
            return;
        var obstacle = ObstacleScene.Instantiate<Obstacle>();
        obstacle.Position = RandomTopSpawn();
        GetNode("Obstacles").AddChild(obstacle);
    }

    private void SpawnHealthPack()
    {
        if (HealthPackScene == null)
            return;
        var pack = HealthPackScene.Instantiate<HealthPack>();
        pack.Position = RandomTopSpawn();
        GetNode("HealthPacks").AddChild(pack);
    }

    private void SpawnBulletPack()
    {
        if (BulletPackScene == null)
            return;
        var pack = BulletPackScene.Instantiate<BulletPack>();
        pack.Position = RandomTopSpawn();
        GetNode("BulletPacks").AddChild(pack);
    }

    private Vector2 RandomTopSpawn()
    {
        var vp = GetViewportRect();
        float x = (float)GD.RandRange(SpawnMarginX, vp.Size.X - SpawnMarginX);
        return new Vector2(x, SpawnY);
    }

    private void OnPlayerDied()
    {
        ShowEndScreen("游戏结束");
    }

    /// <summary>显示结束界面，停止生成并暂停游戏树（玩家死亡时收尾）。</summary>
    private void ShowEndScreen(string message)
    {
        _enemyTimer.Stop();
        _obstacleTimer.Stop();
        _healthTimer.Stop();
        _bulletPackTimer.Stop();
        UpdateHud(); // 玩家此刻仍有效，先刷新 HUD 再暂停
        _endResult.Text = message;
        _startScreen.Visible = false;
        _endScreen.Visible = true;
        GetTree().Paused = true;
    }
}