using Godot;

/// <summary>
/// 玩家战机（CharacterBody2D）：
/// - 仅横向移动（move_left / move_right），并限制在屏幕边界内；
/// - 按住 fire 持续发射玩家子弹，每次发射消耗 1 发弹药（初始 30 发）；
/// - 弹药归零仅是无法开火，不判失败；补充弹药唯一途径是拾取子弹包（BulletPack）；
/// - 拥有血量，被敌方子弹 / 敌机 / 陨石撞击扣血；
/// - 血量归零时生成爆炸、发出 Died 信号并释放自身。
/// 碰撞：根节点 CollisionShape2D 提供物理体形状，子节点 Hitbox(Area2D) 负责
/// body_entered 检测，收到信号后做实体类型判断。
/// </summary>
public partial class Player : CharacterBody2D
{
    [Signal] public delegate void DiedEventHandler();

    [Export] public float MoveSpeed = 420f;
    [Export] public int MaxHealth = 3;
    [Export] public int InitialAmmo = 30;
    [Export] public float FireInterval = 0.18f;
    [Export] public Vector2 MuzzleOffset = new(0f, -34f);
    [Export] public PackedScene PlayerBulletScene;
    [Export] public PackedScene ExplosionScene;

    private Area2D _hitbox;
    private int _health;
    private int _ammo;
    private float _fireCooldown;

    /// <summary>当前血量（供 HUD / 调试读取）。</summary>
    public int Health => _health;

    /// <summary>当前弹药数（供 HUD / 调试读取）。</summary>
    public int Ammo => _ammo;

    public override void _Ready()
    {
        AddToGroup("player");
        _health = MaxHealth;
        _ammo = InitialAmmo;
        _hitbox = GetNode<Area2D>("Hitbox");
        _hitbox.BodyEntered += OnHitboxBodyEntered;
    }

    public override void _PhysicsProcess(double delta)
    {
        float d = (float)delta;

        float direction = 0f;
        if (Input.IsActionPressed("move_left")) direction -= 1f;
        if (Input.IsActionPressed("move_right")) direction += 1f;

        Velocity = new Vector2(direction * MoveSpeed, 0f);
        MoveAndSlide();
        ClampToScreen();

        if (Input.IsActionPressed("fire"))
            TryFire(d);
    }

    private void TryFire(float delta)
    {
        if (_ammo <= 0)
            return;
        _fireCooldown -= delta;
        if (_fireCooldown > 0f)
            return;
        _fireCooldown = FireInterval;

        if (PlayerBulletScene == null)
            return;

        _ammo--;

        var bullet = PlayerBulletScene.Instantiate<Bullet>();
        bullet.GlobalPosition = GlobalPosition + MuzzleOffset;
        AddToContainer(bullet, "player_bullets", GetTree());
    }

    /// <summary>补充弹药（由子弹包 BulletPack 拾取时调用）。</summary>
    public void AddAmmo(int amount)
    {
        _ammo += amount;
    }

    private void OnHitboxBodyEntered(Node2D body)
    {
        if (body == this)
            return;
        // 敌方子弹伤害改由子弹自身 ShapeCast 调用 Hit() 结算（玩家 Hitbox 掩码已移除子弹层），
        // 此处只处理敌机本体与陨石的直接碰撞。
        if (body is Obstacle)
            Die();          // 撞上陨石立即死亡
        else if (body is Enemy)
            TakeDamage(1);
    }

    /// <summary>被敌方子弹命中扣血（由子弹 ShapeCast 触发）。</summary>
    public void Hit(int amount)
    {
        TakeDamage(amount);
    }

    /// <summary>血包等外部对象调用：玩家加血。上限为 MaxHealth。</summary>
    public void Heal(int amount)
    {
        _health += amount;
        if (_health > MaxHealth)
            _health = MaxHealth;
    }

    private void TakeDamage(int amount)
    {
        _health -= amount;
        if (_health <= 0)
            Die();
    }

    private void Die()
    {
        ExplosionFx.Spawn(ExplosionScene, this, this);
        EmitSignal(SignalName.Died);
        QueueFree();
    }

    private void ClampToScreen()
    {
        var vp = GetViewportRect();
        float half = 22f;
        Position = new Vector2(Mathf.Clamp(Position.X, half, vp.Size.X - half), Position.Y);
    }

    private static void AddToContainer(Node2D entity, string group, SceneTree tree)
    {
        // 注意：entity 刚 Instantiate 尚未入树，GetTree() 为 null，
        // 必须用已在树中的 source（this）拿 SceneTree。
        var container = tree.GetFirstNodeInGroup(group) as Node;
        if (container != null)
            container.AddChild(entity);
        else
            tree.CurrentScene.AddChild(entity);
    }
}