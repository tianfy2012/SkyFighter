using Godot;

/// <summary>
/// 敌机（CharacterBody2D），状态机分为 ENTER / IDLE：
/// - ENTER：从屏幕顶部向下飞行，抵达随机目标 Y 后停止纵向移动，切到 IDLE；
/// - IDLE：只允许左右移动，禁止上下；横向由 游走 + 队友分离 + 陨石躲避 共同驱动
///         （躲避优先级最高）；到射击间隔向下发射子弹；
/// - 敌方子弹可击碎陨石；敌机本体碰到陨石会掉血；
/// - 拥有血量，被玩家子弹击中扣血；血量归零时生成爆炸并释放。
/// </summary>
public partial class Enemy : CharacterBody2D
{
    private enum State { Enter, Idle }

    [Export] public int MaxHealth = 2;
    [Export] public float EnterSpeed = 180f;
    [Export] public float IdleMoveSpeed = 240f;
    [Export] public float TargetYMin = 120f;
    [Export] public float TargetYMax = 300f;
    [Export] public float FireInterval = 1.6f;
    [Export] public float DodgeRangeX = 150f;   // 水平方向的危险距离
    [Export] public float DodgeRangeY = 260f;   // 只躲避下方这一段范围内的陨石
    [Export] public float HysteresisOn = 0.2f;  // 超过该威胁强度才切换躲闪方向
    [Export] public float HysteresisOff = 0.05f; // 低于该威胁强度才复位
    [Export] public float HitFlashDuration = 0.15f; // 受击闪红时长
    [Export] public float RoamIntervalMin = 1.2f;   // 游走重选目标的最短间隔
    [Export] public float RoamIntervalMax = 2.8f;   // 游走重选目标的最长间隔
    [Export] public float SeparationRangeX = 70f;   // 队友分离判定：横向距离
    [Export] public float SeparationRangeY = 60f;   // 队友分离判定：纵向距离
    [Export] public PackedScene EnemyBulletScene;
    [Export] public PackedScene ExplosionScene;

    private State _state = State.Enter;
    private int _health;
    private float _targetY;
    private int _dodgeDir; // -1 左 / 0 静止 / +1 右
    private float _hitFlash; // 剩余受击高亮时间
    private float _roamTargetX;  // 当前游走目标横坐标
    private double _roamTimeLeft; // 距离重选目标剩余时间
    private float _separationDir; // 队友推离方向（-1~1）
    private Area2D _hitbox;
    private Timer _fireTimer;

    /// <summary>当前血量（供 HUD / 调试读取）。</summary>
    public int Health => _health;

