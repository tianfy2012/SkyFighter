using Godot;

/// <summary>
/// 血包（CharacterBody2D）：缓慢向下坠落；
/// - 只有玩家能拾取（Hitbox mask 仅含玩家层），敌机触碰无任何效果；
/// - 玩家拾取后加血并立刻销毁；飞出屏幕自动删除。
/// </summary>
public partial class HealthPack : CharacterBody2D
{
    [Export] public float FallSpeed = 55f;
    [Export] public int HealAmount = 1;

    private Area2D _hitbox;

    public override void _Ready()
    {
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
        if (body is Player player)
        {
            player.Heal(HealAmount);
            QueueFree();
        }
    }
}