using Godot;

/// <summary>
/// 静态工具：把爆炸动画实例挂到 "effects" 容器下，播放完毕后自动释放。
/// 不硬编码任何文件路径；场景引用由调用方通过 @export PackedScene 提供。
/// </summary>
public static class ExplosionFx
{
    public static void Spawn(PackedScene explosionScene, Node2D source, Node context)
    {
        if (explosionScene == null)
            return;

        var boom = explosionScene.Instantiate<Node2D>();
        boom.GlobalPosition = source.GlobalPosition;

        var container = context.GetTree().GetFirstNodeInGroup("effects") as Node;
        if (container != null)
            container.AddChild(boom);
        else
            context.GetParent()?.AddChild(boom);
    }
}