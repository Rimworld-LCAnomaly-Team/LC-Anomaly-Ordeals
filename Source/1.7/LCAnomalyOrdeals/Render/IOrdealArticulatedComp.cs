namespace LCAnomalyOrdeals.Render
{
    /// <summary>
    /// 考验多部件 Spine 骨骼组件通用接口：供 <see cref="PawnRenderNode_DawnPart"/> 统一读取朝向、钻地隐藏、警戒/蓄力贴图切换与旋转发射器角度。
    /// </summary>
    public interface IOrdealArticulatedComp
    {
        bool IsFacingLeft { get; }
        bool IsBurrowedHidden { get; }
        bool IsAlertVisual { get; }
        float ChargeProgress { get; }
        float EmitterSpinAngle { get; }
    }
}
