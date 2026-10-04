using Godot;

/// <summary>
/// 陨石障碍物（CharacterBody2D）：匀速向下坠落；
/// - 被玩家子弹击中：生成爆炸并销毁；
/// - 撞击玩家：由玩家自身的 Hitbox 结算扣血，此处不做额外处理；
/// - 飞出屏幕底部自动删除。
/// </summary>
public partial class Obstacle : CharacterBody2D
{
    [Export] public float FallSpeed = 170f;
    [Export] public PackedScene ExplosionScene;

    private Area2D _hitbox;

    public override void _Ready()
    {
        AddToGroup("obstacles");
        _hitbox = GetNode<Area2D>("Hitbox");
        _hitbox.BodyEntered += OnHitboxBodyEntered;
    }

    public override void _PhysicsProcess(double delta)
    {
        Velocity = new Vector2(0f, FallSpeed);
        MoveAndSlide();

        var vp = GetViewportRect();
        if (GlobalPosition.Y > vp.Size.Y + 60f)
            QueueFree();
    }

    private void OnHitboxBodyEntered(Node2D body)
    {
        if (body == this)
            return;
        // 陨石被子弹摧毁改由子弹 ShapeCast 调用 Destroy()；Player 接触由玩家侧处理。
    }

    /// <summary>被玩家子弹命中后销毁（由子弹 ShapeCast 触发）。</summary>
    public void Destroy()
    {
        Die();
    }

    private void Die()
    {
        ExplosionFx.Spawn(ExplosionScene, this, this);
        QueueFree();
    }
}