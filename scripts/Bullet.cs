using Godot;

/// <summary>
/// 子弹（CharacterBody2D），玩家/敌机共用：
/// - MoveUp=true 向上飞（玩家子弹），false 向下飞（敌方子弹）；
/// - 命中检测使用子节点 ShapeCast2D：每帧 force_shapecast_update 沿前进方向
///   采样一段路径，规避 Area2D 离散检测对高速子弹的漏检/穿透问题；
/// - 命中后由子弹统一结算伤害并自毁；任意子弹超出屏幕范围自动销毁。
/// </summary>
public partial class Bullet : CharacterBody2D
{
    [Export] public float Speed = 520f;
    [Export] public bool MoveUp = true;

    /// <summary>每帧向前方采样的命中检测长度（约为每物理帧位移的 1~2 倍）。</summary>
    [Export] public float CastLength = 14f;

    /// <summary>子弹归属：MoveUp=true 即玩家子弹，false 即敌方子弹。</summary>
    public bool IsPlayerBullet => MoveUp;

    private ShapeCast2D _cast;

    public override void _Ready()
    {
        _cast = GetNode<ShapeCast2D>("Cast");
        _cast.Shape = new RectangleShape2D { Size = new Vector2(12f, 12f) };
        _cast.TargetPosition = MoveUp ? new Vector2(0f, -CastLength) : new Vector2(0f, CastLength);
        // 玩家子弹检测 敌机(layer2)+陨石(layer5)；敌方子弹检测 玩家(layer1)+陨石(layer5)
        _cast.CollisionMask = IsPlayerBullet ? 18u : 17u;
        _cast.Enabled = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        Velocity = new Vector2(0f, MoveUp ? -Speed : Speed);
        MoveAndSlide();

        _cast.ForceShapecastUpdate();
        if (_cast.IsColliding() && _cast.GetCollider(0) is Node2D target && target != this)
        {
            HitTarget(target);
            return;
        }

        var vp = GetViewportRect();
        if (GlobalPosition.Y < -40f || GlobalPosition.Y > vp.Size.Y + 40f)
            QueueFree();
    }

    /// <summary>命中目标：按子弹归属对目标结算伤害，然后自毁。</summary>
    private void HitTarget(Node2D target)
    {
        if (IsPlayerBullet)
        {
            if (target is Enemy enemy)
                enemy.Hit(1);
            else if (target is Obstacle obstacle)
                obstacle.Destroy();
        }
        else
        {
            // 敌方子弹：命中玩家扣血，命中陨石也可将其击碎
            if (target is Player player)
                player.Hit(1);
            else if (target is Obstacle obstacle)
                obstacle.Destroy();
        }
        QueueFree();
    }
}