    public override void _Ready()
    {
        AddToGroup("enemies");
        _health = MaxHealth;
        _targetY = (float)GD.RandRange(TargetYMin, TargetYMax);
        PickRoamTarget();

        _hitbox = GetNode<Area2D>("Hitbox");
        _hitbox.BodyEntered += OnHitboxBodyEntered;

        _fireTimer = GetNode<Timer>("FireTimer");
        _fireTimer.WaitTime = FireInterval;
        _fireTimer.Timeout += OnFireTimerTimeout;
        _fireTimer.Start(GD.RandRange(0.5f, 1.5f) * FireInterval);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_state == State.Enter)
        {
            Velocity = new Vector2(0f, EnterSpeed);
            MoveAndSlide();

            // 到达目标 Y，或下落途中被下方友机物理挡下时，转入 IDLE
            if (Position.Y >= _targetY || IsOnFloor())
            {
                _state = State.Idle;
                _dodgeDir = 0;
            }
        }
        else
        {
            UpdateDodge();
            UpdateRoam(delta);
            UpdateSeparation();

            // 躲避陨石优先级最高；无威胁时用 游走 + 队友分离 决定横向移动
            float dir = _dodgeDir != 0f
                ? _dodgeDir
                : Mathf.Clamp(RoamDir() + _separationDir, -1f, 1f);

            Velocity = new Vector2(dir * IdleMoveSpeed, 0f);
            MoveAndSlide();
            ClampToScreen();
        }
    }

    /// <summary>游走：随机选取屏幕内的横向目标点，到达或超时后重新选点。</summary>
    private void UpdateRoam(double delta)
    {
        _roamTimeLeft -= delta;
        if (_roamTimeLeft <= 0.0 || Mathf.Abs(GlobalPosition.X - _roamTargetX) < 6f)
            PickRoamTarget();
    }

    private void PickRoamTarget()
    {
        var vp = GetViewportRect();
        float half = 30f;
        _roamTargetX = (float)GD.RandRange(half, vp.Size.X - half);
        _roamTimeLeft = GD.RandRange(RoamIntervalMin, RoamIntervalMax);
    }

    /// <summary>当前游走方向：相对目标点的左右。</summary>
    private float RoamDir()
    {
        float dx = _roamTargetX - GlobalPosition.X;
        if (Mathf.Abs(dx) < 6f)
            return 0f;
        return Mathf.Sign(dx);
    }

    /// <summary>
    /// 队友分离：累加与附近友机的横向推离方向，避免互相重叠。
    /// 距离越近权重越大；容器节点因类型不符被忽略。
    /// </summary>
    private void UpdateSeparation()
    {
        _separationDir = 0f;
        foreach (Node node in GetTree().GetNodesInGroup("enemies"))
        {
            var other = node as Enemy;
            if (other == null || other == this)
                continue;

            float dx = GlobalPosition.X - other.GlobalPosition.X;
            float dy = GlobalPosition.Y - other.GlobalPosition.Y;
            if (Mathf.Abs(dy) > SeparationRangeY || Mathf.Abs(dx) > SeparationRangeX)
                continue;

            float weight = 1f - Mathf.Abs(dx) / SeparationRangeX;
            _separationDir += Mathf.Sign(dx) * weight;
        }
        _separationDir = Mathf.Clamp(_separationDir, -1f, 1f);
    }

    public override void _Process(double delta)
    {
        if (_hitFlash <= 0f)
            return;
        _hitFlash -= (float)delta;
        Modulate = _hitFlash > 0f ? new Color(1f, 0.35f, 0.35f) : Colors.White;
    }

    /// <summary>
    /// 检测逼近的陨石（从上方下落逼近、或刚经过自身高度），累加"远离"力；
    /// 威胁强度 = 横向接近度 × 纵向接近度，越靠近越危险。
    /// 采用滞回（Schmitt 触发）：威胁超过 On 才转向，低于 Off 才复位，防止左右抖动。
    /// </summary>
    private void UpdateDodge()
    {
        float force = 0f;
        foreach (Node node in GetTree().GetNodesInGroup("obstacles"))
        {
            var obstacle = node as Obstacle;
            if (obstacle == null)
                continue;

            float dx = obstacle.GlobalPosition.X - GlobalPosition.X;
            float dy = obstacle.GlobalPosition.Y - GlobalPosition.Y;

            // 陨石从上方落下：横向接近才构成威胁；已明显落到下方的不再理会，
            // 上方超出反应距离的暂不处理。
            if (Mathf.Abs(dx) > DodgeRangeX)
                continue;
            if (dy > 40f || dy < -DodgeRangeY)
                continue;

            float xw = 1f - Mathf.Abs(dx) / DodgeRangeX;
            float yw = 1f - Mathf.Abs(dy) / DodgeRangeY;
            force += (dx >= 0f ? -1f : 1f) * (xw * yw); // 反方向躲开
        }

        if (force > HysteresisOn)
            _dodgeDir = 1;
        else if (force < -HysteresisOn)
            _dodgeDir = -1;
        else if (Mathf.Abs(force) < HysteresisOff)
            _dodgeDir = 0;
        // 处于 [Off, On] 区间时保持原方向，形成滞回
    }

    private void OnFireTimerTimeout()
    {
        if (_state != State.Idle)
            return;
        if (EnemyBulletScene == null)
            return;

        var bullet = EnemyBulletScene.Instantiate<Bullet>();
        bullet.GlobalPosition = GlobalPosition + new Vector2(0f, 34f);

        var container = GetTree().GetFirstNodeInGroup("enemy_bullets") as Node;
        if (container != null)
            container.AddChild(bullet);
        else
            GetParent()?.AddChild(bullet);
    }

    private void OnHitboxBodyEntered(Node2D body)
    {
        if (body == this)
            return;
        // 子弹伤害改由子弹自身的 ShapeCast 调用 Hit() 结算，避免同一发子弹被重复结算；
        // 此处处理陨石碰撞：撞上即死亡。Player / HealthPack 触碰无效果。
        if (body is Obstacle)
            Die();
    }

    /// <summary>被玩家子弹命中扣血（由子弹 ShapeCast 触发）。</summary>
    public void Hit(int amount)
    {
        TakeDamage(amount);
    }

    private void TakeDamage(int amount)
    {
        _health -= amount;
        _hitFlash = HitFlashDuration;
        if (_health <= 0)
            Die();
    }

    private void Die()
    {
        ExplosionFx.Spawn(ExplosionScene, this, this);
        QueueFree();
    }

    private void ClampToScreen()
    {
        var vp = GetViewportRect();
        float half = 24f;
        Position = new Vector2(Mathf.Clamp(Position.X, half, vp.Size.X - half), Position.Y);
    }
}