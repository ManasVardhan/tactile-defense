namespace TactileWars.Core
{
    /// <summary>Anything on the Pickup layer that reacts to a tap (for example a flag bearer bubble).</summary>
    public interface ITappable
    {
        bool CanTap { get; }
        void OnTapped();
    }
}
